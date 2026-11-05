#!/usr/bin/env python3
"""Token-level differences between a gold CoNLL-U file and a tokenizer's CoNLL-U output over the same text.

    seg_diff.py gold.conllu pred.conllu [max-rows]

Tokens are compared as spans over the text with white space removed (both files hold the same characters), so sentence
boundaries do not matter. Prints counts and the most frequent gold and predicted forms that have no partner.
"""
import collections
import sys


def tokens(path):
    out = []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line or line.startswith("#"):
            continue
        c = line.split("\t")
        if not c[0].isdigit():
            continue
        out.append(c[1])
    return out


def spans(forms):
    pos, res = 0, []
    for f in forms:
        n = len(f.replace(" ", ""))
        res.append((pos, pos + n, f))
        pos += n
    return res


gold, pred = spans(tokens(sys.argv[1])), spans(tokens(sys.argv[2]))
gs, ps = {(a, b) for a, b, _ in gold}, {(a, b) for a, b, _ in pred}
missed = collections.Counter(f for a, b, f in gold if (a, b) not in ps)
extra = collections.Counter(f for a, b, f in pred if (a, b) not in gs)
tp = len(gs & ps)
print(f"gold={len(gs)} pred={len(ps)} correct={tp} P={tp/len(ps):.4f} R={tp/len(gs):.4f} F1={2*tp/(len(gs)+len(ps)):.4f}")
n = int(sys.argv[3]) if len(sys.argv) > 3 else 40
print("gold tokens not produced:", missed.most_common(n))
print("produced tokens not in gold:", extra.most_common(n))
