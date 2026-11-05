#!/usr/bin/env python3
"""Writes black-box reference outputs of normalizers for every Unicode scalar value, one character at a time.

usage: make_sweep.py <output.json.gz> <models-dir>

For each normalizer the file lists only the code points whose output differs from the character itself, so unchanged characters cost
nothing. Normalizers: BertNormalizer with clean_text only, the uncased BertNormalizer, NFC, NFD, NFKC, NFKD, Lowercase, StripAccents, and the
Precompiled character maps of the XLM-R and T5 tokenizer files. Nothing from the package's files is read: it is only called.
"""
import gzip
import json
import os
import sys

import tokenizers
from tokenizers import Tokenizer, normalizers


def precompiled(models, name):
    tok = Tokenizer.from_file(os.path.join(models, name, "tokenizer.json"))
    n = tok.normalizer
    # Sequence [Precompiled, Replace]: only the first step is the character map.
    return n.normalizers[0] if hasattr(n, "normalizers") else n


def main():
    out, models = sys.argv[1], sys.argv[2]
    subjects = {
        "bert_clean": normalizers.BertNormalizer(clean_text=True, handle_chinese_chars=False, strip_accents=False, lowercase=False),
        "bert_uncased": normalizers.BertNormalizer(clean_text=True, handle_chinese_chars=True, strip_accents=None, lowercase=True),
        "nfc": normalizers.NFC(),
        "nfd": normalizers.NFD(),
        "nfkc": normalizers.NFKC(),
        "nfkd": normalizers.NFKD(),
        "lowercase": normalizers.Lowercase(),
        "strip_accents": normalizers.StripAccents(),
        "charsmap_xlmr": precompiled(models, "FacebookAI_xlm-roberta-base"),
        "charsmap_t5": precompiled(models, "google-t5_t5-small"),
    }
    result = {"oracle": "tokenizers " + tokenizers.__version__, "changed": {}}
    for name, n in subjects.items():
        changed = {}
        for cp in range(0x110000):
            if 0xD800 <= cp <= 0xDFFF:
                continue
            s = chr(cp)
            r = n.normalize_str(s)
            if r != s:
                changed[cp] = r
        result["changed"][name] = changed
        print(name, len(changed), file=sys.stderr)
    with gzip.GzipFile(out, "wb", mtime=0) as f:
        f.write(json.dumps(result, ensure_ascii=True).encode("ascii"))


if __name__ == "__main__":
    main()
