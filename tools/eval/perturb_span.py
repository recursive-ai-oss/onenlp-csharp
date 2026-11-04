#!/usr/bin/env python3
"""Writes synthetic IOB2 gold and predicted tag files for cross-checking a span scorer.

Gold sentences are random, valid IOB2 sequences over a few labels; predictions are gold with seeded errors: dropped, extended,
shortened and shifted spans, swapped labels, and (in the later sets) malformed sequences with stray I- tags or type changes inside a
span. Later sets also leave the gold itself malformed in places. Files hold "token TAG" lines with blank lines between sentences.
Standard library only.

usage: perturb_span.py OUTDIR [SETS]    (writes OUTDIR/gold_NN.iob2 and OUTDIR/pred_NN.iob2)
"""
import os
import random
import sys

LABELS = ["PER", "ORG", "LOC", "MISC"]


def gold_sentence(rng):
    n = rng.randint(1, 25)
    tags, i = [], 0
    while i < n:
        if rng.random() < 0.3:
            length = min(rng.choice([1, 1, 2, 3, 4]), n - i)
            label = rng.choice(LABELS)
            tags += ["B-" + label] + ["I-" + label] * (length - 1)
            i += length
        else:
            tags.append("O")
            i += 1
    return tags


def perturb(tags, rng, rate, malformed):
    out = list(tags)
    for i in range(len(out)):
        if rng.random() >= rate:
            continue
        move = rng.randint(0, 5)
        if move == 0:
            out[i] = "O"
        elif move == 1:
            out[i] = "B-" + rng.choice(LABELS)
        elif move == 2 and out[i] != "O":
            out[i] = out[i][:2] + rng.choice(LABELS)
        elif move == 3:
            out[i] = "I-" + rng.choice(LABELS) if malformed else "O"
        elif move == 4 and i + 1 < len(out) and out[i].startswith("B-") and out[i + 1] == "O":
            out[i + 1] = "I-" + out[i][2:]
        elif move == 5 and i > 0 and out[i].startswith("I-") and malformed:
            out[i] = "I-" + rng.choice(LABELS)
    return out


def write(path, sentences):
    with open(path, "w", encoding="utf-8") as f:
        for tags in sentences:
            for k, t in enumerate(tags):
                f.write("w%d %s\n" % (k, t))
            f.write("\n")


def main():
    outdir = sys.argv[1]
    count = int(sys.argv[2]) if len(sys.argv) > 2 else 24
    os.makedirs(outdir, exist_ok=True)
    for k in range(count):
        rng = random.Random(5000 + k)
        sentences = [gold_sentence(rng) for _ in range(300)]
        malformed = k >= 12
        rate = [0.02, 0.05, 0.1, 0.2, 0.4][k % 5]
        pred = [perturb(s, rng, rate, malformed) for s in sentences]
        gold = sentences
        if k >= 18:  # the gold itself is malformed in places
            gold = [perturb(s, rng, 0.03, True) for s in sentences]
        write(os.path.join(outdir, "gold_%02d.iob2" % k), gold)
        write(os.path.join(outdir, "pred_%02d.iob2" % k), pred)
    print("wrote %d sets to %s" % (count, outdir))


if __name__ == "__main__":
    main()
