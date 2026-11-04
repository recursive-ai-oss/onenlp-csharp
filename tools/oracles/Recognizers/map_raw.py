#!/usr/bin/env python3
"""Maps the raw Microsoft.Recognizers.Text results (RecognizersOracle raw) into the gold schema.

Usage: map_raw.py <raw.jsonl> <predictions.jsonl>

The mapping is ours and is the same for every sentence (docs/recognizer-gold.md, "Comparator mappings"):
  * offsets: the package reports an inclusive end; ours is exclusive (end + 1). Offsets are UTF-16 code units.
  * a model's type is the gold type of the same name; "datetimeV2.<x>" becomes the gold type <x>; relative ordinals ("next") are dropped.
  * values are rewritten to the canonical forms of the gold schema; units are lower-cased and the tables below rename the few that differ.
  * when the package lists several candidate values (past and future Friday, am and pm), the first candidate that starts on or after
    the reference time is taken, else the last. Every other candidate is kept under "candidates".
  * granularity and the approximate flag come from the TIMEX string and the package's "Mod" field, when they carry them.
"""
import json
import re
import sys
from decimal import Decimal

CURRENCY = {
    "Dollar": "USD", "United States dollar": "USD", "Euro": "EUR", "Pound": "GBP", "British pound": "GBP", "Yen": "JPY",
    "Japanese yen": "JPY", "Indian rupee": "INR", "Rupee": "INR", "Swiss franc": "CHF", "Canadian dollar": "CAD",
    "Australian dollar": "AUD",
}
TEMPERATURE = {"c": "celsius", "f": "fahrenheit", "k": "kelvin", "degree": "degree", "celsius": "celsius", "fahrenheit": "fahrenheit",
               "kelvin": "kelvin"}
SEQUENCE = {"email", "url", "ip", "phone", "hashtag", "mention", "guid"}


def canon(s):
    """Shortest decimal representation: no exponent, no thousands separator, no trailing zeros."""
    try:
        d = Decimal(s.replace(",", ""))
    except (AttributeError, ArithmeticError):
        return ""  # a unit without a number ("dollars"): the span is kept, the value cannot match
    if d == 0:
        return "0"
    t = format(d.normalize(), "f")
    return t


def time_from_value(v, timex):
    """'09:00:00' with precision taken from the TIMEX string."""
    m = re.match(r"(\d\d):(\d\d):(\d\d)", v)
    t = f"T{m.group(1)}:{m.group(2)}"
    if timex.count(":") >= 2 or (m.group(3) != "00"):
        t += f":{m.group(3)}"
    return t


def dt_text(v, timex):
    d, t = v.split(" ")
    return d + time_from_value(t, timex)


def tod_grain(timex):
    m = re.search(r"T(\d\d)(?::(\d\d))?(?::(\d\d))?", timex)
    if not m:
        return ""
    return "second" if m.group(3) else "minute" if m.group(2) else "hour"


def range_grain(timex):
    if re.fullmatch(r"\d{4}", timex):
        return "year"
    if re.fullmatch(r"\d{4}-\d\d", timex):
        return "month"
    if re.fullmatch(r"\d{4}-W\d\d(-WE)?", timex):
        return "week"
    if re.fullmatch(r"\d{4}-Q\d", timex):
        return "quarter"
    if re.fullmatch(r"\d{4}-(SP|SU|FA|WI)", timex) or re.fullmatch(r"(SP|SU|FA|WI)", timex):
        return "season"
    if re.fullmatch(r"\d{3}X", timex):
        return "decade"
    if re.fullmatch(r"\d{4}-H\d", timex):
        return "half"
    m = re.search(r"P\d+([DWMY])\)?$", timex)
    if m:
        return {"D": "day", "W": "week", "M": "month", "Y": "year"}[m.group(1)]
    return "day"


def set_grain(timex):
    if re.search(r"T\d\d|PT\d+H", timex):
        return "hour"
    m = re.match(r"P\d+([DWMY])", timex)
    if m:
        return {"D": "day", "W": "week", "M": "month", "Y": "year"}[m.group(1)]
    if timex.startswith("XXXX-WXX"):
        return "day"
    return ""


def pick(values, ref_date):
    """First candidate whose start is on or after the reference date; else the last."""
    def start(v):
        s = v.get("value") or v.get("start") or v.get("end") or ""
        return s[:10] if re.match(r"\d{4}-\d\d-\d\d", s) else ""
    for v in values:
        s = start(v)
        if not s or s >= ref_date:
            return v
    return values[-1]


def map_datetime(r, ref_date):
    kind = r["typeName"].split(".", 1)[1]
    if kind not in ("date", "time", "datetime", "daterange", "timerange", "datetimerange", "duration", "set"):
        return None
    values = r["resolution"].get("values", [])
    if not values:
        return None
    v = pick(values, ref_date)
    timex = v.get("timex", "")
    out = {"type": kind}
    approx = v.get("Mod") in ("start", "mid", "end", "approx")
    if kind == "date":
        if "value" not in v:
            return None
        out["value"], out["grain"] = v["value"][:10], "day"
    elif kind == "time":
        out["value"], out["grain"] = time_from_value(v["value"], timex), tod_grain(timex)
    elif kind == "datetime":
        out["value"], out["grain"] = dt_text(v["value"], timex), tod_grain(timex)
    elif kind == "daterange":
        out["value"] = f"{v.get('start', '')}/{v.get('end', '')}"
        out["grain"] = range_grain(timex)
        approx = approx or out["grain"] == "season"
    elif kind in ("timerange", "datetimerange"):
        # A one-sided range ("before 5pm") has only one of start and end; the missing side stays empty and cannot match.
        parts = timex.strip("()").split(",")
        fmt = time_from_value if kind == "timerange" else dt_text
        out["value"] = (fmt(v["start"], parts[0]) if "start" in v else "") + "/" + (fmt(v["end"], parts[1] if len(parts) > 1 else parts[0]) if "end" in v else "")
        out["grain"] = tod_grain(timex)
    elif kind == "duration":
        out["value"] = timex if re.match(r"^P(\d+Y)?(\d+M)?(\d+W)?$", timex) and ("Y" in timex or "M" in timex) else v["value"]
    elif kind == "set":
        t = timex
        if re.search(r"T\d\d$", t):
            t += ":00"
        out["value"], out["grain"] = t, set_grain(timex)
    if approx:
        out["approx"] = True
    if len(values) > 1:
        out["candidates"] = len(values)
    return out


def map_result(r, ref_date):
    model, res = r["model"], r["resolution"]
    out = {"start": r["start"], "end": r["end"] + 1}
    if model == "number":
        out.update(type="number", value=canon(res["value"]))
    elif model == "ordinal":
        if r["typeName"] != "ordinal":
            return None
        out.update(type="ordinal", value=canon(res["value"]))
    elif model == "percentage":
        out.update(type="percentage", value=canon(res["value"].rstrip("%")))
    elif model == "currency":
        iso = res.get("isoCurrency") or CURRENCY.get(res.get("unit", ""), res.get("unit", "").upper())
        out.update(type="currency", value=f"{canon(res['value'])} {iso}")
    elif model == "dimension":
        out.update(type="dimension", value=f"{canon(res['value'])} {res['unit'].lower()}")
    elif model == "temperature":
        u = res.get("unit", "Degree")
        out.update(type="temperature", value=f"{canon(res['value'])} {TEMPERATURE.get(u.lower(), u.lower())}")
    elif model == "age":
        out.update(type="age", value=f"{canon(res['value'])} {res['unit'].lower()}")
    elif model in SEQUENCE:
        out.update(type=model)
    elif model == "datetime":
        m = map_datetime(r, ref_date)
        if m is None:
            return None
        out.update(m)
    else:
        return None
    return out


def main():
    src, dst = sys.argv[1], sys.argv[2]
    refs = {}
    # The reference dates are read from the gold file that sits next to the raw file's name (gold|test).
    split = "test" if ".test." in src else "gold"
    gold_path = sys.argv[3] if len(sys.argv) > 3 else f"testdata/recognizers/en/{split}.jsonl"
    for line in open(gold_path, encoding="utf-8"):
        d = json.loads(line)
        refs[d["id"]] = d["reference"][:10]
    with open(src, encoding="utf-8") as f, open(dst, "w", encoding="utf-8", newline="\n") as out:
        for line in f:
            doc = json.loads(line)
            items = []
            for r in doc["results"]:
                m = map_result(r, refs[doc["id"]])
                if m:
                    items.append(m)
            items.sort(key=lambda i: (i["start"], i["end"], i["type"]))
            out.write(json.dumps({"id": doc["id"], "items": items}, ensure_ascii=False, separators=(",", ":")) + "\n")


if __name__ == "__main__":
    main()
