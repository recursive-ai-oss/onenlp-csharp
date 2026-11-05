#!/usr/bin/env python3
"""Converts UNER .iob2 files into spaCy DocBin files (one Doc per sentence, gold tokens).

usage: prepare_ner.py <in.iob2> <out.spacy> [<lang> [<TYPE=LABEL,...>]]

lang is the spaCy language code of the blank pipeline that supplies the vocab (default en). The optional map
renames entity types and turns every type not listed into O (e.g. PERSON=PER,GPE=LOC,LOC=LOC,ORG=ORG).

The .iob2 files are CoNLL-U-like: '# text = ...' comments, then one token per line
(id, form, BIO tag, ...). Spaces between tokens are recovered by walking the sentence text.
Uses only spaCy's public Doc and DocBin API.
"""
import sys

import spacy
from spacy.tokens import Doc, DocBin


TYPE_MAP = None


def relabel(tag):
    """Applies the optional type map to a BIO tag; unlisted types become O."""
    if TYPE_MAP is None or tag == "O":
        return tag
    prefix, typ = tag.split("-", 1)
    return f"{prefix}-{TYPE_MAP[typ]}" if typ in TYPE_MAP else "O"


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
                rows.append((cols[1], relabel(cols[2])))
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
    global TYPE_MAP
    src, dst = sys.argv[1:3]
    lang = sys.argv[3] if len(sys.argv) > 3 else "en"
    if len(sys.argv) > 4:
        TYPE_MAP = dict(kv.split("=") for kv in sys.argv[4].split(","))
    vocab = spacy.blank(lang).vocab
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
