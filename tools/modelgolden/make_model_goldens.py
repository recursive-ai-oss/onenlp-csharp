#!/usr/bin/env python3
"""Writes model-level goldens for the WordPiece, WordLevel and Unigram models.

Random small vocabularies and texts are run through the `tokenizers` package's own model objects
(`Unigram`, `WordPiece`, `WordLevel`, `.tokenize`), and each result is stored with its offsets converted from the
UTF-8 byte offsets the package reports to UTF-16 code unit offsets. The C# tests read the files and compare the models.

    python make_model_goldens.py OUT_DIR [--seed N] [--configs N] [--texts N]

The package is used only as a black box, through its documented Python API. Its version is written into each file.
"""
import argparse
import base64
import tempfile
import json
import os
import random
import struct

import tokenizers
from tokenizers.models import Unigram, WordLevel, WordPiece

ALPHABET = ["a", "b", "c", "d", "\u00e9", "\u65e5", "\U0001F600", "\u2581", "#", " "]
OUTSIDE = ["x", "y", "z", "\u00fc", "\u4e2d", "\U0001F680"]


def utf16_offsets(text):
    """Maps a UTF-8 byte offset of `text` to a UTF-16 unit offset."""
    table = {0: 0}
    byte = unit = 0
    for ch in text:
        byte += len(ch.encode("utf-8"))
        unit += 2 if ord(ch) > 0xFFFF else 1
        table[byte] = unit
    return table


def run(model, text):
    try:
        table = utf16_offsets(text)
        return {"tokens": [[t.id, t.value, table[t.offsets[0]], table[t.offsets[1]]] for t in model.tokenize(text)]}
    except Exception as e:  # the error itself is the expected result
        return {"error": str(e)[:80]}


def rand_text(rng, max_len, with_outside, alphabet=ALPHABET):
    pool = alphabet + (OUTSIDE if with_outside else [])
    return "".join(rng.choice(pool) for _ in range(rng.randint(0, max_len)))


def dedupe(items):
    seen = set()
    out = []
    for t in items:
        if t not in seen:
            seen.add(t)
            out.append(t)
    return out


def rand_piece(rng, max_len=4):
    return "".join(rng.choice(ALPHABET) for _ in range(rng.randint(1, max_len)))


def unigram_case(rng, n_texts):
    pieces = ["<unk>"]
    seen = {"<unk>"}
    for _ in range(rng.randint(3, 45)):
        p = rand_piece(rng)
        if p not in seen or rng.random() < 0.03:
            seen.add(p)
            pieces.append(p)
    if rng.random() < 0.6:
        for ch in ALPHABET:  # most vocabularies cover every character; the rest force unknowns
            if rng.random() < 0.8 and ch not in seen:
                seen.add(ch)
                pieces.append(ch)
    byte_fallback = rng.random() < 0.4
    if byte_fallback and rng.random() < 0.8:
        every = rng.random() < 0.5
        for b in range(256):
            if every or rng.random() < 0.7 or b in (0xC3, 0xA9, 0xE6, 0x97, 0xA5, 0xF0, 0x9F, 0x98, 0x80, 0x78):
                pieces.append("<0x%02X>" % b)
    rng.shuffle(pieces)
    mode = rng.choice(["ties", "float", "wide"])
    if mode == "ties":
        scores = [float(-rng.randint(1, 4)) for _ in pieces]
    elif mode == "float":
        scores = [struct.unpack("f", struct.pack("f", -rng.uniform(0, 10)))[0] for _ in pieces]
    else:
        scores = [-rng.choice([0.0, 0.5, 1.0, 1.5, 20.0, 100.0]) for _ in pieces]
    unk_id = pieces.index("<unk>") if rng.random() < 0.9 else None
    vocab = [[p, s] for p, s in zip(pieces, scores)]
    model = Unigram([tuple(v) for v in vocab], unk_id, byte_fallback)
    cfg = {"type": "Unigram", "unk_id": unk_id, "vocab": vocab, "byte_fallback": byte_fallback}
    texts = []
    for _ in range(n_texts):
        t = rand_text(rng, 12, rng.random() < 0.5)
        if t and rng.random() < 0.1:
            t += "<unk>"
        texts.append({"text": t, **run(model, t)})
    return {"model": cfg, "texts": [c for c in texts if c["text"]]}


def wordpiece_case(rng, n_texts):
    prefix = rng.choice(["##", "##", "##", "@@", ""])
    unk = rng.choice(["[UNK]", "[UNK]", "<unk>"])
    vocab_tokens = [unk] if rng.random() < 0.92 else ["[PAD]"]
    for ch in ALPHABET:
        if rng.random() < 0.75:
            vocab_tokens.append(ch)
            if rng.random() < 0.7:
                vocab_tokens.append(prefix + ch)
    for _ in range(rng.randint(2, 30)):
        p = rand_piece(rng, 4)
        vocab_tokens.append(p if rng.random() < 0.5 else prefix + p)
    vocab_tokens = dedupe(vocab_tokens)
    rng.shuffle(vocab_tokens)
    ids = list(range(len(vocab_tokens)))
    if rng.random() < 0.2:  # non-contiguous IDs
        ids = [i * 3 + 7 for i in ids]
    vocab = dict(zip(vocab_tokens, ids))
    max_chars = rng.choice([100, 100, 4, 6, 9])
    model = WordPiece(vocab, unk_token=unk, continuing_subword_prefix=prefix, max_input_chars_per_word=max_chars)
    cfg = {"type": "WordPiece", "unk_token": unk, "continuing_subword_prefix": prefix,
           "max_input_chars_per_word": max_chars, "vocab": vocab}
    texts = []
    for _ in range(n_texts):
        t = rand_text(rng, 14, rng.random() < 0.4, [c for c in ALPHABET if c != " "])
        if t:
            texts.append({"text": t, **run(model, t)})
    return {"model": cfg, "texts": texts}


def wordlevel_case(rng, n_texts):
    unk = "[UNK]"
    toks = [unk] if rng.random() < 0.9 else []
    toks += [rand_piece(rng, 3) for _ in range(rng.randint(2, 30))]
    toks = dedupe(toks)
    vocab = {t: i for i, t in enumerate(toks)}
    model = WordLevel(vocab, unk_token=unk)
    cfg = {"type": "WordLevel", "unk_token": unk, "vocab": vocab}
    texts = []
    for _ in range(n_texts):
        t = rand_text(rng, 4, rng.random() < 0.3, [c for c in ALPHABET if c != " "])
        if t:
            texts.append({"text": t, **run(model, t)})
    for t in toks[:5]:
        texts.append({"text": t, **run(model, t)})
    return {"model": cfg, "texts": texts}


def vocab_txt_cases(out):
    """Crafted vocab.txt files with what `WordPiece.read_file` makes of them (token to line number, last duplicate winning)."""
    files = [
        ("basic", b"[UNK]\na\nb\n"),
        ("no-final-newline", b"[UNK]\na\nb"),
        ("trailing-space", b"[UNK]\na \n b\nc\t\n"),
        ("crlf", b"[UNK]\r\na\r\nb\r\n"),
        ("empty-line", b"[UNK]\n\na\n"),
        ("duplicate", b"[UNK]\na\nb\na\n"),
        ("bom", b"\xef\xbb\xbf[UNK]\na\n"),
        ("blank-end", b"[UNK]\na\n\n"),
        ("nel-and-ls-inside", b"[UNK]\na\xc2\x85b\nc\xe2\x80\xa8d\n"),
        ("cr-only", b"[UNK]\ra\rb\r"),
        ("space-only-line", b"[UNK]\n \na\n"),
        ("invalid-utf8", b"[UNK]\na\xff\nb\n"),
    ]
    for c in ["\x0b", "\x0c", "\x1c", "\x1d", "\x1f", "\xa0", "\u2003", "\u2028", "\u200b", "\u180e", "\u3000", "\u0085", "\u202f", "\ufeff", "\u00ad"]:
        files.append(("trailing-U+%04X" % ord(c), ("[UNK]\na%s\nb\n" % c).encode("utf-8")))
    cases = []
    with tempfile.TemporaryDirectory() as d:
        path = os.path.join(d, "vocab.txt")
        for name, data in files:
            with open(path, "wb") as f:
                f.write(data)
            enc = base64.b64encode(data).decode()
            try:
                cases.append({"name": name, "file": enc, "vocab": WordPiece.read_file(path)})
            except Exception:
                cases.append({"name": name, "file": enc, "error": True})
    doc = {"oracle": f"tokenizers {tokenizers.__version__}", "cases": cases}
    with open(os.path.join(out, "vocab-txt.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, ensure_ascii=True, separators=(",", ":"))
        f.write("\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--seed", type=int, default=20261104)
    ap.add_argument("--configs", type=int, default=20)
    ap.add_argument("--texts", type=int, default=25)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    vocab_txt_cases(a.out)
    for name, make in (("unigram", unigram_case), ("wordpiece", wordpiece_case), ("wordlevel", wordlevel_case)):
        rng = random.Random(f"{a.seed}-{name}")
        doc = {"oracle": f"tokenizers {tokenizers.__version__}", "seed": a.seed,
               "cases": [make(rng, a.texts) for _ in range(a.configs)]}
        with open(os.path.join(a.out, f"{name}.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(doc, f, ensure_ascii=True, separators=(",", ":"))
            f.write("\n")


if __name__ == "__main__":
    main()
