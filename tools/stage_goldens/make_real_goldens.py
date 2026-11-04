#!/usr/bin/env python3
"""Writes black-box reference encodings of real tokenizer.json files, for the whole-tokenizer check in the unit tests.

usage: make_real_goldens.py <output.json> <models-dir> [<model-sub-directory> ...]

Each sub-directory (all of them that hold a tokenizer.json when none are named) is loaded with the documented Python API of the
`tokenizers` package and the texts and pairs of make_goldens.py are encoded with and without special tokens. Offsets are converted to
UTF-16 indexes. The package version is recorded. Nothing from the package's files is read: it is only called.
"""
import json
import os
import sys

import tokenizers
from tokenizers import Tokenizer

from make_goldens import PAIRS, TEXTS as BASE_TEXTS, encode_dict

# Cases that reach the SentencePiece character map: line breaks, repeated accents, accents on astral letters, ligatures.
TEXTS = BASE_TEXTS + ["a\r\nb", "ae\u0301\u0301b", "x\U0001D49C\u0301y", "ab\u0301c", "\ufb01x \U0001F600\ufb01"]


def main():
    out, root = sys.argv[1], sys.argv[2]
    names = sys.argv[3:] or sorted(d for d in os.listdir(root) if os.path.isfile(os.path.join(root, d, "tokenizer.json")))
    result = {"oracle": "tokenizers " + tokenizers.__version__, "models": {}}
    for name in names:
        tok = Tokenizer.from_file(os.path.join(root, name, "tokenizer.json"))
        cases = []
        for add in (True, False):
            for text in TEXTS:
                enc = tok.encode(text, add_special_tokens=add)
                cases.append({"text": text, "pair": None, "add_special": add, **encode_dict(enc, [text])})
            for a, b in PAIRS:
                enc = tok.encode(a, b, add_special_tokens=add)
                cases.append({"text": a, "pair": b, "add_special": add, **encode_dict(enc, [a, b])})
        decodes = []
        for text in TEXTS:
            ids = tok.encode(text, add_special_tokens=True).ids
            for skip in (True, False):
                decodes.append({"ids": ids, "skip": skip, "out": tok.decode(ids, skip_special_tokens=skip)})
        result["models"][name] = {"cases": cases, "decode": decodes}
    with open(out, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=True)


if __name__ == "__main__":
    main()
