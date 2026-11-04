#!/usr/bin/env python3
"""Writes small seed inputs for the vocab-txt and sentencepiece-model fuzz targets.

    python make_fuzz_seeds.py OUT_DIR

Creates OUT_DIR/vocab-txt/* and OUT_DIR/sentencepiece-model/*: a BERT-shaped vocabulary file and small SentencePiece model
messages (unigram, with byte fallback, and BPE-typed) made by writing the protocol-buffer fields by hand.
"""
import os
import struct
import sys


def varint(v):
    out = bytearray()
    while v >= 0x80:
        out.append((v & 0x7F) | 0x80)
        v >>= 7
    out.append(v)
    return bytes(out)


def field_bytes(num, data):
    return varint((num << 3) | 2) + varint(len(data)) + data


def field_varint(num, v):
    return varint(num << 3) + varint(v)


def piece(text, score, kind):
    body = field_bytes(1, text.encode()) + varint((2 << 3) | 5) + struct.pack("<f", score) + field_varint(3, kind)
    return field_bytes(1, body)


def model(pieces, model_type=1, byte_fallback=False, charsmap=b""):
    out = b"".join(piece(*p) for p in pieces)
    out += field_bytes(2, field_varint(3, model_type) + (field_varint(35, 1) if byte_fallback else b"") + field_varint(40, 0))
    out += field_bytes(3, field_bytes(1, b"nmt_nfkc") + field_bytes(2, charsmap) + field_varint(3, 1) + field_varint(4, 1) + field_varint(5, 1))
    return out


def main():
    out = sys.argv[1]
    for sub in ("vocab-txt", "sentencepiece-model"):
        os.makedirs(os.path.join(out, sub), exist_ok=True)
    vocab = ["[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]", "hello", "world", "un", "##aff", "##able", "h", "##e", "##l", "##o", "w", "\u65e5", "\u672c"]
    with open(os.path.join(out, "vocab-txt", "bert-like"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(vocab) + "\n")
    base = [("<unk>", 0.0, 2), ("<s>", 0.0, 3), ("</s>", 0.0, 3), ("\u2581hello", -3.0, 1), ("\u2581world", -3.5, 1), ("\u2581", -2.0, 1),
            ("h", -5.0, 1), ("e", -5.0, 1), ("l", -5.0, 1), ("o", -5.0, 1), ("un", -4.0, 1), ("aff", -4.0, 1), ("able", -4.0, 1), ("<x>", 0.0, 4)]
    with open(os.path.join(out, "sentencepiece-model", "unigram"), "wb") as f:
        f.write(model(base, charsmap=bytes(range(32))))
    with open(os.path.join(out, "sentencepiece-model", "byte-fallback"), "wb") as f:
        f.write(model(base + [("<0x%02X>" % b, 0.0, 6) for b in range(256)], byte_fallback=True))
    with open(os.path.join(out, "sentencepiece-model", "bpe-typed"), "wb") as f:
        f.write(model(base, model_type=2))


if __name__ == "__main__":
    main()
