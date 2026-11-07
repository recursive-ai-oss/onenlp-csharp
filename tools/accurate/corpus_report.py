#!/usr/bin/env python3
"""Renders docs/accurate-corpus.md from the corpus spec: every language of the accurate tier with its data, sizes and special handling.

usage: corpus_report.py <corpus.json> <treebanks.tsv> <languages.tsv> <lexicon-proposals.tsv> <out.md>
"""
import csv
import json
import sys


def rows(path):
    with open(path, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f, delimiter="\t"))


def main(spec_path, tb_path, lang_path, lex_path, out):
    spec = json.load(open(spec_path, encoding="utf-8"))
    tbs = {r["repo"][3:]: r for r in rows(tb_path)}
    langs = rows(lang_path)
    lex = {r["code"]: r for r in rows(lex_path)}
    L = spec["languages"]
    o = ["# Accurate-tier corpus", "",
         "The languages the accurate tier is trained on, as `recipes/accurate/common/corpus.json` describes them (written by `tools/accurate/make_corpus_spec.py` from the fetched "
         "files; licence verdicts in `ud-licences.md`). Release r2.18 of Universal Dependencies.", "",
         f"- **{len(L)} languages** with allowed training data; {sum(1 for e in L if e['nine'])} of them keep the data of the earlier nine-language run, so their rows stay comparable.",
         f"- **{sum(e['trainSentences'] for e in L):,} training sentences, {sum(e['trainWords'] for e in L):,} words** (before the dev hold-out).",
         f"- **{sum(1 for e in L if e['ner'])} languages with an entity corpus** ({sum(len(e['ner']) for e in L)} sets).",
         f"- **{sum(1 for e in L if e['segmenter'] != 'none')} languages need a trained word segmenter** in front for raw text: "
         + ", ".join(f"{e['name']} ({e['code']})" for e in L if e["segmenter"] != "none") + ".",
         f"- **{sum(1 for e in L if e['small'])} small languages** (under 500 training sentences in all; the sampler caps their repetition).",
         f"- **Dev split:** {sum(1 for e in L if not e['devFromTrain'] and e['hasDev'])} languages have one in the release; "
         f"{sum(1 for e in L if e['devFromTrain'])} hold out every {spec['heldOutEvery']}th training sentence; "
         f"{sum(1 for e in L if not e['hasDev'])} (fewer than {spec['minDevFromTrain']} sentences, no dev split) train on all of it and are not seen by model selection.", "",
         "## Languages", "",
         "Words are syntactic words of the training files. Extra treebanks are trained on and never scored; the language's test set is its gating treebank's test split. "
         "*Leaks* are extra-treebank training sentences that repeat a gating test sentence; the reader drops them.", "",
         "| Code | Language | Gating treebank (train sentences / words) | Extra treebanks (train sentences) | Dev | Entities (train sentences) | Segmenter | Small | Lemma table (dev) |",
         "|---|---|---|---|---|---|---|---|---|"]
    for e in L:
        g = e["treebanks"][0]
        extra = ", ".join(f"{t['name']} ({t['sizes']['train']['sentences']:,}" + (f", {t['leaks']} leaks" if t.get("leaks") else "") + ")" for t in e["treebanks"][1:])
        dev = "held out" if e["devFromTrain"] else ("release" if e["hasDev"] else "none")
        ner = ", ".join(f"{n['name']} ({n['sizes']['train']['sentences']:,})" for n in e["ner"])
        lx = lex.get(e["code"], {})
        lt = lx.get("setting", "")
        if lt and lt != "none":
            lt = f"{lt} ({lx['coverage_pct']}% at {lx['precision_pct']}%)"
        o.append(f"| {e['code']} | {e['name']} | {g['name']} ({g['sizes']['train']['sentences']:,} / {g['sizes']['train']['words']:,}) | {extra} | {dev} | {ner} | "
                 f"{e['segmenter'] if e['segmenter'] != 'none' else ''} | {'yes' if e['small'] else ''} | {lt} |")
    left = [(e["name"], x) for e in L for x in e.get("left_out", [])]
    if left:
        o += ["", "Left out of training: " + "; ".join(f"{n}: {x['name']} ({x['reason']})" for n, x in left) + "."]
    o += ["", "## Languages without an accurate-tier model", "",
          "Every treebank of these languages is ruled out (`ud-licences.md`, \"Languages without an accurate-tier model\"), so there is no allowed training data: "
          + ", ".join(l["language"].replace("_", " ") for l in langs if l["klass"] == "gap") + ". Hindi has only the test-only PUD treebank.", "",
          "## Evaluation-only data", "",
          "Test-only treebanks of the trained languages are scored as extra rows and never trained on: "
          + ", ".join(f"{x['name']}" for e in L for x in e["evalOnly"]) + ".", ""]
    open(out, "w", encoding="utf-8", newline="\n").write("\n".join(o))
    print("wrote", out)


if __name__ == "__main__":
    main(*sys.argv[1:6])
