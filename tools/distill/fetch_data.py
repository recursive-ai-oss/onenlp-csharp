#!/usr/bin/env python3
"""Fetch the multilingual distillation text: FineWeb-2 (ODC-By 1.0) plus Wikipedia (CC BY-SA 3.0 / GFDL).

    fetch_data.py --langs languages.tsv --out DIR --keys eval-keys.npy [--fw-chars 24000000] [--wiki-chars 6000000]
                  [--workers 8] [--only fr,de]

For each language it streams the first documents of the pinned dataset revisions until the character budget is met and
writes DIR/<iso>.jsonl ({"s": source, "t": text}). Documents are cut to --max-doc-chars; documents shorter than
--min-doc-chars are skipped; a document with any segment whose normalised key is in the evaluation key set is dropped
and counted. DIR/<iso>.stats.json holds the counts. Nothing is fetched twice: a finished language is skipped.

Datasets are read through the `datasets` library's streaming API (documented use). Revisions are pinned below.
"""
import argparse
import json
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from textkeys import key, segments  # noqa: E402

REVISIONS = {
    "HuggingFaceFW/fineweb-2": "af9c13333eb981300149d5ca60a8e9d659b276b9",
    "HuggingFaceFW/fineweb": "9bb295ddab0e05d785b879661af7260fed5140fc",
    "wikimedia/wikipedia": "b04c8d1ceb2f5cd4588862100d08de323dccfbaa",
}
WIKI_DUMP = "20231101"


def read_langs(path):
    rows = []
    with open(path) as f:
        for line in f:
            if line.startswith("#") or not line.strip():
                continue
            p = line.rstrip("\n").split("\t")
            rows.append((p[0], p[1], p[2]))
    return rows


def stream(repo, name, extra=None):
    from datasets import load_dataset
    return load_dataset(repo, name=name, split="train", streaming=True, revision=REVISIONS[repo])


def collect(ds, source, budget, ctx, out, stats, text_field="text", score_floor=None):
    keys, min_doc, max_doc = ctx["keys"], ctx["min_doc"], ctx["max_doc"]
    chars = 0
    read = 0
    for rec in ds:
        read += 1
        t = rec[text_field]
        if score_floor is not None and rec.get("language_score", 1.0) < score_floor:
            stats["low_score"] += 1
            continue
        if len(t) < min_doc:
            stats["short"] += 1
            continue
        if len(t) > max_doc:
            t = t[:max_doc]
        hit = False
        for seg in segments(t):
            k = key(seg)
            if k is not None and keys[min(int(np.searchsorted(keys, np.uint64(k))), len(keys) - 1)] == np.uint64(k):
                hit = True
                break
        if hit:
            stats["dropped_eval_overlap"] += 1
            continue
        out.write(json.dumps({"s": source, "t": t}, ensure_ascii=False) + "\n")
        stats["docs_" + source] += 1
        chars += len(t)
        if chars >= budget:
            break
    stats["read_" + source] = read
    stats["chars_" + source] = chars


def fetch_one(args):
    iso, fw, wiki, outdir, keys_path, fw_chars, wiki_chars, min_doc, max_doc = args
    final = os.path.join(outdir, iso + ".jsonl")
    if os.path.exists(os.path.join(outdir, iso + ".stats.json")):
        return iso, "skipped (done)"
    keys = np.load(keys_path)
    ctx = {"keys": keys, "min_doc": min_doc, "max_doc": max_doc}
    stats = {"iso": iso, "fineweb": fw, "wiki": wiki, "dropped_eval_overlap": 0, "short": 0, "low_score": 0,
             "docs_fw": 0, "docs_wiki": 0}
    t0 = time.time()
    tmp = final + ".part"
    errors = []
    with open(tmp, "w", encoding="utf-8") as out:
        for source, repo, name, budget, floor in (
            ("fw", "HuggingFaceFW/fineweb" if fw == "sample-10BT" else "HuggingFaceFW/fineweb-2", fw, fw_chars, 0.9),
            ("wiki", "wikimedia/wikipedia", f"{WIKI_DUMP}.{wiki}", wiki_chars, None),
        ):
            if name in ("-", f"{WIKI_DUMP}.-"):
                continue
            for attempt in range(3):
                try:
                    ds = stream(repo, name)
                    collect(ds, source, budget, ctx, out, stats, score_floor=floor)
                    break
                except Exception as e:  # network or missing subset: record and retry once more, then move on
                    errors.append(f"{source}:{type(e).__name__}:{str(e)[:120]}")
                    time.sleep(5)
    stats["errors"] = errors
    stats["seconds"] = round(time.time() - t0, 1)
    os.replace(tmp, final)
    with open(os.path.join(outdir, iso + ".stats.json"), "w") as f:
        json.dump(stats, f)
    return iso, json.dumps({k: stats[k] for k in ("docs_fw", "docs_wiki", "dropped_eval_overlap", "seconds")}) + (" ERR " + errors[-1] if errors else "")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--langs", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--keys", required=True)
    ap.add_argument("--fw-chars", type=int, default=24_000_000)
    ap.add_argument("--wiki-chars", type=int, default=6_000_000)
    ap.add_argument("--min-doc-chars", type=int, default=200)
    ap.add_argument("--max-doc-chars", type=int, default=6000)
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--only", default="")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    only = set(filter(None, a.only.split(",")))
    jobs = [(i, fw, w, a.out, a.keys, a.fw_chars, a.wiki_chars, a.min_doc_chars, a.max_doc_chars)
            for i, fw, w in read_langs(a.langs) if not only or i in only]
    with ProcessPoolExecutor(a.workers) as ex:
        for iso, msg in ex.map(fetch_one, jobs):
            print(iso, msg, flush=True)


if __name__ == "__main__":
    main()
