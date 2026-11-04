#!/usr/bin/env python3
"""Writes golden encodings for the parity corpus from the reference tokenizer libraries, called as black boxes.

  oracle.py --families families.json --models DIR --corpus DIR --out DIR [--family ID ...]
  oracle.py --adhoc toy=path/to/tokenizer.json --corpus DIR --out DIR      (a tokenizer.json outside the model directory)

Hugging Face families use `tokenizers.Tokenizer.from_file(...)` with `encode_batch` and `decode_batch`; Tiktoken families use
`tiktoken.get_encoding(...)`. Run it with the interpreter of an environment that has the library installed. One output file per
family: <out>/<family>.golden.jsonl.gz, first line a header (see docs/parity.md), then one line per corpus record.
"""

from __future__ import annotations

import argparse
import bisect
import gzip
import importlib.metadata
import json
import os
import platform
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import (DIGEST, Prepared, digest_ints, digest_strings, digest_text, dumps, prepare, read_jsonl, sha256_file)  # noqa: E402

GOLDEN_FORMAT = 1
TRUNC_MAX = 16
PAD_LENGTH = 24
PAD_ID = 0
PAD_TOKEN = "<pad>"
CHUNK_RECORDS = 256
CHUNK_CHARS = 3_000_000


def trunc_config(index: int) -> dict:
    """Alternates the sides so both are exercised across families: truncate right and pad left, or the reverse."""
    right = index % 2 == 0
    return {"max_length": TRUNC_MAX, "truncate_side": "right" if right else "left", "pad_length": PAD_LENGTH, "pad_side": "left" if right else "right",
            "pad_id": PAD_ID, "pad_token": PAD_TOKEN, "pad_type_id": 0, "strategy": "longest_first"}


def offsets_to_units(flat_cp, p: Prepared):
    """Code-point offsets from the oracle to UTF-16 offsets (and to UTF-8 byte offsets of the caller's input when it has one)."""
    n = len(p.u16) - 1
    o16, o8 = [], ([] if p.u8 is not None else None)
    for v in flat_cp:
        v = min(max(v, 0), n)
        o16.append(p.u16[v])
        if o8 is not None:
            o8.append(p.u8[v])
    return o16, o8


def pack(p: Prepared, ids, tokens, offsets_cp_pairs, mask, types, words, attn=None):
    flat = [x for pair in offsets_cp_pairs for x in pair]
    o16, o8 = offsets_to_units(flat, p)
    d = {"ids": ids, "o16": o16, "sm": mask, "ty": types}
    if tokens is not None:
        d["tok"] = tokens
    if o8 is not None:
        d["o8"] = o8
    if words is not None:
        d["wi"] = words
    if attn is not None:
        d["am"] = attn
    if p.digest:
        for k, v in list(d.items()):
            d[k] = digest_strings(v) if k == "tok" else digest_ints(v)
    return d


def hf_encoding(p: Prepared, e, with_attn=False):
    return pack(p, list(e.ids), list(e.tokens), list(e.offsets), [int(x) for x in e.special_tokens_mask], list(e.type_ids),
                [-1 if w is None else w for w in e.word_ids], list(e.attention_mask) if with_attn else None)


def dec_field(p: Prepared, s: str):
    return digest_text(s) if p.digest else s


def chunks(preps):
    cur, size = [], 0
    for p in preps:
        if cur and (len(cur) >= CHUNK_RECORDS or size + len(p.text) > CHUNK_CHARS):
            yield cur
            cur, size = [], 0
        cur.append(p)
        size += len(p.text)
    if cur:
        yield cur


class HfOracle:
    kind = "hf"

    def __init__(self, path: str, index: int):
        import tokenizers
        self.tk = tokenizers
        self.tok = tokenizers.Tokenizer.from_file(path)
        self.cfg = trunc_config(index)
        self.version = tokenizers.__version__
        self.dist_version = importlib.metadata.version("tokenizers")
        self.vocab_size = self.tok.get_vocab_size(with_added_tokens=True)

    def plain(self):
        self.tok.no_truncation()
        self.tok.no_padding()

    def trunc(self):
        c = self.cfg
        self.tok.enable_truncation(max_length=c["max_length"], direction=c["truncate_side"])
        self.tok.enable_padding(length=c["pad_length"], direction=c["pad_side"], pad_id=c["pad_id"], pad_token=c["pad_token"], pad_type_id=c["pad_type_id"])

    def records(self, batch):
        texts = [p.text for p in batch]
        self.plain()
        sp = self.tok.encode_batch(texts, add_special_tokens=True)
        pl = self.tok.encode_batch(texts, add_special_tokens=False)
        self.trunc()
        tr = self.tok.encode_batch(texts, add_special_tokens=True)
        self.plain()
        ids = [list(e.ids) for e in sp]
        dk = self.tok.decode_batch(ids, skip_special_tokens=False)
        ds = self.tok.decode_batch(ids, skip_special_tokens=True)
        for p, a, b, c, k, s in zip(batch, sp, pl, tr, dk, ds):
            yield p, {"special": hf_encoding(p, a), "plain": hf_encoding(p, b), "trunc": hf_encoding(p, c, True),
                      "dec": {"skip": dec_field(p, s), "keep": dec_field(p, k)}}

    def pair_records(self, pairs):
        out = []
        self.plain()
        sp = self.tok.encode_batch([(a, b) for _, a, b in pairs], add_special_tokens=True)
        self.trunc()
        tr = self.tok.encode_batch([(a, b) for _, a, b in pairs], add_special_tokens=True)
        self.plain()
        for (rid, a, b), x, y in zip(pairs, sp, tr):
            pa, pb = prepare({"id": rid, "class": "pair", "text": a}), prepare({"id": rid, "class": "pair", "text": b})
            # Offsets of the first sequence's tokens are into `a`, of the second's into `b` (sequence_ids says which).
            out.append((rid, {"special": hf_pair(x, pa, pb), "trunc": hf_pair(y, pa, pb, True)}))
        return out


def hf_pair(e, pa: Prepared, pb: Prepared, with_attn=False):
    seq = list(e.sequence_ids)
    o16, o8 = [], []
    for (s, t), q in zip(e.offsets, seq):
        p = pb if q == 1 else pa
        n = len(p.u16) - 1
        s, t = min(max(s, 0), n), min(max(t, 0), n)
        o16 += [p.u16[s], p.u16[t]]
        o8 += [p.u8[s], p.u8[t]]
    d = {"ids": list(e.ids), "tok": list(e.tokens), "o16": o16, "o8": o8, "sm": [int(x) for x in e.special_tokens_mask], "ty": list(e.type_ids),
         "wi": [-1 if w is None else w for w in e.word_ids]}
    if with_attn:
        d["am"] = list(e.attention_mask)
    return d


class TiktokenOracle:
    kind = "tiktoken"

    def __init__(self, name: str, models_dir: str):
        import tiktoken
        # The encoding files are fetched into this directory by tools/fetch; the library only verifies and reads them.
        os.environ.setdefault("TIKTOKEN_CACHE_DIR", os.path.join(models_dir, "tiktoken-cache"))
        self.enc = tiktoken.get_encoding(name)
        self.version = getattr(tiktoken, "__version__", None) or importlib.metadata.version("tiktoken")
        self.dist_version = importlib.metadata.version("tiktoken")
        self.special_ids = {self.enc.encode_single_token(s) for s in self.enc.special_tokens_set}
        self.vocab_size = self.enc.n_vocab
        self.cfg = None

    def records(self, batch):
        texts = [p.text for p in batch]
        # add_special_tokens=true: special-token text in the input is encoded as the special token (allowed_special="all").
        # add_special_tokens=false: it is read as ordinary text (encode_ordinary).
        sp = self.enc.encode_batch(texts, allowed_special="all")
        pl = self.enc.encode_ordinary_batch(texts)
        for p, a, b in zip(batch, sp, pl):
            ds = self.enc.decode(a)
            dk = self.enc.decode([i for i in a if i not in self.special_ids])
            yield p, {"special": self.tt_enc(p, a), "plain": self.tt_enc(p, b), "dec": {"skip": dec_field(p, dk), "keep": dec_field(p, ds)}}

    def tt_enc(self, p: Prepared, ids):
        # Tiktoken reports no offsets here, so they are derived from the exact bytes of each token: every token covers the
        # code points that its bytes touch (a token that ends inside a multi-byte character covers the whole character).
        toks = self.enc.decode_tokens_bytes(ids)
        cum_cp = [0]
        for c in p.text:
            o = ord(c)
            cum_cp.append(cum_cp[-1] + (1 if o < 0x80 else 2 if o < 0x800 else 3 if o < 0x10000 else 4))
        pos = 0
        pairs = []
        for t in toks:
            s, e = pos, pos + len(t)
            pos = e
            cs = bisect.bisect_right(cum_cp, s) - 1
            ce = bisect.bisect_left(cum_cp, e)
            pairs.append((cs, max(ce, cs)))
        mask = [1 if i in self.special_ids else 0 for i in ids]
        return pack(p, list(ids), None, pairs, mask, [0] * len(ids), None)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--families")
    ap.add_argument("--models")
    ap.add_argument("--corpus", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--family", action="append", help="only these family ids (repeatable)")
    ap.add_argument("--digest-min-chars", type=int, default=100_000, help="records whose text is longer are compared by digest")
    ap.add_argument("--adhoc", action="append", default=[], help="ID=PATH of a tokenizer.json to treat as a pairs-capable Hugging Face family")
    a = ap.parse_args()
    DIGEST["min_chars"] = a.digest_min_chars
    os.makedirs(a.out, exist_ok=True)

    fams = []
    if a.families:
        with open(a.families, encoding="utf-8") as f:
            for i, fam in enumerate(json.load(f)["families"]):
                if not a.family or fam["id"] in a.family:
                    fams.append((i, fam))
    for j, spec in enumerate(a.adhoc):
        fid, path = spec.split("=", 1)
        fams.append((j, {"id": fid, "oracle": "hf", "oracle_file": os.path.abspath(path), "pairs": True, "adhoc": True}))

    with open(os.path.join(a.corpus, "manifest.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    rpath, ppath = os.path.join(a.corpus, "records.jsonl"), os.path.join(a.corpus, "pairs.jsonl")

    for index, fam in fams:
        fid = fam["id"]
        if fam["oracle"] == "hf":
            path = fam["oracle_file"] if fam.get("adhoc") else os.path.join(a.models, fam["dir"], fam["oracle_file"])
            o = HfOracle(path, index)
            tok_sha = sha256_file(path)
            lib = "tokenizers"
        else:
            o = TiktokenOracle(fam["oracle_name"], a.models)
            tok_sha = fam.get("sha256")
            lib = "tiktoken"
        header = {"format": GOLDEN_FORMAT, "family": fid, "oracle": lib, "oracle_version": o.version, "oracle_dist_version": o.dist_version,
                  "python": platform.python_version(), "tokenizer_sha256": tok_sha, "vocab_size": o.vocab_size, "corpus_sha256": manifest["corpus_sha256"],
                  "digest_min_chars": DIGEST["min_chars"], "trunc_pad": o.cfg, "has_pairs": bool(fam.get("pairs")) and o.kind == "hf",
                  "has_word_ids": o.kind == "hf", "has_tokens": o.kind == "hf"}
        outp = os.path.join(a.out, f"{fid}.golden.jsonl.gz")
        n = 0
        with gzip.GzipFile(outp, "wb", compresslevel=6, mtime=0) as gz:   # mtime=0: the file bytes are reproducible
            gz.write((dumps({"header": header}) + "\n").encode("utf-8"))
            preps = (prepare(r) for r in read_jsonl(rpath))
            for batch in chunks(preps):
                for p, enc in o.records(batch):
                    rec = {"id": p.id, "cls": p.cls, **enc}
                    if p.extra_text:
                        rec["oracle_text"] = p.text
                    gz.write((dumps(rec) + "\n").encode("utf-8"))
                    n += 1
            m = 0
            if header["has_pairs"]:
                allp = [(r["id"], r["a"], r["b"]) for r in read_jsonl(ppath)]
                for i in range(0, len(allp), 64):
                    for rid, enc in o.pair_records(allp[i:i + 64]):
                        gz.write((dumps({"pair": rid, **enc}) + "\n").encode("utf-8"))
                        m += 1
        print(json.dumps({"family": fid, "oracle": lib, "version": o.version, "records": n, "pairs": m, "file_sha256": sha256_file(outp)}), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
