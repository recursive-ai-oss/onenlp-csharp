#!/usr/bin/env python3
"""Fetch more English web and encyclopedia text for open-type NER labelling.

    fetch_text.py --out en-more.jsonl --keys eval-keys.npy [--fw-chars 50000000] [--wiki-chars 30000000] [--seed 48]

Same sources, pinned revisions and eval-overlap rule as ../distill/fetch_data.py (FineWeb sample-10BT, ODC-By 1.0;
Wikipedia 20231101, CC BY-SA 3.0 / GFDL). Documents are drawn through a seeded shuffle buffer so they are not the
first documents of the dump; text already used by the earlier pilot is removed later by prep.py --skip-ids-file.
Writes {"s": source, "t": text} lines.
"""
import argparse
import os
import sys
from collections import Counter

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "distill"))
import fetch_data  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--keys", required=True)
    ap.add_argument("--fw-chars", type=int, default=50_000_000)
    ap.add_argument("--wiki-chars", type=int, default=30_000_000)
    ap.add_argument("--seed", type=int, default=48)
    a = ap.parse_args()
    ctx = {"keys": np.load(a.keys), "min_doc": 200, "max_doc": 6000}
    stats = Counter()
    with open(a.out, "w", encoding="utf-8") as out:
        for source, repo, name, budget, floor in (
            ("fw", "HuggingFaceFW/fineweb", "sample-10BT", a.fw_chars, None),
            ("wiki", "wikimedia/wikipedia", "20231101.en", a.wiki_chars, None),
        ):
            ds = fetch_data.stream(repo, name).shuffle(seed=a.seed, buffer_size=20000)
            fetch_data.collect(ds, source, budget, ctx, out, stats, score_floor=floor)
    print(dict(stats))


if __name__ == "__main__":
    main()
