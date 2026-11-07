"""Shared text keys for evaluation-set de-duplication.

A training document is dropped when any of its segments has the same normalised key as a sentence of an
evaluation set. The key keeps letters and digits only (case-folded, NFKC), so spacing, punctuation and case
differences do not hide a copy.
"""
import hashlib
import re
import unicodedata

MIN_KEY_CHARS = 20  # shorter segments are too generic to identify a copy
_SPLIT = re.compile(r"[\n\r]+|(?<=[.!?\u3002\uff01\uff1f\u0964\u06d4])\s+")


def normalise(s: str) -> str:
    s = unicodedata.normalize("NFKC", s).casefold()
    return "".join(c for c in s if c.isalnum())


def key(s: str):
    """64-bit key of the normalised text, or None when the text is too short to be a safe key."""
    n = normalise(s)
    if len(n) < MIN_KEY_CHARS:
        return None
    return int.from_bytes(hashlib.blake2b(n.encode("utf-8"), digest_size=8).digest(), "little")


def segments(doc: str):
    for part in _SPLIT.split(doc):
        if part:
            yield part
