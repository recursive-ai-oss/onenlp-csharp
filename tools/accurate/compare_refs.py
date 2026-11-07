#!/usr/bin/env python3
"""Puts the scores of recipes/accurate/multilingual (the JSON files of score_ml.sh) beside the published rows of docs/references.md.

  compare_refs.py SCORE_DIR docs/references.md > comparison.md

A row passes when the published number is not above our 95% interval's upper end ("within the interval, or ahead"). Each published system
is a row of its own: Stanza, UDPipe 2 and Trankit disagree by up to several points and none is averaged away. Gold-token scores are set
against the published gold-token row (UDPipe 2), raw-text scores against the published raw rows. The UD release of the reference is shown;
rows are information where it differs from ours (r2.18) in a way the reference notes say matters.
"""
import json, os, re, sys

score_dir, refs_path = sys.argv[1], sys.argv[2]
COLS = ["Tokens", "Sents", "Words", "UPOS", "XPOS", "UFeats", "AllTags", "Lemmas", "UAS", "LAS"]
SECTION = {"zh": "Chinese", "fr": "French", "es": "Spanish", "pt": "Portuguese", "de": "German", "it": "Italian", "tr": "Turkish", "nl": "Dutch", "en": "English-EWT"}
DATASET = {"en_ewt-ud-test": ("en", None), "de_gsd-ud-test": ("de", None), "fr_gsd-ud-test": ("fr", None), "es_ancora-ud-test": ("es", None),
           "pt_bosque-ud-test": ("pt", None), "it_parlamint-ud-test": ("it", "ParlaMint"), "it_markit-ud-test": ("it", "MarkIT"),
           "nl_alpino-ud-test": ("nl", None), "tr_boun-ud-test": ("tr", None), "zh_gsdsimp-ud-test": ("zh", None)}

# Published rows: {section: [(system, release, input, {col: value})]}
rows, section = {}, None
for line in open(refs_path, encoding="utf8"):
    m = re.match(r"^##+ (.*)", line)
    if m:
        section = m.group(1)
        continue
    if section and line.startswith("|") and not line.startswith("|---") and "System (source)" not in line:
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) == 13 and cells[2] in ("raw", "gold"):
            vals = {}
            for c, v in zip(COLS, cells[3:]):
                try:
                    vals[c] = float(v)
                except ValueError:
                    pass
            rows.setdefault(section, []).append((cells[0], cells[1], cells[2], vals))

# "same model" rows belong to the treebank of the row above them
row_tb, last_tb = {}, None
for sec_name, lst in rows.items():
    for system, release, inp, vals in lst:
        for name in ("ParlaMint", "MarkIT"):
            if name in system:
                last_tb = name
        row_tb[(sec_name, system, inp)] = last_tb

ours = {}
for name in sorted(os.listdir(score_dir)):
    m = re.match(r"(.*)\.(gold|raw)\.json$", name)
    if not m or m.group(1) not in DATASET:
        continue
    data = json.load(open(os.path.join(score_dir, name)))
    entries = data if isinstance(data, list) else data.get("results", data.get("rows", []))
    for e in entries:
        metric = {"Sentences": "Sents"}.get(e["metric"], e["metric"])
        ours[(m.group(1), m.group(2), metric)] = (e["score"]["estimate"], e["score"]["low"], e["score"]["high"])

print("| Language / treebank | Input | Metric | Ours [95% CI] | Reference system (UD release) | Reference | Verdict |")
print("|---|---|---|---|---|---|---|")
misses = 0
total = 0
for (ds, mode, metric), (score, lo, hi) in sorted(ours.items()):
    lang, tb = DATASET[ds]
    sec = [s for s in rows if SECTION[lang] in s]
    if not sec:
        continue
    for system, release, inp, vals in rows[sec[0]]:
        if inp != mode or metric not in vals:
            continue
        if tb and tb != row_tb[(sec[0], system, inp)]:
            continue
        if lang == "en" and "EWT" not in sec[0]:
            continue
        total += 1
        ref = vals[metric]
        ok = ref <= hi
        misses += 0 if ok else 1
        print(f"| {ds} | {mode} | {metric} | {score:.2f} [{lo:.2f}, {hi:.2f}] | {system} ({release}) | {ref:.2f} | {'within or ahead' if ok else '**MISS**'} |")
print(f"\n{total} comparisons, {misses} misses", file=sys.stderr)
