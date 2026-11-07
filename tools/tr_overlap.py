#!/usr/bin/env python3
"""Counts pseudo-labelled sentences that share text with evaluation files.

usage: tr_overlap.py pseudo.conllu eval1.conllu [eval2.conllu ...]

A pseudo sentence overlaps when it equals an evaluation sentence word for word, or shares a run of N=8 lower-cased words with one.
"""
import sys

N = 8


def sentences(path):
    cur = []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line:
            if cur:
                yield cur
                cur = []
        elif not line.startswith("#"):
            f = line.split("\t")
            if f[0].isdigit():
                cur.append(f[1].lower())
    if cur:
        yield cur


grams, whole = set(), set()
for p in sys.argv[2:]:
    for s in sentences(p):
        whole.add(tuple(s))
        for i in range(len(s) - N + 1):
            grams.add(tuple(s[i:i + N]))
total = exact = shared = 0
for s in sentences(sys.argv[1]):
    total += 1
    exact += tuple(s) in whole
    shared += any(tuple(s[i:i + N]) in grams for i in range(len(s) - N + 1))
print(f"{total} pseudo sentences, {exact} equal to an evaluation sentence, {shared} share an {N}-word run with one")
