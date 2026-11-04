#!/usr/bin/env python3
"""Writes <data-root>/manifest-en.json: role, URL, licence, size and SHA-256 of every
fetched English data file, plus a text-level check that no evaluation sentence appears
on the training side. Standard library only.

usage: manifest.py <data-root> <ud-tag> <ud-base-url> <uner-short-commit> <uner-base-url>
"""
import hashlib
import json
import os
import re
import sys

root, ud_tag, ud_base, uner_short, uner_base = sys.argv[1:6]

DATASETS = [
    # (id, dir name, file suffix, licence, base URL, notice dir)
    ("ud-en-ewt", f"ud-en-ewt-{ud_tag}", "conllu", "CC BY-SA 4.0", ud_base),
    ("uner-en-ewt", f"uner-en-ewt-{uner_short}", "iob2", "CC BY-SA 4.0", uner_base),
]
ROLE_DIR = {"train": "train", "dev": "train", "test": "eval"}
ROLE = {"train": "train", "dev": "dev", "test": "eval"}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def sentences(path):
    """Returns the '# text =' line of every sentence (UD and UNER files both carry it)."""
    out = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("# text = "):
                out.append(line[len("# text = "):].rstrip("\n"))
    return out


def norm(t):
    return re.sub(r"\s+", " ", t).strip().casefold()


files = []
texts = {}  # (dataset id, split) -> list of sentence texts
for ds_id, dname, ext, licence, base in DATASETS:
    for split in ("train", "dev", "test"):
        fname = f"en_ewt-ud-{split}.{ext}"
        path = os.path.join(root, ROLE_DIR[split], dname, fname)
        sents = sentences(path)
        texts[(ds_id, split)] = sents
        files.append({
            "dataset": ds_id,
            "role": ROLE[split],
            "path": os.path.relpath(path, root),
            "url": f"{base}/{fname}",
            "licence": licence,
            "bytes": os.path.getsize(path),
            "sha256": sha256(path),
            "sentences": len(sents),
        })

notices = []
for ds_id, dname, *_ in DATASETS:
    for n in ("LICENSE.txt", "README.md"):
        p = os.path.join(root, "notices", dname, n)
        notices.append({"dataset": ds_id, "path": os.path.relpath(p, root), "sha256": sha256(p)})


def overlap(test_key, train_keys):
    train = set()
    train_norm = set()
    for k in train_keys:
        train.update(texts[k])
        train_norm.update(norm(t) for t in texts[k])
    test = texts[test_key]
    exact = sum(1 for t in test if t in train)
    folded = sum(1 for t in test if norm(t) in train_norm)
    return {"test": f"{test_key[0]}:{test_key[1]}", "against": [f"{a}:{b}" for a, b in train_keys],
            "test_sentences": len(test), "exact_text_in_train_or_dev": exact,
            "folded_text_in_train_or_dev": folded}


ud_tr = [("ud-en-ewt", "train"), ("ud-en-ewt", "dev")]
ne_tr = [("uner-en-ewt", "train"), ("uner-en-ewt", "dev")]
dedup = [
    overlap(("ud-en-ewt", "test"), ud_tr),
    overlap(("uner-en-ewt", "test"), ne_tr),
    overlap(("uner-en-ewt", "test"), ud_tr),
    overlap(("ud-en-ewt", "test"), ne_tr),
]
# Within-test duplicates are listed too: the official test split repeats short sentences ("Thanks!").
for d in dedup:
    d["note"] = "exact = identical '# text' line; folded = whitespace-collapsed and case-folded"

# How closely the UNER sentences follow the pinned UD release (UNER was cut from an older UD commit).
alignment = []
for split in ("train", "dev", "test"):
    ud, ne = texts[("ud-en-ewt", split)], texts[("uner-en-ewt", split)]
    alignment.append({"split": split, "ud": len(ud), "uner": len(ne),
                      "same_text_in_same_order": sum(1 for a, b in zip(ud, ne) if a == b),
                      "uner_texts_not_in_ud_split": len(set(ne) - set(ud))})

manifest = {"files": files, "notices": notices, "dedup": dedup, "uner_vs_ud_alignment": alignment}
out = os.path.join(root, "manifest-en.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2, ensure_ascii=False)
    f.write("\n")
print(json.dumps({"files": [(x["path"], x["sentences"], x["sha256"][:12]) for x in files], "dedup": dedup, "alignment": alignment}, indent=1))
