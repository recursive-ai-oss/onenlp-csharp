#!/usr/bin/env python3
"""Counts of the token shapes that decide Dutch tokenization rules, from a CoNLL-U file (train or dev only).

    token_stats.py nl_alpino-ud-train.conllu [nl_alpino-ud-dev.conllu ...]

Prints: multiword tokens, tokens with an apostrophe, a full stop inside or at the end, a hyphen, digits with separators,
and what follows a final-full-stop token inside a sentence (abbreviation evidence).
"""
import collections
import sys


def sentences(path):
    sent = []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line:
            if sent:
                yield sent
            sent = []
        elif not line.startswith("#"):
            sent.append(line.split("\t"))
    if sent:
        yield sent


mwt = collections.Counter()
apos = collections.Counter()
dots = collections.Counter()
dotsmid = collections.Counter()
hyph = collections.Counter()
hyph_sp = collections.Counter()
nums = collections.Counter()
final_dot_tok = collections.Counter()
ex_apos = collections.defaultdict(list)
for path in sys.argv[1:]:
    for s in sentences(path):
        for i, c in enumerate(s):
            if "-" in c[0] and c[0].replace("-", "").isdigit():
                mwt[(c[1], tuple(w[1] for w in s if w[0].isdigit() and int(c[0].split("-")[0]) <= int(w[0]) <= int(c[0].split("-")[1])))] += 1
                continue
            if "." in c[0]:
                continue
            f = c[1]
            if "'" in f or "’" in f:
                apos[f] += 1
            if "." in f and len(f) > 1:
                (dots if f.endswith(".") else dotsmid)[f] += 1
            if "-" in f and len(f) > 1:
                hyph[f] += 1
            if f in ("-", "–"):
                hyph_sp[f] += 1
            if any(ch.isdigit() for ch in f) and any(ch in f for ch in ".,:/"):
                nums[f] += 1
        # last token of the sentence vs inner abbreviation dots
for name, cnt, n in (("MWT", mwt, 40), ("APOSTROPHE", apos, 120), ("ENDS-WITH-DOT", dots, 80), ("DOT-INSIDE", dotsmid, 60), ("HYPHEN", hyph, 40), ("NUMBERS", nums, 40)):
    print(f"== {name}: {len(cnt)} types, {sum(cnt.values())} tokens")
    for k, v in cnt.most_common(n):
        print(f"  {v:6d}  {k}")

# ---- second pass: abbreviations (word with a final full stop kept whole) against the word split from a final full stop ----
inner = collections.Counter()
final_kept = collections.Counter()
final_split = collections.Counter()
punct = collections.Counter()
after = collections.defaultdict(collections.Counter)
for path in sys.argv[1:]:
    for s in sentences(path):
        toks = [c for c in s if c[0].isdigit()]
        for i, c in enumerate(toks):
            f = c[1]
            if f.endswith(".") and len(f) > 1 and not set(f) <= {"."}:
                if i == len(toks) - 1:
                    final_kept[f] += 1
                else:
                    inner[f] += 1
                    after[f][toks[i + 1][1][:1].isupper()] += 1
            if i == len(toks) - 1 and f == "." and i > 0 and toks[i - 1][1].isalpha():
                final_split[toks[i - 1][1]] += 1
            if f and not any(ch.isalnum() for ch in f):
                punct[f] += 1
print("== PUNCT-ONLY tokens")
print("  ", punct.most_common(60))
print("== ABBREVIATIONS inner (token: count, next capitalised/not)")
print("  ", [(k, v, dict(after[k])) for k, v in inner.most_common(150)])
print("== kept with dot at sentence end:", final_kept.most_common(30))
print("== bare word + final '.' (top)", final_split.most_common(25))
