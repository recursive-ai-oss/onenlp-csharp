#!/usr/bin/env python3
"""Runs a trained spaCy pipeline over a CoNLL-U test file and writes CoNLL-U predictions.

usage: predict_ud.py <model-dir> <gold.conllu> <raw|gold> <out.conllu>

raw   the pipeline gets only the text of each document (spaCy's own tokenizer and its
      predicted sentence boundaries), as in the CoNLL 2018 shared-task setting.
      The text is rebuilt from the gold forms and SpaceAfter, so the character sequence
      matches the gold file exactly.
gold  the gold words and sentence boundaries are given; only the annotations are predicted.

Uses spaCy's public API only. Heads are written sentence-relative; the root gets head 0 and 'root'.
"""
import sys

import spacy
from spacy.tokens import Doc


def read_conllu(path):
    """Yields documents: lists of sentences; a sentence is (comments, [row columns])."""
    doc, comments, rows = [], [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("#"):
                if line.startswith("# newdoc") and doc:
                    yield doc
                    doc = []
                comments.append(line)
            elif not line:
                if rows:
                    doc.append((comments, rows))
                comments, rows = [], []
            else:
                rows.append(line.split("\t"))
    if rows:
        doc.append((comments, rows))
    if doc:
        yield doc


def surface(rows):
    """(form, space_after) pairs of the surface tokens: multiword ranges stand for their words."""
    out, skip_to = [], 0
    for c in rows:
        if "." in c[0]:  # empty nodes
            continue
        if "-" in c[0]:
            skip_to = int(c[0].split("-")[1])
            out.append((c[1], "SpaceAfter=No" not in c[9]))
            continue
        if int(c[0]) <= skip_to:
            continue
        out.append((c[1], "SpaceAfter=No" not in c[9]))
    return out


def raw_text(doc):
    parts = []
    for _, rows in doc:
        for form, space in surface(rows):
            parts.append(form + (" " if space else ""))
        if parts and not parts[-1].endswith(" "):
            parts[-1] += " "
    return "".join(parts).rstrip()


def cell(v):
    return v if v else "_"


def write_sentence(out, tokens, base, comments=(), ranges=()):
    """tokens: spaCy tokens of one output sentence; base: index of its first token in the doc."""
    for c in comments:
        out.write(c + "\n")
    ids = {t.i: k + 1 for k, t in enumerate(tokens)}
    root = None
    for k, t in enumerate(tokens):
        for r in ranges:
            if r[0] == k + 1:
                out.write("\t".join(r[1]) + "\n")
        head = ids.get(t.head.i, 0) if t.head.i != t.i else 0
        deprel = "root" if head == 0 else t.dep_
        if head == 0:
            # The scorer needs one root per sentence. With gold sentences the parser may split one into several;
            # the later roots hang off the first one.
            if root is None:
                root = k + 1
            else:
                head, deprel = root, "dep"
        spaceafter = "_" if t.whitespace_ else "SpaceAfter=No"
        out.write("\t".join([str(k + 1), t.text, cell(t.lemma_), cell(t.pos_), cell(t.tag_),
                             cell(str(t.morph)), str(head), deprel, "_", spaceafter]) + "\n")
    out.write("\n")


def main():
    model, gold, mode, dst = sys.argv[1:5]
    nlp = spacy.load(model)
    n_sent = n_tok = 0
    with open(dst, "w", encoding="utf-8") as out:
        for gdoc in read_conllu(gold):
            if mode == "raw":
                doc = nlp(raw_text(gdoc))
                for s in doc.sents:
                    toks = [t for t in s if not t.is_space]
                    if toks:
                        write_sentence(out, toks, 0)
                        n_sent += 1
                        n_tok += len(toks)
            else:
                for comments, rows in gdoc:
                    words = [c for c in rows if "-" not in c[0] and "." not in c[0]]
                    spaces = [("SpaceAfter=No" not in c[9]) for c in words]
                    doc = nlp(Doc(nlp.vocab, words=[c[1] for c in words], spaces=spaces))
                    ranges = []
                    for c in rows:
                        if "-" in c[0]:
                            first = int(c[0].split("-")[0])
                            idx = [i for i, w in enumerate(words) if int(w[0]) >= first][0] + 1
                            ranges.append((idx, c[:9] + [c[9]]))
                    write_sentence(out, list(doc), 0, [x for x in comments if x.startswith("# sent_id")], ranges)
                    n_sent += 1
                    n_tok += len(doc)
    print(f"{dst}: {n_sent} sentences, {n_tok} tokens")


main()
