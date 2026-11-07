#!/usr/bin/env python3
"""Fetches every Universal Dependencies treebank whose terms allow it, hashes each split and writes the manifest.

usage: fetch_ud.py <data-root> [--catalogue ud/treebanks.tsv] [--md ud/MANIFEST.md] [--only Basque-BDT,...]

Reads the treebank catalogue (ud/treebanks.tsv, built by ud_catalog.py) and downloads, from the pinned
release tag, the CoNLL-U splits of every treebank whose verdict is `go` (train, dev, test) or `eval-only`
(test only). Treebanks with any other verdict are never fetched. The nine languages that have their own
fetch scripts (`fetch_lang.py`) are skipped so their manifests stay as they are.

Layout under <data-root>:
  train/ud/<prefix>/<prefix>-ud-{train,dev}.conllu    roles train, dev
  eval/ud/<prefix>/<prefix>-ud-test.conllu            role eval
  notices/ud/<prefix>/                                the treebank's README and LICENSE.txt
  manifest-ud.json                                    role, URL, licence, size, sentences, tokens and SHA-256
                                                      of every file, with the checks below

Checks per treebank, recorded in the manifest and in the Markdown file:
  - sentence count equals the release's stats.xml figure for that split;
  - the forms are real text, not masked (a split whose forms are mostly `_` is reported and flagged);
  - test sentences whose `# text` also occurs in the training split are counted (the official split is kept).
Re-running verifies the hashes of files that already exist. Needs only python3.
"""
import csv
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fetch_lang import UD_RAW, UD_TAG, get, sha256  # noqa: E402

SKIP_LANGUAGES = {"Chinese", "French", "Spanish", "Portuguese", "German", "Italian", "Turkish", "Hindi", "Dutch", "English"}
HERE = os.path.dirname(os.path.abspath(__file__))


def scan(path):
    """Returns (sentences, tokens, masked_fraction, set of `# text` lines) of a CoNLL-U file."""
    sents = toks = masked = 0
    texts = set()
    in_sentence = False
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("# text = "):
                texts.add(line[9:])
            elif line.startswith("#"):
                continue
            elif not line:
                if in_sentence:
                    sents += 1
                in_sentence = False
            else:
                c = line.split("\t")
                in_sentence = True
                if len(c) >= 2 and "-" not in c[0] and "." not in c[0]:
                    toks += 1
                    if c[1] == "_":
                        masked += 1
    if in_sentence:
        sents += 1
    return sents, toks, (masked / toks if toks else 0.0), texts


def main():
    args = sys.argv[1:]
    if not args or args[0].startswith("--"):
        raise SystemExit(__doc__)
    root = args[0]
    opt = lambda k, d: args[args.index(k) + 1] if k in args else d  # noqa: E731
    catalogue = opt("--catalogue", os.path.join(HERE, "ud", "treebanks.tsv"))
    md_path = opt("--md", None)
    only = set(opt("--only", "").split(",")) - {""}
    with open(catalogue, encoding="utf-8") as f:
        rows = [r for r in csv.DictReader(f, delimiter="\t")]
    files, problems = [], []
    for r in rows:
        name = r["repo"][3:]
        if r["verdict"] not in ("go", "eval-only") or r["language"] in SKIP_LANGUAGES or (only and name not in only):
            continue
        pre, repo = r["prefix"], r["repo"]
        train_texts, entry = set(), []
        splits = r["splits"].split(",")
        for split in ("train", "dev", "test"):
            if split not in splits:
                continue
            role_dir = "eval" if split == "test" else "train"
            dest = os.path.join(root, role_dir, "ud", pre, f"{pre}-ud-{split}.conllu")
            url = UD_RAW.format(repo=repo, tag=UD_TAG, file=f"{pre}-ud-{split}.conllu")
            get(url, dest)
            sents, toks, masked, texts = scan(dest)
            expect = int(r[f"{split}_sentences"] or 0)
            e = dict(treebank=name, split=split, role={"train": "train", "dev": "dev", "test": "eval"}[split], path=dest,
                     url=url, licence=r["readme_licence"], verdict=r["verdict"], bytes=os.path.getsize(dest),
                     sentences=sents, tokens=toks, sha256=sha256(dest), masked_fraction=round(masked, 4))
            if sents != expect:
                problems.append(f"{name} {split}: {sents} sentences, stats.xml says {expect}")
            if masked > 0.5:
                problems.append(f"{name} {split}: {masked:.0%} of forms are masked (`_`); text must be restored before use")
            if split == "train":
                train_texts = texts
            if split == "test":
                e["test_texts_also_in_train"] = len(texts & train_texts)
            entry.append(e)
        nd = os.path.join(root, "notices", "ud", pre)
        for n in ("README.md", "README.txt"):
            get(UD_RAW.format(repo=repo, tag=UD_TAG, file=n), os.path.join(nd, n), optional=True)
        get(UD_RAW.format(repo=repo, tag=UD_TAG, file="LICENSE.txt"), os.path.join(nd, "LICENSE.txt"), optional=True)
        files += entry
    out = os.path.join(root, "manifest-ud.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(dict(release=UD_TAG, files=files, problems=problems), f, indent=1)
    print(f"{len(files)} files in {len({x['treebank'] for x in files})} treebanks; {len(problems)} problems")
    for p in problems:
        print("  ", p)
    if md_path:
        write_md(md_path, files, problems)


def write_md(path, files, problems):
    lines = ["# UD treebanks fetched for the accurate tier",
             "",
             f"Release {UD_TAG} (15 May 2026), each repository at tag `{UD_TAG}`. Written by `tools/fetch/fetch_ud.py` from the catalogue "
             "`tools/fetch/ud/treebanks.tsv`; verdicts and quotes are in `docs/ud-licences.md`. Roles: `train` and `dev` feed training and "
             "model selection; `eval` (the test split) is fixed here and never read by training or tuning. Files are fetched on the worker "
             "and never committed. Treebanks of the nine languages with their own fetch scripts are not repeated here.",
             "",
             "| Role | Treebank | File | Sentences | Tokens | Licence | SHA-256 | Notes |",
             "|---|---|---|---|---|---|---|---|"]
    for e in files:
        note = []
        if e["masked_fraction"] > 0.5:
            note.append("forms masked")
        if e.get("test_texts_also_in_train"):
            note.append(f"{e['test_texts_also_in_train']} test texts also in train")
        lines.append(f"| {e['role']} | {e['treebank']} | `{os.path.basename(e['path'])}` | {e['sentences']:,} | {e['tokens']:,} | "
                     f"{e['licence']} | `{e['sha256']}` | {'; '.join(note)} |")
    lines.append("")
    lines.append("URL of every file: `https://raw.githubusercontent.com/UniversalDependencies/UD_<Language>-<Treebank>/" + UD_TAG +
                 "/<file>`.")
    if problems:
        lines += ["", "## Checks that failed", ""] + [f"- {p}" for p in problems]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
