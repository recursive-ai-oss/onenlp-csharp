#!/usr/bin/env python3
"""Fetches the entity corpora of the languages beyond the first nine, hashes every file and writes manifest-<lang>.json.

usage: fetch_ud_ner.py <data-root> [--md-dir tools/fetch/ud-ner] [lang ...]      (default: every language below)

Uses the machinery of fetch_lang.py (layout, hashing, overlap report). Entity types are mapped to the UNER scheme
(PER, ORG, LOC); types outside it become O. Sets that are not trained on (same text as a UNER set) are downloaded and
hashed as raw files only, so the choice can change without a new fetch.

  hr  UNER Croatian-SET (train); hr500k NER layer, raw
  da  UNER Danish-DDT (train); DaNE, raw
  id  UNER Indonesian-GSD and -CSUI (train)
  no  UNER Norwegian-NDT, Bokmaal and Nynorsk (train)
  sr  UNER Serbian-SET (train)    sk  UNER Slovak-SNK (train)    sl  UNER Slovenian-SSJ (train)
  sv  UNER Swedish-Talbanken (train); SUCX 3.0 NER, raw
  pl  KPWr-NER n82 (CC BY 3.0)    ro  RONEC 2.0 (MIT)

UNER sets are CC BY-SA 4.0 and carry their treebank's text. Needs python3 (Romanian also pyarrow).
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fetch_lang as fl  # noqa: E402

TTD = fl.TTD
HF = "https://huggingface.co/datasets/{repo}/resolve/{rev}/{file}"
KPWR_REV = "a20247f8787670ef987fa61e1c9139227c186105"
RONEC_REV = "2a0856f6979067c4ef7ea6a462ff9daabd055826"


def uner(repo, commit, prefix, splits=TTD, note=""):
    return fl.uner("UNER_" + repo, commit, prefix, splits, note=note)


CONFIG = {
    "hr": [uner("Croatian-SET", "20dcb2ef6b6bf9d5e03605a74c4d87215a1574c1", "hr_set"),
           dict(kind="rawfile", id="hr500k-ner", licence="cc-by-sa-4.0 (dataset card)", note="not trained on: the UNER set labels the same treebank text",
                url=HF.format(repo="classla/hr500k", rev="708662e326e2e0ee4ce0fb7fa4e41db6c93771f0", file="data_ner.zip"))],
    "da": [uner("Danish-DDT", "983c4cdc09f8ece6069d2b692b60dc4b3649ec6e", "da_ddt"),
           dict(kind="rawfile", id="dane", licence="cc-by-sa-4.0 (dataset card)", note="not trained on: the same sentences as UNER Danish-DDT",
                url="http://danlp-downloads.alexandra.dk/datasets/ddt.zip")],
    "id": [uner("Indonesian-GSD", "a8a8f52b43c4bf0e20bb67537290a141e618e847", "id_gsd"),
           uner("Indonesian-CSUI", "93847a9fa384c28605ebdae5df6af32a6043badf", "id_csui", ("train", "test"), "no dev split in the release")],
    "no": [uner("Norwegian-NDT", "f1da13196788be028fe8124cdc7b9b197399ad98", "nob_norne", note="Bokmaal"),
           uner("Norwegian-NDT", "f1da13196788be028fe8124cdc7b9b197399ad98", "nno_norne", note="Nynorsk")],
    "sr": [uner("Serbian-SET", "0d9685f001c10895572d844ef789c53dade0e265", "sr_set")],
    "sk": [uner("Slovak-SNK", "886e7c6d7e066e23246af5070d115a036a465c6d", "sk_snk")],
    "sl": [uner("Slovenian_SSJ", "f6c6f40e297749ecb7ce7143e9783083e545aadf", "sl_ssj")],
    "sv": [uner("Swedish-Talbanken", "dbb692085267da83a35d1e1d2c111671b3c7c46b", "sv_talbanken"),
           dict(kind="rawfile", id="sucx3-ner", licence="cc-by-4.0 (dataset card)", note="not trained on: the UNER set labels UD text; SUCX is the larger alternative",
                url=HF.format(repo="KBLab/sucx3_ner", rev="96acc4fd6dad4b6f9a133cca1efa4dedb07a8b99", file="data/simple_tags/cased.tar.gz"))],
    "pl": [dict(kind="kpwr", id="kpwr-ner-n82", licence="CC BY 3.0 (dataset card)",
                note="official train and test; no dev split, so documents (blank-line blocks under one -DOCSTART FILE) with index mod 10 = 9 become dev. "
                     "nam_liv_person is PER, nam_org_* is ORG, nam_loc_* is LOC; other types are O")],
    "ro": [dict(kind="ronec", id="ronec-2.0", licence="MIT (dataset card)",
                note="official train, validation (dev) and test; PERSON is PER, ORG is ORG, GPE and LOC are LOC; other types are O")],
}


def split_paths(root, lang, d, split):
    return fl.ner_paths(root, lang, d, split)


def fetch_rawfile(root, lang, d, notices):
    raw = os.path.join(root, "raw", lang, os.path.basename(d["url"]) if d["id"] != "sucx3-ner" else "sucx3-ner-simple-cased.tar.gz")
    fl.get(d["url"], raw)
    d["raw"] = dict(path=raw, sha256=fl.sha256(raw))
    return []


def mapped(tag, mapping):
    """B-xxx / I-xxx / O to the UNER scheme: mapping(type) returns PER, ORG, LOC or None."""
    if tag == "O":
        return "O"
    kind, typ = tag.split("-", 1)
    m = mapping(typ)
    return "O" if m is None else f"{kind}-{m}"


def fetch_kpwr(root, lang, d, notices):
    def mapping(t):
        return "PER" if t == "nam_liv_person" else "ORG" if t.startswith("nam_org_") else "LOC" if t.startswith("nam_loc_") else None
    parts, out = {"train": [], "dev": [], "test": []}, []
    raws = {}
    for name, base in (("train", "kpwr-ner-n82-train-tune.iob"), ("test", "kpwr-ner-n82-test.iob")):
        url = HF.format(repo="clarin-pl/kpwr-ner", rev=KPWR_REV, file="data/" + base)
        raw = os.path.join(root, "raw", lang, base)
        fl.get(url, raw)
        raws[base] = fl.sha256(raw)
        doc = -1
        words, tags = [], []
        with open(raw, encoding="utf-8") as f:
            for line in list(f) + ["\n"]:
                line = line.rstrip("\n")
                if line.startswith("-DOCSTART FILE"):
                    doc += 1
                elif line.startswith("-DOCSTART"):
                    continue
                elif not line.strip():
                    if words:
                        target = "test" if name == "test" else ("dev" if doc % 10 == 9 else "train")
                        parts[target].append((words, tags))
                    words, tags = [], []
                else:
                    c = line.split("\t")
                    words.append(c[0])
                    tags.append(mapped(c[-1], mapping))
    for split in ("train", "dev", "test"):
        p = split_paths(root, lang, d, split)
        fl.write_iob2(p, parts[split])
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=p,
                        url=HF.format(repo="clarin-pl/kpwr-ner", rev=KPWR_REV, file="data/kpwr-ner-n82-*.iob") + " (converted)"))
    d["raw"] = raws
    nd = os.path.join(root, "notices", lang, d["id"])
    fl.get(HF.format(repo="clarin-pl/kpwr-ner", rev=KPWR_REV, file="README.md"), os.path.join(nd, "README.md"))
    notices.append(nd)
    return out


def fetch_ronec(root, lang, d, notices):
    import pyarrow.parquet as pq  # fetch-time tool only
    readme = os.path.join(root, "notices", lang, d["id"], "README.md")
    fl.get(HF.format(repo="community-datasets/ronec", rev=RONEC_REV, file="README.md"), readme)
    text = open(readme, encoding="utf-8").read()
    block = text[text.index("ner_tags"):]
    block = block[:block.index("splits:")]
    names = {int(i): n for i, n in re.findall(r"'(\d+)': ([A-Z]\S*)", block)}

    def mapping(t):
        return {"PERSON": "PER", "ORG": "ORG", "GPE": "LOC", "LOC": "LOC"}.get(t)
    out, raws = [], {}
    for split, f in (("train", "train"), ("dev", "validation"), ("test", "test")):
        url = HF.format(repo="community-datasets/ronec", rev=RONEC_REV, file=f"ronec/{f}-00000-of-00001.parquet")
        raw = os.path.join(root, "raw", lang, f"ronec-{f}.parquet")
        fl.get(url, raw)
        raws[f] = fl.sha256(raw)
        rows = pq.read_table(raw).to_pylist()
        p = split_paths(root, lang, d, split)
        fl.write_iob2(p, [(r["tokens"], [mapped(names[i], mapping) for i in r["ner_tags"]]) for r in rows])
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=p, url=url + " (converted)"))
    d["raw"] = raws
    notices.append(os.path.dirname(readme))
    return out


def fetch_uner(root, lang, d, notices):
    """As fetch_lang.fetch_uner, but a repository without a LICENSE.txt (Indonesian) is allowed: the licence is then the dataset card's."""
    out = []
    for split in d["splits"]:
        role_dir = "eval" if split == "test" else "train"
        dest = os.path.join(root, role_dir, "uner", lang, f"{d['prefix']}-ud-{split}.iob2")
        url = fl.UNER_RAW.format(repo=d["repo"], commit=d["commit"], file=f"{d['prefix']}-ud-{split}.iob2")
        fl.get(url, dest)
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=dest, url=url))
    nd = os.path.join(root, "notices", lang, d["id"])
    for n in ("README.md", "LICENSE.txt"):
        fl.get(fl.UNER_RAW.format(repo=d["repo"], commit=d["commit"], file=n), os.path.join(nd, n), optional=True)
    notices.append(nd)
    return out


def main():
    argv = sys.argv[1:]
    md_dir = None
    if "--md-dir" in argv:
        i = argv.index("--md-dir")
        md_dir = argv[i + 1]
        del argv[i:i + 2]
    if not argv:
        raise SystemExit(__doc__)
    root, langs = argv[0], argv[1:] or list(CONFIG)
    fl.CONFIG.update(CONFIG)
    fl.FETCHERS.update(rawfile=fetch_rawfile, kpwr=fetch_kpwr, ronec=fetch_ronec, uner=fetch_uner)
    for lang in langs:
        sys.argv = ["fetch_lang.py", lang, root] + (["--md", os.path.join(md_dir, lang + ".md")] if md_dir else [])
        if md_dir:
            os.makedirs(md_dir, exist_ok=True)
        fl.main()


if __name__ == "__main__":
    main()
