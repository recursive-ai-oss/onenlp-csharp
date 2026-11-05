#!/usr/bin/env python3
"""Finds which Leipzig Corpora Collection downloads exist for each language (HEAD requests only).

Usage: probe.py languages.tsv out.tsv
Writes one line per hit: code, leipzig code, name, size in bytes, URL. The download server has no index, so the
names (<iso3>_<type>_<year>_<size>) are tried from a fixed grid. Requests are few and sequential per worker.
"""
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor

BASE = "https://downloads.wortschatz-leipzig.de/corpora/"
TYPES = ["news", "web", "wikipedia", "mixed", "newscrawl", "community", "web-public"]
YEARS = list(range(2025, 2008, -1))
SIZES = ["1M", "300K", "100K", "30K", "10K"]


def head(url):
    req = urllib.request.Request(url, method="HEAD")
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            return int(r.headers.get("Content-Length", "0"))
    except Exception:
        return None


def probe(item):
    code, name, lcodes = item
    hits = []
    for lc in lcodes:
        for t in TYPES:
            for y in YEARS:
                for s in SIZES:
                    url = f"{BASE}{lc}_{t}_{y}_{s}.tar.gz"
                    n = head(url)
                    if n:
                        hits.append((code, lc, name, n, url))
                        break  # largest size for this type and year is enough
    return hits


def main():
    items = []
    for line in open(sys.argv[1], encoding="utf-8"):
        if line.startswith("#") or not line.strip():
            continue
        f = line.rstrip("\n").split("\t")
        items.append((f[0], f[1], f[2].split(",")))
    with ThreadPoolExecutor(12) as ex, open(sys.argv[2], "w", encoding="utf-8") as out:
        for hits in ex.map(probe, items):
            for h in hits:
                out.write("\t".join(map(str, h)) + "\n")
            out.flush()


main()
