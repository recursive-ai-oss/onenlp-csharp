#!/usr/bin/env python3
"""Writes fuzz seed files: small valid .tiktoken ranks files and BPE model objects, plus damaged copies. Usage: make_fuzz_seeds.py <dir>"""
import base64
import json
import os
import sys

out = sys.argv[1]
os.makedirs(out + "/tiktoken-ranks", exist_ok=True)
os.makedirs(out + "/bpe-model", exist_ok=True)

lines = [f"{base64.b64encode(bytes([b])).decode()} {b}" for b in range(256)]
merged = ["ab", "bc", "abc", "aa", "aab", " a", "日", "😀"]
lines += [f"{base64.b64encode(m.encode()).decode()} {256 + i}" for i, m in enumerate(merged)]
good = "\n".join(lines) + "\n"
seeds = {
    "good": good,
    "no-final-newline": good.rstrip("\n"),
    "crlf": good.replace("\n", "\r\n"),
    "truncated": good[: len(good) // 2],
    "dup-rank": good + "YWI= 5\n",
    "bad-base64": good + "!!! 999\n",
    "huge-rank": good + "Yg== 99999999999\n",
    "blank-line": good + "\n\nYWI= 999\n",
    "no-space": good + "YWI=\n",
    "empty": "",
}
for name, text in seeds.items():
    with open(f"{out}/tiktoken-ranks/{name}", "w", newline="") as f:
        f.write(text)

vocab = {"a": 0, "b": 1, "c": 2, "ab": 3, "abc": 4, "<unk>": 5, "é": 6, "<0xF0>": 7, "<0x9F>": 8, "<0x98>": 9, "<0x80>": 10}
models = {
    "plain": {"type": "BPE", "vocab": vocab, "merges": ["a b", "ab c"]},
    "pairs": {"type": "BPE", "vocab": vocab, "merges": [["a", "b"], ["ab", "c"]], "unk_token": "<unk>", "fuse_unk": True},
    "bytefallback": {"type": "BPE", "vocab": vocab, "merges": [], "unk_token": "<unk>", "byte_fallback": True},
    "ignore": {"type": "BPE", "vocab": vocab, "merges": ["a b"], "ignore_merges": True},
    "dropout": {"type": "BPE", "vocab": vocab, "merges": ["a b"], "dropout": 0.3},
    "prefix": {"type": "BPE", "vocab": {"a": 0, "##b": 1, "ab": 2}, "merges": ["a ##b"], "continuing_subword_prefix": "##"},
    "missing": {"type": "BPE", "vocab": vocab, "merges": ["a zz"]},
}
for name, m in models.items():
    with open(f"{out}/bpe-model/{name}", "w") as f:
        json.dump(m, f)
