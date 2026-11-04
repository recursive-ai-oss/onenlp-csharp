#!/usr/bin/env python3
"""Entity-level precision/recall/F1 over a predict_ner.py output, with seqeval (black box, MIT).

usage: score_ner.py <predictions.tsv>
Prints seqeval's default (conlleval-compatible) report.
"""
import sys

from seqeval.metrics import classification_report, f1_score

gold, pred, g, p = [], [], [], []
for line in open(sys.argv[1], encoding="utf-8"):
    line = line.rstrip("\n")
    if not line:
        gold.append(g)
        pred.append(p)
        g, p = [], []
    else:
        _, a, b = line.split("\t")
        g.append(a)
        p.append(b)
print(classification_report(gold, pred, digits=4))
print("micro-F1", f1_score(gold, pred))
