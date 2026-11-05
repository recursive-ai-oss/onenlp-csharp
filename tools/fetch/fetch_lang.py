#!/usr/bin/env python3
"""Fetches the training and evaluation data of one language, hashes every file and writes the manifest.

usage: fetch_lang.py <lang> <data-root> [--md <MANIFEST.md>]
       <lang> is one of zh fr es pt de it tr hi nl

Layout under <data-root>:
  train/ud/<lang>/<treebank>-ud-{train,dev}.conllu   roles train, dev
  eval/ud/<lang>/<treebank>-ud-test.conllu           role eval
  train/uner/<lang>/, eval/uner/<lang>/              Universal NER (.iob2, UD-aligned tokens)
  train/ner/<lang>/<dataset>/, eval/ner/<lang>/<dataset>/   other named-entity corpora (.iob2)
  notices/<lang>/<dataset>/                          each source's own README and LICENSE
  manifest-<lang>.json                               role, URL, licence, size and SHA-256 of every file,
                                                     split sizes and the train/test text-overlap report

Only sources whose terms allow training are fetched (CC BY-SA, CC BY, CC0, ODbL, permissive); test-only
treebanks are fetched for evaluation only. Non-commercial and unclear sources are never fetched. The
verdict for every treebank of the language is in docs/ud-licences.md.
Re-running verifies the hashes of files that already exist. Needs python3 and pyarrow (Turkish only).
"""
import bz2
import hashlib
import io
import json
import os
import re
import sys
import urllib.request
import zipfile

UD_TAG = "r2.18"
DEV_CAP = 2000
UD_RAW = "https://raw.githubusercontent.com/UniversalDependencies/{repo}/{tag}/{file}"
UNER_RAW = "https://raw.githubusercontent.com/UniversalNER/{repo}/{commit}/{file}"

# ---- sources ---------------------------------------------------------------------------------

def ud(repo, commit, prefix, splits, licence, note=""):
    return dict(kind="ud", id="ud-" + prefix.replace("_", "-"), repo=repo, commit=commit, prefix=prefix,
                splits=splits, licence=licence, note=note, tag=UD_TAG)

def uner(repo, commit, prefix, splits, licence="CC BY-SA 4.0", note=""):
    return dict(kind="uner", id="uner-" + prefix.replace("_", "-"), repo=repo, commit=commit, prefix=prefix,
                splits=splits, licence=licence, note=note)

TTD = ("train", "dev", "test")
SA4 = "CC BY-SA 4.0"
SA3 = "CC BY-SA 3.0"

WIKINER = "https://ndownloader.figshare.com/files/{fid}"
WIKINER_FILES = {  # figshare article 5462500, wp3 variants (higher level of link inference); md5 from the figshare API
    "fr": (9446338, "f13a4c6eb7378fd6ea493ed256191b41"), "es": (9446332, "ecd7f585f44855ec6ca5130998ff2fdc"),
    "it": (9446344, "2554a867110a72189ac7f1a4de629de9"), "nl": (9446350, "7fab7fa21b1c57db8fb75320abfeb3e1"),
}

def wikiner(lang):
    fid, md5 = WIKINER_FILES[lang]
    return dict(kind="wikiner", id=f"wikiner-{lang}-wp3", url=WIKINER.format(fid=fid), md5=md5, lang=lang,
                licence="CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links",
                note="no official split: articles (blank-line-delimited blocks) go to test when index mod 20 = 0, "
                     "to dev when index mod 20 = 10, otherwise to train; dev is thinned by a fixed stride to at most 2,000 sentences")

CONFIG = {
    "zh": [
        ud("UD_Chinese-GSDSimp", "7b61ed473f963e911788efdf1f478154bc1053e4", "zh_gsdsimp", TTD, SA4),
        ud("UD_Chinese-PUD", "7b54411fb8cec041c4f8794413e08878605792f3", "zh_pud", ("test",), SA3, "test-only treebank"),
        uner("UNER_Chinese-GSDSIMP", "1f0307599dece7b83f4cba90ce9b63ef37a7b162", "zh_gsdsimp", TTD),
        uner("UNER_Chinese-PUD", "647ba53747e1aa1818541965db99a0196440b7d2", "zh_pud", ("test",), SA3, "test-only"),
    ],
    "fr": [
        ud("UD_French-GSD", "15958a5df1d09e2a360784caaed07b9a3fcbf7a8", "fr_gsd", TTD, SA4),
        ud("UD_French-PUD", "7ca18ac15f248ca0a3ef26e2652cb4e75ca18dea", "fr_pud", ("test",), SA3, "test-only treebank"),
        wikiner("fr"),
    ],
    "es": [
        ud("UD_Spanish-AnCora", "197cca385e0e7db1b1fe26a5772dade1b6fbbee8", "es_ancora", TTD, "CC BY 4.0"),
        ud("UD_Spanish-PUD", "8af7ac68bbd846bc47e525c3193d96f123dd388b", "es_pud", ("test",), SA3, "test-only treebank"),
        wikiner("es"),
    ],
    "pt": [
        ud("UD_Portuguese-Bosque", "53bb7584bec8f6b83a27eaefe0656690df9d7773", "pt_bosque", TTD, SA4),
        ud("UD_Portuguese-PUD", "7b8897dc3d2fef397eed177ab6d89bbf3e33f017", "pt_pud", ("test",), SA3, "test-only treebank"),
        uner("UNER_Portuguese-Bosque", "f2c2ad67aed900385fbe5d655af41f439c9b3333", "pt_bosque", TTD),
        uner("UNER_Portuguese-PUD", "cda2cf86b68d45e896aab97ceca5e91d43a86db7", "pt_pud", ("test",), SA3, "test-only"),
    ],
    "de": [
        ud("UD_German-GSD", "81d8c3612a88f5867fd089e9c0a5466ef230078b", "de_gsd", TTD, SA4),
        ud("UD_German-PUD", "e42201a08c2512818471efcdedc44bb22d9f1df2", "de_pud", ("test",), SA3, "test-only treebank"),
        dict(kind="germeval", id="germeval-2014", url="https://www.inf.uni-hamburg.de/en/inst/ab/lt/resources/data/germaner/germeval2014-complete-data.zip",
             licence="CC BY 4.0 (Benikova, Biemann, Reznicek 2014: \"released under the permissive CC-BY license\")", note="official train/dev/test"),
        uner("UNER_German-PUD", "47eb039b6d80079b280ba70efe4136413fb7af9c", "de_pud", ("test",), SA3, "test-only"),
    ],
    "it": [
        ud("UD_Italian-ParlaMint", "45c92e77b6163e8aa311c205aa510896a66e0654", "it_parlamint", ("train", "test"), SA4,
           "no dev split in the release"),
        ud("UD_Italian-MarkIT", "957127de2b1d10303dc1a1fdd132598ba708d705", "it_markit", TTD, "CC BY 4.0"),
        ud("UD_Italian-PUD", "bae2181e24fef395d662b22190e7eb5725dac3b5", "it_pud", ("test",), SA3, "test-only treebank"),
        wikiner("it"),
    ],
    "tr": [
        ud("UD_Turkish-BOUN", "0def94a229dcbc28d32192b0138b560c075c6d87", "tr_boun", TTD, SA4),
        ud("UD_Turkish-PUD", "4817a5e719906e25662fd6ea8d937b4992c6897d", "tr_pud", ("test",), SA3, "test-only treebank"),
        dict(kind="trwikiner", id="turkish-wikiner", base="https://huggingface.co/datasets/turkish-nlp-suite/turkish-wikiNER/resolve/{rev}/data/{f}-00000-of-00001.parquet",
             rev="99df1dd7f5a01d39cd5903e51e361d708cb2a5a6", licence=SA4, note="official train/validation/test; 18 OntoNotes-style types"),
    ],
    "hi": [
        ud("UD_Hindi-PUD", "66d03982c23dd3049b7f7d11964247794411079c", "hi_pud", ("test",), SA3, "test-only treebank; the only Hindi treebank whose terms allow use"),
        dict(kind="naamapadam", id="naamapadam-hi", url="https://huggingface.co/datasets/ai4bharat/naamapadam/resolve/9d4f21ac57d11ed4f9ea64854fdc9f5618e61acc/data/hi_IndicNER_v1.0.zip",
             licence="CC0 1.0 (packaging; the text is not owned by the curators)",
             note="train is silver (labels projected from English); train every 10th sentence, dev every 7th of the validation file; "
                  "test is the gold-annotated set, all of it"),
    ],
    "nl": [
        ud("UD_Dutch-Alpino", "d64749ac918f18d31dd4c4e75ff56626432d9ba3", "nl_alpino", TTD, SA4),
        wikiner("nl"),
    ],
}

# Dutch treebank README is README.txt; the others README.md.
README_NAMES = ("README.md", "README.txt")

# ---- helpers ---------------------------------------------------------------------------------

def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def get(url, dest, optional=False):
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        return True
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    last = None
    for _ in range(3):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "onenlp-fetch"}), timeout=120) as r, \
                    open(dest + ".part", "wb") as f:
                while True:
                    b = r.read(1 << 20)
                    if not b:
                        break
                    f.write(b)
            os.replace(dest + ".part", dest)
            return True
        except Exception as e:  # noqa: BLE001 - report and retry
            last = e
    if optional:
        return False
    raise SystemExit(f"download failed: {url}: {last}")


def read_sentences(path):
    """Yields (text, [(form, tag-or-None)]) for UD and .iob2 files; text is the '# text =' comment or the joined forms."""
    text, rows = None, []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("# text = "):
                text = line[9:]
            elif line.startswith("#"):
                continue
            elif not line:
                if rows:
                    yield (text if text is not None else " ".join(r[0] for r in rows)), rows
                text, rows = None, []
            else:
                c = line.split("\t")
                if "-" in c[0] or "." in c[0]:
                    continue  # multiword-token range or empty node
                rows.append((c[1], c[2] if len(c) > 2 else None))
    if rows:
        yield (text if text is not None else " ".join(r[0] for r in rows)), rows


def write_iob2(path, sents):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    n = 0
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for words, tags in sents:
            f.write("# text = " + " ".join(words) + "\n")
            for i, (w, t) in enumerate(zip(words, tags), 1):
                f.write(f"{i}\t{w}\t{t}\t_\t_\n")
            f.write("\n")
            n += 1
    return n


def iob1_to_iob2(tags):
    out, prev = [], "O"
    for t in tags:
        if t == "O":
            out.append("O")
        else:
            kind, typ = t.split("-", 1)
            prev_typ = prev.split("-", 1)[1] if prev != "O" else None
            out.append(("I-" if kind == "I" and prev_typ == typ else "B-") + typ if kind == "I" else t)
        prev = out[-1]
    return out

# ---- per-kind fetchers: return list of (role, path, url) ---------------------------------------

def fetch_ud(root, lang, d, notices):
    out = []
    repo, tag, pre = d["repo"], d["tag"], d["prefix"]
    for split in d["splits"]:
        role_dir = "eval" if split == "test" else "train"
        dest = os.path.join(root, role_dir, "ud", lang, f"{pre}-ud-{split}.conllu")
        url = UD_RAW.format(repo=repo, tag=tag, file=f"{pre}-ud-{split}.conllu")
        get(url, dest)
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=dest, url=url))
    nd = os.path.join(root, "notices", lang, d["id"])
    for n in README_NAMES:
        get(UD_RAW.format(repo=repo, tag=tag, file=n), os.path.join(nd, n), optional=True)
    get(UD_RAW.format(repo=repo, tag=tag, file="LICENSE.txt"), os.path.join(nd, "LICENSE.txt"))
    notices.append(nd)
    return out


def fetch_uner(root, lang, d, notices):
    out = []
    for split in d["splits"]:
        role_dir = "eval" if split == "test" else "train"
        dest = os.path.join(root, role_dir, "uner", lang, f"{d['prefix']}-ud-{split}.iob2")
        url = UNER_RAW.format(repo=d["repo"], commit=d["commit"], file=f"{d['prefix']}-ud-{split}.iob2")
        get(url, dest)
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=dest, url=url))
    nd = os.path.join(root, "notices", lang, d["id"])
    for n in ("README.md", "LICENSE.txt"):
        get(UNER_RAW.format(repo=d["repo"], commit=d["commit"], file=n), os.path.join(nd, n))
    notices.append(nd)
    return out


def ner_paths(root, lang, d, split):
    role_dir = "eval" if split == "test" else "train"
    return os.path.join(root, role_dir, "ner", lang, d["id"], f"{d['id']}-{split}.iob2")


def fetch_wikiner(root, lang, d, notices):
    raw = os.path.join(root, "raw", lang, d["id"] + ".bz2")
    get(d["url"], raw)
    md5 = hashlib.md5(open(raw, "rb").read()).hexdigest()
    if md5 != d["md5"]:
        raise SystemExit(f"{raw}: md5 {md5} != {d['md5']}")
    blocks, cur = [], []
    data = bz2.open(raw).read()
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError:
        text = data.decode("latin-1")  # the Dutch file is ISO-8859-1 (observed: it holds bytes such as E9 for 'e' with acute)
    for line in text.split("\n"):
        line = line.rstrip("\n")
        if not line.strip():
            if cur:
                blocks.append(cur)
                cur = []
            continue
        words, tags = [], []
        for tok in line.split(" "):
            parts = tok.rsplit("|", 2)  # token|POS|NER; the token itself may contain '|'
            if len(parts) != 3:
                continue
            words.append(parts[0])
            tags.append(parts[2])
        if words:
            cur.append((words, iob1_to_iob2(tags)))
    if cur:
        blocks.append(cur)
    split_of = lambda i: "test" if i % 20 == 0 else "dev" if i % 20 == 10 else "train"  # noqa: E731
    buckets = {"train": [], "dev": [], "test": []}
    for i, b in enumerate(blocks):
        buckets[split_of(i)].extend(b)
    if len(buckets["dev"]) > DEV_CAP:  # dev only drives model selection; a fixed stride keeps evaluation during training short
        stride = -(-len(buckets["dev"]) // DEV_CAP)
        buckets["dev"] = buckets["dev"][::stride]
    out = []
    for split in ("train", "dev", "test"):
        p = ner_paths(root, lang, d, split)
        write_iob2(p, buckets[split])
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=p,
                        url=d["url"] + " (converted by tools/fetch/fetch_lang.py)"))
    d["raw"] = dict(path=raw, md5=md5, sha256=sha256(raw), articles=len(blocks))
    return out


def fetch_germeval(root, lang, d, notices):
    raw = os.path.join(root, "raw", lang, "germeval2014-complete-data.zip")
    get(d["url"], raw)
    out = []
    z = zipfile.ZipFile(raw)
    for split, member in (("train", "NER-de-train.tsv"), ("dev", "NER-de-dev.tsv"), ("test", "NER-de-test.tsv")):
        sents, words, tags = [], [], []
        for line in z.read("GermEval2014_complete_data/" + member).decode("utf-8").split("\n"):
            line = line.rstrip("\r")
            if line.startswith("#"):
                continue
            if not line.strip():
                if words:
                    sents.append((words, tags))
                words, tags = [], []
                continue
            c = line.split("\t")
            words.append(c[1])
            tags.append(c[2])  # outer level; the embedded level (column 4) is not used
        if words:
            sents.append((words, tags))
        p = ner_paths(root, lang, d, split)
        write_iob2(p, sents)
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=p,
                        url=d["url"] + f"#{member} (converted)"))
    d["raw"] = dict(path=raw, sha256=sha256(raw))
    return out


def fetch_trwikiner(root, lang, d, notices):
    import pyarrow.parquet as pq  # fetch-time tool only
    names = ["O", "B-CARDINAL", "B-DATE", "B-EVENT", "B-FAC", "B-GPE", "B-LANGUAGE", "B-LAW", "B-LOC", "B-MONEY", "B-NORP",
             "B-ORDINAL", "B-ORG", "B-PERCENT", "B-PERSON", "B-PRODUCT", "B-QUANTITY", "B-TIME", "B-TITLE", "B-WORK_OF_ART",
             "I-CARDINAL", "I-DATE", "I-EVENT", "I-FAC", "I-GPE", "I-LANGUAGE", "I-LAW", "I-LOC", "I-MONEY", "I-NORP",
             "I-ORDINAL", "I-ORG", "I-PERCENT", "I-PERSON", "I-PRODUCT", "I-QUANTITY", "I-TIME", "I-TITLE", "I-WORK_OF_ART"]
    out, raws = [], {}
    for split, f in (("train", "train"), ("dev", "validation"), ("test", "test")):
        url = d["base"].format(rev=d["rev"], f=f)
        raw = os.path.join(root, "raw", lang, f"turkish-wikiner-{f}.parquet")
        get(url, raw)
        raws[f] = sha256(raw)
        t = pq.read_table(raw).to_pylist()
        p = ner_paths(root, lang, d, split)
        write_iob2(p, [(r["tokens"], [names[i] for i in r["tags"]]) for r in t])
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=p, url=url + " (converted)"))
    d["raw"] = raws
    nd = os.path.join(root, "notices", lang, d["id"])
    get(f"https://huggingface.co/datasets/turkish-nlp-suite/turkish-wikiNER/raw/{d['rev']}/README.md", os.path.join(nd, "README.md"))
    notices.append(nd)
    return out


def fetch_naamapadam(root, lang, d, notices):
    import json as js
    raw = os.path.join(root, "raw", lang, "hi_IndicNER_v1.0.zip")
    get(d["url"], raw)
    z = zipfile.ZipFile(raw)
    out = []
    for split, member, stride in (("train", "hi_train.json", 10), ("dev", "hi_val.json", 7), ("test", "hi_test.json", 1)):
        sents = []
        with z.open(member) as f:
            for i, line in enumerate(io.TextIOWrapper(f, encoding="utf-8")):
                if not line.strip():
                    continue
                if i % stride:
                    continue
                r = js.loads(line)
                if len(r["words"]) == len(r["ner"]) and r["words"]:
                    sents.append((r["words"], r["ner"]))
        p = ner_paths(root, lang, d, split)
        write_iob2(p, sents)
        out.append(dict(split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=p,
                        url=d["url"] + f"#{member} (every {stride}th line, converted)"))
    d["raw"] = dict(path=raw, sha256=sha256(raw))
    nd = os.path.join(root, "notices", lang, d["id"])
    get("https://huggingface.co/datasets/ai4bharat/naamapadam/raw/9d4f21ac57d11ed4f9ea64854fdc9f5618e61acc/README.md", os.path.join(nd, "README.md"))
    notices.append(nd)
    return out


FETCHERS = {"ud": fetch_ud, "uner": fetch_uner, "wikiner": fetch_wikiner, "germeval": fetch_germeval,
            "trwikiner": fetch_trwikiner, "naamapadam": fetch_naamapadam}

# ---- Italian: the release has no dev split and the gating treebank is tiny -------------------------

def derive_italian(root, files):
    """it_gating = ParlaMint (train minus every 8th sentence) + MarkIT train, with dev = every 8th ParlaMint train
    sentence + MarkIT dev. Written beside the sources so the recipe has one train and one dev file."""
    def lines(path):
        blocks, cur = [], []
        with open(path, encoding="utf-8") as f:
            for line in f:
                cur.append(line)
                if line.strip() == "":
                    blocks.append("".join(cur))
                    cur = []
        if cur:
            blocks.append("".join(cur))
        return blocks
    base = os.path.join(root, "train", "ud", "it")
    parla = lines(os.path.join(base, "it_parlamint-ud-train.conllu"))
    mk_tr = lines(os.path.join(base, "it_markit-ud-train.conllu"))
    mk_dev = lines(os.path.join(base, "it_markit-ud-dev.conllu"))
    tr = [b for i, b in enumerate(parla) if i % 8 != 7] + mk_tr
    dv = [b for i, b in enumerate(parla) if i % 8 == 7] + mk_dev
    out = []
    for split, bl in (("train", tr), ("dev", dv)):
        p = os.path.join(base, f"it_gating-ud-{split}.conllu")
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write("".join(bl))
        out.append(dict(dataset="ud-it-gating", split=split, role=split, path=p,
                        url="derived from it_parlamint and it_markit (tools/fetch/fetch_lang.py)", licence="CC BY-SA 4.0 and CC BY 4.0"))
    return out

# ---- main ------------------------------------------------------------------------------------

def stats(path):
    n = t = 0
    for _, rows in read_sentences(path):
        n += 1
        t += len(rows)
    return n, t


def folded(s):
    return re.sub(r"\s+", " ", s).strip().casefold()


def main():
    lang, root = sys.argv[1], os.path.abspath(sys.argv[2])
    md_out = sys.argv[sys.argv.index("--md") + 1] if "--md" in sys.argv else None
    if lang not in CONFIG:
        raise SystemExit(f"unknown language {lang}")
    notices, files = [], []
    for d in CONFIG[lang]:
        for fl in FETCHERS[d["kind"]](root, lang, d, notices):
            fl.update(dataset=d["id"], licence=d["licence"])
            files.append(fl)
    if lang == "it":
        files += derive_italian(root, files)
    for fl in files:
        fl["sentences"], fl["tokens"] = stats(fl["path"])
        fl["bytes"] = os.path.getsize(fl["path"])
        fl["sha256"] = sha256(fl["path"])
    # Text overlap: evaluation sentences that also occur on the training side of the same family (UD or NER).
    def family(f):
        return "ud" if f["dataset"].startswith("ud-") else "ner"
    dedup = []
    for ev in [f for f in files if f["role"] == "eval"]:
        # Same family (UD against UD, entities against entities) and the other family: a UD test sentence that
        # sits in an entity-corpus training file would leak when one encoder trains on both.
        for label, same in (("same", True), ("cross", False)):
            pool = [f for f in files if f["role"] in ("train", "dev") and (family(f) == family(ev)) == same
                    and f["dataset"] != "ud-it-gating"]
            if not pool:
                continue
            train = set()
            for f in pool:
                train.update(t for t, _ in read_sentences(f["path"]))
            trf = {folded(t) for t in train}
            ts = [t for t, _ in read_sentences(ev["path"])]
            dedup.append(dict(test=os.path.relpath(ev["path"], root), kind=label, against=sorted({p["dataset"] for p in pool}),
                              test_sentences=len(ts), exact_text_in_train_or_dev=sum(1 for t in ts if t in train),
                              folded_text_in_train_or_dev=sum(1 for t in ts if folded(t) in trf)))
    nots = []
    for nd in notices:
        for n in sorted(os.listdir(nd)):
            nots.append(dict(path=os.path.relpath(os.path.join(nd, n), root), sha256=sha256(os.path.join(nd, n))))
    raws = {d["id"]: d["raw"] for d in CONFIG[lang] if "raw" in d}
    for r in raws.values():
        if "path" in r:
            r["path"] = os.path.relpath(r["path"], root)
    for fl in files:
        fl["path"] = os.path.relpath(fl["path"], root)
    manifest = dict(language=lang, ud_release=UD_TAG, files=files, notices=nots, raw_downloads=raws, dedup=dedup,
                    datasets=[{k: v for k, v in d.items() if k in ("id", "kind", "repo", "commit", "licence", "note")} for d in CONFIG[lang]])
    with open(os.path.join(root, f"manifest-{lang}.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    if md_out:
        write_md(md_out, manifest)
    for fl in files:
        print(f"{fl['role']:6} {fl['dataset']:24} {os.path.basename(fl['path']):34} {fl['sentences']:>7} sents {fl['tokens']:>8} toks {fl['sha256'][:12]}")
    for d in dedup:
        print("overlap", d)


def write_md(path, m):
    L = ["## Files", "", "| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |", "|---|---|---|---|---|---|---|---|"]
    for f in m["files"]:
        L.append(f"| {f['role']} | {f['dataset']} | `{os.path.basename(f['path'])}` | {f['sentences']:,} | {f['tokens']:,} | {f['licence']} | {f['url']} | `{f['sha256']}` |")
    if m["raw_downloads"]:
        L += ["", "## Raw downloads converted by the script", "", "| Dataset | Raw file | SHA-256 |", "|---|---|---|"]
        for k, v in m["raw_downloads"].items():
            if "sha256" in v:
                L.append(f"| {k} | `{os.path.basename(v['path'])}` | `{v['sha256']}` |")
            else:
                for kk, vv in v.items():
                    L.append(f"| {k} | `{kk}` | `{vv}` |")
    L += ["", "## Overlap between test text and training text", "",
          "Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.", "",
          "| Test file | Against | Test sentences | Identical | Folded |", "|---|---|---|---|---|"]
    for d in m["dedup"]:
        L.append(f"| `{d['test']}` | {', '.join(d['against'])}{' (other family)' if d['kind'] == 'cross' else ''} | {d['test_sentences']:,} | {d['exact_text_in_train_or_dev']:,} | {d['folded_text_in_train_or_dev']:,} |")
    L += ["", "## Notices", "", "| File | SHA-256 |", "|---|---|"]
    for n in m["notices"]:
        L.append(f"| `{n['path']}` | `{n['sha256']}` |")
    body = "\n".join(L) + "\n"
    header = os.path.join(os.path.dirname(path), "HEADER.md")
    pre = open(header, encoding="utf-8").read().rstrip() + "\n\n" if os.path.exists(header) else ""
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(pre + body)


if __name__ == "__main__":
    main()
