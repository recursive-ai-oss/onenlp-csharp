#!/usr/bin/env python3
"""Builds the English recognizer gold files from en.src.txt.

Usage: build.py [--out DIR]   (default: ../../testdata/recognizers/en)

An '@split dev|test|auto' line in the source assigns the sentences below it; 'auto' (default) uses the stratified hash rule.
Writes gold.jsonl (the development split) and test.jsonl (the held-out split), validates every label against the schema in
docs/recognizer-gold.md and prints the counts and SHA-256 of both files. The split is stratified by the type of each
sentence's first item; it is final once the test file is published, after which lines are only added knowingly.
"""
import argparse
import datetime as dt
import hashlib
import json
import os
import re
import sys

TYPES = {
    "number", "ordinal", "percentage", "dimension", "temperature", "age", "currency",
    "email", "url", "ip", "phone", "hashtag", "mention", "guid",
    "date", "time", "datetime", "daterange", "timerange", "datetimerange", "duration", "set",
}
SEQUENCE = {"email", "url", "ip", "phone", "hashtag", "mention", "guid"}
GRAINS = {"second", "minute", "hour", "day", "week", "month", "quarter", "season", "half", "year", "decade"}
DEC = r"-?\d+(\.\d+)?"
UNIT = r"[a-z]+( [a-z]+)*"
ZONE = r"(Z|[+-]\d\d:\d\d)?"
DATE = r"\d{4}-\d\d-\d\d"
TOD = r"T\d\d:\d\d(:\d\d)?"
DATETIME = DATE + TOD[0:] + ZONE
VALUE = {
    "number": rf"^{DEC}$",
    "ordinal": r"^\d+$",
    "percentage": rf"^{DEC}$",
    "dimension": rf"^{DEC} {UNIT}$",
    "temperature": rf"^{DEC} (celsius|fahrenheit|kelvin|degree)$",
    "age": rf"^{DEC} (year|month|week|day)$",
    "currency": rf"^{DEC} [A-Z]{{3}}$",
    "date": rf"^{DATE}$",
    "time": rf"^{TOD}{ZONE}$",
    "datetime": rf"^{DATE}{TOD}{ZONE}$",
    "daterange": rf"^{DATE}/{DATE}$",
    "timerange": rf"^{TOD}/{TOD}$",
    "datetimerange": rf"^{DATE}{TOD}/{DATE}{TOD}$",
    "duration": r"^(\d+|P(\d+Y)?(\d+M)?|P\d+BD|PT\d+BH)$",
    "set": r"^(P\d+[YMWD]|PT\d+H|XXXX-WXX-\d(T\d\d:\d\d)?|T\d\d:\d\d)$",
}
REF = re.compile(r"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d$")


def utf16_len(s):
    return len(s.encode("utf-16-le")) // 2


def check_value(t, v, loc):
    if t in SEQUENCE:
        assert not v, f"{loc}: sequences take no value"
        return
    assert v and re.match(VALUE[t], v), f"{loc}: bad value {v!r} for {t}"
    for part in re.findall(DATE, v):
        dt.date.fromisoformat(part)
    if t in ("daterange", "datetimerange", "timerange"):
        a, b = v.split("/")
        assert a < b, f"{loc}: range start not before end"
    if t == "set" and v.startswith("XXXX-WXX-"):
        assert 1 <= int(v[9]) <= 7, loc


def parse_line(line, ref, n):
    items, text, pos = [], [], 0
    cut = 0
    for m in re.finditer(r"\[\[(.*?)\]\]", line):
        text.append(line[cut:m.start()])
        fields = m.group(1).split("|")
        expr, t = fields[0], fields[1]
        fields += [""] * (6 - len(fields))
        value, grain, flags = fields[2], fields[3], fields[4]
        loc = f"line {n} [{expr}]"
        assert t in TYPES, f"{loc}: unknown type {t}"
        check_value(t, value, loc)
        if t in ("date", "time", "datetime", "daterange", "timerange", "datetimerange", "set"):
            assert grain in GRAINS, f"{loc}: grain {grain!r}"
        item = {"start": None, "end": None, "type": t, "text": expr}
        if value:
            item["value"] = value
        if grain:
            item["grain"] = grain
        for flag in filter(None, flags.split(",")):
            if flag == "approx":
                item["approx"] = True
            elif flag.startswith("alt="):
                item["alt"] = flag[4:].split(";")
                for a in item["alt"]:
                    check_value(t, a, loc)
            else:
                raise AssertionError(f"{loc}: unknown flag {flag}")
        start = sum(utf16_len(x) for x in text)
        item["start"], item["end"] = start, start + utf16_len(expr)
        items.append(item)
        text.append(expr)
        cut = m.end()
    text.append(line[cut:])
    full = "".join(text)
    assert "[[" not in full and "]]" not in full, f"line {n}: unbalanced markup"
    for it in items:
        u = full.encode("utf-16-le")
        assert u[it["start"] * 2:it["end"] * 2].decode("utf-16-le") == it["text"], f"line {n}: offset"
    for a, b in zip(items, items[1:]):
        assert a["end"] <= b["start"], f"line {n}: overlapping items"
    return full, items


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(here, "..", "..", "testdata", "recognizers", "en"))
    out = ap.parse_args().out
    ref = None
    forced = "auto"
    docs = {}
    assigned = {}
    for n, raw in enumerate(open(os.path.join(here, "en.src.txt"), encoding="utf-8"), 1):
        line = raw.rstrip("\n")
        if not line.strip() or line.startswith("#"):
            continue
        if line.startswith("@ref "):
            ref = line[5:].strip()
            assert REF.match(ref), f"line {n}: bad reference"
            continue
        if line.startswith("@split "):
            forced = line[7:].strip()
            assert forced in ("auto", "dev", "test"), f"line {n}: bad split"
            continue
        assert ref, "no reference time"
        text, items = parse_line(line, ref, n)
        did = hashlib.sha256((ref + "\n" + text).encode("utf-8")).hexdigest()[:12]
        assert did not in docs, f"line {n}: duplicate sentence"
        assigned[did] = forced
        docs[did] = {"id": did, "text": text, "reference": ref, "timezone": "UTC", "items": items}
    # Sentences under an explicit '@split dev' or '@split test' line go where it says. The others ('@split auto', the default) are
    # stratified by the type of the first item (or none), so every type reaches both splits: within a stratum, ordered by the
    # hash of the id, the first 40% (rounded up) are test.
    strata = {}
    for did, d in docs.items():
        if assigned[did] != "auto":
            continue
        strata.setdefault(d["items"][0]["type"] if d["items"] else "none", []).append(did)
    test_ids = {d for d, how in assigned.items() if how == "test"}
    for ids in strata.values():
        ids.sort(key=lambda i: hashlib.sha256(i.encode()).hexdigest())
        test_ids.update(ids[:-(-len(ids) * 2 // 5)])
    split = {"gold": [], "test": []}
    for did in sorted(docs):
        split["test" if did in test_ids else "gold"].append(docs[did])
    os.makedirs(out, exist_ok=True)
    for name, rows in split.items():
        path = os.path.join(out, name + ".jsonl")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            for r in rows:
                f.write(json.dumps(r, ensure_ascii=False, separators=(",", ":")) + "\n")
        digest = hashlib.sha256(open(path, "rb").read()).hexdigest()
        counts = {}
        for r in rows:
            for it in r["items"]:
                counts[it["type"]] = counts.get(it["type"], 0) + 1
        print(f"{name}.jsonl: {len(rows)} sentences, {sum(counts.values())} items, sha256 {digest}")
        print("  " + ", ".join(f"{k} {counts[k]}" for k in sorted(counts)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
