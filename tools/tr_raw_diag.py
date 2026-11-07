#!/usr/bin/env python3
"""Splits the raw-text parse loss of the Turkish pipeline by what went wrong in each gold sentence.

usage: tr_raw_diag.py gold.conllu gold-mode-pred.conllu raw-pred.conllu

Words are located by their character span in the concatenation of all forms without spaces, so the two files
can be compared although their tokens and sentences differ. For each gold sentence:
  exact    its first and last character are sentence edges in the raw run and the raw words are the gold words;
  words    the edges agree, some word differs;
  edges    an edge of the gold sentence is not a sentence edge in the raw run.
A gold word counts as correct when the raw run has a word with the same span, the head has the same span (or both
are roots) and the relation is equal. Reports, per class, the share of gold words, the labelled attachment of the
gold-token run, the labelled attachment of the raw run, and the share of gold words with an identical raw word.
"""
import sys


def read(path):
    """Sentences as lists of (start, end, head_span_or_None, deprel, form), spans counting non-space characters.
    A multiword token occupies the length of its surface form, whatever the words spell."""
    sents, cur, pos, mwt_end = [], [], 0, None
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line:
            if cur:
                sents.append(cur)
                cur = []
        elif not line.startswith("#"):
            f = line.split("\t")
            if "." in f[0]:
                continue
            if "-" in f[0]:
                mwt_end = (int(f[0].split("-")[1]), pos + len(f[1].replace(" ", "")))
                continue
            n = len(f[1].replace(" ", ""))
            cur.append([int(f[0]), pos, pos + n, int(f[6]), f[7], f[1]])
            pos += n
            if mwt_end and int(f[0]) == mwt_end[0]:
                pos = mwt_end[1]
                cur[-1][2] = min(cur[-1][2], pos)
                mwt_end = None
    if cur:
        sents.append(cur)
    out = []
    for s in sents:
        spans = {w[0]: (w[1], w[2]) for w in s}
        out.append([(w[1], w[2], spans.get(w[3]) if w[3] else None, w[4], w[5]) for w in s])
    return out


def flat(sents):
    d = {}
    for s in sents:
        for w in s:
            d[(w[0], w[1])] = w
    return d


gold, gmode, raw = read(sys.argv[1]), read(sys.argv[2]), read(sys.argv[3])
raw_words = flat(raw)
raw_edges = {s[0][0] for s in raw} | {s[-1][1] for s in raw}
gm_words = flat(gmode)
stats = {}
for g in gold:
    first, last = g[0][0], g[-1][1]
    edges_ok = first in raw_edges and last in raw_edges
    same = all((w[0], w[1]) in raw_words and raw_words[(w[0], w[1])][4] == w[4] for w in g)
    cls = "edges" if not edges_ok else ("exact" if same else "words")
    st = stats.setdefault(cls, [0, 0, 0, 0, 0])
    for w in g:
        st[0] += 1
        gm = gm_words.get((w[0], w[1]))
        st[1] += gm is not None and gm[2:4] == w[2:4]
        r = raw_words.get((w[0], w[1]))
        if r is not None:
            st[3] += 1
            st[2] += r[2:4] == w[2:4]
        st[4] += 0
total = sum(v[0] for v in stats.values())
for k in ("exact", "words", "edges"):
    if k in stats:
        n, a, b, c, _ = stats[k]
        print(f"{k:6} sentences' words {100*n/total:5.1f}%  gold-token LAS {100*a/n:5.2f}  raw LAS {100*b/n:5.2f}  gold words found in raw {100*c/n:5.2f}  raw LAS of found words {100*b/max(c,1):5.2f}")

if len(sys.argv) > 4:
    # Examples: the first N sentences of class "words" with every gold word marked by whether the raw run had it right.
    shown = 0
    for g in gold:
        if not (g[0][0] in raw_edges and g[-1][1] in raw_edges):
            continue
        if all((w[0], w[1]) in raw_words and raw_words[(w[0], w[1])][4] == w[4] for w in g):
            continue
        marks = []
        for w in g:
            r = raw_words.get((w[0], w[1]))
            marks.append(w[4] + ("" if r is None else ("+" if r[2:4] == w[2:4] else "~")) + ("?" if r is None else ""))
        raws = [r[4] for r in raw if r[0][0] >= g[0][0] and r[-1][1] <= g[-1][1] for r in [r]] if False else None
        rw = [x[4] for x in sorted((v for k, v in raw_words.items() if g[0][0] <= k[0] and k[1] <= g[-1][1]), key=lambda x: x[0])]
        print("GOLD", " ".join(marks))
        print("RAW ", " ".join(rw))
        shown += 1
        if shown >= int(sys.argv[4]):
            break
