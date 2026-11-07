#!/usr/bin/env python3
"""Puts the accurate-tier rows next to the fast tier and the published references, one table per language and input mode.

    compare_rows.py --scores DIR --languages tools/fetch/ud/languages.tsv --references docs/references.tsv
                    --fast-table docs/results-table.md [--langs en,de,...] [--names en=English,...] > table.md

DIR holds the JSON files score_student_ml.sh wrote (one per treebank and input mode: <name>.gold.json, <name>.raw.json; a list of rows with
component, language, dataset, metric and a score with estimate, low and high). The gating treebank of each language comes from the language
catalogue, and the published Stanza, UDPipe 2 and Trankit rows from the references table, so a new language is a line in those files.
The fast-tier column is read from the results table of the repository (rows "Ours" with their interval).
"""
import argparse
import csv
import glob
import json
import os
import re

NINE = "en=English,de=German,fr=French,es=Spanish,pt=Portuguese,it=Italian,nl=Dutch,tr=Turkish,zh=Chinese"
METRICS = ["Tokens", "Sentences", "Words", "UPOS", "XPOS", "UFeats", "AllTags", "Lemmas", "UAS", "LAS", "CLAS", "MLAS"]
COLUMN = {"Tokens": "tokens", "Sentences": "sents", "Words": "words", "UPOS": "upos", "XPOS": "xpos", "UFeats": "ufeats", "AllTags": "alltags",
          "Lemmas": "lemmas", "UAS": "uas", "LAS": "las", "CLAS": "clas", "MLAS": "mlas"}


def fast_rows(path):
    """(language, input, metric) -> 'score [low, high]' from the 'Other languages' and English tables of the results table."""
    rows = {}
    language = "English"
    for line in open(path, encoding="utf-8"):
        if line.startswith("### "):
            language = line[4:].strip()
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) == 5 and cells[0] in ("gold", "raw") or (len(cells) == 5 and cells[0].startswith("gold")):
            rows.setdefault((language, "raw" if cells[0] == "raw" else "gold", cells[1].split(" ")[0]), cells[2])
        elif len(cells) == 6 and cells[1] in ("gold", "raw") or (len(cells) == 6 and cells[1].startswith("gold")):
            rows.setdefault((cells[0], "raw" if cells[1] == "raw" else "gold", cells[2].split(" ")[0]), cells[3])
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scores", required=True)
    ap.add_argument("--languages", required=True)
    ap.add_argument("--references", required=True)
    ap.add_argument("--fast-table", required=True)
    ap.add_argument("--langs", default="en,de,fr,es,pt,it,nl,tr,zh")
    ap.add_argument("--names", default=NINE)
    a = ap.parse_args()
    names = dict(x.split("=") for x in a.names.split(","))
    gating = {}
    for r in csv.DictReader(open(a.languages, encoding="utf-8"), delimiter="\t"):
        gating[r["language"]] = r["gating"]
    refs = {}
    for r in csv.DictReader(open(a.references, encoding="utf-8"), delimiter="\t"):
        refs[(r["treebank"], r["input"], r["system"])] = r
    fast = fast_rows(a.fast_table)
    ours = {}
    for f in glob.glob(os.path.join(a.scores, "*.json")):
        kind = "raw" if f.endswith(".raw.json") else "gold" if f.endswith(".gold.json") else None
        if kind is None:
            continue
        for row in json.load(open(f, encoding="utf-8")):
            if row["metric"] in METRICS:
                ours[(row["language"], kind, os.path.basename(f).split(".")[0], row["metric"])] = row["score"]
    for code in a.langs.split(","):
        name = names[code]
        tb = gating.get(name, "")
        for mode in ("gold", "raw"):
            have = sorted({k[2] for k in ours if k[0] == code and k[1] == mode})
            if not have:
                continue
            print(f"\n### {name} ({tb}), {mode} input\n")
            print("| Metric | Distilled tier | Fast tier | Stanza 1.5.1 | UDPipe 2 | Trankit |")
            print("|---|---|---|---|---|---|")
            for m in METRICS:
                for ds in have:
                    s = ours.get((code, mode, ds, m))
                    if s is None:
                        continue
                    cells = [f"{s['estimate']:.2f} [{s['low']:.2f}, {s['high']:.2f}]" + (f" ({ds})" if len(have) > 1 else "")]
                    cells.append(fast.get((name, mode, m), "") if ds == have[0] else "")
                    for system in ("Stanza", "UDPipe 2", "Trankit"):
                        r = refs.get((tb, mode, system))
                        cells.append(r[COLUMN[m]] if r and r.get(COLUMN[m]) else "")
                    print(f"| {m} | " + " | ".join(cells) + " |")


if __name__ == "__main__":
    main()
