#!/usr/bin/env python3
"""Pick a fixed, deterministic set of multilingual probe texts from the tokenizer parity corpus.

    select_texts.py --corpus records.jsonl --out texts.jsonl [--n 200] [--seed 7] [--max-chars 400]

3/4 sentence-sized Tatoeba records (one per language first, so as many languages as possible), 1/4 UDHR
paragraphs. Texts are cut at --max-chars code points. The output is one JSON object per line: id, lang, class, text.
"""
import argparse
import json
import random


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--corpus", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--n", type=int, default=200)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--max-chars", type=int, default=400)
    a = ap.parse_args()

    by_class = {"tatoeba": {}, "udhr-para": {}}
    with open(a.corpus, encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            c = r.get("class")
            if c in by_class and r["text"].strip():
                by_class[c].setdefault(r["lang"], []).append(r)

    rng = random.Random(a.seed)
    out = []
    n_t = a.n * 3 // 4
    for cls, n in (("tatoeba", n_t), ("udhr-para", a.n - n_t)):
        langs = sorted(by_class[cls])
        rng.shuffle(langs)
        picks = []
        i = 0
        while len(picks) < n:
            lang = langs[i % len(langs)]
            i += 1
            picks.append(rng.choice(by_class[cls][lang]))
        out.extend(picks)

    with open(a.out, "w", encoding="utf-8") as f:
        for k, r in enumerate(out):
            t = r["text"][: a.max_chars]
            if any("\ud800" <= ch <= "\udfff" for ch in t):
                t = "".join(ch for ch in t if not "\ud800" <= ch <= "\udfff")
            f.write(json.dumps({"id": k, "lang": r["lang"], "class": r["class"], "text": t}, ensure_ascii=False) + "\n")
    print(f"{len(out)} texts, {len({r['lang'] for r in out})} languages")


if __name__ == "__main__":
    main()
