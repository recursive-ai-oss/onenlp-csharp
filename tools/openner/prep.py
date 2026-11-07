"""Cut web and encyclopedia text into passages for open-type NER labelling, without text that occurs in any
evaluation or dev set.

  prep.py --text en.jsonl --keys eval-keys.npy --extra-keys-from DIR [--extra-keys-from DIR ...] --out passages.jsonl --n 20000 --seed 1

--text is a JSONL file of {"s": source, "t": text}. --keys is a NumPy file of 64-bit text keys (see ../distill/textkeys.py).
Every .jsonl file under --extra-keys-from adds the keys of its sentences ({"tokens": [...]}), so the open-type
evaluation sets are excluded as well. A passage is dropped when any of its segments has a key in the set.
"""
import argparse
import json
import os
import random
import re
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "distill"))
import textkeys  # noqa: E402

_SENT = re.compile(r"(?<=[.!?])\s+(?=[\"'(A-Z0-9])")


def passages(doc: str, lo=250, hi=900):
    for para in re.split(r"\n+", doc):
        para = para.strip()
        if len(para) < lo:
            continue
        cur = ""
        for s in _SENT.split(para):
            if cur and len(cur) + len(s) + 1 > hi:
                if len(cur) >= lo:
                    yield cur
                cur = s
            else:
                cur = (cur + " " + s).strip()
        if len(cur) >= lo:
            yield cur


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--text", required=True)
    ap.add_argument("--keys", required=True)
    ap.add_argument("--extra-keys-from", action="append", default=[])
    ap.add_argument("--out", required=True)
    ap.add_argument("--n", type=int, required=True)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--skip-ids-file", help="JSONL of passages whose texts are excluded (earlier chunks)")
    a = ap.parse_args()

    keys = set(np.load(a.keys).tolist())
    extra = 0
    for d in a.extra_keys_from:
        for fn in os.listdir(d):
            if fn.endswith(".jsonl"):
                for line in open(os.path.join(d, fn), encoding="utf-8"):
                    k = textkeys.key(" ".join(json.loads(line)["tokens"]))
                    if k is not None:
                        keys.add(k)
                        extra += 1
    skip = set()
    if a.skip_ids_file:
        skip = {json.loads(l)["text"] for l in open(a.skip_ids_file, encoding="utf-8")}

    cand, dropped = [], 0
    for line in open(a.text, encoding="utf-8"):
        rec = json.loads(line)
        for p in passages(rec["t"]):
            if p in skip:
                continue
            if any((textkeys.key(s) in keys) for s in textkeys.segments(p) if textkeys.key(s) is not None):
                dropped += 1
                continue
            cand.append({"src": rec["s"], "text": p})
    random.Random(a.seed).shuffle(cand)
    cand = cand[: a.n]
    with open(a.out, "w", encoding="utf-8") as f:
        for i, c in enumerate(cand):
            f.write(json.dumps({"id": i, **c}, ensure_ascii=False) + "\n")
    print(f"extra keys {extra}, dropped {dropped} passages for eval overlap, wrote {len(cand)}")


if __name__ == "__main__":
    main()
