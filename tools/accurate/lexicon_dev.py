#!/usr/bin/env python3
"""Picks the settings of the lemma lexicon on a development set.

For each (minCount, minShare) it builds the table from the training treebank (lower-cased form + UPOS -> most frequent lemma, kept when
its share is at least minShare), puts it in front of the model's predicted lemmas (the model's own UPOS is the key) and scores lemma
accuracy against the gold development file. Gold tokens are used, so words line up by position.

  lexicon_dev.py TRAIN.conllu DEV_GOLD.conllu DEV_PRED.conllu
"""
import collections
import sys


def words(path):
    out = []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line or line[0] == "#":
            continue
        f = line.split("\t")
        if "-" in f[0] or "." in f[0]:
            continue
        out.append(f)
    return out


def main(train_path, gold_path, pred_path):
    counts = collections.defaultdict(collections.Counter)
    for f in words(train_path):
        if f[2] != "_" and f[3] != "_":
            counts[(f[1].lower(), f[3])][f[2]] += 1
    gold, pred = words(gold_path), words(pred_path)
    if len(gold) != len(pred):
        raise SystemExit(f"{len(gold)} gold words but {len(pred)} predicted")
    base = sum(g[2] == p[2] for g, p in zip(gold, pred)) / len(gold) * 100
    print(f"no lexicon: {base:.2f}")
    for min_count in (1, 2):
        for share in (0.6, 0.8, 0.9, 1.0):
            table = {}
            for key, c in counts.items():
                lemma, best = c.most_common(1)[0]
                if sum(c.values()) >= min_count and best >= share * sum(c.values()):
                    table[key] = lemma
            hit = right = 0
            for g, p in zip(gold, pred):
                lemma = table.get((g[1].lower(), p[3]), p[2])
                hit += (g[1].lower(), p[3]) in table
                right += lemma == g[2]
            print(f"minCount={min_count} minShare={share}: {right / len(gold) * 100:.2f}  (entries {len(table)}, hit {hit / len(gold) * 100:.1f}% of words)")


if __name__ == "__main__":
    main(*sys.argv[1:4])
