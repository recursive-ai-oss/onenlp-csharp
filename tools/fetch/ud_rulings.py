#!/usr/bin/env python3
"""Applies the rulings on `unclear` treebanks to the catalogue tables, in place.

usage: ud_rulings.py <ud-dir>          (the directory holding treebanks.tsv and languages.tsv)

Rulings (2026-11-07):
  1. README says non-commercial and LICENSE.txt is the generic template: no-go, the README statement wins.
  2. CC BY-SA annotation over platform or terms-restricted text: no-go.
  3. Polish-LFG (GNU GPL 3.0 on a treebank): no-go.
  4. Ligurian-GLT (Computational Use of Data Agreement 1.0): go, credited. Already `go` in the tables.
So no treebank stays `unclear`. A language whose only data was unclear becomes a `gap`.
Re-running is a no-op. ud_catalog.py applies the same rule when it rebuilds the tables from the survey.
"""
import csv
import os
import sys


def rewrite(path, rows, fields):
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fields, delimiter="\t", lineterminator="\n")
        w.writeheader()
        w.writerows(rows)


def main():
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    d = sys.argv[1]
    tb_path, lg_path = os.path.join(d, "treebanks.tsv"), os.path.join(d, "languages.tsv")
    with open(tb_path, encoding="utf-8") as f:
        r = csv.DictReader(f, delimiter="\t")
        tf, tbs = r.fieldnames, list(r)
    with open(lg_path, encoding="utf-8") as f:
        r = csv.DictReader(f, delimiter="\t")
        lf, lgs = r.fieldnames, list(r)
    ruled = {}
    for t in tbs:
        if t["verdict"] == "unclear":
            rule = "ruled no-go (README statement over a template LICENSE.txt)" if t["flag"] == "conflict" else "ruled no-go (terms-restricted text)"
            if t["repo"] == "UD_Polish-LFG":
                rule = "ruled no-go (GNU GPL 3.0 on a treebank)"
            t["verdict"], t["reason"] = "no-go", rule + ": " + t["reason"]
            ruled[t["repo"][3:]] = True
    for l in lgs:
        if l["unclear"]:
            l["no_go"] = ";".join(x for x in (l["no_go"], l["unclear"]) if x)
            l["unclear"] = ""
        if l["klass"] == "pending":
            l["klass"] = "gap"
    rewrite(tb_path, tbs, tf)
    rewrite(lg_path, lgs, lf)
    print("ruled:", len(ruled), "treebanks;", sum(1 for l in lgs if l["klass"] == "gap"), "gap languages")


if __name__ == "__main__":
    main()
