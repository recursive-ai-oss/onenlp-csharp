#!/usr/bin/env python3
"""Writes model-level goldens for real vocabularies: whole words (from public text) cut by the reference package's model.

    python make_real_model_goldens.py MODELS_DIR TEXT_DIR OUT_DIR [--words N]

MODELS_DIR holds one folder per model, each with a tokenizer.json. TEXT_DIR holds `sentences.csv` (tab separated: id, language,
text) and a `udhr` folder of XML files. For each WordPiece or Unigram model, N distinct words are run through `Tokenizer.model.tokenize`
and written as JSON lines {"t": word, "r": [[id, value, start, end], ...]} with offsets in UTF-16 units, after a first line naming
the tokenizer.json. For Unigram models every word is also tried with the U+2581 prefix, as the pre-tokenizer would give it.
"""
import argparse
import json
import os
import re

from tokenizers import Tokenizer


def words_from(text_dir, limit):
    seen = {}
    pattern = re.compile(r"<[^>]*>")
    udhr = os.path.join(text_dir, "udhr")
    for root, _, files in os.walk(udhr):
        for name in sorted(files):
            if name.endswith(".xml"):
                with open(os.path.join(root, name), encoding="utf-8", errors="replace") as f:
                    for w in pattern.sub(" ", f.read()).split():
                        seen.setdefault(w, None)
    with open(os.path.join(text_dir, "sentences.csv"), encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f):
            if i % 150 == 0:
                parts = line.rstrip("\n").split("\t", 2)
                if len(parts) == 3:
                    for w in parts[2].split():
                        seen.setdefault(w, None)
            if len(seen) >= limit * 3:
                break
    words = list(seen)
    step = max(1, len(words) // limit)
    return words[::step][:limit]


def offsets(text):
    table = {0: 0}
    byte = unit = 0
    for ch in text:
        byte += len(ch.encode("utf-8"))
        unit += 2 if ord(ch) > 0xFFFF else 1
        table[byte] = unit
    return table


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("models")
    ap.add_argument("text")
    ap.add_argument("out")
    ap.add_argument("--words", type=int, default=60000)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    words = words_from(a.text, a.words)
    for name in sorted(os.listdir(a.models)):
        path = os.path.join(a.models, name, "tokenizer.json")
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8") as f:
            model = json.load(f)["model"]
        kind = model.get("type") or ("Unigram" if isinstance(model.get("vocab"), list) else "WordPiece" if "merges" not in model else "BPE")
        if kind not in ("WordPiece", "Unigram"):
            continue
        tok = Tokenizer.from_file(path)
        inputs = words + (["\u2581" + w for w in words] if kind == "Unigram" else [])
        with open(os.path.join(a.out, f"real-{name}.jsonl"), "w", encoding="utf-8", newline="\n") as out:
            out.write(json.dumps({"tokenizer_json": path, "model": kind}) + "\n")
            for w in inputs:
                try:
                    table = offsets(w)
                    r = [[t.id, t.value, table[t.offsets[0]], table[t.offsets[1]]] for t in tok.model.tokenize(w)]
                    out.write(json.dumps({"t": w, "r": r}, ensure_ascii=True, separators=(",", ":")) + "\n")
                except Exception as e:
                    out.write(json.dumps({"t": w, "error": str(e)[:60]}, ensure_ascii=True, separators=(",", ":")) + "\n")


if __name__ == "__main__":
    main()
