#!/usr/bin/env python
"""Black-box interop with spaCy's documented Doc.to_json / Doc.from_json.

    spacy_json.py produce OUT_DIR   write <case>.spacy.json: documents built through the documented
                                    Doc(vocab, words, spaces, ...) constructor, serialized by Doc.to_json
    spacy_json.py consume IN_DIR    read every <case>.ours.json with Doc.from_json, serialize it again with
                                    Doc.to_json and compare the shared fields with the input; exit 1 on a difference

Run it with the interpreter of an environment that has spaCy installed. It prints the spaCy version it ran against.
"""
import json
import sys
from pathlib import Path

import spacy
from spacy.tokens import Doc

SHARED_TOKEN_KEYS = ("id", "start", "end", "tag", "pos", "morph", "lemma", "dep", "head")


def cases(vocab):
    # Each case is text we wrote. Annotations are set by hand, so no model is involved.
    yield "plain", Doc(vocab, words=["Hello", ",", "world", "!"], spaces=[False, True, False, False])
    yield "annotated", Doc(
        vocab,
        words=["Ada", "Lovelace", "wrote", "notes", "in", "London", "."],
        spaces=[True, True, True, True, True, False, False],
        tags=["NNP", "NNP", "VBD", "NNS", "IN", "NNP", "."],
        pos=["PROPN", "PROPN", "VERB", "NOUN", "ADP", "PROPN", "PUNCT"],
        morphs=["Number=Sing", "Number=Sing", "Tense=Past|VerbForm=Fin", "Number=Plur", "", "Number=Sing", ""],
        lemmas=["Ada", "Lovelace", "write", "note", "in", "London", "."],
        heads=[1, 2, 2, 2, 2, 4, 2],
        deps=["compound", "nsubj", "ROOT", "dobj", "prep", "pobj", "punct"],
        ents=["B-PERSON", "I-PERSON", "O", "O", "O", "B-GPE", "O"],
        sent_starts=[True, False, False, False, False, False, False],
    )
    yield "two_sentences", Doc(
        vocab,
        words=["It", "rained", ".", "We", "stayed", "in", "."],
        spaces=[True, False, True, True, True, False, False],
        pos=["PRON", "VERB", "PUNCT", "PRON", "VERB", "ADV", "PUNCT"],
        heads=[1, 1, 1, 4, 4, 4, 4],
        deps=["nsubj", "ROOT", "punct", "nsubj", "ROOT", "advmod", "punct"],
        sent_starts=[True, False, False, True, False, False, False],
    )
    yield "entities_only", Doc(
        vocab,
        words=["Acme", "Corp", "hired", "Bo", "."],
        spaces=[True, True, True, False, False],
        ents=["B-ORG", "I-ORG", "O", "B-PERSON", "O"],
    )
    yield "unicode", Doc(vocab, words=["Caf\u00e9", "\u65e5\u672c\u8a9e", "\U0001F600", "e\u0301"], spaces=[True, True, True, False])
    yield "empty", Doc(vocab, words=[], spaces=[])


def produce(out):
    out.mkdir(parents=True, exist_ok=True)
    nlp = spacy.blank("en")
    for name, doc in cases(nlp.vocab):
        (out / f"{name}.spacy.json").write_text(json.dumps(doc.to_json(), indent=1, ensure_ascii=False) + "\n", encoding="utf-8")


def project(j):
    return {
        "text": j["text"],
        "ents": [(e["start"], e["end"], e["label"]) for e in j.get("ents", [])],
        "sents": [(s["start"], s["end"]) for s in j.get("sents", [])],
        "tokens": [{k: t[k] for k in SHARED_TOKEN_KEYS if k in t} for t in j["tokens"]],
    }


def consume(indir):
    nlp = spacy.blank("en")
    bad = 0
    files = sorted(indir.glob("*.ours.json"))
    for f in files:
        ours = json.loads(f.read_text(encoding="utf-8"))
        doc = Doc(nlp.vocab).from_json(ours)
        back = doc.to_json()
        if project(back) != project(ours):
            bad += 1
            print("DIFF", f.name)
            print("  ours :", project(ours))
            print("  spacy:", project(back))
        else:
            print("ok  ", f.name, len(doc), "tokens")
    return 1 if bad or not files else 0


if __name__ == "__main__":
    print("spaCy", spacy.__version__)
    if len(sys.argv) == 3 and sys.argv[1] == "produce":
        produce(Path(sys.argv[2]))
    elif len(sys.argv) == 3 and sys.argv[1] == "consume":
        sys.exit(consume(Path(sys.argv[2])))
    else:
        sys.exit(__doc__)
