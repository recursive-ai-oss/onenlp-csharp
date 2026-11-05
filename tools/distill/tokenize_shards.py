#!/usr/bin/env python3
"""Tokenise the fetched text into fixed windows of teacher-tokenizer ids.

    tokenize_shards.py --text DIR --tokenizer tokenizer.json --out DIR [--window 512] [--dev-every 100]

Each document is encoded without special tokens and cut into windows of `window - 2` ids wrapped in the tokenizer's own
begin and end ids (found by encoding a probe text). A window with fewer than --min-tokens ids is dropped. Documents
whose index is a multiple of --dev-every go to the held-out dev file. Output per language: <iso>.train.npy and
<iso>.dev.npy, uint32 [windows, window], padded with the pad id (0); plus tok-stats.json.
"""
import argparse
import json
import os
import sys

import numpy as np
from tokenizers import Tokenizer


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--text", required=True)
    ap.add_argument("--tokenizer", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--window", type=int, default=512)
    ap.add_argument("--dev-every", type=int, default=100)
    ap.add_argument("--min-tokens", type=int, default=24)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    tok = Tokenizer.from_file(a.tokenizer)
    probe = tok.encode("a").ids
    plain = tok.encode("a", add_special_tokens=False).ids
    assert probe[1:-1] == plain and len(probe) == len(plain) + 2, probe
    bos, eos = probe[0], probe[-1]
    body = a.window - 2
    stats = {}
    for fn in sorted(os.listdir(a.text)):
        if not fn.endswith(".jsonl"):
            continue
        iso = fn[:-6]
        out_train, out_dev = a.out + f"/{iso}.train.npy", a.out + f"/{iso}.dev.npy"
        docs = []
        with open(os.path.join(a.text, fn), encoding="utf-8") as f:
            for line in f:
                docs.append(json.loads(line)["t"])
        if not docs:
            stats[iso] = {"docs": 0}
            continue
        enc = tok.encode_batch(docs, add_special_tokens=False)
        rows = {"train": [], "dev": []}
        ntok = 0
        for i, e in enumerate(enc):
            ids = e.ids
            ntok += len(ids)
            dest = rows["dev"] if (i % a.dev_every == a.dev_every - 1) else rows["train"]
            for s in range(0, len(ids), body):
                piece = ids[s:s + body]
                if len(piece) < a.min_tokens:
                    continue
                w = np.zeros(a.window, dtype=np.uint32)
                w[0] = bos
                w[1:1 + len(piece)] = piece
                w[1 + len(piece)] = eos
                dest.append(w)
        for name, path in (("train", out_train), ("dev", out_dev)):
            np.save(path, np.stack(rows[name]) if rows[name] else np.zeros((0, a.window), np.uint32))
        stats[iso] = {"docs": len(docs), "tokens": ntok, "train_windows": len(rows["train"]), "dev_windows": len(rows["dev"]),
                      "chars": sum(len(d) for d in docs)}
        print(iso, stats[iso], flush=True)
    with open(os.path.join(a.out, "tok-stats.json"), "w") as f:
        json.dump({"tokenizer": a.tokenizer, "window": a.window, "bos": bos, "eos": eos, "languages": stats}, f, indent=1)


if __name__ == "__main__":
    sys.exit(main())
