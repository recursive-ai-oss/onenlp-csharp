#!/usr/bin/env python3
"""Downloads the language-ID corpora, verifies them against SHA256SUMS and builds the train and eval sets.

Usage:
  fetch.py choose  probe.tsv sources.tsv        pick corpora per language from the probe result
  fetch.py fetch   sources.tsv DIR              download archives into DIR/archives (resumes), write DIR/archives/SHA256SUMS.new
  fetch.py prepare sources.tsv DIR TATOEBA OUT  build OUT/train/langid/<lang>.txt and OUT/eval/langid/<set>/<bucket>.tsv

Everything is deterministic: the train/eval split is decided by the SHA-256 of each normalized sentence, so a sentence in the
held-out pool can never be a training sentence, and the eval files are hashed (MANIFEST.tsv) before any training run.

Sources and licences (see docs/language-id.md):
  Leipzig Corpora Collection downloads, Creative Commons Attribution (CC BY 4.0), https://wortschatz.uni-leipzig.de/en/usage
  Tatoeba sentence export, default Creative Commons Attribution 2.0 France, https://tatoeba.org/en/terms_of_use
  Wikipedia dumps (st, ts, tn only), CC BY-SA 4.0, https://dumps.wikimedia.org/
"""
import bz2
import collections
import hashlib
import os
import random
import shutil
import re
import sys
import tarfile
import unicodedata
import urllib.request
import xml.etree.ElementTree as ET

BASE = "https://downloads.wortschatz-leipzig.de/corpora/"
SIZES = ["100K", "300K", "30K", "10K"]
WIKI = {"st": "stwiki", "ts": "tswiki", "tn": "tnwiki"}
# Scripts written without spaces between words: "words" for the evaluation buckets are character runs.
UNSPACED = {"zh", "ja", "th"}
# Other corpora needed (sentences) to keep the evaluation corpus out of training entirely.
MIN_STRICT = 20000
# Most Tatoeba sentences taken for training per language.
MAX_TATOEBA = 20000


def norm(s):
    return re.sub(r"\s+", " ", s).strip()


def key(s):
    """Sentence identity for splitting and de-duplication: lowercase letters only, so digits, punctuation and spacing do not hide a copy."""
    return re.sub(r"[\W\d_]+", "", s.lower())


def bucket(s):
    """Stable 0..9999 bucket of a sentence, from its identity key."""
    h = hashlib.sha256(key(s).encode("utf-8")).digest()
    return int.from_bytes(h[:4], "big") % 10000


def choose(probe, out):
    by = collections.defaultdict(list)
    for line in open(probe, encoding="utf-8"):
        code, lc, name, size, url = line.rstrip("\n").split("\t")
        m = re.search(r"/(\w+)_([\w-]+)_(\d{4})_(\w+)\.tar\.gz", url)
        by[code].append((m.group(2), int(m.group(3)), lc))
    with open(out, "w", encoding="utf-8") as o:
        o.write("# lang\trole\tleipzig code\ttype\tyear\n")
        for code, v in by.items():
            news = sorted([x for x in v if x[0] in ("news", "newscrawl")], key=lambda x: -x[1])
            wiki = sorted([x for x in v if x[0] == "wikipedia"], key=lambda x: -x[1])
            other = sorted([x for x in v if x[0] not in ("news", "newscrawl", "wikipedia")], key=lambda x: -x[1])
            ordered = []
            for group in (news[:1], wiki[:1], news[1:2], other[:1], wiki[1:2], news[2:3]):
                ordered.extend(group)
            if not ordered:
                continue
            for i, (t, y, lc) in enumerate(ordered[:3]):
                o.write(f"{code}\t{'eval-pool' if i == 0 else 'train'}\t{lc}\t{t}\t{y}\n")


def read_sources(path):
    rows = []
    for line in open(path, encoding="utf-8"):
        if line.startswith("#") or not line.strip():
            continue
        code, role, lc, t, y = line.rstrip("\n").split("\t")
        rows.append((code, role, lc, t, int(y)))
    return rows


def head_ok(url):
    try:
        with urllib.request.urlopen(urllib.request.Request(url, method="HEAD"), timeout=30):
            return True
    except Exception:
        return False


def archive_name(lc, t, y, size):
    return f"{lc}_{t}_{y}_{size}.tar.gz"


def download(url, dest):
    tmp = dest + ".part"
    # Wikimedia refuses the default Python agent string; say who we are.
    req = urllib.request.Request(url, headers={"User-Agent": "onenlp-langid-fetch/1.0 (corpus download for language identification)"})
    with urllib.request.urlopen(req, timeout=120) as r, open(tmp, "wb") as f:
        while True:
            b = r.read(1 << 20)
            if not b:
                break
            f.write(b)
    os.replace(tmp, dest)


def sha256_file(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def fetch(sources, d):
    arch = os.path.join(d, "archives")
    os.makedirs(arch, exist_ok=True)
    sums = []
    for code, role, lc, t, y in read_sources(sources):
        for size in SIZES:
            name = archive_name(lc, t, y, size)
            dest = os.path.join(arch, name)
            if os.path.exists(dest) or head_ok(BASE + name):
                if not os.path.exists(dest):
                    download(BASE + name, dest)
                sums.append(f"{sha256_file(dest)}  {name}")
                break
        else:
            print("missing", code, lc, t, y, file=sys.stderr)
    for code, wiki in WIKI.items():
        dest = os.path.join(arch, f"{wiki}-latest-pages-articles.xml.bz2")
        if not os.path.exists(dest):
            download(f"https://dumps.wikimedia.org/{wiki}/latest/{wiki}-latest-pages-articles.xml.bz2", dest)
        sums.append(f"{sha256_file(dest)}  {os.path.basename(dest)}")
    with open(os.path.join(arch, "SHA256SUMS.new"), "w", encoding="utf-8") as f:
        f.write("\n".join(sorted(sums, key=lambda s: s.split()[1])) + "\n")


def leipzig_sentences(path):
    with tarfile.open(path) as tf:
        for m in tf.getmembers():
            if m.name.endswith("-sentences.txt"):
                with tf.extractfile(m) as f:
                    for line in f:
                        parts = line.decode("utf-8", "replace").rstrip("\n").split("\t", 1)
                        if len(parts) == 2:
                            yield norm(parts[1])


MARKUP = [
    (re.compile(r"<ref[^>]*?/>|<ref[^>]*?>.*?</ref>", re.S), " "),
    (re.compile(r"<!--.*?-->", re.S), " "),
    (re.compile(r"\{\{[^{}]*\}\}"), " "),
    (re.compile(r"\{\|.*?\|\}", re.S), " "),
    (re.compile(r"\[\[(?:File|Image|Category|Foto|Lefaela|Setshwantsho)[^\]]*\]\]", re.I), " "),
    (re.compile(r"\[\[(?:[^\]|]*\|)?([^\]]*)\]\]"), r"\1"),
    (re.compile(r"\[https?://[^\s\]]*\s*([^\]]*)\]"), r"\1"),
    (re.compile(r"<[^>]+>"), " "),
    (re.compile(r"'{2,}"), ""),
    (re.compile(r"^[=*#:;|!].*$", re.M), " "),
]


def wiki_sentences(path):
    with bz2.open(path) as f:
        for _, el in ET.iterparse(f):
            if el.tag.endswith("}text") and el.text:
                text = el.text
                for rx, rep in MARKUP:
                    text = rx.sub(rep, text)
                for para in text.split("\n"):
                    for s in re.split(r"(?<=[.!?])\s+", para):
                        s = norm(s)
                        if 25 <= len(s) <= 400 and s.count("&") == 0:
                            yield s
            if el.tag.endswith("}page"):
                el.clear()


def clean_words(sentence):
    """Words made only of letters and marks (inner apostrophes and hyphens allowed), punctuation trimmed."""
    out = []
    for tok in sentence.split(" "):
        tok = tok.strip("\"'.,;:!?()[]{}«»„“”‘’“”¡¿…–—-—/\\")
        if not tok:
            continue
        ok = all(unicodedata.category(c)[0] in "LM" or c in "'’-" for c in tok) and unicodedata.category(tok[0])[0] in "LM"
        if ok:
            out.append(tok)
    return out


def make_eval(lang, pool, rnd, n):
    """Lingua-style buckets: words of at least 5 characters, word pairs of at least 10, whole sentences."""
    sents = [s for s in pool if len(s) >= 15]
    rnd.shuffle(sents)
    sentences = sents[:n]
    words, pairs = [], []
    seen_w, seen_p = set(), set()
    for s in sents:
        if lang in UNSPACED:
            run = "".join(c for c in s if unicodedata.category(c)[0] in "LM")
            if len(run) < 14:
                continue
            k = rnd.randint(5, 7)
            i = rnd.randint(0, len(run) - k)
            w = run[i:i + k]
            k2 = rnd.randint(10, 14)
            j = rnd.randint(0, len(run) - k2)
            p = run[j:j + k2]
        else:
            ws = clean_words(s)
            if len(ws) < 2:
                continue
            cand = [w for w in ws if len(w) >= 5]
            w = rnd.choice(cand) if cand else None
            i = rnd.randrange(len(ws) - 1)
            p = ws[i] + " " + ws[i + 1]
            if len(p) < 10:
                p = None
        if w and w.lower() not in seen_w and len(words) < n:
            seen_w.add(w.lower())
            words.append(w)
        if p and p.lower() not in seen_p and len(pairs) < n:
            seen_p.add(p.lower())
            pairs.append(p)
        if len(words) >= n and len(pairs) >= n:
            break
    return {"words": words, "pairs": pairs, "sentences": sentences}


def prepare(sources, d, tatoeba, out):
    rows = read_sources(sources)
    langs = sorted({r[0] for r in rows} | set(WIKI))
    tcodes = {}
    for line in open(os.path.join(os.path.dirname(sources), "languages.tsv"), encoding="utf-8"):
        if line.startswith("#") or not line.strip():
            continue
        f = line.rstrip("\n").split("\t")
        tcodes[f[3]] = f[0]
    tat = collections.defaultdict(list)
    for line in open(tatoeba, encoding="utf-8"):
        parts = line.rstrip("\n").split("\t")
        if len(parts) == 3 and parts[1] in tcodes:
            tat[tcodes[parts[1]]].append(norm(parts[2]))
    arch = os.path.join(d, "archives")
    shutil.rmtree(os.path.join(out, "eval", "langid"), ignore_errors=True)
    shutil.rmtree(os.path.join(out, "train", "langid"), ignore_errors=True)
    report = ["lang\tmode\tpool\ttrain\teval_pool\tdup_removed\ttatoeba_train\ttatoeba_eval"]
    manifest = []
    for lang in sorted(langs):
        pool_src, train_src = [], []
        for code, role, lc, t, y in rows:
            if code != lang:
                continue
            for size in SIZES:
                p = os.path.join(arch, archive_name(lc, t, y, size))
                if os.path.exists(p):
                    (pool_src if role == "eval-pool" else train_src).extend(leipzig_sentences(p))
                    break
        if lang in WIKI:
            train_src.extend(wiki_sentences(os.path.join(arch, f"{WIKI[lang]}-latest-pages-articles.xml.bz2")))
        # Other corpora, Wikipedia text and Tatoeba train. When they hold at least MIN_STRICT sentences, the evaluation corpus is kept
        # out of training entirely (the held-out texts then come from documents the model never saw, as in Lingua's own protocol).
        # Otherwise the evaluation texts are a hash slice of the one corpus and the language is marked "same-corpus" in the report.
        tat_all = list(dict.fromkeys(tat.get(lang, [])))
        other = len(set(map(key, train_src))) + len(tat_all)
        mode = "strict" if pool_src and other >= MIN_STRICT else "same-corpus"
        if pool_src:
            uniq = list(dict.fromkeys(pool_src))
            rate = min(3000, max(1, int(len(uniq) * 0.3))) / len(uniq)
            eval_pool = [s for s in uniq if bucket(s) < int(rate * 10000)]
            rest = [] if mode == "strict" else [s for s in uniq if bucket(s) >= int(rate * 10000)]
        else:
            # Wikipedia-only languages (st, ts, tn): hold out a hash slice of the Wikipedia text.
            mode = "same-corpus"
            uniq = list(dict.fromkeys(train_src))
            thr = int(min(2000, len(uniq) * 0.3) / len(uniq) * 10000)
            eval_pool = [s for s in uniq if bucket(s) < thr]
            train_src = [s for s in uniq if bucket(s) >= thr]
            rest = []
        held = {key(s) for s in eval_pool}
        # Tatoeba: a 5% hash slice is a second, out-of-domain evaluation pool; the rest trains.
        tat_eval = [s for s in tat_all if bucket(s) < 500]
        # Tatoeba is huge for a few languages and very different in style from news; cap it so it cannot swamp the other text.
        tat_train = sorted((s for s in tat_all if bucket(s) >= 500), key=bucket)[:MAX_TATOEBA]
        held_t = {key(s) for s in tat_eval}
        train, removed = [], 0
        for s in rest + train_src + tat_train:
            k = key(s)
            if k in held or k in held_t:
                removed += 1
                continue
            train.append(s)
        train = list(dict.fromkeys(train))
        rnd = random.Random(f"eval-{lang}")
        rnd.shuffle(train)
        os.makedirs(os.path.join(out, "train", "langid"), exist_ok=True)
        with open(os.path.join(out, "train", "langid", f"{lang}.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(train[:150000]) + "\n")
        for name, pool in (("main", eval_pool), ("tatoeba", tat_eval)):
            if len(pool) < 20:
                continue
            e = make_eval(lang, pool, random.Random(f"{name}-{lang}"), 1000 if name == "main" else 500)
            od = os.path.join(out, "eval", "langid", name)
            os.makedirs(od, exist_ok=True)
            for b, items in e.items():
                p = os.path.join(od, f"{b}.{lang}.tsv")
                with open(p, "w", encoding="utf-8", newline="\n") as f:
                    f.write("".join(f"{lang}\t{x}\n" for x in items))
        report.append(f"{lang}\t{mode}\t{len(pool_src)}\t{len(train)}\t{len(eval_pool)}\t{removed}\t{len(tat_train)}\t{len(tat_eval)}")
    # Merge per-language eval files into one file per bucket and hash everything.
    for name in ("main", "tatoeba"):
        od = os.path.join(out, "eval", "langid", name)
        for b in ("words", "pairs", "sentences"):
            parts = sorted(f for f in os.listdir(od) if f.startswith(b + "."))
            with open(os.path.join(od, f"{b}.tsv"), "w", encoding="utf-8", newline="\n") as o:
                for f in parts:
                    o.write(open(os.path.join(od, f), encoding="utf-8").read())
                    os.remove(os.path.join(od, f))
            manifest.append(f"{sha256_file(os.path.join(od, b + '.tsv'))}  eval/langid/{name}/{b}.tsv")
    td = os.path.join(out, "train", "langid")
    for f in sorted(os.listdir(td)):
        manifest.append(f"{sha256_file(os.path.join(td, f))}  train/langid/{f}")
    with open(os.path.join(out, "eval", "langid", "MANIFEST.sha256"), "w") as f:
        f.write("\n".join(manifest) + "\n")
    with open(os.path.join(out, "eval", "langid", "dedup-report.tsv"), "w") as f:
        f.write("\n".join(report) + "\n")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "choose":
        choose(sys.argv[2], sys.argv[3])
    elif cmd == "fetch":
        fetch(sys.argv[2], sys.argv[3])
    elif cmd == "prepare":
        prepare(*sys.argv[2:6])
    else:
        sys.exit(__doc__)
