#!/usr/bin/env python3
"""Count self-training sentences whose normalised key occurs in the evaluation key set.

    check_overlap.py KEYS.npy [--clean-to DIR] FILE...   (CoNLL-U with '# text =' lines, or JSONL with a "text" field)

Prints one JSON line per file: sentences, keyed sentences (long enough to carry a key) and hits. A hit means the
text copies an evaluation or development sentence; the expected count is 0. With --clean-to the files are rewritten into DIR without the hit sentences.
Exit status 1 when any hit is found.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from textkeys import key  # noqa: E402


def sentences(path):
    """Yield (text, raw lines) per sentence; CoNLL-U text comes from '# text =' or is rebuilt from FORM and SpaceAfter."""
    with open(path, encoding="utf-8", errors="replace") as f:
        if path.endswith(".jsonl"):
            for line in f:
                if line.strip():
                    yield json.loads(line)["text"], [line]
            return
        block, text, parts = [], None, []
        for line in f:
            if line.strip() == "":
                if block:
                    yield (text if text is not None else "".join(parts)), block
                block, text, parts = [], None, []
                continue
            block.append(line)
            if line.startswith("# text ="):
                text = line.split("=", 1)[1].strip()
            elif not line.startswith("#"):
                c = line.rstrip("\n").split("\t")
                if len(c) >= 10 and "-" not in c[0] and "." not in c[0]:
                    parts.append(c[1] + ("" if "SpaceAfter=No" in c[9] else " "))
        if block:
            yield (text if text is not None else "".join(parts)), block


def main():
    keys = np.load(sys.argv[1])
    args = sys.argv[2:]
    out_dir = None
    if args and args[0] == "--clean-to":
        out_dir, args = args[1], args[2:]
        os.makedirs(out_dir, exist_ok=True)
    bad = 0
    for path in args:
        n = keyed = hits = 0
        out = open(os.path.join(out_dir, os.path.basename(path)), "w", encoding="utf-8") if out_dir else None
        for t, block in sentences(path):
            n += 1
            k = key(t)
            hit = False
            if k is not None:
                keyed += 1
                i = min(int(np.searchsorted(keys, np.uint64(k))), len(keys) - 1)
                hit = keys[i] == np.uint64(k)
            if hit:
                hits += 1
            elif out:
                out.write("".join(block))
                if not path.endswith(".jsonl"):
                    out.write("\n")
        if out:
            out.close()
        print(json.dumps({"file": os.path.basename(path), "sentences": n, "keyed": keyed, "hits": hits}))
        bad += hits
    sys.exit(1 if bad else 0)


main()
