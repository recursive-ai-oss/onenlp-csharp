#!/usr/bin/env python3
"""Fetches and prepares the two text-classification datasets and writes JSON Lines in the library's data format.

usage:
  fetch.py banking77 <train-dir> <eval-dir>
  fetch.py eurlex    <train-dir> <eval-dir> [--words 512]

banking77  BANKING77 (PolyAI, CC BY 4.0): the official train and test files; dev is every tenth training text by SHA-256 of the text.
eurlex     LexGLUE EUR-LEX (Chalkidis et al. 2021; English EU legislation labelled by the EU Publications Office with 100 EuroVoc concepts,
           multi-label), official 55,000 / 5,000 / 5,000 train / validation / test split, read from the Parquet files of the
           coastalcph/lex_glue dataset at a pinned revision (cc-by-4.0 on its card; the texts are EUR-Lex documents, reusable under Commission
           Decision 2011/833/EU, Article 6, with acknowledgement of the source). Each document is cut to its first --words whitespace words, the
           usual first-N convention for long documents. Needs pyarrow (the worker's data venv).

Writes <train-dir>/{train,dev}.jsonl and <eval-dir>/test.jsonl ({"id", "text", "cats"}), and a MANIFEST.json in each directory (role,
count, bytes, SHA-256 of each file, source URLs, fetch time). Training code is given only the train directory. Standard library only. The data is never committed.
"""
import argparse
import csv
import hashlib
import io
import json
import os
import time
import urllib.request

BANKING = "https://raw.githubusercontent.com/PolyAI-LDN/task-specific-datasets/master/banking_data/{}.csv"


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "onenlp-data-fetch/1"}), timeout=300) as r:
        return r.read()


def write_split(out, name, rows):
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, name + ".jsonl")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")
    return {"role": {"train": "train", "dev": "dev", "test": "eval"}[name], "file": name + ".jsonl", "documents": len(rows),
            "bytes": os.path.getsize(path), "sha256": sha256_file(path)}


def save(manifest, train_dir, eval_dir):
    for d, keep in ((train_dir, ("train", "dev")), (eval_dir, ("test",))):
        part = dict(manifest, files=[f for f in manifest["files"] if f["file"].split(".")[0] in keep])
        with open(os.path.join(d, "MANIFEST.json"), "w") as f:
            json.dump(part, f, indent=2)


def banking77(out, eval_out):
    raw = {split: fetch(BANKING.format(split)) for split in ("train", "test")}
    licence = fetch("https://raw.githubusercontent.com/PolyAI-LDN/task-specific-datasets/master/LICENSE")
    rows = {}
    for split, data in raw.items():
        rows[split] = [(r["text"], r["category"]) for r in csv.DictReader(io.StringIO(data.decode("utf-8")))]
    labels = sorted({c for _, c in rows["train"]} | {c for _, c in rows["test"]})

    def line(i, text, cat):
        return {"id": i, "text": text, "cats": {c: (1 if c == cat else 0) for c in labels}}

    train, dev = [], []
    for i, (text, cat) in enumerate(rows["train"]):
        bucket = int(sha256_bytes(text.encode("utf-8"))[:8], 16) % 10
        (dev if bucket == 0 else train).append(line("train-%d" % i, text, cat))
    test = [line("test-%d" % i, text, cat) for i, (text, cat) in enumerate(rows["test"])]
    manifest = {
        "dataset": "BANKING77", "source": BANKING.format("{train,test}"), "licence": "CC BY 4.0 (LICENSE of the PolyAI-LDN/task-specific-datasets repository)",
        "licence_sha256": sha256_bytes(licence), "raw_sha256": {k: sha256_bytes(v) for k, v in raw.items()},
        "labels": len(labels), "fetched": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "files": [write_split(out, "train", train), write_split(out, "dev", dev), write_split(eval_out, "test", test)],
    }
    save(manifest, out, eval_out)
    print(json.dumps(manifest["files"]))


LEXGLUE_REV = "c23fdff1a6bf74e0e1a71cb86f1e781d37da888c"
LEXGLUE = "https://huggingface.co/datasets/coastalcph/lex_glue/resolve/" + LEXGLUE_REV + "/eurlex/{}-00000-of-00001.parquet"


def eurlex(out, eval_out, words):
    import pyarrow.parquet as pq
    raw, rows = {}, {}
    for split in ("train", "validation", "test"):
        raw[split] = fetch(LEXGLUE.format(split))
        t = pq.read_table(io.BytesIO(raw[split])).to_pylist()
        rows[split] = [(r["text"], r["labels"]) for r in t]
    labels = sorted({l for rs in rows.values() for _, ls in rs for l in ls})
    names = {l: "c%02d" % l for l in labels}

    def line(prefix, i, text, ls):
        return {"id": "%s-%d" % (prefix, i), "text": " ".join(text.split()[:words]), "cats": {names[l]: (1 if l in ls else 0) for l in labels}}

    manifest = {
        "dataset": "LexGLUE EUR-LEX (EuroVoc multi-label)", "source": LEXGLUE.format("{train,validation,test}"), "revision": LEXGLUE_REV,
        "licence": "cc-by-4.0 (dataset card); EUR-Lex documents reusable under Commission Decision 2011/833/EU Art. 6 with acknowledgement; (c) European Union",
        "credit": "EU Publications Office (EuroVoc annotation); Chalkidis, Fergadiotis, Androutsopoulos 2021 (MultiEURLEX, EMNLP); Chalkidis et al. 2022 (LexGLUE, ACL)",
        "raw_sha256": {k: sha256_bytes(v) for k, v in raw.items()}, "words_per_document": words, "labels": [names[l] for l in labels],
        "split": "official: train / validation (dev) / test",
        "fetched": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "files": [write_split(out, "train", [line("train", i, *r) for i, r in enumerate(rows["train"])]),
                  write_split(out, "dev", [line("dev", i, *r) for i, r in enumerate(rows["validation"])]),
                  write_split(eval_out, "test", [line("test", i, *r) for i, r in enumerate(rows["test"])])],
    }
    save(manifest, out, eval_out)
    print(json.dumps(manifest["files"]), len(labels), "labels")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("dataset", choices=["banking77", "eurlex"])
    p.add_argument("out")
    p.add_argument("eval_out")
    p.add_argument("--words", type=int, default=512)
    a = p.parse_args()
    if a.dataset == "banking77":
        banking77(a.out, a.eval_out)
    else:
        eurlex(a.out, a.eval_out, a.words)


main()
