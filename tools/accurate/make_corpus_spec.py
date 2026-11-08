#!/usr/bin/env python3
"""Writes recipes/accurate/common/corpus.json: the multilingual training corpus as data.

usage: make_corpus_spec.py <data-root> <out.json> [--catalogue tools/fetch/ud] [--cs src/OneNlp/ModelPackages.AccurateLanguages.g.cs]

One entry per language that has allowed training data (class `train+eval` in languages.tsv): its treebanks with
roles, the entity sets, the sizes counted from the fetched files, whether a development split has to be held out
of training, whether raw text needs a trained word segmenter, and the sentences that would leak the test set.
The recipes read this file; adding a language means fetching its data and re-running this script.

Rules (each is a field of the output, not hidden in code):
  - gating treebank: the one languages.tsv names; its test split is the language's test set (the nine languages
    keep the data and paths of the earlier recipe, extra treebanks unused so their rows stay comparable);
  - extra treebanks: every other `go` treebank with a training split, trained on, their dev splits joined;
    Japanese-GSDLUW is left out (the same sentences as Japanese-GSD, cut into other word units);
  - no dev split anywhere and at least 200 training sentences: every 10th training sentence is held out as dev
    (`devFromTrain`); fewer than 200: no dev, the model trains on all of it and selection ignores the language;
  - `leaks`: training sentences of other treebanks of the language whose `# text` also occurs in the gating test
    split; the reader drops them;
  - `segmenter`: `character-tagger` when the gating treebank's text is not separated by spaces (fewer than 0.3
    spaces per word) or its words contain spaces (more than 10% of words), so raw text needs a trained segmenter;
  - `small`: fewer than 500 training sentences in all (the sampler caps its repetition).
"""
import csv
import json
import os
import sys

NINE = {  # code -> (train dir, file prefix, test file) as the earlier recipe read them, relative to the data root
    "en": ("train/ud-en-ewt-r2.18", "en_ewt-ud", "eval/ud-en-ewt-r2.18/en_ewt-ud-test.conllu"),
    "de": ("train/ud/de", "de_gsd-ud", "eval/ud/de/de_gsd-ud-test.conllu"),
    "fr": ("train/ud/fr", "fr_gsd-ud", "eval/ud/fr/fr_gsd-ud-test.conllu"),
    "es": ("train/ud/es", "es_ancora-ud", "eval/ud/es/es_ancora-ud-test.conllu"),
    "pt": ("train/ud/pt", "pt_bosque-ud", "eval/ud/pt/pt_bosque-ud-test.conllu"),
    "it": ("train/ud/it", "it_gating-ud", "eval/ud/it/it_markit-ud-test.conllu"),
    "nl": ("train/ud/nl", "nl_alpino-ud", "eval/ud/nl/nl_alpino-ud-test.conllu"),
    "tr": ("train/ud/tr", "tr_boun-ud", "eval/ud/tr/tr_boun-ud-test.conllu"),
    "zh": ("train/ud/zh", "zh_gsdsimp-ud", "eval/ud/zh/zh_gsdsimp-ud-test.conllu"),
}
NINE_NAMES = {"en": "English", "de": "German", "fr": "French", "es": "Spanish", "pt": "Portuguese", "it": "Italian", "nl": "Dutch",
              "tr": "Turkish", "zh": "Chinese"}
# Entity sets: code -> [(name, train/dev dir, file prefix, test dir, licence)], relative to the data root.
NER_NINE = {
    "en": [("uner-en-ewt", "train/uner-en-ewt-8ed072de", "en_ewt-ud", "eval/uner-en-ewt-8ed072de", "CC BY-SA 4.0")],
    "de": [("germeval-2014", "train/ner/de/germeval-2014", "germeval-2014", "eval/ner/de/germeval-2014", "CC BY 4.0")],
    "es": [("wikiner-es-wp3", "train/ner/es/wikiner-es-wp3", "wikiner-es-wp3", "eval/ner/es/wikiner-es-wp3", "CC BY 4.0 (text CC BY-SA)")],
    "fr": [("wikiner-fr-wp3", "train/ner/fr/wikiner-fr-wp3", "wikiner-fr-wp3", "eval/ner/fr/wikiner-fr-wp3", "CC BY 4.0 (text CC BY-SA)")],
    "it": [("wikiner-it-wp3", "train/ner/it/wikiner-it-wp3", "wikiner-it-wp3", "eval/ner/it/wikiner-it-wp3", "CC BY 4.0 (text CC BY-SA)")],
    "nl": [("wikiner-nl-wp3", "train/ner/nl/wikiner-nl-wp3", "wikiner-nl-wp3", "eval/ner/nl/wikiner-nl-wp3", "CC BY 4.0 (text CC BY-SA)")],
    "tr": [("turkish-wikiner", "train/ner/tr/turkish-wikiner", "turkish-wikiner", "eval/ner/tr/turkish-wikiner", "CC BY-SA 4.0")],
    "pt": [("uner-pt-bosque", "train/uner/pt", "pt_bosque-ud", "eval/uner/pt", "CC BY-SA 4.0")],
    "zh": [("uner-zh-gsdsimp", "train/uner/zh", "zh_gsdsimp-ud", "eval/uner/zh", "CC BY-SA 4.0")],
}
UNER = "CC BY-SA 4.0"
NER_NEW = {
    "hr": [("uner-hr-set", "train/uner/hr", "hr_set-ud", "eval/uner/hr", UNER)],
    "da": [("uner-da-ddt", "train/uner/da", "da_ddt-ud", "eval/uner/da", UNER)],
    "id": [("uner-id-gsd", "train/uner/id", "id_gsd-ud", "eval/uner/id", UNER), ("uner-id-csui", "train/uner/id", "id_csui-ud", "eval/uner/id", UNER)],
    "no": [("uner-no-bokmaal", "train/uner/no", "nob_norne-ud", "eval/uner/no", UNER), ("uner-no-nynorsk", "train/uner/no", "nno_norne-ud", "eval/uner/no", UNER)],
    "sr": [("uner-sr-set", "train/uner/sr", "sr_set-ud", "eval/uner/sr", UNER)],
    "sk": [("uner-sk-snk", "train/uner/sk", "sk_snk-ud", "eval/uner/sk", UNER)],
    "sl": [("uner-sl-ssj", "train/uner/sl", "sl_ssj-ud", "eval/uner/sl", UNER)],
    "sv": [("uner-sv-talbanken", "train/uner/sv", "sv_talbanken-ud", "eval/uner/sv", UNER)],
    "pl": [("kpwr-ner-n82", "train/ner/pl/kpwr-ner-n82", "kpwr-ner-n82", "eval/ner/pl/kpwr-ner-n82", "CC BY 3.0")],
    "ro": [("ronec-2.0", "train/ner/ro/ronec-2.0", "ronec-2.0", "eval/ner/ro/ronec-2.0", "MIT")],
}
EXCLUDE = {"Japanese-GSDLUW": "the same sentences as Japanese-GSD, cut into other word units"}
MIN_DEV_FROM_TRAIN = 200
SMALL = 500


def count(path):
    """(sentences, words, texts) of a CoNLL-U or .iob2 file; words skip multiword-token ranges and empty nodes."""
    sents = words = spaces = spaced_words = 0
    texts = set()
    inside = False
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("# text = "):
                t = line[9:]
                texts.add(t)
                spaces += t.count(" ")
            elif line.startswith("#"):
                continue
            elif not line:
                if inside:
                    sents += 1
                inside = False
            else:
                c = line.split("\t")
                inside = True
                if len(c) > 1 and "-" not in c[0] and "." not in c[0]:
                    words += 1
                    if " " in c[1]:
                        spaced_words += 1
    if inside:
        sents += 1
    return sents, words, texts, spaces, spaced_words


def read(path):
    with open(path, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f, delimiter="\t"))


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    cs_path = sys.argv[sys.argv.index("--cs") + 1] if "--cs" in sys.argv else None
    if cs_path:
        args.remove(cs_path)
    cat_dir = sys.argv[sys.argv.index("--catalogue") + 1] if "--catalogue" in sys.argv else "tools/fetch/ud"
    if "--catalogue" in sys.argv:
        args.remove(cat_dir)
    root, out = args
    tbs = {r["repo"]: r for r in read(os.path.join(cat_dir, "treebanks.tsv"))}
    langs = [l for l in read(os.path.join(cat_dir, "languages.tsv")) if l["klass"] == "train+eval"]
    result = []
    for l in langs:
        gating = tbs[l["gating"]]
        code = gating["prefix"].split("_")[0]
        nine = None
        for c, name in NINE_NAMES.items():
            if name == l["language"]:
                nine, code = c, c
        entry = dict(code=code, name=l["language"].replace("_", " "), nine=nine is not None, treebanks=[], ner=[], evalOnly=[])
        members = [("gating", gating)]
        if nine is None:
            for x in [x for x in l["extra_train"].split(";") if x]:
                t = tbs["UD_" + x]
                if x in EXCLUDE:
                    entry.setdefault("left_out", []).append(dict(name=x, reason=EXCLUDE[x]))
                elif t["train_sentences"]:
                    members.append(("extra", t))
        test_texts = set()
        any_dev = False
        for role, t in members:
            if nine is not None:
                d, pre, test = NINE[nine]
                tr, dv, te = f"{d}/{pre}-train.conllu", f"{d}/{pre}-dev.conllu", test
            else:
                p = t["prefix"]
                tr, dv, te = f"train/ud/{p}/{p}-ud-train.conllu", f"train/ud/{p}/{p}-ud-dev.conllu", f"eval/ud/{p}/{p}-ud-test.conllu"
            row = dict(name=t["repo"][3:], role=role, licence=t["readme_licence"], train=tr, dev=None, test=None)
            sizes = {}
            for key, rel in (("train", tr), ("dev", dv), ("test", te)):
                path = os.path.join(root, rel)
                if not os.path.exists(path):
                    continue
                s, w, texts, sp, sw = count(path)
                sizes[key] = dict(sentences=s, words=w)
                if key != "train":
                    row[key] = rel
                if key == "test" and role == "gating":
                    test_texts = texts
                if key == "dev":
                    any_dev = any_dev or s > 0
                if key == "train" and role == "gating":
                    row["_spaces"], row["_spaced_words"] = sp / max(1, w), sw / max(1, w)
            if not os.path.exists(os.path.join(root, tr)):
                if role == "gating":
                    sys.exit(f"{t['repo']}: no training file {tr}")
                continue
            row["sizes"] = sizes
            entry["treebanks"].append(row)
        # sentences of non-gating treebanks that repeat a gating test sentence
        for row in entry["treebanks"]:
            if row["role"] != "gating":
                _, _, texts, _, _ = count(os.path.join(root, row["train"]))
                row["leaks"] = len(texts & test_texts)
        g = entry["treebanks"][0]
        spaces, spaced = g.pop("_spaces"), g.pop("_spaced_words")
        for row in entry["treebanks"][1:]:
            row.pop("_spaces", None)
            row.pop("_spaced_words", None)
        total = sum(r["sizes"]["train"]["sentences"] for r in entry["treebanks"])
        entry["trainSentences"] = total
        entry["trainWords"] = sum(r["sizes"]["train"]["words"] for r in entry["treebanks"])
        entry["devFromTrain"] = (not any_dev) and total >= MIN_DEV_FROM_TRAIN
        entry["hasDev"] = any_dev or entry["devFromTrain"]
        entry["small"] = total < SMALL
        entry["segmenter"] = "character-tagger" if (spaces < 0.3 or spaced > 0.10) else "none"
        entry["spacesPerWord"] = round(spaces, 3)
        entry["spacedWordFraction"] = round(spaced, 3)
        ner_sets = NER_NINE.get(code) if nine is not None else NER_NEW.get(code)
        for name, d, pre, ed, lic in ner_sets or []:
            sets = dict(name=name, licence=lic, train=f"{d}/{pre}-train.iob2")
            for key, rel in (("dev", f"{d}/{pre}-dev.iob2"), ("test", f"{ed}/{pre}-test.iob2")):
                if os.path.exists(os.path.join(root, rel)):
                    sets[key] = rel
            if not os.path.exists(os.path.join(root, sets["train"])):
                sys.exit(f"missing {sets['train']}")
            sets["sizes"] = {k: dict(sentences=count(os.path.join(root, sets[k]))[0], words=count(os.path.join(root, sets[k]))[1]) for k in ("train", "dev", "test") if k in sets}
            entry["ner"].append(sets)
        for x in [x for x in l["test_only"].split(";") if x]:
            p = tbs["UD_" + x]["prefix"]
            rel = f"eval/ud/{p}/{p}-ud-test.conllu"
            if os.path.exists(os.path.join(root, rel)):
                entry["evalOnly"].append(dict(name=x, test=rel, sentences=count(os.path.join(root, rel))[0]))
        result.append(entry)
    result.sort(key=lambda e: e["code"])
    doc = dict(release="r2.18", note="generated by tools/accurate/make_corpus_spec.py; see its header for the rules",
               minDevFromTrain=MIN_DEV_FROM_TRAIN, heldOutEvery=10, languages=result)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, indent=1, ensure_ascii=False)
        f.write("\n")
    if cs_path:
        codes = ", ".join(f'"{e["code"]}"' for e in result)
        distilled = ", ".join(f'"{e["code"]}"' for e in result if e["nine"])
        with open(cs_path, "w", encoding="utf-8", newline="\n") as f:
            f.write("// <auto-generated> by tools/accurate/make_corpus_spec.py from the accurate-tier training corpus. Do not edit. </auto-generated>\n"
                    "namespace RecursiveAI.OneNlp;\n\npublic static partial class ModelPackages\n{\n"
                    "    // The distilled package retains the original nine; the base-size package covers the full corpus.\n"
                    "    private static readonly HashSet<string> MultilingualAccurateLanguages = new(StringComparer.Ordinal)\n    {\n"
                    f"        {distilled},\n    }};\n\n"
                    "    private static readonly HashSet<string> LargeAccurateLanguages = new(StringComparer.Ordinal)\n    {\n"
                    f"        {codes},\n    }};\n}}\n")
    print(len(result), "languages;", sum(1 for e in result if e["segmenter"] != "none"), "need a segmenter;",
          sum(1 for e in result if e["small"]), "small;", sum(1 for e in result if e["ner"]), "with entities;",
          sum(1 for e in result if not e["hasDev"]), "without dev")


if __name__ == "__main__":
    main()
