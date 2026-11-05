"""Records what spaCy's doc.noun_chunks returns on trees, as a black box, for the noun-chunk differential test.

usage: make_cases.py OUT.json [--ud DEV.conllu] [--random N] [--seed S]

Trees are built with the public Doc(vocab, words, pos, heads, deps) constructor, so no tagger or parser runs. Two kinds:
  random   small random forests (crossing arcs included) with relation names from the scheme spaCy's English rule knows;
  ud       the structure of a UD treebank's sentences, relations mapped to that scheme (nsubj:pass -> nsubjpass, obj -> dobj,
           iobj -> dative, obl/nmod -> pobj, xcomp -> oprd, root -> ROOT; others unchanged). Words are placeholders: only the
           tree and the parts of speech are kept.
Only the documented API is called. The output records the spaCy version.
"""
import json
import random
import sys

import spacy
from spacy.tokens import Doc

HEAD_RELATIONS = ["ROOT", "nsubj", "nsubjpass", "dobj", "dative", "pobj", "appos", "attr", "pcomp", "oprd"]
OTHER = ["det", "amod", "compound", "prep", "punct", "poss", "advmod", "relcl", "acl", "cc", "aux", "nummod", "dep"]
POS = ["NOUN", "PROPN", "PRON", "VERB", "ADJ", "DET", "ADP", "NUM", "AUX"]


def random_tree(rng):
    n = rng.randint(1, 10)
    order = list(range(n))
    rng.shuffle(order)
    heads = [0] * n
    for k, i in enumerate(order):
        heads[i] = i if k == 0 or rng.random() < 0.1 else order[rng.randrange(k)]
    deps = [rng.choice(HEAD_RELATIONS + ["conj"] * 3 + OTHER * 2) for _ in range(n)]
    return [rng.choice(POS) for _ in range(n)], heads, deps


def map_relation(rel):
    base, _, sub = rel.partition(":")
    if base == "root":
        return "ROOT"
    if base == "nsubj":
        return "nsubjpass" if sub == "pass" else "nsubj"
    if base == "obj":
        return "dobj"
    if base == "iobj":
        return "dative"
    if base in ("obl", "nmod"):
        return "npadvmod" if sub in ("tmod", "npmod") else ("poss" if sub == "poss" else "pobj")
    if base == "xcomp":
        return "oprd"
    return base


def ud_trees(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("#"):
                continue
            if not line:
                if rows:
                    yield rows
                rows = []
                continue
            c = line.split("\t")
            if "-" not in c[0] and "." not in c[0]:
                rows.append(c)
    if rows:
        yield rows


def main():
    out = sys.argv[1]
    opts = dict(zip(sys.argv[2::2], sys.argv[3::2]))
    nlp = spacy.blank("en")
    rng = random.Random(int(opts.get("--seed", 1)))
    trees = [("random",) + random_tree(rng) for _ in range(int(opts.get("--random", 0)))]
    if "--ud" in opts:
        for rows in ud_trees(opts["--ud"]):
            heads = [int(r[6]) - 1 if int(r[6]) > 0 else i for i, r in enumerate(rows)]
            trees.append(("ud", [r[3] for r in rows], heads, [map_relation(r[7]) for r in rows]))
    cases = []
    for kind, pos, heads, deps in trees:
        doc = Doc(nlp.vocab, words=["w%d" % i for i in range(len(pos))], pos=pos, heads=heads, deps=deps)
        cases.append({"kind": kind, "pos": pos, "heads": heads, "deps": deps, "chunks": [[c.start, c.end] for c in doc.noun_chunks]})
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"spacy": spacy.__version__, "cases": cases}, f, separators=(",", ":"))
    print(out, len(cases), "cases")


main()
