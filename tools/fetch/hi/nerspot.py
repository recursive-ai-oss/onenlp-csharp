#!/usr/bin/env python3
"""Builds testdata/hi/nerspot.iob2: a spot check of Hindi entity labels that we assigned by hand.

    nerspot.py <candidates.txt> <out.iob2>

The candidates are 100 random sentences (6 to 35 tokens) of held-out documents of the labelled FineWeb-2 Hindi text, one per line as
"N<k><TAB>1:form 2:form ...". Entities are PER, ORG and LOC, written below as (first token, last token, type) with 1-based token
numbers, from our own reading of the sentences: named people, organizations and places only (generic institutions such as
"city council", titles, film names and nicknames are left out). Sentences with explicit sexual content are skipped and not
redistributed. Not a gated row: there is no allowed Hindi entity gold.
"""
import sys

ENT = {
 2: [(1, 1, "LOC")], 4: [(13, 13, "PER")], 6: [(4, 4, "ORG"), (6, 8, "PER")], 8: [(3, 4, "PER")],
 9: [(1, 1, "LOC"), (3, 3, "LOC")], 10: [(5, 6, "PER"), (9, 10, "PER")], 13: [(1, 1, "ORG")], 17: [(1, 1, "LOC")],
 18: [(1, 1, "PER"), (9, 9, "ORG"), (11, 13, "PER")], 23: [(1, 2, "LOC"), (4, 4, "LOC"), (9, 10, "PER")], 24: [(1, 1, "LOC")],
 25: [(1, 1, "PER"), (4, 7, "ORG")], 26: [(1, 2, "PER"), (11, 12, "PER")], 31: [(1, 4, "ORG"), (8, 12, "PER")],
 32: [(1, 2, "PER")], 34: [(6, 7, "PER")], 35: [(6, 7, "ORG"), (12, 13, "ORG")],
 40: [(1, 1, "LOC"), (5, 6, "PER"), (12, 13, "PER"), (14, 14, "PER"), (22, 22, "LOC")], 43: [(1, 1, "PER")],
 51: [(1, 2, "PER"), (3, 4, "LOC"), (6, 8, "ORG")], 52: [(1, 1, "ORG"), (6, 6, "ORG")], 53: [(4, 4, "PER"), (6, 6, "PER")],
 54: [(3, 4, "LOC")], 55: [(1, 3, "ORG")], 57: [(25, 26, "PER")], 58: [(1, 1, "ORG")], 59: [(2, 2, "PER")],
 65: [(2, 2, "PER"), (6, 6, "PER")], 72: [(6, 6, "PER"), (8, 8, "PER")], 76: [(5, 5, "LOC")], 79: [(12, 12, "PER")],
 91: [(5, 5, "PER")], 92: [(1, 3, "ORG"), (6, 7, "PER"), (11, 11, "PER")], 97: [(1, 2, "ORG"), (4, 4, "PER"), (16, 17, "PER"), (21, 23, "PER")],
 99: [(11, 12, "PER")],
}
SKIPPED = {3, 12, 27, 42, 46, 48, 60, 80, 89}

out = []
kept = entities = 0
for line in open(sys.argv[1], encoding="utf-8"):
    key, rest = line.rstrip("\n").split("\t")
    k = int(key[1:])
    if k in SKIPPED:
        continue
    forms = [t.split(":", 1)[1] for t in rest.split(" ")]
    tags = ["O"] * len(forms)
    for a, b, label in ENT.get(k, []):
        assert 1 <= a <= b <= len(forms), (k, a, b)
        tags[a - 1] = "B-" + label
        for i in range(a, b):
            tags[i] = "I-" + label
        entities += 1
    kept += 1
    out.append("# text = " + " ".join(forms))
    out.extend(f"{i + 1}\t{f}\t{t}" for i, (f, t) in enumerate(zip(forms, tags)))
    out.append("")
open(sys.argv[2], "w", encoding="utf-8").write("\n".join(out) + "\n")
print(kept, "sentences", entities, "entities")
