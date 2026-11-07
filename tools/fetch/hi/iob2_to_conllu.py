#!/usr/bin/env python3
"""Writes the tokens of an .iob2 file as a CoNLL-U file with no annotation, so that the labeller can be run on them.

    iob2_to_conllu.py in.iob2 out.conllu
"""
import sys

out, forms = [], []


def flush():
    if forms:
        out.append("# text = " + " ".join(forms))
        out.extend(f"{i + 1}\t{f}\t_\t_\t_\t_\t0\t_\t_\t_" for i, f in enumerate(forms))
        out.append("")
        forms.clear()


for line in open(sys.argv[1], encoding="utf-8"):
    line = line.rstrip("\n")
    if line.startswith("#"):
        continue
    if not line:
        flush()
    else:
        forms.append(line.split("\t")[1])
flush()
open(sys.argv[2], "w", encoding="utf-8").write("\n".join(out) + "\n")
