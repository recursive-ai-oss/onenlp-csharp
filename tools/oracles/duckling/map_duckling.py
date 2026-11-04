#!/usr/bin/env python3
"""Maps raw Duckling replies (run_duckling.py) into the recognizer gold schema.

Usage: map_duckling.py <raw.jsonl> <predictions.jsonl>

The mapping is ours and the same for every sentence (docs/recognizer-gold.md, "Comparator mappings"):
  * dimensions: number, ordinal, amount-of-money (currency), distance/volume/quantity (dimension), temperature, duration, email, url,
    phone-number (phone), time (all the date and time types). Duckling has no percentage, age, IP, hashtag, mention, GUID or recurrence
    dimension, so those types are never predicted. Interval results of the non-time dimensions ("over 5 dollars") are dropped.
  * offsets: Duckling's start and end are code-point offsets with an exclusive end; for the text here they equal UTF-16 offsets.
  * a time result becomes a date (day grain), a daterange (longer grain: start and start plus one period), a time or datetime (finer
    grain; "datetime" when the matched text names a day, month or relative day, else "time"), or an interval of the matching range type.
  * Duckling's interval ends are exclusive at the end point's grain. Gold time ranges state a clock end point ("3pm to 5pm" ends at 17:00),
    so for ranges with clock end points (the text has an am/pm or hh:mm time) one grain is subtracted from the end. Day-part intervals
    ("tomorrow morning") and date ranges are mapped as reported.
  * offsets of the time zone are kept only when the matched text names a zone abbreviation; "+00:00" is written "Z".
"""
import datetime as dt
import json
import re
import sys
from decimal import Decimal

UNIT = {"kilometre": "kilometer", "metre": "meter", "centimetre": "centimeter", "millimetre": "millimeter", "litre": "liter",
        "millilitre": "milliliter"}
MONEY = {"$": "USD", "£": "GBP", "€": "EUR", "¥": "JPY", "₹": "INR"}
ZONE = re.compile(r"\b(PST|PDT|EST|EDT|CST|CDT|MST|MDT|UTC|GMT|CET|CEST|JST)\b")
DATE_WORDS = re.compile(
    r"\b(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\b|\b(mon|tue|wed|thu|fri|sat|sun)[a-z]*\b|today|tomorrow|yesterday|"
    r"tonight|\bago\b|\bnext\b|\blast\b|\bthis\b|\d{4}-\d\d-\d\d|\d+/\d+|\bin \d|\bin (an?|one|two|three|four|five|six|seven|eight|nine|ten)\b",
    re.I)
CLOCK = re.compile(r"\d\s*(am|pm|a\.m\.|p\.m\.)|\d:\d\d", re.I)
GRAIN_ORDER = ["second", "minute", "hour", "day", "week", "month", "quarter", "year"]


def canon(s):
    try:
        d = Decimal(str(s).replace(",", ""))
    except ArithmeticError:
        return ""
    return "0" if d == 0 else format(d.normalize(), "f")


def parse_ts(s):
    return dt.datetime.fromisoformat(s)


def add_period(t, grain, n=1):
    if grain in ("second", "minute", "hour", "day", "week"):
        return t + dt.timedelta(**{grain + "s": n})
    months = {"month": 1, "quarter": 3, "year": 12}[grain] * n
    y, m = divmod(t.year * 12 + t.month - 1 + months, 12)
    return t.replace(year=y, month=m + 1)


def zone_suffix(text, ts):
    if not ZONE.search(text):
        return ""
    off = ts[-6:]
    return "Z" if off == "+00:00" else off


def fmt(t, grain, tod_only, suffix=""):
    if grain in ("second", "minute", "hour"):
        s = t.strftime("%H:%M:%S" if grain == "second" else "%H:%M")
        return "T" + s + suffix if tod_only else t.strftime("%Y-%m-%dT") + s + suffix
    return t.strftime("%Y-%m-%d")


def map_time(r, text):
    v = r["value"]
    if v["type"] == "value":
        t0 = parse_ts(v["value"])
        g = v["grain"]
        suffix = zone_suffix(text, v["value"])
        if g == "day":
            return {"type": "date", "value": fmt(t0, g, False), "grain": g}
        if g in ("week", "month", "quarter", "year"):
            return {"type": "daterange", "value": f"{fmt(t0, g, False)}/{fmt(add_period(t0, g), g, False)}", "grain": g}
        tod = not DATE_WORDS.search(text)
        return {"type": "time" if tod else "datetime", "value": fmt(t0, g, tod, suffix), "grain": g}
    if v["type"] != "interval":
        return None
    if "from" not in v or "to" not in v:
        return None  # one-sided ("before 5pm"): no range to compare
    f, t = v["from"], v["to"]
    gf, gt = f["grain"], t["grain"]
    a, b = parse_ts(f["value"]), parse_ts(t["value"])
    if gf in ("second", "minute", "hour") or gt in ("second", "minute", "hour"):
        g = gt if gt in ("second", "minute", "hour") else gf
        if CLOCK.search(text):
            b = add_period(b, g, -1)
        tod = not DATE_WORDS.search(text)
        suffix = zone_suffix(text, f["value"])
        return {"type": "timerange" if tod else "datetimerange",
                "value": f"{fmt(a, g, tod, suffix)}/{fmt(b, g, tod, suffix)}", "grain": g}
    g = gt
    return {"type": "daterange", "value": f"{fmt(a, gf, False)}/{fmt(b, gt, False)}", "grain": g}


def map_duration(r):
    v = r["value"]
    if v["type"] != "value":
        return None
    unit, n = v["unit"], int(v["value"])
    if unit in ("month", "year"):
        months = n * 12 if unit == "year" else n
        y, m = divmod(months, 12)
        return {"type": "duration", "value": "P" + (f"{y}Y" if y else "") + (f"{m}M" if m else "")}
    return {"type": "duration", "value": str(v["normalized"]["value"])}


def map_one(r):
    dim, v, body = r["dim"], r["value"], r["body"]
    out = {"start": r["start"], "end": r["end"]}
    if dim == "time":
        m = map_time(r, body)
    elif dim == "duration":
        m = map_duration(r)
    elif v.get("type") != "value":
        return None
    elif dim == "number":
        m = {"type": "number", "value": canon(v["value"])}
    elif dim == "ordinal":
        m = {"type": "ordinal", "value": canon(v["value"])}
    elif dim == "amount-of-money":
        m = {"type": "currency", "value": f"{canon(v['value'])} {MONEY.get(v['unit'], v['unit'])}"}
    elif dim in ("distance", "volume", "quantity"):
        m = {"type": "dimension", "value": f"{canon(v['value'])} {UNIT.get(v['unit'], v['unit'])}"}
    elif dim == "temperature":
        m = {"type": "temperature", "value": f"{canon(v['value'])} {v['unit']}"}
    elif dim == "email":
        m = {"type": "email"}
    elif dim == "url":
        m = {"type": "url"}
    elif dim == "phone-number":
        m = {"type": "phone"}
    else:
        return None
    if m is None:
        return None
    out.update(m)
    return out


def main():
    src, dst = sys.argv[1], sys.argv[2]
    with open(src, encoding="utf-8") as f, open(dst, "w", encoding="utf-8", newline="\n") as out:
        for line in f:
            doc = json.loads(line, parse_float=str)
            items = [m for m in (map_one(r) for r in doc["results"]) if m]
            items.sort(key=lambda i: (i["start"], i["end"], i["type"]))
            out.write(json.dumps({"id": doc["id"], "items": items}, ensure_ascii=False, separators=(",", ":")) + "\n")


if __name__ == "__main__":
    main()
