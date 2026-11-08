#!/usr/bin/env python3
"""Proposes lemma-table settings for every language of the corpus spec, from its development split alone.

usage: lexicon_proposals.py <data-root> <corpus.json> <out.tsv>

A lemma table (LemmaLexicon) maps lower-cased form + UPOS to the lemma most often seen in the training treebank, and is kept in front of the
model's own edit-tree lemmas. It helps where the table is right more often than the model, so a setting is judged by the table's precision on
the words of the dev split that it covers. Here the UPOS is the gold one, because no model exists yet; lexicon_dev.py repeats the choice with
the model's predictions and its own base accuracy once a run has produced them, and that choice is the one that ships.

Grid: minimum count 1 or 2, minimum share 0.6, 0.8, 0.9, 1.0. The proposal is the setting with the highest coverage whose dev precision is at
least 97%; `none` when no setting reaches it or the language has no dev split. The training side is the spec's training sentences after the
same hold-out as the reader (every n-th sentence of a language with no dev split), so the table never saw the words it is judged on.
"""
import collections
import json
import os
import sys

PRECISION_FLOOR = 0.97


def sentences(path):
    """Yields lists of (form, lemma, upos) per sentence of a CoNLL-U file; multiword ranges and empty nodes are skipped."""
    cur = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line:
                if cur:
                    yield cur
                cur = []
            elif line[0] != "#":
                c = line.split("\t")
                if "-" not in c[0] and "." not in c[0]:
                    cur.append((c[1], c[2], c[3]))
    if cur:
        yield cur


def main(root, spec_path, out):
    spec = json.load(open(spec_path, encoding="utf-8"))
    every = spec["heldOutEvery"]
    rows = []
    for lang in spec["languages"]:
        train, dev = [], []
        i = 0
        for tb in lang["treebanks"]:
            for s in sentences(os.path.join(root, tb["train"])):
                if lang["devFromTrain"] and i % every == every - 1:
                    dev.append(s)
                else:
                    train.append(s)
                i += 1
            if tb.get("dev"):
                dev.extend(sentences(os.path.join(root, tb["dev"])))
        counts = collections.defaultdict(collections.Counter)
        for s in train:
            for form, lemma, upos in s:
                if lemma != "_" and upos != "_":
                    counts[(form.lower(), upos)][lemma] += 1
        words = [w for s in dev for w in s]
        best = None
        if words:
            for min_count in (1, 2):
                for share in (0.6, 0.8, 0.9, 1.0):
                    table = {}
                    for key, c in counts.items():
                        lemma, top = c.most_common(1)[0]
                        n = sum(c.values())
                        if n >= min_count and top >= share * n:
                            table[key] = lemma
                    hit = right = 0
                    for form, lemma, upos in words:
                        t = table.get((form.lower(), upos))
                        if t is not None:
                            hit += 1
                            right += t == lemma
                    if hit and right / hit >= PRECISION_FLOOR:
                        cand = (hit / len(words), min_count, share, right / hit, len(table))
                        if best is None or cand[0] > best[0]:
                            best = cand
        if best:
            rows.append((lang["code"], len(words), f"{best[1]}:{best[2]}", f"{100 * best[0]:.1f}", f"{100 * best[3]:.1f}", best[4]))
        else:
            rows.append((lang["code"], len(words), "none", "", "", 0))
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("code\tdev_words\tsetting\tcoverage_pct\tprecision_pct\tentries\n")
        for r in rows:
            f.write("\t".join(str(x) for x in r) + "\n")
    print(len(rows), "languages;", sum(1 for r in rows if r[2] != "none"), "with a proposed table")


if __name__ == "__main__":
    main(*sys.argv[1:4])
