#!/usr/bin/env python3
"""Turns the published performance pages of Stanza, UDPipe 2 and Trankit into one machine-readable table.

usage: build_references.py <stanza.html> <udpipe2-models.html> <trankit.html> <survey.tsv> <out.tsv>

Inputs are the pages as published (read as documents, nothing is run):
  Stanza   https://stanfordnlp.github.io/stanza/performance.html      (release 1.5.1, UD 2.12)
  UDPipe 2 https://ufal.mff.cuni.cz/udpipe/2/models                   (UD 2.17 models, version 251125)
  Trankit  https://trankit.readthedocs.io/en/latest/performance.html  (EACL 2021, UD 2.5)
<survey.tsv> is the output of ud_survey.py; it is used to tie each row to its treebank repository name.

Every row is one system on one treebank test split in one input mode (raw text or gold tokenization).
Numbers are copied as published (F1 x 100); a missing cell is empty. UD columns are the CoNLL 2018
definitions. Stanza and Trankit report raw text only; UDPipe 2 reports both modes.
"""
import csv
import re
import sys

COLUMNS = ["system", "system_version", "model", "treebank", "ud_release", "input", "tokens", "sents", "words",
           "upos", "xpos", "ufeats", "alltags", "lemmas", "uas", "las", "clas", "mlas", "blex", "source"]


# Treebanks the sources name differently from today's repositories (renamed since).
ALIASES = {"frenchspoken": "UD_French-Rhapsodie", "kurmanjimg": "UD_Northern_Kurdish-Kurmanji",
           "norwegiannynorsknynorsk": "UD_Norwegian-Nynorsk", "oldrussiantorot": "UD_Old_East_Slavic-TOROT",
           "simplifiedchinesegsdsimp": "UD_Chinese-GSDSimp"}


def key(name):
    return re.sub(r"[^a-z0-9]", "", name.lower())


def resolve(repos, name):
    """Repository name for a source's treebank name; a treebank absent from the release keeps its own name, marked."""
    k = key(name)
    return ALIASES.get(k) or repos.get(k) or "UD_" + name.replace(" ", "_") + "(not in r2.18)"


def cells(row_html):
    return [re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", c)).replace("\u200b", "").strip() for c in re.split(r"<t[dh]\b[^>]*>", row_html)[1:]]


def num(x):
    x = x.strip()
    return x if re.fullmatch(r"\d+(\.\d+)?", x) else ""


def stanza(path, repos):
    html = open(path, encoding="utf-8").read()
    table = html[html.index('id="conll18-results"'):]
    table = table[:table.index("</table>")]
    rows = []
    for tr in table.split("<tr>")[1:]:
        c = cells(tr)
        m = re.search(r'href="https://github.com/UniversalDependencies/(UD_[^"]+)"', tr)
        if not m or len(c) < 17:
            continue  # header and the macro-average row
        # Language, Treebank, lcode, Default, Tokens, Sentences, Words, UPOS, XPOS, UFeats, AllTags, Lemmas, UAS, LAS, CLAS, MLAS, BLEX
        v = [num(x) for x in c[4:17]]
        rows.append(dict(system="Stanza", system_version="1.5.1", model="pretrained, charlm, no transformer",
                         treebank=m.group(1), ud_release="2.12", input="raw", tokens=v[0], sents=v[1], words=v[2],
                         upos=v[3], xpos=v[4], ufeats=v[5], alltags=v[6], lemmas=v[7], uas=v[8], las=v[9],
                         clas=v[10], mlas=v[11], blex=v[12],
                         source="https://stanfordnlp.github.io/stanza/performance.html"))
    return rows


def udpipe(path, repos):
    html = open(path, encoding="utf-8").read()
    start = html.rfind("<tr>", 0, html.index('id="afrikaans-afribooms-ud-2.17-251125"'))  # first table is the 2.17 models
    end = html.index("</table>", start)
    rows = []
    for tr in html[start:end].split("<tr>"):
        c = cells(tr)
        if len(c) != 13 or not re.search(r"-ud-2\.17-251125$", c[0]):
            continue
        # Model, Mode, Words, Sents, UPOS, XPOS, UFeats, AllTags, Lemma, UAS, LAS, MLAS, BLEX
        v = [num(x) for x in c[2:]]
        name = c[0][:-len("-ud-2.17-251125")]
        repo = resolve(repos, name)
        rows.append(dict(system="UDPipe 2", system_version="2.17 models 251125", model=c[0], treebank=repo,
                         ud_release="2.17", input="raw" if c[1].startswith("Raw") else "gold",
                         words=v[0], sents=v[1], upos=v[2], xpos=v[3], ufeats=v[4], alltags=v[5], lemmas=v[6],
                         uas=v[7], las=v[8], mlas=v[9], blex=v[10], source="https://ufal.mff.cuni.cz/udpipe/2/models"))
    return rows


def trankit(path, repos):
    html = open(path, encoding="utf-8").read()
    rows, current = [], None
    for tr in html.split("<tr")[1:]:
        c = cells(tr.split(">", 1)[1])
        if len(c) != 15:
            continue  # the page's first, smaller table (a handful of treebanks) repeats numbers from the big one
        if c[0]:
            current = c[0]
        if current in (None, "Overall performance") or not c[1].startswith("Trankit[") or c[1].count("-"):
            continue  # the page also lists Stanza 1.1.1 runs on UD 2.5 and per-row differences; not used as references
        # Treebank, System, Tokens, Sentences, Words, UPOS, XPOS, UFeats, AllTags, Lemmas, UAS, LAS, CLAS, MLAS, BLEX
        v = [num(x) for x in c[2:]]
        repo = resolve(repos, current)
        size = c[1][len("Trankit"):].strip("[]")
        rows.append(dict(system="Trankit", system_version=size, model="XLM-R " + size, treebank=repo,
                         ud_release="2.5", input="raw", tokens=v[0], sents=v[1], words=v[2], upos=v[3], xpos=v[4],
                         ufeats=v[5], alltags=v[6], lemmas=v[7], uas=v[8], las=v[9], clas=v[10], mlas=v[11], blex=v[12],
                         source="https://trankit.readthedocs.io/en/latest/performance.html"))
    return rows


def main():
    if len(sys.argv) != 6:
        raise SystemExit(__doc__)
    s_path, u_path, t_path, survey, out = sys.argv[1:]
    with open(survey, encoding="utf-8") as f:
        repos = {key(r["repo"][3:]): r["repo"] for r in csv.DictReader(f, delimiter="\t")}
    rows = stanza(s_path, repos) + udpipe(u_path, repos) + trankit(t_path, repos)
    bad = sorted({r["treebank"] for r in rows if "(not in" in r["treebank"]})
    with open(out, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, COLUMNS, delimiter="\t", lineterminator="\n", extrasaction="ignore", restval="")
        w.writeheader()
        w.writerows(rows)
    print(f"{len(rows)} rows; unmatched treebank names: {bad}")


if __name__ == "__main__":
    main()
