#!/usr/bin/env python3
"""spaCy black-box baseline for text classification and span categorization, on the library's JSON Lines data.

usage:
  textcat_data.py prepare <textcat|textcat_multilabel|spancat> <in.jsonl> <out.spacy>
  textcat_data.py predict <textcat|textcat_multilabel|spancat> <model-dir> <in.jsonl> <out.jsonl>

prepare   writes a DocBin: text categories as doc.cats, or the gold spans as the span group "sc" over the gold tokens of the file.
predict   writes one line per input line: {"cats": {label: score}} (the model tokenizes the text itself, with its default tokenizer),
          or {"spans": [[start, end, label, score], ...]} with character offsets (the gold tokens are given to the model).
Only spaCy's documented public API is used.
"""
import json
import sys

import spacy
from spacy.tokens import Doc, DocBin, Span, SpanGroup

KEY = "sc"


def lines(path):
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.strip():
                yield json.loads(line)


def gold_doc(nlp, row):
    """A Doc over the file's gold tokens: a space after a token when the next token starts later."""
    text, toks = row["text"], row["tokens"]
    words = [text[s:e] for s, e in toks]
    spaces = [i + 1 < len(toks) and toks[i + 1][0] > toks[i][1] for i in range(len(toks))]
    return Doc(nlp.vocab, words=words, spaces=spaces)


def token_span(doc, toks, s, e, label):
    starts = {t[0]: i for i, t in enumerate(toks)}
    ends = {t[1]: i for i, t in enumerate(toks)}
    if s not in starts or e not in ends:
        return None
    return Span(doc, starts[s], ends[e] + 1, label=label)


def prepare(mode, src, dst):
    nlp = spacy.blank("en")
    db = DocBin(store_user_data=False)
    for row in lines(src):
        if mode == "spancat":
            doc = gold_doc(nlp, row)
            spans = [token_span(doc, row["tokens"], s, e, l) for s, e, l in row["spans"][KEY]]
            doc.spans[KEY] = SpanGroup(doc, name=KEY, spans=[s for s in spans if s is not None])
        else:
            doc = nlp.make_doc(row["text"])
            doc.cats = {k: float(v) for k, v in row["cats"].items()}
        db.add(doc)
    db.to_disk(dst)
    print(f"{dst}: {len(db)} documents")


def predict(mode, model, src, dst):
    nlp = spacy.load(model)
    n = 0
    with open(dst, "w", encoding="utf-8", newline="\n") as out:
        for row in lines(src):
            if mode == "spancat":
                doc = nlp(gold_doc(nlp, row))
                toks = row["tokens"]
                group = doc.spans.get(KEY)
                scores = group.attrs.get("scores") if group is not None else None
                spans = []
                for i, s in enumerate(group or []):
                    spans.append([toks[s.start][0], toks[s.end - 1][1], s.label_, float(scores[i]) if scores is not None else 1.0])
                out.write(json.dumps({"spans": spans}, ensure_ascii=False) + "\n")
            else:
                doc = nlp(row["text"])
                out.write(json.dumps({"cats": {k: float(v) for k, v in doc.cats.items()}}, ensure_ascii=False) + "\n")
            n += 1
    print(f"{dst}: {n} documents")


if __name__ == "__main__":
    cmd, mode = sys.argv[1:3]
    if cmd == "prepare":
        prepare(mode, sys.argv[3], sys.argv[4])
    else:
        predict(mode, *sys.argv[3:6])
