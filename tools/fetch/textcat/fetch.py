#!/usr/bin/env python3
"""Fetches and prepares the two text-classification datasets and writes JSON Lines in the library's data format.

usage:
  fetch.py banking77 <train-dir> <eval-dir>
  fetch.py arxiv     <train-dir> <eval-dir> [--from 2024-01-01] [--max-records 60000] [--min-label-docs 300]

banking77  BANKING77 (PolyAI, CC BY 4.0): the official train and test files; dev is every tenth training text by SHA-256 of the text.
arxiv      arXiv computer-science metadata (title, abstract, categories) from arXiv's OAI-PMH service, which arXiv dedicates to the public
           domain under CC0 1.0 (https://info.arxiv.org/help/license/index.html, "Metadata license"). Only the metadata feed is read; no
           paper text, PDF or source is fetched. The service is called at most once every 4 seconds, as its terms of use ask, and
           Retry-After answers are honoured. Records are split by SHA-256 of the arXiv id (1/20 test, 1/20 dev, the rest train) before
           anything is trained.

Writes <train-dir>/{train,dev}.jsonl and <eval-dir>/test.jsonl ({"id", "text", "cats"}), and a MANIFEST.json in each directory (role,
count, bytes, SHA-256 of each file, source URLs, fetch time). Training code is given only the train directory. Standard library only. The data is never committed.
"""
import argparse
import csv
import hashlib
import io
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

BANKING = "https://raw.githubusercontent.com/PolyAI-LDN/task-specific-datasets/master/banking_data/{}.csv"
OAI = "https://oaipmh.arxiv.org/oai"
NS = {"o": "http://www.openarchives.org/OAI/2.0/", "a": "http://arxiv.org/OAI/arXiv/"}
MIN_INTERVAL = 4.0


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "onenlp-data-fetch/1"}), timeout=300) as r:
        return r.read()


def write_split(out, name, rows):
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, name + ".jsonl")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")
    return {"role": {"train": "train", "dev": "dev", "test": "eval"}[name], "file": name + ".jsonl", "documents": len(rows),
            "bytes": os.path.getsize(path), "sha256": sha256_file(path)}


def save(manifest, train_dir, eval_dir):
    for d, keep in ((train_dir, ("train", "dev")), (eval_dir, ("test",))):
        part = dict(manifest, files=[f for f in manifest["files"] if f["file"].split(".")[0] in keep])
        with open(os.path.join(d, "MANIFEST.json"), "w") as f:
            json.dump(part, f, indent=2)


def banking77(out, eval_out):
    raw = {split: fetch(BANKING.format(split)) for split in ("train", "test")}
    licence = fetch("https://raw.githubusercontent.com/PolyAI-LDN/task-specific-datasets/master/LICENSE")
    rows = {}
    for split, data in raw.items():
        rows[split] = [(r["text"], r["category"]) for r in csv.DictReader(io.StringIO(data.decode("utf-8")))]
    labels = sorted({c for _, c in rows["train"]} | {c for _, c in rows["test"]})

    def line(i, text, cat):
        return {"id": i, "text": text, "cats": {c: (1 if c == cat else 0) for c in labels}}

    train, dev = [], []
    for i, (text, cat) in enumerate(rows["train"]):
        bucket = int(sha256_bytes(text.encode("utf-8"))[:8], 16) % 10
        (dev if bucket == 0 else train).append(line("train-%d" % i, text, cat))
    test = [line("test-%d" % i, text, cat) for i, (text, cat) in enumerate(rows["test"])]
    manifest = {
        "dataset": "BANKING77", "source": BANKING.format("{train,test}"), "licence": "CC BY 4.0 (LICENSE of the PolyAI-LDN/task-specific-datasets repository)",
        "licence_sha256": sha256_bytes(licence), "raw_sha256": {k: sha256_bytes(v) for k, v in raw.items()},
        "labels": len(labels), "fetched": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "files": [write_split(out, "train", train), write_split(out, "dev", dev), write_split(eval_out, "test", test)],
    }
    save(manifest, out, eval_out)
    print(json.dumps(manifest["files"]))


def list_records(first_url, budget):
    last = 0.0
    failures = 0
    url = first_url
    while url and budget():
        wait = MIN_INTERVAL - (time.time() - last)
        if wait > 0:
            time.sleep(wait)
        last = time.time()
        try:
            body = fetch(url)
            failures = 0
        except urllib.error.HTTPError as e:
            if e.code == 503:
                time.sleep(float(e.headers.get("Retry-After", "30")) + 1)
                continue
            raise
        except (TimeoutError, urllib.error.URLError, ConnectionError):
            # The service is slow to start a large listing; the same request is simply tried again (at most 6 times in a row).
            failures += 1
            if failures > 6:
                raise
            time.sleep(30)
            continue
        root = ET.fromstring(body)
        err = root.find("o:error", NS)
        if err is not None:
            if err.get("code") == "noRecordsMatch":
                return
            raise RuntimeError("OAI error: %s %s" % (err.get("code"), err.text))
        for rec in root.findall("o:ListRecords/o:record", NS):
            yield rec
        token = root.find("o:ListRecords/o:resumptionToken", NS)
        url = OAI + "?" + urllib.parse.urlencode({"verb": "ListRecords", "resumptionToken": token.text}) if token is not None and token.text else None


def norm(s):
    return " ".join(s.split())


def arxiv(out, eval_out, start, max_records, min_label_docs):
    url = OAI + "?" + urllib.parse.urlencode({"verb": "ListRecords", "metadataPrefix": "arXiv", "set": "cs", "from": start})
    records, seen = [], set()
    for rec in list_records(url, lambda: len(records) < max_records):
        meta = rec.find("o:metadata/a:arXiv", NS)
        if meta is None:
            continue
        ident = meta.findtext("a:id", namespaces=NS)
        cats = [c for c in (meta.findtext("a:categories", namespaces=NS) or "").split() if c.startswith("cs.")]
        title = norm(meta.findtext("a:title", namespaces=NS) or "")
        abstract = norm(meta.findtext("a:abstract", namespaces=NS) or "")
        if not ident or ident in seen or not cats or len(abstract) < 50:
            continue
        seen.add(ident)
        records.append((ident, title + ". " + abstract, cats))
        if len(records) % 5000 == 0:
            print("records", len(records), file=sys.stderr, flush=True)
    bucket = lambda i: int(sha256_bytes(i.encode("utf-8"))[:8], 16) % 20
    split = {"train": [], "dev": [], "test": []}
    for r in records:
        split["test" if bucket(r[0]) == 0 else "dev" if bucket(r[0]) == 1 else "train"].append(r)
    counts = {}
    for _, _, cats in split["train"]:
        for c in cats:
            counts[c] = counts.get(c, 0) + 1
    labels = sorted(c for c, n in counts.items() if n >= min_label_docs)

    def line(r):
        return {"id": r[0], "text": r[1], "cats": {c: (1 if c in r[2] else 0) for c in labels}}

    # A document with none of the kept labels stays: it is a negative for every label, as in the full task.
    manifest = {
        "dataset": "arXiv cs metadata (title + abstract -> cs.* categories)", "source": OAI + "?verb=ListRecords&metadataPrefix=arXiv&set=cs&from=" + start,
        "licence": "CC0 1.0 for all arXiv metadata (info.arxiv.org/help/license/index.html, 'Metadata license'); paper text not used",
        "labels": labels, "label_train_counts": {c: counts[c] for c in labels}, "min_label_docs": min_label_docs,
        "split": "sha256(arXiv id)[:8] mod 20: 0 test, 1 dev, else train",
        "fetched": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "files": [write_split(out if n != "test" else eval_out, n, [line(r) for r in split[n]]) for n in ("train", "dev", "test")],
    }
    save(manifest, out, eval_out)
    print(json.dumps(manifest["files"]), len(labels), "labels")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("dataset", choices=["banking77", "arxiv"])
    p.add_argument("out")
    p.add_argument("eval_out")
    p.add_argument("--from", dest="start", default="2024-01-01")
    p.add_argument("--max-records", type=int, default=60000)
    p.add_argument("--min-label-docs", type=int, default=300)
    a = p.parse_args()
    if a.dataset == "banking77":
        banking77(a.out, a.eval_out)
    else:
        arxiv(a.out, a.eval_out, a.start, a.max_records, a.min_label_docs)


main()
