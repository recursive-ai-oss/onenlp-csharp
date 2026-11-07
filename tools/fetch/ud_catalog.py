#!/usr/bin/env python3
"""Builds the treebank catalogue (verdict, role, gating treebank per language) and the reference table.

usage: ud_catalog.py <survey-dir> <references-raw.tsv> <out-dir>

Inputs: <survey-dir>/survey.tsv from ud_survey.py (and its page cache), and the raw reference table from
build_references.py. Writes into <out-dir>:
  ud-treebanks.tsv    one row per treebank of the release: licence lines, splits, sizes, verdict, reason
  ud-languages.tsv    one row per language: class, gating treebank, extra training treebanks, test-only sets
  references.tsv      the reference table with, for every row, whether the test split changed since the
                      release the source used (sentence and token counts from each repository's stats.xml at
                      that release's tag)

The mechanical part of a verdict follows the licence line of the README (`License:` metadata) and the
LICENSE.txt; the rest is the explicit tables below, each entry with the sentence of the treebank's own
README (or the source's own page) that decided it.
"""
import csv
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ud_survey as survey  # noqa: E402

TAG = "r2.18"
# Languages whose fast-tier manifests already name a treebank; the accurate tier keeps the same test sets.
NINE_GATING = {"English": "UD_English-EWT", "Chinese": "UD_Chinese-GSDSimp", "French": "UD_French-GSD",
               "Spanish": "UD_Spanish-AnCora", "Portuguese": "UD_Portuguese-Bosque", "German": "UD_German-GSD",
               "Italian": "UD_Italian-ParlaMint", "Turkish": "UD_Turkish-BOUN", "Dutch": "UD_Dutch-Alpino"}
NINE = {"Chinese", "French", "Spanish", "Portuguese", "German", "Italian", "Turkish", "Hindi", "Dutch", "English"}
REF_TAGS = {"2.5": "r2.5", "2.12": "r2.12", "2.17": "r2.17"}

# --- explicit rulings ------------------------------------------------------------------------------
# treebank (without "UD_") -> reason. Every one of these has a licence line that passes the mechanical
# test, so the text source decides.
NO_GO = {
    "German-HDT": 'README: "Heise gave permission to distribute the text for academic use" (research-only text; ruled)',
    "Italian-TWITTIRO": 'README: "a collection of ironic Italian tweets" (platform terms on the text; ruled)',
    "Portuguese-DANTEStocks": "tweets (platform terms on the text; ruled)",
    "Turkish-Penn": "sentences translated from the LDC Penn Treebank (ruled)",
    "English-GUMReddit": 'README: "This repository only contains annotations, without the underlying textual data from Reddit" (Reddit text; ToS-restricted)',
    "Irish-TwittIrish": 'README: "A Universal Dependencies treebank of 2596 tweets in modern Irish." (platform text)',
    "Romanian-TueCL": 'README: "It contains 210 annotated tweets selected from CoRoSeOf" (platform text)',
    "Arabic-NYUAD": 'README: "Due to LDC licensing restrictions, the annotations are released without the data." (LDC text, not distributed)',
    "Shanghainese-ShUD": 'README: the source corpus is "licensed under Creative Commons BY-NC-ND 4.0", with "additional permission for derivative research" (research-only)',
    "South_Levantine_Arabic-MADAR": 'README: sentences come from the MADAR parallel corpus, built on BTEC; the MADAR corpus "is made available to the research community under a non-commercial license" (NYU Abu Dhabi CAMeL Lab resources page)',
}
UNCLEAR = {
    "Finnish-OOD": 'README: texts from "nursing narratives (hospital patient records), discussion forums, tweets, general web crawls and poetry"; "The underlying texts come from various sources collected from the Internet. These may hold different copyright owners."',
    "Belarusian-HSE": 'README: genre list includes "5) social media: messages from the Telegram channels"; annotations CC BY-SA 4.0, no statement about the channel text',
    "Russian-Taiga": 'README: "source: vk.com, instagram, facebook, twitter, youtube comments, questions & answers (otvet.mail.ru), reviews (reviews.yandex.ru)"; annotations CC BY-SA 4.0, no statement about the platform text',
    "Manx-Cadhan": 'README: sentences "came from a large web-crawled corpus of Manx"; genres include blog and social',
    "Maghrebi_Arabic_French-Arabizi": 'README: sources include "Algerian newspaper\u2019s web forums"; no statement about the forum text',
    "Romanian-Nonstandard": 'README: nonstandard genres "Old Romanian, Chat, Folklore"; the chat source is not stated',
    "Polish-LFG": "licence line and LICENSE.txt: GNU GPL 3.0 on a treebank (copyleft terms for data, and for what is trained on it, are untested); 3% of the text is forums and chatrooms",
    "Naga-Suansu": 'README: test set includes "film_Bridge: Begining of the subtitles from Bridge of Spies (2015)" (a commercial film\u2019s subtitles); the rest is grammar examples',
}
# LICENSE.txt mentions a non-commercial licence, but not as the treebank's terms.
LF_NC_OK = {
    "Dutch-LassySmall": 'LICENSE.txt states "distributed under the Creative Commons Attribution-ShareAlike 4.0 International license (CC BY-SA 4.0)"; its informal-reference links point at the by-nc-sa URL, which is read as a typo',
    "Finnish-FTB": 'LICENSE.txt offers LGPLv3+ or, alternatively, "CC BY 4.0 ... even commercially"; the GPL text it appends has the word "noncommercially" in an unrelated clause',
    "Polish-LFG": "",
}
GO_NOTES = {
    "Ligurian-GLT": 'Computational Use of Data Agreement 1.0 (C-UDA): "You agree that you will use the Data solely for Computational Use"; training is Computational Use and "The C-UDA does not impose any restriction with respect to the use, modification, or distribution of Results". Redistributing the data would bind recipients; we do not redistribute it',
    "Classical_Chinese-Kyoto": "README licence line says PD; LICENSE.txt says CC BY-SA 4.0; either passes",
    "Chinese-HK": "film subtitles by students of the authors' university and legislative proceedings",
    "Cantonese-HK": "film subtitles by students of the authors' university and legislative proceedings",
    "Czech-CAC": 'built on a corpus the LDC distributes; the UD release is stated to be "made freely available under the CreativeCommons license"',
    "English-EWT": "built on the LDC English Web Treebank; the UD release is CC BY-SA 4.0 (kept as in the English units)",
    "Bavarian-MaiBaam": "Wikipedia (CC BY-SA) and Tatoeba sentences; per-sentence source URLs given",
    "Japanese-GSD": 'Google: "Google gave permission to drop the NC restriction from the license"',
    "Japanese-GSDLUW": "same as Japanese-GSD",
    "Korean-GSD": "same as Japanese-GSD",
    "Indonesian-GSD": "same as Japanese-GSD",
}

# --- helpers ---------------------------------------------------------------------------------------


def is_nc(text):
    t = text.lower().replace("-", " ")
    return bool(re.search(r"\bnc\b|non ?commercial|noncommercial|\bnd\b|no ?deriv", t))


def read_rows(path):
    with open(path, encoding="utf-8") as f:
        return list(csv.DictReader(f, delimiter="\t"))


def stats_at(work, tag, repo):
    c = os.path.join(work, "cache", tag, repo, "stats.xml")
    body = survey.fetch(survey.RAW.format(repo=repo, tag=tag, file="stats.xml"), c)
    return survey.stats(body) if body else None


def licence_file_nc(work, repo):
    """True when LICENSE.txt, read in full, names a non-commercial licence."""
    path = os.path.join(work, "cache", TAG, repo, "LICENSE.txt")
    if not os.path.exists(path):
        return False
    with open(path, encoding="utf-8", errors="replace") as f:
        t = f.read()
    return t != "\0404" and bool(re.search(r"non-?commercial|by-nc|\bBY NC\b", t, re.I))


def verdict(r, work):
    """Returns (verdict, flag, reason). verdict: go | eval-only | no-go | unclear."""
    name = r["repo"][3:]
    lic, lf = r["readme_licence"], r["licence_file_head"]
    lf_nc = licence_file_nc(work, r["repo"])
    sp = r["splits"].split(",") if r["splits"] else []
    if is_nc(lic):
        if lf_nc or not lf:
            return "no-go", "", f"README licence line {lic}; LICENSE.txt agrees"
        return "unclear", "conflict", f'README licence line says {lic}, but LICENSE.txt says: "{lf}"'
    if lf_nc and name not in LF_NC_OK:
        return "unclear", "conflict", f'README licence line says {lic}, but LICENSE.txt names a non-commercial licence ("{lf}")'
    if name in NO_GO:
        return "no-go", "text", NO_GO[name]
    if name in UNCLEAR:
        return "unclear", "text", UNCLEAR[name]
    note = f"; {GO_NOTES[name]}" if name in GO_NOTES else ""
    if LF_NC_OK.get(name):
        note += "; " + LF_NC_OK[name]
    if "train" not in sp:
        return "eval-only", "", f"{lic}; no training split{note}"
    return "go", "", lic + note


def main():
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    work, refs_path, out = sys.argv[1:]
    os.makedirs(out, exist_ok=True)
    rows = [r for r in read_rows(os.path.join(work, "survey.tsv")) if r["in_release"] == "yes"]
    for r in rows:
        # A split can be several files (cs_pdtc-ud-train-c.conllu ...); stats.xml still counts it.
        counts = r["sentences"].split(",")
        r["splits"] = ",".join(n for n, c in zip(("train", "dev", "test"), counts) if c)
    refs = read_rows(refs_path)
    ref_systems = {}
    for x in refs:
        ref_systems.setdefault(x["treebank"], set()).add(x["system"])

    cat = []
    for r in rows:
        v, flag, why = verdict(r, work)
        st = {s: (a, b) for s, a, b in zip(("train", "dev", "test"), r["sentences"].split(","), r["tokens"].split(","))}
        cat.append(dict(repo=r["repo"], language=r["language"], treebank=r["treebank"], readme_licence=r["readme_licence"],
                        licence_file=r["licence_file_head"], prefix=r["prefix"], splits=r["splits"],
                        train_sentences=st["train"][0], dev_sentences=st["dev"][0], test_sentences=st["test"][0],
                        test_tokens=st["test"][1], verdict=v, flag=flag, reason=why,
                        refs=",".join(sorted(ref_systems.get(r["repo"], [])))))
    with open(os.path.join(out, "ud-treebanks.tsv"), "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, list(cat[0].keys()), delimiter="\t", lineterminator="\n")
        w.writeheader()
        w.writerows(cat)

    langs = {}
    for c in cat:
        langs.setdefault(c["language"], []).append(c)
    lrows = []
    for lang, tbs in sorted(langs.items()):
        trainable = [t for t in tbs if t["verdict"] == "go" and "test" in t["splits"]]
        gating = None
        if lang in NINE_GATING:
            gating = next((t for t in tbs if t["repo"] == NINE_GATING[lang]), None)
        elif trainable:
            # The treebank the published systems report most; then the larger training set.
            gating = max(trainable, key=lambda t: (len(t["refs"].split(",")) if t["refs"] else 0, int(t["train_sentences"] or 0)))
        extra = [t for t in tbs if t["verdict"] == "go" and t is not gating]
        evalonly = [t for t in tbs if t["verdict"] == "eval-only"]
        if gating:
            cls = "train+eval"
        elif extra:
            cls = "train-no-test"
        elif evalonly:
            cls = "eval-only"
        elif any(t["verdict"] == "unclear" for t in tbs):
            cls = "pending"
        else:
            cls = "gap"
        lrows.append(dict(language=lang, klass=cls, gating=gating["repo"] if gating else "",
                          gating_train=gating["train_sentences"] if gating else "",
                          extra_train=";".join(t["repo"][3:] for t in extra),
                          test_only=";".join(t["repo"][3:] for t in evalonly),
                          unclear=";".join(t["repo"][3:] for t in tbs if t["verdict"] == "unclear"),
                          no_go=";".join(t["repo"][3:] for t in tbs if t["verdict"] == "no-go"),
                          refs=gating["refs"] if gating else "", nine="yes" if lang in NINE else ""))
    with open(os.path.join(out, "ud-languages.tsv"), "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, list(lrows[0].keys()), delimiter="\t", lineterminator="\n")
        w.writeheader()
        w.writerows(lrows)

    # Reference rows: did the test split change between the source's release and r2.18?
    by_repo = {c["repo"]: c for c in cat}
    for x in refs:
        repo = x["treebank"]
        x["in_r2_18"] = "yes" if repo in by_repo else "no"
        x["test_sents_ref"] = x["test_sents_r2_18"] = x["test_tokens_ref"] = x["test_tokens_r2_18"] = ""
        if repo not in by_repo:
            x["test_changed"] = "treebank not in r2.18"
            continue
        if by_repo[repo]["verdict"] not in ("go", "eval-only"):
            x["test_changed"] = "not checked (no allowed data)"
            continue
        old, now = stats_at(work, REF_TAGS[x["ud_release"]], repo), stats_at(work, TAG, repo)
        if old is None:
            x["test_changed"] = "treebank not in release " + x["ud_release"]
        elif "test" not in old or "test" not in now:
            x["test_changed"] = "no test split to compare"
        else:
            x["test_sents_ref"], x["test_tokens_ref"] = str(old["test"][0]), str(old["test"][1])
            x["test_sents_r2_18"], x["test_tokens_r2_18"] = str(now["test"][0]), str(now["test"][1])
            x["test_changed"] = "same size" if old["test"] == now["test"] else "changed"
    with open(os.path.join(out, "references.tsv"), "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, list(refs[0].keys()), delimiter="\t", lineterminator="\n")
        w.writeheader()
        w.writerows(refs)

    vc, lc = {}, {}
    for c in cat:
        vc[c["verdict"]] = vc.get(c["verdict"], 0) + 1
    for l in lrows:
        lc[l["klass"]] = lc.get(l["klass"], 0) + 1
    print("treebanks", len(cat), vc)
    print("languages", len(lrows), lc)
    print("conflicts", sum(1 for c in cat if c["flag"] == "conflict"))
    print("reference rows by change:", {k: sum(1 for x in refs if x["test_changed"] == k) for k in sorted({x["test_changed"] for x in refs})})


if __name__ == "__main__":
    main()
