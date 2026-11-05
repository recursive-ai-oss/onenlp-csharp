#!/usr/bin/env python3
"""Builds the Turkish multiword-token suffix table from a UD training file and measures it on a development file.

usage: mwt_table.py <train.conllu> <dev.conllu> [out.tsv|-] [min-count=3] [purity=0.9]  (mwt_sweep.py chooses the two thresholds on the development file)

Turkish writes a copula or a derivational suffix to the word it follows (vardır = var + dır, arasındaki = arasında + ki, geldiydi =
geldi + ydi), and UD Turkish-BOUN splits those into syntactic words, so the split cannot come from a word list: it is productive. The table
maps the last k letters of a token (k from 2 to 9, lower-cased with Turkish case rules) to the length of the final piece the treebank cuts
off (0 = the token is not split). It is read longest-key-first. A key is kept only when its answer differs from its shorter key's answer.
Only two-word multiword tokens whose words spell the surface form are counted; the rest of the multiword tokens are listed.
"""
import collections
import sys


def lower(s):
    return s.replace("İ", "i").replace("I", "ı").lower()


def tokens(path):
    """Yields (surface, pieces) per token of the file: pieces is the list of word forms of a multiword token, else None."""
    with open(path, encoding="utf8") as f:
        sent = []
        for line in list(f) + [""]:
            line = line.rstrip("\n")
            if line.startswith("#"):
                continue
            if not line:
                i = 0
                while i < len(sent):
                    c = sent[i]
                    if "-" in c[0]:
                        a, b = map(int, c[0].split("-"))
                        yield c[1], [w[1] for w in sent[i + 1 : i + 1 + (b - a + 1)]]
                        i += 1 + (b - a + 1)
                    else:
                        yield c[1], None
                        i += 1
                sent = []
                continue
            c = line.split("\t")
            if "." in c[0]:
                continue
            sent.append(c)


MAXK = int(__import__("os").environ.get("MAXK", "9"))
MINK = 2


def outcome(surface, pieces):
    """Length of the final piece, 0 for a whole token, None for a multiword token the table cannot express."""
    if pieces is None:
        return 0
    if len(pieces) != 2 or lower("".join(pieces)) != lower(surface):
        return None
    return len(pieces[1])


def keyed(surface):
    s = lower(surface)
    if not s.isalpha():
        return None
    return s


def build(train, min_count, purity):
    counts = collections.defaultdict(collections.Counter)
    skipped = collections.Counter()
    for surface, pieces in tokens(train):
        s = keyed(surface)
        if s is None:
            continue
        o = outcome(surface, pieces)
        if o is None:
            skipped[len(pieces)] += 1
            continue
        for k in range(MINK, min(MAXK, len(s)) + 1):
            counts[s[-k:]][o] += 1
    table = {}
    for key in sorted(counts, key=lambda k: (len(k), k)):
        c = counts[key]
        n = sum(c.values())
        if n < min_count:
            continue
        best, bn = c.most_common(1)[0]
        if bn / n < purity:
            continue
        if best > len(key):
            continue
        parent = decide(table, key[1:] if len(key) > MINK else "", exact=False)
        if best != parent:
            table[key] = best
    return table, skipped


def decide(table, s, exact=True):
    for k in range(min(MAXK, len(s)), MINK - 1, -1):
        v = table.get(s[-k:])
        if v is not None:
            return v
    return 0


def main():
    train, dev = sys.argv[1], sys.argv[2]
    out = sys.argv[3] if len(sys.argv) > 3 and sys.argv[3] != "-" else None
    min_count = int(sys.argv[4]) if len(sys.argv) > 4 else 3
    purity = float(sys.argv[5]) if len(sys.argv) > 5 else 0.9
    table, skipped = build(train, min_count, purity)
    print(f"entries {len(table)}, multiword tokens not expressible (by number of words): {dict(skipped)}")
    tp = fp = fn = wrong = 0
    for surface, pieces in tokens(dev):
        s = keyed(surface)
        o = outcome(surface, pieces)
        if o is None:
            fn += 1
            continue
        p = decide(table, s) if s else 0
        if o > 0 and p == o:
            tp += 1
        elif o > 0 and p > 0:
            wrong += 1
        elif o > 0:
            fn += 1
        elif p > 0:
            fp += 1
    print(f"dev: correct splits {tp}, wrong cut {wrong}, missed {fn}, false splits {fp}; precision {tp/max(1,tp+wrong+fp):.3f} recall {tp/max(1,tp+wrong+fn):.3f}")
    if out:
        with open(out, "w", encoding="utf8", newline="\n") as f:
            for key in sorted(table, key=lambda k: (len(k), k)):
                f.write(f"{key}\t{table[key]}\n")
        print("wrote", out)


main()
