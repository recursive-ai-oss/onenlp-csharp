#!/usr/bin/env python3
"""Turns the OneNlp.Eval JSON files of score_lang.sh into Markdown result tables.

usage: summarize.py <ci-dir> <lang> [<lang> ...]   (env STAGE=<predictions-root> adds fallback rows)

<ci-dir>/<lang>/ holds ud-<test>-<raw|gold|gold-gp>.json and ner-<test>[-gp].json. One table per test set, one
row per mode, each cell 'score [low, high]' (95% bootstrap interval, 1000 resamples over sentences).
"""
import glob
import json
import os
import sys

UD = ["Tokens", "Sentences", "Words", "UPOS", "XPOS", "UFeats", "AllTags", "Lemmas", "UAS", "LAS"]


def cell(r):
    s = r["score"]
    return f"{s['estimate']:.2f} [{s['low']:.2f}, {s['high']:.2f}]"


def conll18_row(path):
    """F1 column of the shared-task script's table (no interval) for a test the OneNlp.Eval reader rejected."""
    f1 = {}
    for line in open(path, encoding="utf-8"):
        c = [x.strip() for x in line.split("|")]
        if len(c) >= 4 and c[0] in UD:
            f1[c[0]] = c[3]
    return f1


def main():
    root, langs = sys.argv[1], sys.argv[2:]
    stage = os.environ.get("STAGE")
    for lang in langs:
        d = os.path.join(root, lang)
        tests = sorted({os.path.basename(f)[3:].rsplit("-", 1)[0].replace("-gold", "")
                        for f in glob.glob(os.path.join(d, "ud-*.json"))})
        seen = []
        for f in sorted(glob.glob(os.path.join(d, "ud-*.json"))):
            b = os.path.basename(f)[3:-5]
            test = b.split("-")[0]
            if test not in seen:
                seen.append(test)
        if stage:  # tests with predictions but no OneNlp.Eval output
            for f in sorted(glob.glob(os.path.join(stage, lang, "scores-ud-*-gold.txt"))):
                t = os.path.basename(f)[len("scores-ud-"):-len("-gold.txt")]
                if t not in seen:
                    seen.append(t)
        for test in seen:
            print(f"\n**{lang}: UD r2.18 {test} test**\n")
            print("| Input | " + " | ".join(UD) + " |")
            print("|---|" + "---|" * len(UD))
            for mode, label in (("raw", "raw text"), ("gold", "gold tokens"), ("gold-gp", "gold tokens, gold_preproc training")):
                p = os.path.join(d, f"ud-{test}-{mode}.json")
                if not os.path.exists(p):
                    q = os.path.join(stage or "", lang, f"scores-ud-{test}-{mode.replace('gold-gp', 'gold-gp')}.txt")
                    if stage and os.path.exists(q):
                        r = conll18_row(q)
                        print(f"| {label} (script, no interval) | " + " | ".join(r.get(m, "n/a") for m in UD) + " |")
                    continue
                rows = {r["metric"]: r for r in json.load(open(p))}
                print(f"| {label} | " + " | ".join(cell(rows[m]) for m in UD) + " |")
        for f in sorted(glob.glob(os.path.join(d, "ner-*.json"))):
            b = os.path.basename(f)[4:-5]
            rows = {r["metric"]: r for r in json.load(open(f))}
            gp = b.endswith("-gp")
            print(f"\n**{lang}: NER {b[:-3] if gp else b} test, gold tokens{', gold_preproc training' if gp else ''}**\n")
            print("| micro F1 | micro precision | micro recall |\n|---|---|---|")
            print(f"| {cell(rows['micro-F1'])} | {cell(rows['micro-precision'])} | {cell(rows['micro-recall'])} |")


main()
