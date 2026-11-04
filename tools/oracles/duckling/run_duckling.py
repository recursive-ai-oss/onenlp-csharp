#!/usr/bin/env python3
"""Runs the released Duckling server over a recognizer gold file and stores its replies unchanged.

Usage: run_duckling.py --in gold.jsonl --out raw.jsonl [--url http://localhost:8000] [--locale en_US]

One POST /parse per sentence with the documented parameters text, locale, reftime (milliseconds since the epoch, UTC) and tz.
The server is the binary from the published rasa/duckling container image (see fetch.sh); nothing of it is read.
"""
import argparse
import datetime as dt
import json
import urllib.parse
import urllib.request


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="inp", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--url", default="http://localhost:8000")
    ap.add_argument("--locale", default="en_US")
    a = ap.parse_args()
    with open(a.inp, encoding="utf-8") as f, open(a.out, "w", encoding="utf-8", newline="\n") as out:
        for line in f:
            if not line.strip():
                continue
            doc = json.loads(line)
            ref = dt.datetime.fromisoformat(doc["reference"]).replace(tzinfo=dt.timezone.utc)
            body = urllib.parse.urlencode({
                "text": doc["text"],
                "locale": a.locale,
                "reftime": str(int(ref.timestamp() * 1000)),
                "tz": doc.get("timezone", "UTC"),
            }).encode("utf-8")
            with urllib.request.urlopen(urllib.request.Request(a.url + "/parse", data=body), timeout=60) as r:
                results = json.loads(r.read().decode("utf-8"))
            out.write(json.dumps({"id": doc["id"], "results": results}, ensure_ascii=False, separators=(",", ":")) + "\n")


if __name__ == "__main__":
    main()
