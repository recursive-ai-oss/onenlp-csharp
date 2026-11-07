"""Fetch the open-type NER data sets and write them as JSONL, one sentence per line:
{"tokens": [...], "spans": [[start, end_exclusive, "type name"], ...]}

  fetch.py --out DIR

Sets (terms recorded in docs/open-ner.md):
  few-nerd   huggingface.co/datasets/DFKI-SLT/few-nerd (supervised config), CC BY-SA 4.0: train, dev, test
  crossner   github.com/zliucr/CrossNER ner_data, MIT: five domains, test only (zero-shot evaluation)

The type names are turned into plain words ("organization-sportsteam" -> "sports team") so that an open-type model sees
natural-language labels. Files are written with their SHA-256 in MANIFEST.tsv.
"""
import argparse
import hashlib
import io
import json
import os
import re
import urllib.request

FEWNERD_REV = "main"
FEWNERD = "https://huggingface.co/datasets/DFKI-SLT/few-nerd/resolve/%s/supervised/%s-00000-of-00001.parquet"
FEWNERD_API = "https://huggingface.co/api/datasets/DFKI-SLT/few-nerd/parquet/supervised/%s/0.parquet"
CROSSNER = "https://raw.githubusercontent.com/zliucr/CrossNER/main/ner_data/%s/%s.txt"
CROSS_DOMAINS = ["politics", "science", "music", "literature", "ai"]

# Few-NERD fine types, in the order of the dataset card's label list, mapped to words.
_SPLIT_CAMEL = re.compile(r"(?<=[a-z])(?=[A-Z])")
_WORDS = {
    "broadcastprogram": "broadcast program", "writtenart": "written art", "sportsfacility": "sports facility",
    "attack/battle/war/militaryconflict": "battle or war", "sportsevent": "sports event", "GPE": "geopolitical entity",
    "bodiesofwater": "body of water", "road/railway/highway/transit": "road or railway",
    "government/governmentagency": "government agency", "media/newspaper": "media company",
    "politicalparty": "political party", "showorganization": "show organization", "sportsleague": "sports league",
    "sportsteam": "sports team", "astronomything": "astronomical object", "biologything": "biological thing",
    "chemicalthing": "chemical thing", "educationaldegree": "educational degree", "livingthing": "living thing",
    "artist/author": "artist or author",
}


def words(fine: str) -> str:
    coarse, _, sub = fine.partition("-")
    if sub == "other":
        return {"art": "work of art", "building": "building", "event": "event", "location": "location",
                "organization": "organization", "other": "other entity", "person": "person",
                "product": "product"}[coarse]
    return _WORDS.get(sub, _SPLIT_CAMEL.sub(" ", sub).lower())


def get(url: str) -> bytes:
    with urllib.request.urlopen(url, timeout=120) as r:
        return r.read()


def spans_from_io(tags):
    """Spans from a tag sequence of IO (Few-NERD) or BIO labels; consecutive equal types merge unless a B starts."""
    out, cur = [], None
    for i, t in enumerate(list(tags) + ["O"]):
        kind = None if t == "O" else t.split("-", 1)[-1] if t[:2] in ("B-", "I-") else t
        begin = t.startswith("B-")
        if cur and (kind != cur[2] or begin):
            out.append(cur)
            cur = None
        if kind and cur is None:
            cur = [i, i + 1, kind]
        elif kind:
            cur[1] = i + 1
    return out


def write(path, rows):
    with open(path, "w", encoding="utf-8") as f:
        for r in rows:
            f.write(json.dumps(r, ensure_ascii=False) + "\n")
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def fewnerd(out, manifest):
    import pyarrow.parquet as pq
    names = None
    for split, fn in (("train", "train"), ("validation", "dev"), ("test", "test")):
        url = FEWNERD_API % split
        data = get(url)
        t = pq.read_table(io.BytesIO(data))
        if names is None:
            # label names come from the dataset card; the parquet stores class ids
            names = [n for n in json.loads(t.schema.metadata[b"huggingface"])["info"]["features"]["fine_ner_tags"]["feature"]["names"]]
        rows = []
        for tokens, fine in zip(t.column("tokens").to_pylist(), t.column("fine_ner_tags").to_pylist()):
            tags = [names[i] for i in fine]
            # A span is both its fine type and its coarse type ("actor" and "person"), so the two levels never contradict each other.
            spans = []
            for s, e, k in spans_from_io(tags):
                spans.append([s, e, words(k)])
                coarse = words(k.partition("-")[0] + "-other")
                if coarse != spans[-1][2]:
                    spans.append([s, e, coarse])
            rows.append({"tokens": tokens, "spans": spans})
        p = os.path.join(out, "few-nerd-%s.jsonl" % fn)
        manifest.append((os.path.basename(p), len(rows), write(p, rows), "CC BY-SA 4.0", fn if fn == "train" else "dev" if fn == "dev" else "eval"))


_CROSS_WORDS = {"misc": "miscellaneous", "politicalparty": "political party", "organisation": "organisation",
                "otherperson": "person", "musicalartist": "musical artist", "musicalinstrument": "musical instrument",
                "musicgenre": "music genre", "literarygenre": "literary genre", "astronomicalobject": "astronomical object",
                "chemicalelement": "chemical element", "chemicalcompound": "chemical compound",
                "academicjournal": "academic journal", "enzyme": "enzyme", "protein": "protein", "theory": "theory",
                "discipline": "discipline", "scientist": "scientist", "award": "award", "event": "event",
                "location": "location", "person": "person", "organisation": "organisation", "country": "country",
                "election": "election", "politician": "politician", "band": "band", "album": "album", "song": "song",
                "writer": "writer", "poem": "poem", "book": "book", "magazine": "magazine", "researcher": "researcher",
                "field": "field", "task": "task", "product": "product", "algorithm": "algorithm", "metric": "metric",
                "programlang": "programming language", "conference": "conference", "university": "university",
                "organization": "organisation", "else": "other", "misc": "miscellaneous"}


def crossner(out, manifest):
    for dom in CROSS_DOMAINS:
        text = get(CROSSNER % (dom, "test")).decode("utf-8")
        rows, toks, tags = [], [], []
        for line in text.split("\n") + [""]:
            if not line.strip():
                if toks:
                    rows.append({"tokens": toks, "spans": [[s, e, _CROSS_WORDS.get(k.lower(), k.lower())] for s, e, k in spans_from_io(tags)]})
                toks, tags = [], []
                continue
            w, _, tag = line.rpartition("\t") if "\t" in line else line.rpartition(" ")
            toks.append(w)
            tags.append(tag.strip())
        p = os.path.join(out, "crossner-%s-test.jsonl" % dom)
        manifest.append((os.path.basename(p), len(rows), write(p, rows), "MIT (repository LICENSE); Wikipedia text", "eval"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    manifest = []
    crossner(a.out, manifest)
    fewnerd(a.out, manifest)
    with open(os.path.join(a.out, "MANIFEST.tsv"), "w") as f:
        f.write("file\tsentences\tsha256\tlicence\trole\n")
        for m in manifest:
            f.write("\t".join(map(str, m)) + "\n")
    print(open(os.path.join(a.out, "MANIFEST.tsv")).read())
