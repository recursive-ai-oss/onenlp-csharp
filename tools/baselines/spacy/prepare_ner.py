#!/usr/bin/env python3
"""Converts UNER .iob2 files into spaCy DocBin files (one Doc per sentence, gold tokens).

usage: prepare_ner.py <in.iob2> <out.spacy>

The .iob2 files are CoNLL-U-like: '# text = ...' comments, then one token per line
(id, form, BIO tag, ...). Spaces between tokens are recovered by walking the sentence text.
Uses only spaCy's public Doc and DocBin API.
"""
import sys

import spacy
from spacy.tokens import Doc, DocBin


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
    """True after a word when the sentence text has a space there. Falls back to a single space."""
    spaces, pos = [], 0
    for w in words:
        at = text.find(w, pos) if text is not None else -1
        if at < 0:
            return [True] * (len(words) - 1) + [False]
        pos = at + len(w)
        spaces.append(pos < len(text) and text[pos].isspace())
    return spaces


def main():
    src, dst = sys.argv[1:3]
    vocab = spacy.blank("en").vocab
    db = DocBin(attrs=["ENT_IOB", "ENT_TYPE"])
    n = 0
    for text, rows in read_sentences(src):
        words = [w for w, _ in rows]
        doc = Doc(vocab, words=words, spaces=spaces_for(text, words), ents=[t for _, t in rows])
        db.add(doc)
        n += 1
    db.to_disk(dst)
    print(f"{src}: {n} docs -> {dst}")


main()
