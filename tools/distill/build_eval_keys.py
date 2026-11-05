#!/usr/bin/env python3
"""Build the set of evaluation-text keys that no distillation document may contain.

    build_eval_keys.py --data DATA_ROOT --out keys.npy --report report.json

Reads, as plain text only: every CoNLL-U / CoNLL-like file under the UD and UNER directories (the `# text =` lines),
the language-ID evaluation and training lines, the parity corpus sentence list and the UDHR translations. Keys are
sorted unique uint64 values (see textkeys.py). The report counts keys per source.
"""
import argparse
import glob
import json
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from textkeys import key  # noqa: E402


def conllu_texts(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("# text = ") or line.startswith("# text="):
                yield line.split("=", 1)[1].strip()


def plain_lines(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.rstrip("\n")
            if line:
                yield line


def tatoeba(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            p = line.rstrip("\n").split("\t")
            if len(p) >= 3:
                yield p[2]


def udhr(root):
    tag = re.compile(r"<[^>]+>")
    for p in glob.glob(os.path.join(root, "**", "*.xml"), recursive=True):
        with open(p, encoding="utf-8", errors="replace") as f:
            for chunk in re.split(r"</(?:para|title)>", f.read()):
                t = tag.sub(" ", chunk).strip()
                if t:
                    yield t


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--report", required=True)
    a = ap.parse_args()
    d = a.data
    sources = {}

    def add(name, it):
        keys = set()
        n = 0
        for t in it:
            n += 1
            k = key(t)
            if k is not None:
                keys.add(k)
        sources[name] = {"texts": n, "keys": len(keys), "set": keys}

    for sub in ("eval/ud", "train/ud", "eval/uner", "train/uner", "train/uner-en-ewt-8ed072de", "eval/ner", "train/ner", "eval/ud-en-ewt-r2.18", "train/ud-en-ewt-r2.18"):
        files = sorted(glob.glob(os.path.join(d, sub, "**", "*.conll*"), recursive=True))
        files += sorted(glob.glob(os.path.join(d, sub, "**", "*.iob2"), recursive=True))
        if files:
            add(sub, (t for p in files for t in conllu_texts(p)))
    for sub in ("eval/langid", "train/langid"):
        files = sorted(glob.glob(os.path.join(d, sub, "**", "*.txt"), recursive=True))
        files += sorted(glob.glob(os.path.join(d, sub, "**", "*.tsv"), recursive=True))
        if files:
            add(sub, (t for p in files for t in plain_lines(p)))
    tp = os.path.join(d, "parity/raw/sentences.csv")
    if os.path.exists(tp):
        add("parity/tatoeba", tatoeba(tp))
    up = os.path.join(d, "parity/raw/udhr")
    if os.path.isdir(up):
        add("parity/udhr", udhr(up))

    allk = set()
    for s in sources.values():
        allk |= s.pop("set")
    arr = np.array(sorted(allk), dtype=np.uint64)
    np.save(a.out, arr)
    rep = {"total_keys": int(arr.size), "sources": sources}
    with open(a.report, "w") as f:
        json.dump(rep, f, indent=1)
    print(json.dumps(rep))


if __name__ == "__main__":
    main()
