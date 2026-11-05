#!/usr/bin/env python3
"""Counts the multiword tokens of a Turkish UD training file, to choose the tokenizer's splits from the treebank itself.

usage: mwt_stats.py <train.conllu> [min-count] [dev.conllu]

Prints, for every surface form written as a multiword token at least min-count times: how often it is split, how often the same form
is a single word, and the lengths of its pieces; then the words that split off at the end of a multiword token (the suffix words) with
how often a token ending in that suffix is split. With a dev file, reports how many dev multiword tokens the training list covers.
Only the training split decides what goes into the data; the development split only measures it.
"""
import collections
import sys


def lower(s):
    # Turkish case mapping: I -> ı, İ -> i (the neutral str.lower() maps İ to i + combining dot).
    return s.replace("İ", "i").replace("I", "ı").lower()


def read(path):
    forms = collections.Counter()
    split = collections.defaultdict(collections.Counter)
    with open(path, encoding="utf8") as f:
        sent = []
        for line in list(f) + [""]:
            line = line.rstrip("\n")
            if line.startswith("#"):
                continue
            if not line:
                i = 0
                while i < len(sent):
                    c = sent[i]
                    if "-" in c[0]:
                        a, b = map(int, c[0].split("-"))
                        words = [w[1] for w in sent[i + 1 : i + 1 + (b - a + 1)]]
                        split[lower(c[1])][tuple(len(w) for w in words)] += 1
                        i += 1 + (b - a + 1)
                    else:
                        forms[lower(c[1])] += 1
                        i += 1
                sent = []
                continue
            c = line.split("\t")
            if "." in c[0]:
                continue
            sent.append(c)
    return forms, split


def main():
    train = sys.argv[1]
    min_count = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    forms, split = read(train)
    total = sum(sum(v.values()) for v in split.values())
    print(f"multiword tokens: {total}, distinct forms {len(split)}")
    rows = []
    for f, v in split.items():
        n = sum(v.values())
        if n >= min_count:
            rows.append((n, f, forms.get(f, 0), v.most_common(2)))
    rows.sort(reverse=True)
    for n, f, single, v in rows[:60]:
        print(f"{f}\tsplit {n}\tsingle {single}\t{v}")
    print("forms that always split, and never occur whole:", sum(1 for r in rows if r[2] == 0), "of", len(rows))
    if len(sys.argv) > 3:
        _, dsplit = read(sys.argv[3])
        known = {f for f, v in split.items() if sum(v.values()) >= min_count and sum(v.values()) > forms.get(f, 0)}
        dn = sum(sum(v.values()) for v in dsplit.values())
        covered = sum(sum(v.values()) for f, v in dsplit.items() if f in known)
        print(f"dev multiword tokens {dn}, covered by the list ({len(known)} forms) {covered}")


main()
