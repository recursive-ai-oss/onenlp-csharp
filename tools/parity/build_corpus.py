#!/usr/bin/env python3
"""Builds the tokenizer parity corpus: deterministic for a given seed and inputs.

  build_corpus.py --udhr DIR --tatoeba sentences.csv --out DIR [--seed 20261104]
  build_corpus.py --profile toy --out DIR            (synthetic classes only, small; used for the harness self-check)

Outputs in --out: records.jsonl, pairs.jsonl and manifest.json (counts per class, sizes, SHA-256 of every file, corpus hash).
Standard library only. The corpus is never committed; docs/parity.md describes the inputs and their licences.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import sys
import unicodedata
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import dumps, sha256_file  # noqa: E402

TATOEBA_PER_LANG = 200
UDHR_PARAS_PER_LANG = 4
PAIRS_FROM_TATOEBA = 150


def rng_for(seed: int, name: str) -> random.Random:
    h = hashlib.sha256(f"{seed}/{name}".encode()).digest()
    return random.Random(int.from_bytes(h[:8], "big"))


def pick(r: random.Random, lo: int, hi: int) -> str:
    return chr(r.randint(lo, hi))


# ---------------------------------------------------------------- real text

def udhr_records(udhr_dir: str, seed: int):
    """One full-text record per language plus a few sampled paragraphs. Files are read in sorted order."""
    data = os.path.join(udhr_dir, "data", "udhr")
    r = rng_for(seed, "udhr")
    ns = "{http://efele.net/udhr}"
    inputs = hashlib.sha256()
    for name in sorted(os.listdir(data)):
        if not (name.startswith("udhr_") and name.endswith(".xml")):
            continue
        path = os.path.join(data, name)
        with open(path, "rb") as f:
            raw = f.read()
        inputs.update(name.encode() + hashlib.sha256(raw).digest())
        root = ET.fromstring(raw)
        lang = f"{root.get('iso639-3', 'und')}-{root.get('iso15924', 'Zzzz')}"
        paras = []
        for el in root.iter():
            if el.tag in (ns + "para", ns + "title"):
                t = "".join(el.itertext()).strip()
                if t:
                    paras.append(t)
        if not paras:
            continue
        yield {"class": "udhr-full", "lang": lang, "text": "\n".join(paras)}
        for p in r.sample(paras, min(UDHR_PARAS_PER_LANG, len(paras))):
            yield {"class": "udhr-para", "lang": lang, "text": p}
    yield {"_inputs_udhr": inputs.hexdigest()}


def tatoeba_sentences(csv_path: str, seed: int):
    """Reservoir sample of TATOEBA_PER_LANG sentences per language with at least that many; file order is fixed."""
    r = rng_for(seed, "tatoeba")
    pools: dict[str, list[tuple[int, str]]] = {}
    counts: dict[str, int] = {}
    with open(csv_path, "r", encoding="utf-8", newline="\n") as f:
        for line in f:
            parts = line.rstrip("\n").split("\t", 2)
            if len(parts) != 3:
                continue
            sid, lang, text = parts
            if not text or not sid.isdigit():
                continue
            n = counts.get(lang, 0)
            counts[lang] = n + 1
            pool = pools.setdefault(lang, [])
            if n < TATOEBA_PER_LANG:
                pool.append((int(sid), text))
            else:
                j = r.randint(0, n)
                if j < TATOEBA_PER_LANG:
                    pool[j] = (int(sid), text)
    for lang in sorted(pools):
        if counts[lang] >= TATOEBA_PER_LANG:
            for sid, text in sorted(pools[lang]):
                yield {"class": "tatoeba", "lang": lang, "text": text}


# ---------------------------------------------------------------- synthetic classes

WORDS = ("the of and to in is that for it was on are as with his they at be this from have or by one had not but what all were "
         "when we there can an your which their said if do will each about how up out them then she many some so these would "
         "other into has more her two like him see time could no make than first been its who now people my made over did").split()


def gen_cjk(r, n):
    ranges = [(0x4E00, 0x9FFF), (0x3400, 0x4DBF), (0x3041, 0x3096), (0x30A1, 0x30FA), (0xAC00, 0xD7A3), (0x20000, 0x2A6DF), (0x3000, 0x303F)]
    for i in range(n):
        s = []
        for _ in range(r.choice([1, 2, 5, 20, 80, 200])):
            if r.random() < 0.06:
                s.append(r.choice("abcXYZ0123,.!?"))
            else:
                lo, hi = r.choice(ranges)
                s.append(pick(r, lo, hi))
        yield "".join(s)


def gen_emoji(r, n):
    bases = ["\U0001F600", "\U0001F44D", "\U0001F44B", "\U0001F9D1", "\U0001F469", "\U0001F468", "\U0001F466", "\U0001F467", "\u2764",
             "\U0001F525", "\U0001F680", "\u263A", "\u2615", "\U0001F3F3", "\U0001F9D1", "\u2640", "\u2642", "\U0001F4BB", "\U0001F91D"]
    tones = [chr(c) for c in range(0x1F3FB, 0x1F400)]
    zwj, vs16 = "\u200d", "\ufe0f"
    fixed = ["\U0001F468\u200d\U0001F469\u200d\U0001F467\u200d\U0001F466", "\U0001F469\U0001F3FD\u200d\U0001F4BB", "\U0001F3F3\ufe0f\u200d\U0001F308",
             "\U0001F9D1\u200d\U0001F91D\u200d\U0001F9D1", "1\ufe0f\u20e3", "#\ufe0f\u20e3", "*\ufe0f\u20e3", "\U0001F1FA\U0001F1F8", "\U0001F1EF\U0001F1F5",
             "\U0001F1FA", "\U0001F1FA\U0001F1F8\U0001F1EF", "\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F", "\u2764\ufe0f\u200d\U0001F525",
             "\U0001F44D\U0001F3FF", "\u00a9\ufe0f", "\u2122", "\U0001F600\U0001F600\U0001F600"]
    for i in range(n):
        parts = []
        for _ in range(r.randint(1, 8)):
            k = r.random()
            if k < 0.3:
                parts.append(r.choice(fixed))
            elif k < 0.5:
                parts.append(r.choice(bases) + r.choice(tones))
            elif k < 0.7:
                parts.append(r.choice(bases) + zwj + r.choice(bases) + (vs16 if r.random() < 0.5 else ""))
            elif k < 0.8:
                parts.append(pick(r, 0x1F1E6, 0x1F1FF) + pick(r, 0x1F1E6, 0x1F1FF))
            elif k < 0.9:
                parts.append(r.choice(WORDS))
            else:
                parts.append(r.choice(bases) + vs16)
        yield r.choice(["", " "]).join(parts)


def gen_combining(r, n):
    ranges = [(0x0300, 0x036F), (0x0483, 0x0489), (0x0591, 0x05BD), (0x064B, 0x065F), (0x0900, 0x0903), (0x093A, 0x094F), (0x1AB0, 0x1ABD),
              (0x1DC0, 0x1DFF), (0x20D0, 0x20F0), (0xFE20, 0xFE2F), (0x0E31, 0x0E3A)]
    for i in range(n):
        if i % 6 == 0:
            yield unicodedata.normalize("NFD", r.choice(["caf\u00e9", "\u00e0\u00e8\u00ec\u00f2\u00f9", "\u1e9b\u0323", "Å", "\u0958\u0959", "\u1e0b\u0323", "ñandú"]))
            continue
        s = []
        for _ in range(r.randint(1, 6)):
            s.append(r.choice("aeioubcdnx\u0915\u0627\u05d0 "))
            for _ in range(r.choice([0, 1, 2, 5, 12])):
                lo, hi = r.choice(ranges)
                s.append(pick(r, lo, hi))
        if i % 11 == 0:
            s.insert(0, pick(r, 0x0300, 0x036F))
        yield "".join(s)


def gen_hangul(r, n):
    for i in range(n):
        s = []
        for _ in range(r.randint(1, 10)):
            k = r.random()
            if k < 0.35:
                s.append(pick(r, 0x1100, 0x1112) + pick(r, 0x1161, 0x1175) + (pick(r, 0x11A8, 0x11C2) if r.random() < 0.5 else ""))
            elif k < 0.5:
                s.append(pick(r, 0x1100, 0x1112))
            elif k < 0.6:
                s.append(pick(r, 0x1161, 0x1175) + pick(r, 0x11A8, 0x11C2))
            elif k < 0.75:
                s.append(pick(r, 0x3131, 0x3163))
            elif k < 0.8:
                s.append(pick(r, 0xFFA0, 0xFFDC))
            elif k < 0.9:
                s.append(unicodedata.normalize("NFD", pick(r, 0xAC00, 0xD7A3)))
            else:
                s.append(pick(r, 0xAC00, 0xD7A3))
        yield "".join(s)


def gen_rtl(r, n):
    controls = ["\u200e", "\u200f", "\u202a", "\u202b", "\u202c", "\u202d", "\u202e", "\u2066", "\u2067", "\u2068", "\u2069", "\u061c"]
    for i in range(n):
        s = []
        for _ in range(r.randint(2, 12)):
            k = r.random()
            if k < 0.3:
                s.append("".join(pick(r, 0x05D0, 0x05EA) for _ in range(r.randint(1, 8))))
            elif k < 0.6:
                s.append("".join(pick(r, 0x0627, 0x064A) for _ in range(r.randint(1, 8))))
            elif k < 0.7:
                s.append(r.choice(WORDS))
            elif k < 0.8:
                s.append(str(r.randint(0, 99999)))
            else:
                s.append(r.choice(controls))
            s.append(r.choice([" ", "", " ", r.choice(controls)]))
        yield "".join(s)


def gen_whitespace(r, n):
    ws = [" ", "\t", "\u00a0", "\u3000", "\u2003", "\u2009", "\u200a", "\u200b", "\r\n", "\n", "\r", "\u2028", "\u2029", "\u0085", "\x0b", "\x0c",
          "\u1680", "\u202f", "\u205f", "  ", "    "]
    fixed = ["", " ", "  ", "\t", "\n", "\r\n", "\u00a0", "\u3000", " " * 100, "\t" * 50, "hello  world", " hello", "hello ", "\nhello\n", "a\r\nb\r\nc",
             "a \u00a0 b", "x\u3000\u3000y", "\u200b\u200bhello"]
    for i in range(n):
        if i < len(fixed):
            yield fixed[i]
            continue
        s = []
        for _ in range(r.randint(1, 8)):
            s.append(r.choice(ws) * r.choice([1, 1, 2, 3, 10, 40]))
            if r.random() < 0.7:
                s.append(r.choice(WORDS))
        yield "".join(s)


def gen_control(r, n):
    pools = [(0x00, 0x1F), (0x7F, 0x9F), (0xFEFF, 0xFEFF), (0x200C, 0x200D), (0xE0020, 0xE007F), (0xFE00, 0xFE0F), (0xE0100, 0xE01EF), (0xE000, 0xF8FF),
             (0xF0000, 0xF00FF), (0xFDD0, 0xFDEF), (0xFFFE, 0xFFFF), (0x1FFFE, 0x1FFFF), (0x10FFFE, 0x10FFFF), (0xFFF9, 0xFFFD), (0x2060, 0x206F)]
    for i in range(n):
        s = []
        for _ in range(r.randint(1, 10)):
            lo, hi = r.choice(pools)
            s.append(pick(r, lo, hi))
            if r.random() < 0.5:
                s.append(r.choice(["a", "bc", " ", "xyz", "1"]))
        yield "".join(s)


def gen_numbers(r, n):
    fixed = ["0", "007", "3.14159", "1,234,567.89", "1e-10", "-42", "+1 (555) 010-9999", "1234567890123456789012345678901234567890", "\uff11\uff12\uff13",
             "\u0661\u0662\u0663", "\u0967\u0968\u0969", "2026-11-04T12:30:00Z", "10:30", "3/4", "\u00bd", "\u2155", "1st 2nd 3rd 4th", "$1,000.00", "\u20ac5", "99%"]
    for i in range(n):
        if i < len(fixed):
            yield fixed[i]
            continue
        k = r.random()
        if k < 0.4:
            yield "".join(r.choice("0123456789") for _ in range(r.choice([5, 20, 60, 200, 1000])))
        elif k < 0.7:
            yield " ".join(str(r.randint(0, 10 ** r.randint(1, 12))) for _ in range(r.randint(1, 8)))
        else:
            yield "".join(pick(r, c, c + 9) for c in [0x0660, 0x06F0, 0x0966, 0xFF10] for _ in range(r.randint(0, 5)))


CODE = ["https://example.com/a/b?c=d&e=f#g", "http://user:pass@host:8080/path/to/file.html", "mailto:someone@example.org", "someone.name+tag@sub.example.co.uk",
        "ftp://ftp.example.com/pub/file.tar.gz", "C:\\Users\\name\\Documents\\file.txt", "/usr/local/bin/python3 -m venv .venv", "git commit -m \"fix: handle \\\"quotes\\\"\"",
        "def f(x):\n    return x ** 2  # square\n", "for (int i = 0; i < n; i++) { sum += a[i]; }", "SELECT * FROM t WHERE a = 'x' AND b <> 3;",
        "{\"key\": [1, 2, {\"nested\": null}], \"s\": \"\\u00e9\"}", "<html><body class=\"x\">Hi &amp; bye</body></html>", "x = y if z else w; print(f\"{x!r:>10}\")",
        "^\\d{3}-\\d{4}$", "#include <stdio.h>\nint main(void){return 0;}", "www.example.com", "192.168.0.1:443", "2001:db8::ff00:42:8329", "@user #hashtag $TICKER",
        "a.b.c.d.e.f", "snake_case_name camelCaseName PascalCaseName kebab-case-name", "0xDEADBEEF 0b1010 0o777", "https://\u4f8b\u3048.jp/\u30d1\u30b9?q=\u691c\u7d22"]


def gen_code(r, n):
    for i in range(n):
        if i < len(CODE):
            yield CODE[i]
        else:
            yield r.choice(["", " ", "\n"]).join(r.choice(CODE) for _ in range(r.randint(1, 4)))


SPECIALS = ["[CLS]", "[SEP]", "[MASK]", "[PAD]", "[UNK]", "<s>", "</s>", "<pad>", "<unk>", "<mask>", "<|endoftext|>", "<|im_start|>", "<|im_end|>",
            "<|fim_prefix|>", "<|fim_middle|>", "<|fim_suffix|>", "<|endofprompt|>", "<extra_id_0>", "<extra_id_99>", "[unused1]", "[CLS", "CLS]", "<s", "s>",
            "<|endoftext|", "|endoftext|>", "<|endoftex", "[[CLS]]", "<<s>>", "<S>", "[cls]", "< /s>", "\u2581", "\u0120", "\u010a", "##ing", "<0x0A>",
            "<|endoftext|><|endoftext|>", "<|im_start|>user\nhi<|im_end|>", "[CLS][SEP]", "[CLS] [SEP]", "<s></s>", "<s> </s>", "[MASK][MASK]", "<unk>x<unk>"]


def gen_specials(r, n):
    for s in SPECIALS:
        yield s
    for s in SPECIALS:
        yield f"hello {s} world"
        yield f"hello{s}world"
    for i in range(n):
        yield " ".join(r.choice(SPECIALS + WORDS) for _ in range(r.randint(2, 8)))


def gen_single(r):
    out = [chr(c) for c in range(32, 127)] + ["\t", "\n", "\r", "\x00", "\x7f", "\u00a0", "\u00e9", "\u00df", "\u0131", "\u0130", "\u01c5", "\u03a3", "\u03c2", "\u4e2d", "\u3042",
                                               "\u30a2", "\uac00", "\u0627", "\u05d0", "\u0915", "\u0e01", "\u200b", "\u200d", "\ufe0f", "\u0301", "\ufeff", "\ufffd",
                                               "\U0001F600", "\U00020000", "\U0010FFFF", "\U000E0020", "\u2028"]
    for _ in range(40):
        out.append(chr(r.choice([r.randint(0xA1, 0x2FF), r.randint(0x400, 0x4FF), r.randint(0x4E00, 0x9FFF), r.randint(0x1F300, 0x1F64F), r.randint(0x1000, 0x1FFF)])))
    return out


def gen_surrogates(r):
    H, L = 0xD800, 0xDC00
    base = [[H], [0xDBFF], [L], [0xDFFF], [L, H], [H, H], [L, L], [H, 0x41], [0x41, H], [0x41, L, 0x42], [0x61, 0x62, H], [L, 0x61, 0x62],
            [0xD83D, 0xDE00], [0xD83D, 0xDE00, 0xD83D], [0xDE00, 0xD83D, 0xDE00], [H, 0x20, L], [0x4E2D, H, 0x6587], [0x65, 0x301, L], [H, 0x200D, L]]
    out = [{"u16": u} for u in base]
    for _ in range(21):
        units = []
        for _ in range(r.randint(2, 12)):
            k = r.random()
            if k < 0.35:
                units.append(r.randint(0xD800, 0xDFFF))
            elif k < 0.5:
                units.extend([r.randint(0xD800, 0xDBFF), r.randint(0xDC00, 0xDFFF)])
            else:
                units.append(r.randint(0x20, 0x7E) if r.random() < 0.7 else r.randint(0x4E00, 0x9FFF))
        out.append({"u16": units})
    return out


def gen_invalid_utf8(r, n_random):
    hexes = ["ff", "c080", "e08080", "eda080", "f4908080", "f5808080", "80", "c2", "e282", "f09f98", "61f18080e180c262", "f0808080", "e09f80", "ed9fbf", "efbbbf",
             "c0af", "e0a080", "f0908080", "f48f8080", "f8888080 80".replace(" ", ""), "fe", "fffe", "c2c2", "e2e282ac", "f0f09f98 80".replace(" ", ""),
             "68656c6c6fe282", "e282ac80", "80e282ac", "7f80", "c1bf", "eda0bdedb880"]
    out = [{"utf8_hex": h} for h in hexes]
    valid = ["h\u00e9llo w\u00f6rld", "\u4e2d\u6587\u6d4b\u8bd5", "\U0001F600 ok", "na\u00efve caf\u00e9", "\u0645\u0631\u062d\u0628\u0627"]
    for _ in range(n_random):
        k = r.random()
        if k < 0.5:
            b = bytes(r.randint(0, 255) for _ in range(r.randint(1, 40)))
        else:
            b = bytearray(r.choice(valid).encode("utf-8"))
            for _ in range(r.randint(1, 3)):
                pos = r.randint(0, len(b))
                b[pos:pos] = bytes(r.choice([0x80, 0xBF, 0xC0, 0xC2, 0xE2, 0xED, 0xF0, 0xF4, 0xF5, 0xFF]) for _ in range(r.randint(1, 3)))
            if r.random() < 0.3:
                del b[-1:]
            b = bytes(b)
        out.append({"utf8_hex": b.hex()})
    return out


def gen_long(r, toy):
    n = 20_000 if toy else 1_000_000
    english = " ".join(r.choice(WORDS) for _ in range(n // 3))[:n]
    yield "english-words", english
    if toy:
        yield "single-char-run", "a" * n
        return
    yield "cjk", "".join(pick(r, 0x4E00, 0x9FFF) for _ in range(n))
    mixed_pool = ["a", "Z", " ", "\u00e9", "\u4e2d", "\U0001F600", "\u0627", "\u05d0", "\u0915", "1", ".", "\u0301", "\u200d", "\n", "\t", "\u3042", "\uac00"]
    yield "mixed-unicode", "".join(r.choice(mixed_pool) for _ in range(n))
    yield "single-char-run", "a" * n
    yield "whitespace-run", (" " * 999 + "x") * (n // 1000)
    yield "emoji", "".join(r.choice(["\U0001F600", "\U0001F468\u200d\U0001F469\u200d\U0001F467", "\U0001F1FA\U0001F1F8", "1\ufe0f\u20e3", "\U0001F44D\U0001F3FD"]) for _ in range(n // 4))


def synthetic(seed: int, toy: bool):
    """Yields record dicts (without ids) for the synthetic classes. `toy` shrinks counts and the long lines."""
    s = 1 if toy else 4

    def cls(name, strings):
        for t in strings:
            yield {"class": name, "text": t}

    yield {"class": "empty", "text": ""}
    yield from cls("single-char", gen_single(rng_for(seed, "single")))
    yield from cls("cjk-run", gen_cjk(rng_for(seed, "cjk"), 15 * s))
    yield from cls("emoji", gen_emoji(rng_for(seed, "emoji"), 20 * s))
    yield from cls("combining", gen_combining(rng_for(seed, "combining"), 15 * s))
    yield from cls("hangul", gen_hangul(rng_for(seed, "hangul"), 12 * s))
    yield from cls("rtl-bidi", gen_rtl(rng_for(seed, "rtl"), 12 * s))
    yield from cls("whitespace", gen_whitespace(rng_for(seed, "ws"), 20 * s + 18))
    yield from cls("control", gen_control(rng_for(seed, "control"), 15 * s))
    yield from cls("numbers", gen_numbers(rng_for(seed, "numbers"), 12 * s + 20))
    yield from cls("url-code", gen_code(rng_for(seed, "code"), 15 * s + len(CODE)))
    yield from cls("special-lookalike", gen_specials(rng_for(seed, "special"), 20 * s))
    for rec in gen_surrogates(rng_for(seed, "surrogate")):
        yield {"class": "lone-surrogate", **rec}
    for rec in gen_invalid_utf8(rng_for(seed, "invalid"), 30 if toy else 60):
        yield {"class": "invalid-utf8", **rec}
    for name, text in gen_long(rng_for(seed, "long"), toy):
        yield {"class": "long-line", "lang": name, "text": text}


def synthetic_pairs():
    return [("", ""), ("", "x"), ("x", ""), ("hello [SEP] world", "[CLS] x <s> y </s>"), ("a" * 3000, "short"), ("short", "word " * 1500),
            ("\U0001F600 \u4e2d\u6587", "caf\u00e9 \u00e9"), ("hello world", "hello world"), ("one two three four five six seven eight", "nine ten eleven twelve"),
            ("a b c d e f g h i j k l m n o p q r s t", "u v w x y z"), ("\u200b", " "), ("[CLS]", "[SEP]")]


# ---------------------------------------------------------------- driver

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=20261104)
    ap.add_argument("--profile", choices=["full", "toy"], default="full")
    ap.add_argument("--udhr", help="directory holding the UDHR XML tree (contains data/udhr/udhr_*.xml)")
    ap.add_argument("--tatoeba", help="Tatoeba sentences.csv (id, language, text; tab separated)")
    a = ap.parse_args()
    toy = a.profile == "toy"
    if not toy and not (a.udhr and a.tatoeba):
        ap.error("--udhr and --tatoeba are required unless --profile toy")
    os.makedirs(a.out, exist_ok=True)

    raw_records = []
    inputs = {}
    tat_by_lang: dict[str, list[str]] = {}
    if not toy:
        for rec in udhr_records(a.udhr, a.seed):
            if "_inputs_udhr" in rec:
                inputs["udhr"] = rec["_inputs_udhr"]
            else:
                raw_records.append(rec)
        for rec in tatoeba_sentences(a.tatoeba, a.seed):
            raw_records.append(rec)
            tat_by_lang.setdefault(rec["lang"], []).append(rec["text"])
        inputs["tatoeba_sentences_csv"] = sha256_file(a.tatoeba)
    raw_records.extend(synthetic(a.seed, toy))

    rp = os.path.join(a.out, "records.jsonl")
    counts: dict[str, int] = {}
    chars: dict[str, int] = {}
    with open(rp, "w", encoding="utf-8", newline="\n") as f:
        for i, rec in enumerate(raw_records):
            out = {"id": i, **rec}
            f.write(dumps(out) + "\n")
            c = rec["class"]
            counts[c] = counts.get(c, 0) + 1
            chars[c] = chars.get(c, 0) + len(rec.get("text", "")) + len(rec.get("u16", [])) + len(rec.get("utf8_hex", "")) // 2

    pr = rng_for(a.seed, "pairs")
    pairs = []
    for langs in [sorted(l for l, v in tat_by_lang.items() if len(v) >= 2)]:
        for _ in range(PAIRS_FROM_TATOEBA if langs else 0):
            lang = pr.choice(langs)
            x, y = pr.sample(tat_by_lang[lang], 2)
            pairs.append(("tatoeba-pair", lang, x, y))
    for x, y in synthetic_pairs():
        pairs.append(("synthetic-pair", None, x, y))
    pp = os.path.join(a.out, "pairs.jsonl")
    with open(pp, "w", encoding="utf-8", newline="\n") as f:
        for i, (c, lang, x, y) in enumerate(pairs):
            f.write(dumps({"id": i, "class": c, "lang": lang, "a": x, "b": y}) + "\n")
            counts[c] = counts.get(c, 0) + 1

    files = {"records.jsonl": sha256_file(rp), "pairs.jsonl": sha256_file(pp)}
    corpus_hash = hashlib.sha256((files["records.jsonl"] + files["pairs.jsonl"]).encode()).hexdigest()
    manifest = {"format": 1, "profile": a.profile, "seed": a.seed, "records": len(raw_records), "pairs": len(pairs), "counts": dict(sorted(counts.items())),
                "chars_or_bytes_by_class": dict(sorted(chars.items())), "files": {k: {"sha256": v, "bytes": os.path.getsize(os.path.join(a.out, k))} for k, v in files.items()},
                "inputs": inputs, "corpus_sha256": corpus_hash}
    with open(os.path.join(a.out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, sort_keys=True)
        f.write("\n")
    print(json.dumps({"records": len(raw_records), "pairs": len(pairs), "corpus_sha256": corpus_hash}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
