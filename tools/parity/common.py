"""Shared helpers for the parity corpus builder and the oracle driver (standard library only).

A corpus record is one JSON object per line with an "id", a "class" tag and exactly one input form:

  "text"      a string of Unicode scalar values (usable through the char API and the UTF-8 API)
  "u16"       a list of UTF-16 code units that may contain lone surrogates (char API only)
  "utf8_hex"  the hex of a byte string that may be invalid UTF-8 (UTF-8 API only)

docs/parity.md defines what the oracle sees for the last two forms.
"""

from __future__ import annotations

import hashlib
import itertools
import json
import struct
import sys
from dataclasses import dataclass

REPLACEMENT = "\ufffd"

# Records whose oracle text is longer than this many code points are compared by digest, not element by element.
DIGEST = {"min_chars": 100_000}


def sha256_file(path: str, chunk: int = 1 << 20) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            b = f.read(chunk)
            if not b:
                break
            h.update(b)
    return h.hexdigest()


def digest_ints(values) -> dict:
    """sha256 over the values as little-endian int32, with the count. The C# side computes the same digest."""
    h = hashlib.sha256()
    n = 0
    buf = bytearray()
    for v in values:
        buf += struct.pack("<i", int(v))
        n += 1
        if len(buf) >= 1 << 16:
            h.update(buf)
            buf.clear()
    h.update(buf)
    return {"n": n, "sha256": h.hexdigest()}


def digest_strings(values) -> dict:
    """sha256 over each string as little-endian int32 UTF-8 length followed by the UTF-8 bytes, with the count."""
    h = hashlib.sha256()
    n = 0
    for s in values:
        b = s.encode("utf-8")
        h.update(struct.pack("<i", len(b)))
        h.update(b)
        n += 1
    return {"n": n, "sha256": h.hexdigest()}


def digest_text(s: str) -> dict:
    b = s.encode("utf-8")
    return {"n": len(s), "sha256": hashlib.sha256(b).hexdigest()}


@dataclass
class Prepared:
    """A corpus record as the oracle sees it, with the maps from code-point boundaries to the caller's offsets."""

    id: int
    cls: str
    text: str                 # what the oracle is given
    u16: list[int]            # UTF-16 offset of each code-point boundary (len(text) + 1 entries)
    u8: list[int] | None      # byte offset in the caller's UTF-8 input of each boundary; None if the record has no UTF-8 form
    has_char_form: bool       # the char API can take this record
    has_utf8_form: bool       # the UTF-8 API can take this record
    digest: bool              # compare by digest
    extra_text: bool          # oracle text differs from the corpus form, so the golden stores it


def _u16_boundaries(text: str) -> list[int]:
    return [0] + list(itertools.accumulate(2 if ord(c) > 0xFFFF else 1 for c in text))


def _u8_boundaries(text: str) -> list[int]:
    def width(c: str) -> int:
        o = ord(c)
        return 1 if o < 0x80 else 2 if o < 0x800 else 3 if o < 0x10000 else 4

    return [0] + list(itertools.accumulate(width(c) for c in text))


def decode_utf8_replacing(data: bytes) -> tuple[str, list[int]]:
    """Decodes UTF-8 replacing each maximal invalid subpart with one U+FFFD (Unicode 17 section 3.9, "U+FFFD Substitution
    of Maximal Subparts"). Returns the text and, for each code-point boundary, its byte offset in `data`."""
    out: list[str] = []
    bounds: list[int] = [0]
    pos = 0
    n = len(data)
    while pos < n:
        # Find the longest valid prefix by decoding strictly and reading the failure position.
        try:
            chunk = data[pos:].decode("utf-8")
            end_valid = n
            bad = None
        except UnicodeDecodeError as e:
            end_valid = pos + e.start
            bad = (pos + e.start, pos + e.end)
            chunk = data[pos:end_valid].decode("utf-8")
        for c in chunk:
            o = ord(c)
            pos += 1 if o < 0x80 else 2 if o < 0x800 else 3 if o < 0x10000 else 4
            out.append(c)
            bounds.append(pos)
        if bad is not None:
            # CPython reports the maximal invalid subpart as [start, end) (documented in the codecs error handlers).
            out.append(REPLACEMENT)
            pos = bad[1]
            bounds.append(pos)
    return "".join(out), bounds


def units_to_text(units: list[int]) -> str:
    """UTF-16 code units to a string, each lone surrogate becoming U+FFFD."""
    out: list[str] = []
    i = 0
    n = len(units)
    while i < n:
        u = units[i]
        if 0xD800 <= u <= 0xDBFF and i + 1 < n and 0xDC00 <= units[i + 1] <= 0xDFFF:
            out.append(chr(0x10000 + ((u - 0xD800) << 10) + (units[i + 1] - 0xDC00)))
            i += 2
        elif 0xD800 <= u <= 0xDFFF:
            out.append(REPLACEMENT)
            i += 1
        else:
            out.append(chr(u))
            i += 1
    return "".join(out)


def prepare(rec: dict) -> Prepared:
    rid, cls = rec["id"], rec["class"]
    if "text" in rec:
        text = rec["text"]
        return Prepared(rid, cls, text, _u16_boundaries(text), _u8_boundaries(text), True, True, len(text) > DIGEST["min_chars"], False)
    if "u16" in rec:
        text = units_to_text(rec["u16"])
        # A lone surrogate and U+FFFD are both one UTF-16 unit, so the offsets are those of the original units.
        return Prepared(rid, cls, text, _u16_boundaries(text), None, True, False, len(text) > DIGEST["min_chars"], True)
    if "utf8_hex" in rec:
        data = bytes.fromhex(rec["utf8_hex"])
        text, u8 = decode_utf8_replacing(data)
        return Prepared(rid, cls, text, _u16_boundaries(text), u8, False, True, len(text) > DIGEST["min_chars"], True)
    raise ValueError(f"record {rid} has no input form")


def read_jsonl(path: str):
    with open(path, "r", encoding="utf-8", newline="\n") as f:
        for line in f:
            line = line.rstrip("\n")
            if line:
                yield json.loads(line)


def dumps(obj) -> str:
    return json.dumps(obj, ensure_ascii=False, separators=(",", ":"))


def die(msg: str) -> None:
    print(msg, file=sys.stderr)
    sys.exit(2)
