#!/usr/bin/env python3
"""Writes reference outputs for the BPE and Tiktoken tests from the released Python packages, used only through their documented APIs.

  bpe-model   <tokenizer.json> <out.jsonl>   pieces (after the file's own normalizer and pre-tokenizer) with the tokens the
                                             model produces for each: id, vocabulary string, start and end (UTF-16 units of the piece)
  tiktoken    <name> <out.jsonl> [--sweep f]  ids, decode and start offsets from encode / encode_ordinary / decode_with_offsets

Common options: --sentences <tab separated id, language, text file>  --per-language N  --seed S
"""
import argparse
import hashlib
import json
import random
import sys


def utf16_len(s):
    return sum(2 if ord(c) > 0xFFFF else 1 for c in s)


def sample_sentences(path, per_language, seed):
    if not path:
        return []
    buckets = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.rstrip("\n").split("\t", 2)
            if len(parts) != 3:
                continue
            # A fixed hash picks the same lines on every run, whatever the file order.
            h = hashlib.sha256(f"{seed}:{parts[0]}".encode()).digest()
            b = buckets.setdefault(parts[1], [])
            b.append((h, parts[2]))
            if len(b) > 8 * per_language:
                b.sort()
                del b[per_language:]
    out = []
    for lang in sorted(buckets):
        items = sorted(buckets[lang])[:per_language]
        out.extend(t for _, t in items)
    return out


def synthetic():
    t = [
        "", " ", "a", "Hello, world!", "  leading and trailing  ", "tabs\tand\nnewlines\r\n\r\n", "\n\n\n", " \n ",
        "It's we're they've I'm you'll he'd THEY'RE I'M ʼs", "don't DON'T Don'T", "12345678901234567890", "3.14159 1,000,000 -42",
        "日本語のテキスト、漢字とひらがな。", "中文，没有空格的句子。", "한국어 텍스트입니다", "Привет, мир! Ёлка", "مرحبا بالعالم", "שלום עולם",
        "हिन्दी पाठ", "ไทยไม่มีช่องว่าง", "Ελληνικά κείμενο", "emoji 😀👍🏽 family 👨‍👩‍👧‍👦 flag 🇯🇵", "e\u0301 a\u0308 \u0915\u094d\u0937",
        "x\u200by\u200dz\ufeff", "\x00\x01\x1f\x7f", "\x1c\x1d\x1e\x1f \x85 \u00a0 \u2028 \u2029 \u3000 \u2003", "tab\x0bvt\x0cff",
        "<|endoftext|>", "a<|endoftext|>b", "<|endoftext|><|endoftext|>", "<|fim_prefix|>x<|fim_suffix|>y<|fim_middle|>", "<|endofprompt|>",
        "<s> </s> <unk> <pad> [CLS] [SEP] <mask>", "<|im_start|>user\nhi<|im_end|>", "<|endoftext", "|endoftext|>",
        "a" * 7, "a" * 49, "a" * 200, "a" * 5000, "ab" * 3000, " " * 300, "\n" * 100, ("word " * 500), "z" * 30000,
        "🙂" * 100, "日" * 500, "é" * 300, "a\u0301" * 200,
        "ſ ſtill K K İ ı ǅ ß ẞ", "SS ss Ss", "'S 'T 'RE 'VE 'M 'LL 'D 'ſ", "Ǆ ǅ ǆ ᾈ", "x²y³ ½ ① Ⅻ ٣ ३", "a_b-c.d/e\\f", "foo/**/bar\r\n// c",
        "\u00ad\u061c\u180e\u2060", "\ud7ff\ue000\uffff", "\U0010ffff\U0001f600\U00020000",
    ]
    r = random.Random(7)
    alphabet = "abcXYZ 019,.'\n\t\r!?-é日😀\u0301\u200d<|>"
    for _ in range(300):
        t.append("".join(r.choice(alphabet) for _ in range(r.randint(1, 40))))
    return t


def texts(args):
    return synthetic() + sample_sentences(args.sentences, args.per_language, args.seed)


def to_char_offsets(piece, s, e):
    """HF reports byte offsets into the UTF-8 piece; a range that cuts a character covers the whole character."""
    b = piece.encode("utf-8")
    starts, ends, pos = [], [], 0
    pos = 0
    char_start, char_end = [], []
    u = 0
    for ch in piece:
        n = len(ch.encode("utf-8"))
        w = 2 if ord(ch) > 0xFFFF else 1
        for _ in range(n):
            char_start.append(u)
            char_end.append(u + w)
        u += w
    if len(b) == 0:
        return 0, 0
    s = min(s, len(b) - 1)
    e = max(e, s + 1)
    return char_start[s], char_end[min(e, len(b)) - 1]


def cmd_bpe_model(args):
    from tokenizers import Tokenizer
    tok = Tokenizer.from_file(args.file)
    seen = set()
    with open(args.out, "w", encoding="utf-8") as out:
        for text in texts(args):
            norm = tok.normalizer.normalize_str(text) if tok.normalizer is not None else text
            if tok.pre_tokenizer is not None:
                pieces = [p for p, _ in tok.pre_tokenizer.pre_tokenize_str(norm)]
            else:
                pieces = [norm]
            for piece in pieces:
                if piece == "" or piece in seen:
                    continue
                seen.add(piece)
                toks = tok.model.tokenize(piece)
                rec = []
                for t in toks:
                    off = getattr(t, "offsets", None) or t.offset
                    s, e = to_char_offsets(piece, off[0], off[1])
                    rec.append([t.id, t.value, s, e])
                out.write(json.dumps({"p": piece, "t": rec}, ensure_ascii=False) + "\n")
    print("pieces", len(seen), file=sys.stderr)


def cmd_tiktoken(args):
    import tiktoken
    enc = tiktoken.get_encoding(args.name)
    with open(args.out, "w", encoding="utf-8") as out:
        all_texts = texts(args)
        for text in all_texts:
            rec = {"s": text}
            try:
                ids = enc.encode(text, allowed_special="all")
                rec["all"] = ids
                rec["ord"] = enc.encode_ordinary(text)
                rec["dec"] = enc.decode(ids)
                rec["decb"] = enc.decode_bytes(ids).hex()
                _, offs = enc.decode_with_offsets(ids)
                rec["off"] = offs
            except Exception as ex:  # lone surrogates cannot be encoded by the Python API
                rec["err"] = type(ex).__name__
            out.write(json.dumps(rec, ensure_ascii=True) + "\n")
    # One line per code point, in two contexts, ids only: finds differences in the split pattern's Unicode classes.
    if args.sweep:
        with open(args.sweep, "w", encoding="ascii") as out:
            for cp in range(0, 0x110000):
                if 0xD800 <= cp <= 0xDFFF:
                    continue
                c = chr(cp)
                a = enc.encode_ordinary("a" + c + "b")
                b = enc.encode_ordinary(c + "\n")
                out.write(f"{cp:x} {','.join(map(str, a))} {','.join(map(str, b))}\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sentences")
    ap.add_argument("--per-language", type=int, default=40)
    ap.add_argument("--seed", type=int, default=1)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("bpe-model")
    p.add_argument("file")
    p.add_argument("out")
    p = sub.add_parser("tiktoken")
    p.add_argument("name")
    p.add_argument("out")
    p.add_argument("--sweep")
    args = ap.parse_args()
    {"bpe-model": cmd_bpe_model, "tiktoken": cmd_tiktoken}[args.cmd](args)


if __name__ == "__main__":
    main()
