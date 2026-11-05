#!/usr/bin/env python3
"""Observes, black-box, which leading/trailing characters sentence-transformers' tokenize() drops before tokenizing, by comparing
its token ids for 'a'+c, c+'a' with the raw tokenizer.json encoding of the same strings, for every code point c.
Prints the set as ranges, and whether it equals Python's str.isspace set.

    observe_strip.py MODEL_DIR REPO REVISION
"""
import sys, json
import sentence_transformers
from tokenizers import Tokenizer

d, repo, rev = sys.argv[1:4]
st = sentence_transformers.SentenceTransformer(repo, revision=rev, device="cpu")
raw = Tokenizer.from_file(d + "/tokenizer.json")
raw.no_truncation(); raw.no_padding()

def st_ids(texts):
    out = []
    for i in range(0, len(texts), 256):
        enc = st.tokenize(texts[i:i + 256])
        for row, m in zip(enc["input_ids"].tolist(), enc["attention_mask"].tolist()):
            out.append([x for x, k in zip(row, m) if k])
    return out

stripped = {"trailing": [], "leading": []}
cps = [c for c in range(0x110000) if not 0xD800 <= c <= 0xDFFF]
for side in ("trailing", "leading"):
    texts = ["a" + chr(c) if side == "trailing" else chr(c) + "a" for c in cps]
    ours = st_ids(texts)
    for c, t, ids in zip(cps, texts, ours):
        if raw.encode(t).ids != ids:
            stripped[side].append(c)

def ranges(cs):
    out = []; s = p = None
    for c in cs:
        if s is None: s = p = c
        elif c == p + 1: p = c
        else: out.append((s, p)); s = p = c
    if s is not None: out.append((s, p))
    return [f"U+{a:04X}" if a == b else f"U+{a:04X}-U+{b:04X}" for a, b in out]

iss = [c for c in cps if chr(c).isspace()]
for side in stripped:
    print(side, ranges(stripped[side]))
    print(side, "equals str.isspace set:", stripped[side] == iss, "| only in observed:", ranges([c for c in stripped[side] if c not in set(iss)]),
          "| only in isspace:", ranges([c for c in iss if c not in set(stripped[side])]))
