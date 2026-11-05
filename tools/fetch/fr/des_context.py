"""Words after which French "des" is the plural article (one word) more often than the contraction "de les" (a multiword token).

Reads the training split of UD French-GSD only and prints the list as C# string literals for SegmentationData.French.
A word counts when it precedes "des" at least twice and at least 1.5 times as often with the article as with the contraction.
usage: des_context.py fr_gsd-ud-train.conllu [fr_gsd-ud-dev.conllu]   (the dev file, when given, is only scored)
"""
import collections
import sys


def instances(path):
    out = []
    sentence = []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if line and line[0] != "#":
            sentence.append(line.split("\t"))
            continue
        if line:
            continue
        tokens, i = [], 0
        while i < len(sentence):
            row = sentence[i]
            if "." in row[0]:
                i += 1
            elif "-" in row[0]:
                a, b = map(int, row[0].split("-"))
                tokens.append((row[1], True))
                i += 1 + b - a + 1
            else:
                tokens.append((row[1], False))
                i += 1
        for j, (form, merged) in enumerate(tokens):
            if form.lower() == "des":
                out.append((tokens[j - 1][0].lower() if j else None, merged))
        sentence = []
    return out


def has_letter(word):
    return any(c.isalnum() for c in word)


train = instances(sys.argv[1])
counts = collections.defaultdict(lambda: [0, 0])  # [article, contraction]
for prev, merged in train:
    counts[prev][int(merged)] += 1
article_after = sorted(p for p, (a, m) in counts.items() if p and has_letter(p) and a >= 2 and a >= 1.5 * m)
if len(sys.argv) > 2:
    dev = instances(sys.argv[2])
    chosen = set(article_after)
    correct = sum((p is not None and (not has_letter(p) and p == "%" or has_letter(p) and p not in chosen)) == m for p, m in dev)
    print(f"// dev accuracy of the rule: {correct / len(dev):.4f} on {len(dev)} instances", file=sys.stderr)
print(", ".join('"' + w.replace("\\", "\\\\").replace('"', '\\"') + '"' for w in article_after))
