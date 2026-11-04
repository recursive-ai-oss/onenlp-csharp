"""Rebuilds raw text from a CoNLL-U file: the '# text =' lines joined by one space.

Sentences are joined by a space, never a newline, so the raw text carries no sentence-boundary hint beyond the text itself.
usage: ud_raw.py <in.conllu> <out.txt>
"""
import sys

sentences = []
with open(sys.argv[1], encoding="utf-8") as f:
    for line in f:
        if line.startswith("# text = "):
            sentences.append(line[len("# text = "):].rstrip("\n"))
with open(sys.argv[2], "w", encoding="utf-8", newline="") as out:
    out.write(" ".join(sentences))
print(f"{len(sentences)} sentences, {sum(len(s) for s in sentences)} characters")
