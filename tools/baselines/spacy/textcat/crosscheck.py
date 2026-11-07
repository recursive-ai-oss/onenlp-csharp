#!/usr/bin/env python3
"""Scores prediction files with spaCy's own Scorer, to cross-check OneNlp.Eval's `cats` and `spangroup` numbers.

usage:
  crosscheck.py single <gold.jsonl> <pred.jsonl>     single-label cats: micro-F (= accuracy) and macro-F
  crosscheck.py multi  <gold.jsonl> <pred.jsonl>     multi-label cats: micro-F, macro-F, macro ROC-AUC (threshold 0.5)
  crosscheck.py spans  <gold.jsonl> <pred.jsonl>     span group "sc": precision, recall, F (exact character spans over the gold tokens)

Files are the library's data format: gold {"text", "cats"} or {"text", "tokens", "spans": {"sc": [[start, end, label]]}}, predictions
{"cats": {label: score}} or {"spans": [[start, end, label, score]]}, one line per document in the same order. Run with the spaCy
oracle environment (spaCy 3.8.18); it calls only the released Scorer and Example API.
"""
import json
import sys

import spacy
from spacy.scorer import Scorer
from spacy.tokens import Doc, Span
from spacy.training import Example


def lines(path):
    with open(path, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def cats_examples(gold, pred, nlp):
    out = []
    for g, p in zip(gold, pred):
        ref = nlp.make_doc(g["text"])
        ref.cats = {k: float(v) for k, v in g["cats"].items()}
        hyp = nlp.make_doc(g["text"])
        hyp.cats = {k: float(p["cats"].get(k, 0.0)) for k in g["cats"]}
        out.append(Example(hyp, ref))
    return out


def doc_over_tokens(g, nlp):
    text, tokens = g["text"], g["tokens"]
    words = [text[s:e] for s, e in tokens]
    spaces = [i + 1 < len(tokens) and tokens[i + 1][0] > tokens[i][1] for i in range(len(tokens))]
    return Doc(nlp.vocab, words=words, spaces=spaces)


def token_span(doc, g, start, end, label):
    # Character offsets refer to the original text, which the rebuilt Doc does not reproduce when tokens are separated by more than
    # one space, so spans are placed by token index.
    first = next((i for i, (a, _) in enumerate(g["tokens"]) if a == start), None)
    last = next((i for i, (_, b) in enumerate(g["tokens"]) if b == end), None)
    return None if first is None or last is None or last < first else Span(doc, first, last + 1, label=label)


def span_examples(gold, pred, nlp):
    out = []
    for g, p in zip(gold, pred):
        ref, hyp = doc_over_tokens(g, nlp), doc_over_tokens(g, nlp)
        ref.spans["sc"] = [x for x in (token_span(ref, g, a, b, l) for a, b, l in g["spans"]["sc"]) if x is not None]
        hyp.spans["sc"] = [x for x in (token_span(hyp, g, a, b, l) for a, b, l, _ in p["spans"]) if x is not None]
        out.append(Example(hyp, ref))
    return out


def overlaps(spans):
    spans = sorted(spans)
    return any(spans[i][1] > spans[i + 1][0] for i in range(len(spans) - 1))


def main():
    mode, gold, pred = sys.argv[1], lines(sys.argv[2]), lines(sys.argv[3])
    if len(gold) != len(pred):
        sys.exit("length mismatch: %d vs %d" % (len(gold), len(pred)))
    nlp = spacy.blank("en")
    if mode in ("single", "multi"):
        labels = sorted({k for g in gold for k in g["cats"]})
        s = Scorer.score_cats(cats_examples(gold, pred, nlp), "cats", labels=labels, multi_label=(mode == "multi"))
        print(json.dumps({k: s[k] for k in ("cats_micro_p", "cats_micro_r", "cats_micro_f", "cats_macro_f", "cats_macro_auc") if k in s and s[k] is not None}))
    elif mode == "spans":
        # spaCy's scorer is not reliable for overlapping predicted spans (observed: a document whose prediction contains the gold span and
        # a second, overlapping span can score 0.0), so documents with overlapping predictions are left out here and the plain set
        # definition (exact label, start and end) is printed beside it for the same documents.
        keep = [i for i, p in enumerate(pred) if not overlaps([tuple(x[:2]) for x in p["spans"]])]
        gold, pred = [gold[i] for i in keep], [pred[i] for i in keep]
        s = Scorer.score_spans(span_examples(gold, pred, nlp), "sc", getter=lambda doc, key: doc.spans.get(key, []))
        tp = sum(len({tuple(x) for x in g["spans"]["sc"]} & {tuple(x[:3]) for x in p["spans"]}) for g, p in zip(gold, pred))
        n_pred, n_gold = sum(len(p["spans"]) for p in pred), sum(len(g["spans"]["sc"]) for g in gold)
        sp, sr = tp / n_pred, tp / n_gold
        print(json.dumps({"documents": len(keep), "spacy_scorer": {k: s[k] for k in ("sc_p", "sc_r", "sc_f")}, "set_definition": {"p": sp, "r": sr, "f": 2 * sp * sr / (sp + sr)}}))
    else:
        sys.exit("mode is single, multi or spans")


main()
