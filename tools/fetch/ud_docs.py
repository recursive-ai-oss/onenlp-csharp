#!/usr/bin/env python3
"""Renders the generated sections of docs/ud-licences.md and docs/references.md from the catalogue tables.

usage: ud_docs.py <catalogue-dir> <docs-dir>

<catalogue-dir> holds ud-treebanks.tsv, ud-languages.tsv and references.tsv (ud_catalog.py output). Each
document keeps its hand-written text; only the region between the BEGIN/END markers is replaced, so the
script can be re-run after a release or a ruling changes. Also copies references.tsv next to references.md.
"""
import csv
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ud_catalog import GO_NOTES, LF_NC_OK  # noqa: E402

BEGIN = "<!-- BEGIN generated: {name} (tools/fetch/ud_docs.py) -->"
END = "<!-- END generated: {name} -->"

# Entity-recognition data beyond UD, checked at each source (card, repository or paper) on 2026-11-06.
# (name, languages, terms as stated, verdict, use)
NER = [
    ("UNER v1 repositories `UniversalNER/UNER_*` (Mayhew et al. 2024)", "see the UNER table", "labels CC BY-SA 4.0 (CC BY-SA 3.0 for PUD sets); a few repositories carry no LICENSE file and rely on the dataset card's \"released under the terms of the Creative Commons Attribution-ShareAlike 4.0\"", "go where the underlying UD treebank is go; the text keeps the treebank's own licence", "train and eval"),
    ("WikiNER wp3 (Nothman et al. 2013)", "de en es fr it nl pl pt ru", "figshare record: CC BY 4.0; silver labels projected from Wikipedia links, text is Wikipedia (CC BY-SA)", "go (SA lineage)", "silver NER for es fr it nl pt (already fetched for fr es it nl); de en pl ru not fetched"),
    ("DaNE `alexandrainst/dane`", "da", "card: cc-by-sa-4.0; text from Parole (the UD Danish-DDT text)", "go (SA lineage)", "Danish NER; the same sentences as UNER Danish-DDT"),
    ("KPWr-NER `clarin-pl/kpwr-ner`", "pl", "card: \"Creative Commons Attribution 3.0 Unported Licence\"; \"various sources, covering many domains and genres\"", "go on the card's licence (text sources are not itemised on the card)", "Polish NER"),
    ("hr500k `classla/hr500k`", "hr", "card: cc-by-sa-4.0; NER layer of the Croatian reference corpus", "go (SA lineage)", "Croatian NER"),
    ("RONEC `community-datasets/ronec`", "ro", "card: MIT; \"sentences that are free of copyright\" from SEETimes, Romanian Wikipedia and Common Crawl", "go", "Romanian NER (15 classes)"),
    ("SUCX 3.0 NER `KBLab/sucx3_ner`", "sv", "card: cc-by-4.0 (SUC 3.0, Spr\u00e5kbanken)", "go", "Swedish NER; the Swedish-Talbanken UNER set is the UD-aligned alternative"),
    ("MasakhaNER 2.0 `masakhane/masakhaner2`", "20 African languages (bm bbj ee fon ha ig rw lg luo mos ny pcm sn sw tn tw wo xh yo zu)", "card: afl-3.0; text from news outlets: \"The data is sourced from newspaper source\"", "out (ruled 2026-11-07), unclear terms: the annotations' licence is permissive, the news publishers' terms on the text are not stated", "none"),
    ("MasakhaNER 1.0 `masakhane/masakhaner`", "10 African languages", "card: license unknown", "out (ruled 2026-11-07), no stated terms", "none"),
    ("Hausa VOA NER `UdS-LSV/hausa_voa_ner`", "ha", "card: cc-by-4.0; \"crawled from VOA Hausa news\"", "out (ruled 2026-11-07), unclear terms: crawled news site; the site's terms are not stated on the card", "none"),
    ("HiNER original `cfilt/HiNER-original`", "hi", "card: cc-by-sa-4.0; \"The dataset contains sentences from ILCI and other sources. ILCI dataset requires license from Indian Language Consortium due to which we do not distribute the ILCI portion of the data.\"", "out (ruled 2026-11-07), unclear terms: the remaining sources are not named on the card (would be the first Hindi NER candidate)", "none"),
    ("KLUE-NER `klue/klue`", "ko", "card: cc-by-sa-4.0; the card's examples show news, review and other web text", "out (ruled 2026-11-07), unclear terms: the terms on those texts are not stated", "none"),
    ("FiNER `mpsilfve/finer`", "fi", "card: mit; the card is a blank template and names no text source", "out (ruled 2026-11-07), unclear terms: the text source is not stated", "none"),
    ("Persian NER `HaniehPoostchi/persian_ner`", "fa", "card: cc-by-4.0", "out (ruled 2026-11-07), unclear terms: the card does not name the text source", "none"),
    ("Catalan NER `Ugiat/ner-cat`", "ca", "card: apache-2.0; television transcriptions", "out (ruled 2026-11-07), unclear terms: rights in the transcribed programmes are not stated", "none"),
    ("fiNERweb `whoisjones/fiNERweb`", "91 languages", "card: mit; \"annotated text in multiple languages\"; the card does not name the label generator", "out (ruled 2026-11-07), unclear terms: the label generator (and so the labels' terms) is not stated", "none"),
    ("Swedish NER corpus `klintan/swedish_ner_corpus`", "sv", "card: cc-by-4.0; \"news from Swedish newspapers' websites\"", "out (ruled 2026-11-07), unclear terms: newspaper text, terms not stated", "none"),
    ("Europeana Newspapers `community-datasets/euronews`", "de fr nl", "card: cc0-1.0; historical newspapers", "go (public-domain scans, CC0 card)", "silver-quality historical NER; not chosen (old orthography)"),
    ("WikiANN / PAN-X", "many languages", "card: `license: unknown`", "no-go [ruled]", "-"),
    ("WikiNEuRal, MultiNERD (Babelscape)", "9 languages", "cards: cc-by-nc-sa-4.0", "no-go (NC)", "-"),
    ("Naamapadam (ai4bharat)", "11 Indic languages", "text from Samanantar, CC BY-NC 4.0", "no-go [ruled]", "-"),
    ("MultiCoNER v1/v2", "several languages", "cards: cc-by-4.0; fine-grained scheme, web and search-query text", "not examined (scheme does not map onto PER/ORG/LOC)", "-"),
]
# UNER repositories: name -> (UD treebank it labels, note)
UNER_SETS = [
    ("Cebuano-GJA", "Cebuano-GJA"), ("Chinese-GSD", "Chinese-GSD"), ("Chinese-GSDSIMP", "Chinese-GSDSimp"), ("Chinese-PUD", "Chinese-PUD"),
    ("Croatian-SET", "Croatian-SET"), ("Czech-PUD", "Czech-PUD"), ("Danish-DDT", "Danish-DDT"), ("English-EWT", "English-EWT"),
    ("English-PUD", "English-PUD"), ("German-PUD", "German-PUD"), ("Greek-GDT", "Greek-GDT"), ("Hebrew-HTB", "Hebrew-HTB"),
    ("Indonesian-CSUI", "Indonesian-CSUI"), ("Indonesian-GSD", "Indonesian-GSD"), ("Indonesian-PUD", "Indonesian-PUD"),
    ("Japanese-PUD", "Japanese-PUD"), ("Korean-PUD", "Korean-PUD"), ("Maghrebi_Arabic_French-Arabizi", "Maghrebi_Arabic_French-Arabizi"),
    ("Norwegian-NDT", "Norwegian-Bokmaal"), ("Portuguese-Bosque", "Portuguese-Bosque"), ("Portuguese-PUD", "Portuguese-PUD"),
    ("Romanian-LegalNERo", "Romanian-LegalNERo"), ("Russian-PUD", "Russian-PUD"), ("Serbian-SET", "Serbian-SET"),
    ("Slovak-SNK", "Slovak-SNK"), ("Slovenian-SSJ", "Slovenian-SSJ"), ("Swedish-Lines", "Swedish-LinES"),
    ("Swedish-PUD", "Swedish-PUD"), ("Swedish-Talbanken", "Swedish-Talbanken"), ("Tagalog-TRG", "Tagalog-TRG"),
    ("Tagalog-Ugnayan", "Tagalog-Ugnayan"),
]


def rows(path):
    with open(path, encoding="utf-8") as f:
        return list(csv.DictReader(f, delimiter="\t"))


def replace_region(path, name, body):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    b, e = BEGIN.format(name=name), END.format(name=name)
    block = f"{b}\n\n{body.strip()}\n\n{e}"
    if b in text:
        text = re.sub(re.escape(b) + r".*?" + re.escape(e), lambda m: block, text, flags=re.S)
    else:
        text = text.rstrip("\n") + "\n\n" + block + "\n"
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def lf_label(head):
    h = head
    for pat, lab in ((r"NonCommercial-ShareAlike|BY-NC-SA", "CC BY-NC-SA"), (r"NonCommercial-NoDeriv", "CC BY-NC-ND"),
                     (r"Attribution-ShareAlike 4", "CC BY-SA 4.0"), (r"Attribution-ShareAlike 3", "CC BY-SA 3.0"),
                     (r"Attribution 4", "CC BY 4.0"), (r"CC0|Creative Commons Legal Code", "CC0"), (r"GNU GENERAL PUBLIC", "GPL"),
                     (r"Lesser General Public", "LGPL-LR"), (r"Computational Use", "C-UDA 1.0")):
        if re.search(pat, h, re.I):
            return lab
    return "own text"


def licences_doc(cat, langs, docs):
    n = len(cat)
    vc = {}
    for c in cat:
        vc[c["verdict"]] = vc.get(c["verdict"], 0) + 1
    lc = {}
    for l in langs:
        lc[l["klass"]] = lc.get(l["klass"], 0) + 1
    big = sum(1 for l in langs if l["klass"] == "train+eval" and int(l["gating_train"] or 0) >= 1000)
    small = sum(1 for l in langs if l["klass"] == "train+eval" and int(l["gating_train"] or 0) < 500)
    o = []
    o.append("## Every treebank of release 2.18\n")
    o.append(f"Release 2.18 (15 May 2026) has {n} treebanks in {len(langs)} languages. Each repository was read at tag `r2.18` on 2026-11-06 "
             "(`tools/fetch/ud_survey.py`): the `License:` line of the README, the whole of `LICENSE.txt`, and the README text for the source of "
             "the sentences. The full table with sizes is `tools/fetch/ud/treebanks.tsv`; the per-language classification is `tools/fetch/ud/languages.tsv`. "
             "The sections above give the nine languages' detail; this one covers all languages with the same rules.\n")
    o.append("### Summary\n")
    o.append("| Verdict | Treebanks |\n|---|---|")
    for k in ("go", "eval-only", "no-go"):
        o.append(f"| {k} | {vc.get(k, 0)} |")
    o.append(f"| total | {n} |\n")
    o.append("| Language class | Languages |\n|---|---|")
    o.append(f"| **train+eval**: at least one `go` treebank with training and test splits | {lc.get('train+eval', 0)} (of which {big} have 1,000 or more training sentences in the gating treebank and {small} fewer than 500) |")
    o.append(f"| **eval-only**: only test-only or evaluation data is allowed | {lc.get('eval-only', 0)} |")
    o.append(f"| **gap**: every treebank is non-commercial, research-only or otherwise ruled out: the accurate tier has no UD data | {lc.get('gap', 0)} |")
    o.append(f"| total | {len(langs)} |\n")
    o.append("The nine languages with fast-tier work (zh fr es pt de it tr nl, plus en) keep their treebanks and manifests; Hindi stays a recorded gap for training (HDTB is NC), "
             "with Hindi-PUD as its test set.\n")
    o.append("### Rules applied\n")
    o.append("- **go**: the README licence line is CC BY-SA, CC BY, CC0, PD, LGPL-LR or another permissive licence, `LICENSE.txt` does not contradict it, no README sentence restricts the text, and the treebank has a training split. SA lineage is recorded in provenance. Credit goes in THIRD-PARTY-NOTICES.")
    o.append("- **eval-only**: allowed licence but no training split (PUD and other test-only treebanks).")
    o.append("- **no-go**: non-commercial or no-derivatives licence (the README line and `LICENSE.txt` agree), research-only text, platform-restricted text (tweets, Reddit), LDC-derived text, or the rulings of 2026-11-05.")
    o.append("- **Rulings of 2026-11-07 on the 22 treebanks that were `unclear`**: (1) the README says non-commercial and `LICENSE.txt` is the generic CC BY-SA template: no-go, the README statement wins; (2) CC BY-SA annotation over platform or terms-restricted text: no-go; (3) Polish-LFG (GNU GPL 3.0): no-go; (4) Ligurian-GLT (Computational Use of Data Agreement 1.0): go, credited. Nothing stays unclear.")
    o.append("- The `LICENSE.txt` column abbreviates its first lines; `own text` means a bespoke document (read in full for the decision).\n")

    byl = {}
    for c in cat:
        byl.setdefault(c["language"], []).append(c)
    o.append("### Languages\n")
    o.append("Gating treebank: among the `go` treebanks with training and test splits, the one the most published systems report (Stanza, UDPipe 2, Trankit), then the larger training set; the nine fast-tier languages keep theirs. Training sentences in brackets. *Extra training* are further `go` treebanks. *Test-only* are `eval-only` sets. *Not usable* lists `no-go` treebanks.\n")
    o.append("| Language | Class | Gating treebank (train sentences) | Extra training | Test-only | Not usable |")
    o.append("|---|---|---|---|---|---|")
    for l in langs:
        gate = f"{l['gating'][3:]} ({int(l['gating_train']):,})" if l["gating"] else ""
        nu = []
        if l["no_go"]:
            nu.append("no-go: " + l["no_go"].replace(";", ", "))
        o.append(f"| {l['language'].replace('_', ' ')} | {l['klass']} | {gate} | {l['extra_train'].replace(';', ', ')} | {l['test_only'].replace(';', ', ')} | {'; '.join(nu)} |")
    o.append("")
    o.append("### Languages without an accurate-tier model\n")
    o.append("Every treebank of these languages is ruled out, so there is no allowed training data. Hindi has UD data only in the test-only PUD set (HDTB is non-commercial); its fast-tier model is trained on labels from the accurate tier.\n")
    o.append("| Language | Treebanks and why they are out |")
    o.append("|---|---|")
    for l in langs:
        if l["klass"] != "gap":
            continue
        why = "; ".join(f"{c['repo'][3:]}: {c['reason'][:110].rstrip()}" for c in cat if c["language"] == l["language"] and c["verdict"] == "no-go")
        o.append(f"| {l['language'].replace('_', ' ')} | {why.replace('|', '/')} |")
    o.append("")
    o.append("### Treebanks\n")
    o.append("Splits are sentence counts (train/dev/test) from `stats.xml`. \"Licence line\" is the README's `License:` metadata; the quote for any other decision is in the Rulings section.\n")
    o.append("| Treebank | Licence line | LICENSE.txt | Splits | Verdict |")
    o.append("|---|---|---|---|---|")
    for c in cat:
        sp = "/".join(c[f"{s}_sentences"] or "-" for s in ("train", "dev", "test"))
        v = c["verdict"] + (" (conflict)" if c["flag"] == "conflict" else "")
        o.append(f"| {c['repo'][3:]} | {c['readme_licence']} | {lf_label(c['licence_file'])} | {sp} | {v} |")
    o.append("")
    o.append("### Rulings and quotes\n")
    o.append("**no-go on the text source or the sources of terms** (licence line alone would pass):\n")
    for c in cat:
        if c["verdict"] == "no-go" and c["flag"] == "text" and not c["reason"].startswith("ruled no-go"):
            o.append(f"- **{c['repo'][3:]}**: {c['reason']}")
    o.append("\n**ruled no-go from `unclear`** (not fetched; the quotes behind the rulings):\n")
    for c in cat:
        if c["reason"].startswith("ruled no-go"):
            o.append(f"- **{c['repo'][3:]}** ({c['readme_licence']}; {c['train_sentences'] or 0} train sentences): {c['reason']}")
    o.append("\n**go with a condition or note**:\n")
    for c in cat:
        n = c["repo"][3:]
        if c["verdict"] in ("go", "eval-only") and (n in GO_NOTES or LF_NC_OK.get(n)):
            o.append(f"- **{n}**: {GO_NOTES.get(n) or LF_NC_OK[n]}")
    o.append("")
    o.append("### Named-entity data beyond the nine languages\n")
    o.append("UNER v1 (`github.com/UniversalNER`, listing read 2026-11-06) holds 31 datasets. Each labels the sentences of one UD treebank, so a UNER set is usable only where that treebank is. Verdicts:\n")
    o.append("| UNER repository | UD treebank | UD verdict | UNER set |")
    o.append("|---|---|---|---|")
    by_repo = {c["repo"]: c for c in cat}
    for u, tb in UNER_SETS:
        c = by_repo.get("UD_" + tb)
        v = c["verdict"] if c else "not in r2.18"
        if u == "Romanian-LegalNERo":
            res = "out (ruled 2026-11-07), unclear terms: the UNER README says \"Initially, the LegalNERo dataset was made available under the license CC BY-NC-ND 4.0 ... The Zenodo version remains under that license. This UNER version of the work is provided under the license CC BY-SA 4.0\""
        elif v == "go":
            res = "go (train, dev, eval; SA lineage)"
        elif v == "eval-only":
            res = "eval-only"
        elif v == "no-go":
            res = "no-go (its text is NC)"
        else:
            res = v
        o.append(f"| UNER_{u} | {tb} | {v} | {res} |")
    o.append("")
    o.append("Languages with a usable UNER training set beyond the nine: Croatian, Danish, Indonesian (CSUI, GSD), Norwegian, Serbian, Slovak, Slovenian, Swedish (Talbanken); eval-only: Cebuano, Czech, Japanese, Korean, Russian, Swedish (PUD), Tagalog (TRG). "
             "UNER Chinese and Portuguese are the nine-language sets. Each UNER set was cut from an older UD commit, so its tokens must be checked against the r2.18 files before training (the English-EWT set is not aligned with r2.18; Portuguese-Bosque is). Hebrew, Greek and Swedish-LinES carry NC text; Tagalog-Ugnayan is NC-SA.\n")
    o.append("Other named-entity corpora checked at their cards (a card's licence is the dataset's, and the text source is read where the card states it):\n")
    o.append("| Corpus | Languages | Terms (as stated) | Verdict | Use |")
    o.append("|---|---|---|---|---|")
    for name, lg, terms, v, use in NER:
        o.append(f"| {name} | {lg} | {terms} | {v} | {use} |")
    o.append("")
    haveuner = {"Croatian", "Danish", "Indonesian", "Norwegian", "Serbian", "Slovak", "Slovenian", "Swedish"}
    extra_ner = {"Polish": "KPWr-NER", "Romanian": "RONEC", "Swedish": "SUCX 3.0", "Danish": "DaNE", "Croatian": "hr500k"}
    gaps = [l["language"].replace("_", " ") for l in langs
            if l["klass"] == "train+eval" and l["language"] not in haveuner and l["language"] not in extra_ner
            and l["language"] not in {"Chinese", "Portuguese", "English", "French", "Spanish", "Italian", "Dutch", "German", "Turkish"}]
    o.append(f"**NER gaps.** Of the {lc.get('train+eval', 0)} train+eval languages, those with no allowed NER training data at all: {', '.join(gaps)}. "
             "The nine languages are covered in the sections above (Hindi remains a gap). The base-size accurate tier has entity heads for the 19 languages with allowed entity training data. The other languages provide UD annotations only; a language without an entity head does not claim cross-lingual NER.\n")
    replace_region(os.path.join(docs, "ud-licences.md"), "all-treebanks", "\n".join(o))


def ref_val(r, col):
    return r.get(col, "") or "-"


def references_doc(cat, langs, refs, docs):
    by = {}
    for r in refs:
        by.setdefault(r["treebank"], []).append(r)
    o = []
    o.append("## Every gating treebank: Stanza, UDPipe 2 and Trankit\n")
    o.append("`docs/references.tsv` holds every number as published, one row per system, treebank and input mode, for all treebanks the three sources list "
             f"({len(refs)} rows). Columns: `system`, `system_version`, `model`, `treebank` (repository name), `ud_release`, `input` (`raw` or `gold` tokenization), "
             "`tokens`, `sents`, `words`, `upos`, `xpos`, `ufeats`, `alltags`, `lemmas`, `uas`, `las`, `clas`, `mlas`, `blex` (F1 × 100, empty when the source does not report it), "
             "`source`, then the comparison with r2.18: `in_r2_18`, `test_sents_ref`/`test_tokens_ref` (the test split at the source's release), `test_sents_r2_18`/`test_tokens_r2_18`, and `test_changed`.\n")
    o.append("Sources (read 2026-11-06, copied, nothing re-run):\n")
    o.append("- **Stanza 1.5.1**, pretrained models with charlm and no transformer, UD 2.12, end to end from raw text, CoNLL 2018 script: `stanfordnlp.github.io/stanza/performance.html` (137 treebanks).")
    o.append("- **UDPipe 2**, models `*-ud-2.17-251125` on UD 2.17 (mBERT embeddings), raw text and gold tokenization: `ufal.mff.cuni.cz/udpipe/2/models`. The models are CC BY-NC-SA; only the published numbers are used. The page reports Words, not Tokens.")
    o.append("- **Trankit** (large = XLM-R large, base = XLM-R base), UD 2.5, raw text: `trankit.readthedocs.io/en/latest/performance.html` (EACL 2021, Nguyen et al.). The page repeats runs of Stanza 1.1.1, UDPipe 1.2 and spaCy 2.3 on UD 2.5; those are not used.\n")
    o.append("**Version caveat.** `test_changed` compares the number of sentences and tokens in the test split at the source's release tag with r2.18 (`stats.xml` of each repository at that tag): `same size` means the test set has the same size (annotations may still have been corrected: UD fixes labels in most releases, so scores move by a few tenths even then), `changed` means the test set itself differs in size and the comparison is information, not a gate. Rows whose treebank has no allowed data are marked `not checked`. Compare like with like: Stanza and Trankit are raw-text end-to-end; UDPipe 2 also gives gold-tokenization rows.\n")
    o.append("The table lists UPOS / UFeats / Lemmas / UAS / LAS for each gating treebank (the treebank per language is in `docs/ud-licences.md`). Stanza: UD 2.12, raw. UDPipe 2: UD 2.17, raw and gold. Trankit large: UD 2.5, raw. `=` means the test split has the same size in r2.18 as in the source's release, `\u2260` that it differs, `-` that the source does not list the treebank.\n")
    o.append("| Language | Treebank | Stanza 2.12 | UDPipe 2 raw (2.17) | UDPipe 2 gold (2.17) | Trankit large (2.5) |")
    o.append("|---|---|---|---|---|---|")

    def cell(rs, **kw):
        for r in rs:
            if all(r.get(k) == v for k, v in kw.items()):
                nums = " / ".join(ref_val(r, c) for c in ("upos", "ufeats", "lemmas", "uas", "las"))
                mark = "=" if r["test_changed"] == "same size" else "\u2260" if r["test_changed"] == "changed" else "?"
                return f"{nums} {mark}"
        return "-"
    n_any = 0
    for l in langs:
        if not l["gating"]:
            continue
        rs = by.get(l["gating"], [])
        if rs:
            n_any += 1
        o.append(f"| {l['language'].replace('_', ' ')} | {l['gating'][3:]} | {cell(rs, system='Stanza')} | {cell(rs, system='UDPipe 2', input='raw')} | "
                 f"{cell(rs, system='UDPipe 2', input='gold')} | {cell(rs, system='Trankit', system_version='large')} |")
    nog = [l["gating"][3:] for l in langs if l["gating"] and not by.get(l["gating"])]
    o.append("")
    o.append(f"{n_any} of the {sum(1 for l in langs if l['gating'])} gating treebanks have published numbers from at least one source. "
             f"No source lists: {', '.join(nog)}. For these the accurate tier reports its own numbers with the bootstrap interval and no reference row (small treebanks, mostly under 100 training sentences, which the three systems do not cover).\n")
    n_ch = sum(1 for l in langs if l["gating"] for r in by.get(l["gating"], []) if r["test_changed"] == "changed")
    o.append(f"Of the rows on gating treebanks, {n_ch} are on a test split whose size differs from r2.18 (marked \u2260); most are Trankit's UD 2.5 and Stanza's UD 2.12 rows on treebanks that grew or were resplit since.\n")
    replace_region(os.path.join(docs, "references.md"), "all-languages", "\n".join(o))


def main():
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    d, docs = sys.argv[1:]
    cat, langs, refs = rows(os.path.join(d, "ud-treebanks.tsv")), rows(os.path.join(d, "ud-languages.tsv")), rows(os.path.join(d, "references.tsv"))
    licences_doc(cat, langs, docs)
    references_doc(cat, langs, refs, docs)
    shutil.copyfile(os.path.join(d, "references.tsv"), os.path.join(docs, "references.tsv"))


if __name__ == "__main__":
    main()
