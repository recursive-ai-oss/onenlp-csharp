#!/usr/bin/env python3
"""Runs a trained spaCy NER pipeline over a UNER .iob2 file (gold tokens, one sentence at a time)
and writes 'token<TAB>gold<TAB>predicted' lines with a blank line between sentences.

usage: predict_ner.py <model-dir> <gold.iob2> <out.tsv>
"""
import sys

import spacy
from spacy.tokens import Doc



def read_sentences(path):
    text, rows = None, []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("# text = "):
                text = line[len("# text = "):]
            elif line.startswith("#"):
                continue
            elif not line:
                if rows:
                    yield text, rows
                text, rows = None, []
            else:
                cols = line.split("\t")
                rows.append((cols[1], cols[2]))
    if rows:
        yield text, rows


def spaces_for(text, words):
    spaces, pos = [], 0
    for w in words:
        at = text.find(w, pos) if text is not None else -1
        if at < 0:
            return [True] * (len(words) - 1) + [False]
        pos = at + len(w)
        spaces.append(pos < len(text) and text[pos].isspace())
    return spaces


def main():
    model, gold, dst = sys.argv[1:4]
    nlp = spacy.load(model)
    n = 0
    with open(dst, "w", encoding="utf-8") as out:
        for text, rows in read_sentences(gold):
            words = [w for w, _ in rows]
            doc = nlp(Doc(nlp.vocab, words=words, spaces=spaces_for(text, words)))
            for (w, g), t in zip(rows, doc):
                pred = "O" if t.ent_iob_ in ("O", "") else f"{t.ent_iob_}-{t.ent_type_}"
                out.write(f"{w}\t{g}\t{pred}\n")
            out.write("\n")
            n += 1
    print(f"{dst}: {n} sentences")


main()
