#!/usr/bin/env python3
"""Entity F1 on character spans, for systems that bring their own tokenization.

usage: score_spans.py <gold.iob2> <catalyst-ner.tsv>

Gold spans come from the BIO tags of the UNER file, placed on the sentence text by walking the
words through the '# text =' line. Predicted spans come from the rows written by `CatalystOracle ner`
(sentence, begin, end, type, tag); consecutive Begin/Inside/End rows of one type become one span.
Catalyst's Person/Organization/Location map to PER/ORG/LOC; its other types (EmailOrURL) are
dropped, since UNER has no such label. A predicted span counts only if label, begin and end all match.
Standard library only.
"""
import sys
from collections import Counter, defaultdict

MAP = {"Person": "PER", "Organization": "ORG", "Location": "LOC"}


def gold_spans(path):
    out, sent = [], -1
    text, rows = None, []

    def flush():
        spans, pos, cur = set(), 0, None
        for w, tag in rows:
            at = text.find(w, pos)
            if at < 0:
                raise SystemExit(f"cannot place {w!r} in {text!r}")
            b, e = at, at + len(w)
            pos = e
            if tag.startswith("B-") or (tag.startswith("I-") and (cur is None or cur[2] != tag[2:])):
                if cur:
                    spans.add((cur[0], cur[1], cur[2]))
                cur = [b, e, tag[2:]]
            elif tag.startswith("I-"):
                cur[1] = e
            else:
                if cur:
                    spans.add((cur[0], cur[1], cur[2]))
                cur = None
        if cur:
            spans.add((cur[0], cur[1], cur[2]))
        out.append(spans)

    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("# text = "):
                text = line[len("# text = "):]
            elif line.startswith("#"):
                continue
            elif not line:
                if rows:
                    flush()
                text, rows = None, []
            else:
                c = line.split("\t")
                rows.append((c[1], c[2]))
    if rows:
        flush()
    return out


def pred_spans(path, n):
    out = [set() for _ in range(n)]
    cur = {}  # sentence -> [begin, end, label] being built
    for line in open(path, encoding="utf-8"):
        s, b, e, typ, tag = line.rstrip("\n").split("\t")
        s, b, e = int(s), int(b), int(e)
        label = MAP.get(typ)
        if label is None:
            continue
        if tag == "Single":
            out[s].add((b, e, label))
        elif tag == "Begin":
            cur[s] = [b, e, label]
        elif tag == "Inside" and s in cur and cur[s][2] == label:
            cur[s][1] = e
        elif tag == "End" and s in cur and cur[s][2] == label:
            cur[s][1] = e
            out[s].add(tuple(cur.pop(s)))
    return out


gold = gold_spans(sys.argv[1])
pred = pred_spans(sys.argv[2], len(gold))
tp, fp, fn = Counter(), Counter(), Counter()
for g, p in zip(gold, pred):
    for x in p & g:
        tp[x[2]] += 1
    for x in p - g:
        fp[x[2]] += 1
    for x in g - p:
        fn[x[2]] += 1


def prf(t, f_p, f_n):
    p = t / (t + f_p) if t + f_p else 0.0
    r = t / (t + f_n) if t + f_n else 0.0
    return p, r, (2 * p * r / (p + r) if p + r else 0.0)


for lab in sorted(set(tp) | set(fp) | set(fn)):
    p, r, f = prf(tp[lab], fp[lab], fn[lab])
    print(f"{lab}: P {p:.4f} R {r:.4f} F1 {f:.4f}  (tp {tp[lab]}, fp {fp[lab]}, fn {fn[lab]})")
p, r, f = prf(sum(tp.values()), sum(fp.values()), sum(fn.values()))
print(f"micro: P {p:.4f} R {r:.4f} F1 {f:.4f}  (gold spans {sum(len(g) for g in gold)})")
