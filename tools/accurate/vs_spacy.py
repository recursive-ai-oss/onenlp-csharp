#!/usr/bin/env python3
"""Our scores beside the black-box spaCy baselines trained on the same data (tools/baselines/spacy/lang/PROVENANCE.md), per language and input mode.

  vs_spacy.py SCORE_DIR PROVENANCE.md > table.md

Information only: the spaCy predictions were not at hand, so the comparison is of two separately bootstrapped intervals, not a paired one.
"overlap" means the intervals overlap, "ahead" and "behind" that they do not.
"""
import glob
import json
import os
import re
import sys

score_dir, prov = sys.argv[1], sys.argv[2]
COLS = ["Tokens", "Sentences", "Words", "UPOS", "XPOS", "UFeats", "AllTags", "Lemmas", "UAS", "LAS"]
KEEP = ["UPOS", "UFeats", "Lemmas", "UAS", "LAS"]


def cell(text):
    m = re.match(r"([\d.]+) \[([\d.]+), ([\d.]+)\]", text.strip())
    return (float(m.group(1)), float(m.group(2)), float(m.group(3))) if m else None


spacy = {}
section = None
for line in open(prov, encoding="utf-8"):
    m = re.match(r"\*\*(\w+): UD r2\.18 (\S+) test\*\*", line)
    if m:
        section = m.group(2) + "-ud-test"
        continue
    if line.startswith("**"):
        section = None
    if section and (line.startswith("| raw text") or line.startswith("| gold tokens")):
        mode = "raw" if "raw" in line.split("|")[1] else "gold"
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        for col, c in zip(COLS, cells[1:]):
            if cell(c):
                spacy[(section, mode, col)] = cell(c)
    n = re.match(r"\*\*(\w+): NER (\S+) test, gold tokens\*\*", line)
    if n:
        section = "ner:" + n.group(2)
    if section and section.startswith("ner:") and re.match(r"\| [\d.]+ \[", line):
        spacy[(section, "gold", "F1")] = cell(line.strip().strip("|").split("|")[0])
        section = None

print("| Test set | Input | Metric | Ours [95% CI] | spaCy 3.8.18 [95% CI] | Intervals |")
print("|---|---|---|---|---|---|")
for f in sorted(glob.glob(os.path.join(score_dir, "*-test.*.json"))):
    name = os.path.basename(f).split(".")[0]
    mode = os.path.basename(f).split(".")[1]
    for e in json.load(open(f)):
        col = {"Tokens": "Tokens", "Sents": "Sentences", "Sentences": "Sentences"}.get(e["metric"], e["metric"])
        if col not in KEEP:
            continue
        ours = (e["score"]["estimate"], e["score"]["low"], e["score"]["high"])
        ref = spacy.get((name, mode, col))
        if not ref:
            continue
        verdict = "ahead" if ours[1] > ref[2] else "behind" if ours[2] < ref[1] else "overlap"
        print(f"| {name.replace('-test', '')} | {mode} | {col} | {ours[0]:.2f} [{ours[1]:.2f}, {ours[2]:.2f}] | {ref[0]:.2f} [{ref[1]:.2f}, {ref[2]:.2f}] | {verdict} |")
