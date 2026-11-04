#!/usr/bin/env python3
"""Writes systematically perturbed copies of a gold CoNLL-U file, for cross-checking a UD scorer.

Each output set applies a mix of seeded errors to the gold file: wrong labels (UPOS, XPOS, features, lemmas, relations), wrong heads,
split and merged words, multiword tokens added and removed, words of multiword tokens re-spelled, merged and split sentences, and
blanked columns. Character content is preserved (the evaluation requires the same text), and every output tree has one root and no
cycles. Standard library only.

usage: perturb_ud.py GOLD.conllu OUTDIR [SETS]    (writes OUTDIR/pred_00.conllu ... )
"""
import random
import sys

COLUMNS = ("form", "lemma", "upos", "xpos", "feats")


def read(path):
    sentences, words, mwts = [], [], []
    ids = {}
    pending = None
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    heads = []
    for line in lines + [""]:
        if line.startswith("#"):
            continue
        if not line.strip():
            if words:
                for w, h in zip(words, heads):
                    w["head"] = None if h == 0 else words[h - 1]
                sentences.append({"words": words, "mwts": [(words[a], words[b], form) for a, b, form in mwts]})
            words, mwts, heads = [], [], []
            continue
        c = line.split("\t")
        if "." in c[0]:
            continue
        if "-" in c[0]:
            a, b = c[0].split("-")
            mwts.append((int(a) - 1, int(b) - 1, c[1]))
            continue
        words.append({"form": c[1], "lemma": c[2], "upos": c[3], "xpos": c[4], "feats": c[5], "deprel": c[7]})
        heads.append(int(c[6]))
    return sentences


def write(sentences, path):
    out = []
    for n, s in enumerate(sentences):
        words = s["words"]
        index = {id(w): i + 1 for i, w in enumerate(words)}
        starts = {id(a): (a, b, form) for a, b, form in s["mwts"]}
        out.append("# sent_id = %d" % n)
        for w in words:
            if id(w) in starts:
                a, b, form = starts[id(w)]
                out.append("\t".join(["%d-%d" % (index[id(a)], index[id(b)]), form] + ["_"] * 8))
            head = 0 if w["head"] is None else index[id(w["head"])]
            out.append("\t".join([str(index[id(w)]), w["form"], w["lemma"], w["upos"], w["xpos"], w["feats"], str(head), w["deprel"], "_", "_"]))
        out.append("")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")


def mwt_of(s, w):
    words = s["words"]
    i = next(k for k, x in enumerate(words) if x is w)
    for m, (a, b, _) in enumerate(s["mwts"]):
        ia = next(k for k, x in enumerate(words) if x is a)
        ib = next(k for k, x in enumerate(words) if x is b)
        if ia <= i <= ib:
            return m
    return None


def has_cycle_from(w, limit):
    steps = 0
    while w is not None:
        steps += 1
        if steps > limit:
            return True
        w = w["head"]
    return False


def normalize(s):
    """One root, no cycles."""
    words = s["words"]
    if not words:
        return
    roots = [w for w in words if w["head"] is None]
    if not roots:
        roots = [words[0]]
        words[0]["head"] = None
    root = roots[0]
    for r in roots[1:]:
        r["head"] = root
        r["deprel"] = "dep"
    root["deprel"] = "root"
    for w in words:
        if has_cycle_from(w, len(words)):
            w["head"] = root
            if w is root:
                w["head"] = None
    for w in words:
        if w is not root and has_cycle_from(w, len(words)):
            w["head"] = root


def pool(sentences):
    p = {k: [] for k in ("lemma", "upos", "xpos", "feats", "deprel")}
    for s in sentences:
        for w in s["words"]:
            for k in p:
                p[k].append(w[k])
    return p


def perturb_labels(s, rng, rate, p):
    for w in s["words"]:
        for k in ("upos", "xpos", "feats", "lemma", "deprel"):
            if rng.random() < rate:
                w[k] = rng.choice(p[k]) if rng.random() < 0.9 else "_"


def perturb_heads(s, rng, rate):
    words = s["words"]
    for w in words:
        if w["head"] is not None and rng.random() < rate:
            old = w["head"]
            w["head"] = rng.choice(words)
            if w["head"] is w or has_cycle_from(w, len(words)):
                w["head"] = old


def split_words(s, rng, rate):
    words, mwts = s["words"], s["mwts"]
    k = 0
    while k < len(words):
        w = words[k]
        if len(w["form"]) >= 2 and rng.random() < rate:
            cut = rng.randint(1, len(w["form"]) - 1)
            w1, w2 = dict(w), dict(w)
            w1["form"], w2["form"] = w["form"][:cut], w["form"][cut:]
            w1["lemma"], w2["lemma"] = w1["form"].lower(), w2["form"].lower()
            w2["head"] = w1
            w2["deprel"] = rng.choice(["dep", "fixed", "flat", "punct", "case"])
            w2["upos"] = rng.choice(["X", "PART", "PUNCT", w["upos"]])
            for x in words:
                if x["head"] is w:
                    x["head"] = rng.choice([w1, w2])
            words[k:k + 1] = [w1, w2]
            s["mwts"] = [(w1 if a is w else a, w2 if b is w else b, f) for a, b, f in mwts]
            mwts = s["mwts"]
            k += 2
        else:
            k += 1


def merge_words(s, rng, rate):
    words = s["words"]
    k = 0
    while k + 1 < len(words):
        a, b = words[k], words[k + 1]
        ma, mb = mwt_of(s, a), mwt_of(s, b)
        allowed = (ma is None and mb is None) or (ma is not None and ma == mb)
        if allowed and rng.random() < rate:
            m = dict(a)
            m["form"] = a["form"] + b["form"]
            if a["head"] is None or b["head"] is None:
                m["head"] = None
            elif a["head"] is b:
                m["head"] = b["head"]
            else:
                m["head"] = a["head"]
            if m["head"] is a or m["head"] is b:
                m["head"] = None
            for x in words:
                if x["head"] is a or x["head"] is b:
                    x["head"] = m
            words[k:k + 2] = [m]
            new = []
            for (fa, fb, f) in s["mwts"]:
                fa = m if fa is a or fa is b else fa
                fb = m if fb is a or fb is b else fb
                if fa is not fb:
                    new.append((fa, fb, f))
                elif fa is m:
                    m["form"] = f  # the merged word now stands for the whole multiword token, so it keeps that surface form
            s["mwts"] = new
            normalize(s)
        else:
            k += 1


def change_mwts(s, rng, rate, add_rate):
    words = s["words"]
    keep = []
    for a, b, form in s["mwts"]:
        ia = next(k for k, x in enumerate(words) if x is a)
        ib = next(k for k, x in enumerate(words) if x is b)
        joined = "".join(w["form"] for w in words[ia:ib + 1]).replace(" ", "")
        if rng.random() < rate and joined == form.replace(" ", ""):
            continue  # drop the multiword token; the words become plain tokens
        keep.append((a, b, form))
    s["mwts"] = keep
    k = 0
    while k + 1 < len(words):
        if rng.random() < add_rate and all(mwt_of(s, words[k + j]) is None for j in range(2)):
            n = rng.choice([2, 2, 3])
            if k + n <= len(words) and all(mwt_of(s, words[k + j]) is None for j in range(n)):
                form = "".join(w["form"] for w in words[k:k + n])
                s["mwts"].append((words[k], words[k + n - 1], form))
                k += n
                continue
        k += 1
    s["mwts"].sort(key=lambda m: next(i for i, x in enumerate(words) if x is m[0]))


def respell_mwt_words(s, rng, rate):
    words = s["words"]
    for a, b, _ in s["mwts"]:
        ia = next(k for k, x in enumerate(words) if x is a)
        ib = next(k for k, x in enumerate(words) if x is b)
        for w in words[ia:ib + 1]:
            if rng.random() < rate:
                w["form"] = rng.choice([w["form"].upper(), w["form"].capitalize(), w["form"] + "x", w["form"][:-1] or "z"])


def blank(s, columns):
    for w in s["words"]:
        for c in columns:
            w[c] = "_"


def merge_sentences(sentences, rng, rate):
    out = []
    for s in sentences:
        if out and rng.random() < rate:
            prev = out[-1]
            prev["words"] = prev["words"] + s["words"]
            prev["mwts"] = prev["mwts"] + s["mwts"]
            normalize(prev)
        else:
            out.append(s)
    return out


def split_sentences(sentences, rng, rate):
    out = []
    for s in sentences:
        words = s["words"]
        if len(words) >= 3 and rng.random() < rate:
            cut = rng.randint(1, len(words) - 1)
            if mwt_of(s, words[cut - 1]) is not None and mwt_of(s, words[cut - 1]) == mwt_of(s, words[cut]):
                out.append(s)
                continue
            left, right = words[:cut], words[cut:]
            lset = {id(w) for w in left}
            for w in left:
                if w["head"] is not None and id(w["head"]) not in lset:
                    w["head"] = None
            for w in right:
                if w["head"] is not None and id(w["head"]) in lset:
                    w["head"] = None
            lm = [m for m in s["mwts"] if id(m[0]) in lset]
            rm = [m for m in s["mwts"] if id(m[0]) not in lset]
            a, b = {"words": left, "mwts": lm}, {"words": right, "mwts": rm}
            normalize(a)
            normalize(b)
            out += [a, b]
        else:
            out.append(s)
    return out


# (labels, heads, split, merge, mwt drop, mwt add, respell, merge sentences, split sentences, blank columns)
PROFILES = [
    dict(labels=0.05),
    dict(labels=0.3),
    dict(heads=0.15),
    dict(labels=0.1, heads=0.1),
    dict(split=0.05),
    dict(merge=0.05),
    dict(split=0.03, merge=0.03, labels=0.1, heads=0.1),
    dict(mwt_drop=0.5, mwt_add=0.05),
    dict(mwt_drop=0.3, mwt_add=0.1, respell=0.5, labels=0.1, heads=0.1),
    dict(merge_sent=0.1),
    dict(split_sent=0.1),
    dict(labels=0.1, heads=0.1, split=0.03, merge=0.03, mwt_drop=0.3, mwt_add=0.05, respell=0.3, merge_sent=0.05, split_sent=0.05),
    dict(blank=["lemma", "feats"]),
    dict(blank=["xpos", "upos"], heads=0.05),
    dict(),
]


def make(gold, seed, profile):
    rng = random.Random(seed)
    p = pool(gold)
    sentences = [{"words": list(s["words"]), "mwts": list(s["mwts"])} for s in gold]
    # copy words so edits do not touch the gold structures
    clones = []
    for s in sentences:
        mapping = {id(w): dict(w) for w in s["words"]}
        for old in s["words"]:
            new = mapping[id(old)]
            new["head"] = None if old["head"] is None else mapping[id(old["head"])]
        clones.append({"words": [mapping[id(w)] for w in s["words"]], "mwts": [(mapping[id(a)], mapping[id(b)], f) for a, b, f in s["mwts"]]})
    sentences = clones
    for s in sentences:
        if profile.get("labels"):
            perturb_labels(s, rng, profile["labels"], p)
        if profile.get("heads"):
            perturb_heads(s, rng, profile["heads"])
        if profile.get("respell"):
            respell_mwt_words(s, rng, profile["respell"])
        if profile.get("mwt_drop") or profile.get("mwt_add"):
            change_mwts(s, rng, profile.get("mwt_drop", 0), profile.get("mwt_add", 0))
        if profile.get("split"):
            split_words(s, rng, profile["split"])
            normalize(s)
        if profile.get("merge"):
            merge_words(s, rng, profile["merge"])
        if profile.get("blank"):
            blank(s, profile["blank"])
    if profile.get("merge_sent"):
        sentences = merge_sentences(sentences, rng, profile["merge_sent"])
    if profile.get("split_sent"):
        sentences = split_sentences(sentences, rng, profile["split_sent"])
    return sentences


def main():
    gold_path, outdir = sys.argv[1], sys.argv[2]
    count = int(sys.argv[3]) if len(sys.argv) > 3 else 24
    import os
    os.makedirs(outdir, exist_ok=True)
    gold = read(gold_path)
    for k in range(count):
        profile = PROFILES[k % len(PROFILES)]
        write(make(gold, 1000 + k, profile), os.path.join(outdir, "pred_%02d.conllu" % k))
    print("wrote %d sets to %s" % (count, outdir))


if __name__ == "__main__":
    main()
