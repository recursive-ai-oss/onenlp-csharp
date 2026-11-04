#!/usr/bin/env python3
"""Builds a throughput corpus from the development split: paragraphs of 20 sentences, one per line, until the size is reached.

Usage: make_bench_text.py <gold.jsonl> <out.txt> [megabytes=10]
The sentence order is a seeded shuffle, so the file is reproducible. Only the development file may be used.
"""
import json
import random
import sys

gold, out = sys.argv[1], sys.argv[2]
limit = int(float(sys.argv[3] if len(sys.argv) > 3 else 10) * 1_000_000)
sentences = [json.loads(line)["text"] for line in open(gold, encoding="utf-8") if line.strip()]
rng = random.Random(20260101)
size = 0
with open(out, "w", encoding="utf-8", newline="\n") as f:
    while size < limit:
        line = " ".join(rng.choice(sentences) for _ in range(20))
        f.write(line + "\n")
        size += len(line.encode("utf-8")) + 1
