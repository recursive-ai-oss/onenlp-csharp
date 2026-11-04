"""Black-box oracle: spaCy's rule-based sentencizer on spacy.blank("en"), written as CoNLL-U (ID and FORM only).

usage: spacy_segment.py <raw.txt> <out.conllu> [mwt]     (run with the oracle's own Python)

With "mwt", the pieces of a split contraction (do + n't) are also written as one multi-word token line, by the same rule as
the OneNlp.Eval tool, so both systems are scored alike.
"""
import sys

import spacy

CLITICS = {"n't", "n\u2019t", "'s", "\u2019s", "'re", "\u2019re", "'ve", "\u2019ve", "'ll", "\u2019ll", "'d", "\u2019d", "'m", "\u2019m"}
FUSED = {"cannot", "gonna", "gotta", "wanna", "gimme", "lemme"}

mwt = len(sys.argv) > 3 and sys.argv[3] == "mwt"
nlp = spacy.blank("en")
nlp.add_pipe("sentencizer")
text = open(sys.argv[1], encoding="utf-8").read()
nlp.max_length = max(nlp.max_length, len(text) + 1)
doc = nlp(text)
out = []
for sent in doc.sents:
    # spaCy keeps runs of extra white space as tokens; the scorer ignores white space, and so does our tokenizer, so they are dropped.
    toks = [t for t in sent if not t.is_space]
    if not toks:
        continue
    i = 0
    while i < len(toks):
        end = i
        if mwt and i + 1 < len(toks) and toks[i].idx + len(toks[i].text) == toks[i + 1].idx:
            second = toks[i + 1].text.lower()
            whole = (toks[i].text + toks[i + 1].text).lower()
            if second in CLITICS or whole in FUSED:
                end = i + 1
        if end > i:
            form = text[toks[i].idx : toks[end].idx + len(toks[end].text)]
            out.append(f"{i + 1}-{end + 1}\t{form}\t_\t_\t_\t_\t_\t_\t_\t_")
        for j in range(i, end + 1):
            head = "0\troot" if j == 0 else "1\tdep"
            out.append(f"{j + 1}\t{toks[j].text}\t_\t_\t_\t_\t{head}\t_\t_")
        i = end + 1
    out.append("")
with open(sys.argv[2], "w", encoding="utf-8", newline="") as f:
    f.write("\n".join(out) + "\n")
print("spaCy", spacy.__version__)
