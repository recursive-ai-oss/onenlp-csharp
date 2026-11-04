#!/usr/bin/env python3
"""Maps the output of `OneNlp.Eval recognize` into the recognizer gold schema.

Usage: map_ours.py <recognize output.jsonl> <predictions.jsonl>

Types: CARDINAL, ORDINAL and PERCENT map to number, ordinal and percentage; MONEY to currency ("<value> <ISO code>"); QUANTITY to
temperature or age when its dimension says so and to dimension otherwise ("<value> <unit>"); the Sequence kinds keep their names
(PHONE_LIKE is phone, IP_ADDRESS is ip, GUID is guid). Values are the shortest decimal string. Offsets are the engine's UTF-16 offsets.
Temporal results (DATE, TIME, DATETIME, DURATION, SET) keep their kind in lower case (date, daterange, ...), value, grain and approx.
"""
import json
import sys
from decimal import Decimal

SEQUENCE = {"EMAIL": "email", "URL": "url", "IP_ADDRESS": "ip", "PHONE_LIKE": "phone", "HASHTAG": "hashtag", "MENTION": "mention", "GUID": "guid"}


def canon(s):
    d = Decimal(s)
    return "0" if d == 0 else format(d.normalize(), "f")


def map_result(r):
    label = r["label"]
    item = {"start": r["start"], "end": r["end"]}
    if label in SEQUENCE:
        item["type"] = SEQUENCE[label]
    elif label == "CARDINAL":
        item.update(type="number", value=canon(r["value"]))
    elif label == "ORDINAL":
        item.update(type="ordinal", value=canon(r["value"]))
    elif label == "PERCENT":
        item.update(type="percentage", value=canon(r["value"]))
    elif label == "MONEY":
        item.update(type="currency", value=f"{canon(r['value'])} {r.get('currency', '')}")
    elif label == "QUANTITY":
        kind = {"temperature": "temperature", "age": "age"}.get(r.get("dimension"), "dimension")
        item.update(type=kind, value=f"{canon(r['value'])} {r.get('unit', '')}")
    elif label in ("DATE", "TIME", "DATETIME", "DURATION", "SET"):
        item.update(type=r["kind"].lower(), value=r["value"])
        if "grain" in r:
            item["grain"] = r["grain"]
        if r.get("approx"):
            item["approx"] = True
    else:
        return None
    return item


def main():
    with open(sys.argv[1], encoding="utf-8") as f, open(sys.argv[2], "w", encoding="utf-8", newline="\n") as out:
        for line in f:
            doc = json.loads(line)
            items = [m for m in (map_result(r) for r in doc["results"]) if m]
            out.write(json.dumps({"id": doc["id"], "items": items}, ensure_ascii=False, separators=(",", ":")) + "\n")


if __name__ == "__main__":
    main()
