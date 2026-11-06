#!/usr/bin/env python3
"""Counts the multiword tokens of CoNLL-U files: surface form, the words it splits into and how often.

Used to choose the Italian tokenizer's contraction table from the training and development files only.
Usage: mwt_counts.py file.conllu [...]
"""
import collections
import sys


def main(paths):
    counts = collections.Counter()
    whole = collections.Counter()
    for path in paths:
        skip_to = 0
        with open(path, encoding="utf-8") as f:
            for line in f:
                line = line.rstrip("\n")
                if not line or line.startswith("#"):
                    skip_to = 0
                    continue
                cols = line.split("\t")
                if "-" in cols[0] and cols[0].replace("-", "").isdigit():
                    a, b = cols[0].split("-")
                    skip_to = int(b)
                    words = []
                    cur = [cols[1], int(a), int(b)]
                    pending = cur
                    continue
                if "." in cols[0]:
                    continue
                i = int(cols[0])
                if skip_to and i <= skip_to:
                    pending.append(cols[1])
                    if i == skip_to:
                        counts[(pending[0], tuple(pending[3:]))] += 1
                        skip_to = 0
                else:
                    whole[cols[1].lower()] += 1
    for (surface, words), n in sorted(counts.items(), key=lambda kv: -kv[1]):
        print(f"{n}\t{surface}\t{'|'.join(words)}\twhole={whole.get(surface.lower(), 0)}")


if __name__ == "__main__":
    main(sys.argv[1:])
