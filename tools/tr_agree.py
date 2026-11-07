#!/usr/bin/env python3
"""Keeps the sentences that two parsers analyse identically (the same head and relation for every word).

usage: tr_agree.py a.conllu b.conllu out.conllu [min-agreement [max-sentences [exclude.conllu]]]

Sentences whose words occur in exclude.conllu are dropped.

min-agreement is the share of words with the same head and relation, 1.0 by default.

Both files come from `label` on the same text with the same tokenizer, so sentences are paired by position; a pair whose words
differ is dropped. The output is file a's sentences.
"""
import sys


def sents(path):
    cur, out = [], []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if line:
            cur.append(line)
        elif cur:
            out.append(cur)
            cur = []
    if cur:
        out.append(cur)
    return out


def key(s):
    return [tuple(l.split("\t")[1:2] + l.split("\t")[6:8]) for l in s if not l.startswith("#")]


a, b = sents(sys.argv[1]), sents(sys.argv[2])
kept = 0
skip = set()
if len(sys.argv) > 6:
    skip = {tuple(l.split("\t")[1] for l in s if not l.startswith("#")) for s in sents(sys.argv[6])}
need = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
limit = int(sys.argv[5]) if len(sys.argv) > 5 else 10**9
with open(sys.argv[3], "w", encoding="utf-8") as out:
    for x, y in zip(a, b):
        kx, ky = key(x), key(y)
        same = len(kx) == len(ky) and sum(p == q for p, q in zip(kx, ky)) >= need * len(kx) and [p[0] for p in kx] == [q[0] for q in ky]
        if same and kept < limit and tuple(l.split("\t")[1] for l in x if not l.startswith("#")) not in skip:
            out.write("\n".join(x) + "\n\n")
            kept += 1
print(f"{kept} of {len(a)} sentences agree ({100*kept/max(1,len(a)):.1f}%)")
