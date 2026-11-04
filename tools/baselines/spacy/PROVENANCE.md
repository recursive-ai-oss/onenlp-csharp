# Provenance: spaCy 3.8.18 default-config baselines (English-EWT, UNER-EWT)

Comparison baselines, not shipped. Trained with spaCy's released command-line tool on the same data our English models use. Format: `docs/provenance.md`.

- **Model:** two pipelines. (1) `tagger,morphologizer,trainable_lemmatizer,parser` on UD English-EWT; (2) `ner` on UNER English-EWT. Both from `spacy init config --lang en --optimize efficiency`, no pretrained vectors or transformers.
- **Recipe:** `tools/baselines/spacy/` (`run.sh`, the two `.cfg` files). Config hashes: `tagger_parser.cfg` `0e6bdce9ef61afcd8e9f4e92ad4c81a4b96ebf8560e6806f7e7ca3635719dc68`, `ner.cfg` `a10ef56c6562a685251ab9e428e10434e38cbd88070cdb88394ae8fc8c0dade0`. Only the data paths are overridden.
- **Seed(s):** spaCy's default `seed = 0` (training and data order); the data splits are the official ones.
- **Produced:** 2026-11-04. Parser pipeline: Apple M2 (arm64, macOS), one core; NER: AMD Ryzen 9 7950X3D (shared Linux machine). spaCy 3.8.18, thinc 8.3.14, numpy 2.5.4, Python 3.13 on both. spaCy's training loop uses one core; the queue reservations are in the table below.
- **Weights:** not distributed; kept in the work directory (`model-ud/model-best`, `model-ner/model-best` under the work directory).
- **SA lineage:** yes. UD English-EWT and UNER English-EWT are CC BY-SA 4.0.
- **Licence of the weights:** not applicable (not released).

## Training data

| Dataset | Release | Role | File | Licence | URL | sha256 |
|---|---|---|---|---|---|---|
| UD English-EWT | r2.18 | train | `en_ewt-ud-train.conllu` | CC BY-SA 4.0 (README: "licensed under a Creative Commons Attribution-ShareAlike 4.0 International License") | UniversalDependencies/UD_English-EWT, tag r2.18 | `d68e06122a702464c613076523d56740f047e5bbe89dd90ec32737e04d952143` |
| UD English-EWT | r2.18 | dev | `en_ewt-ud-dev.conllu` | same | same | `39239e0a60db3ae68f4b7036189f11b6692741d10ff8240dd91f74f2760d90f8` |
| UNER English-EWT | commit `8ed072de` (Universal NER v1 data) | train | `en_ewt-ud-train.iob2` | CC BY-SA 4.0 (`LICENSE.txt`) | UniversalNER/UNER_English-EWT | `647b296624433d2b0f304f81974404fb1d5ed1520cc05649b4646f5777a5497d` |
| UNER English-EWT | same | dev | `en_ewt-ud-dev.iob2` | same | same | `0cafd881cf31d39897b6d588420fab8fbbe4423d271725c59921ac9dd49c0713` |

Converted training files: `ud/en_ewt-ud-train.spacy` `89a07fd6…455fa`, `ud/en_ewt-ud-dev.spacy` `acaf2fbc…8086`, `ner/train.spacy` `00526e0c…7009`, `ner/dev.spacy` `12bb42e9…c5`.

## Label generator

None: labels are the datasets' gold annotations.

## Pretrained components

None; trained from scratch (no vectors, no transformer).

## Evaluation data

| Dataset | Release | Role | File | sha256 | Used for |
|---|---|---|---|---|---|
| UD English-EWT | r2.18 | eval | `en_ewt-ud-test.conllu` | `fa024f43dc5da3c5ac02563bc9bd0e974f46cbb1560823976a8f342a37dc494a` | tokens, sentences, UPOS, XPOS, UFeats, lemmas, UAS, LAS |
| UNER English-EWT | same | eval | `en_ewt-ud-test.iob2` | `821ad826d5f503ac03b4edc3fbc2174f21fbb005ec25863caf2051c434616d6e` | entity F1 |

## Train/test separation

- Manifest: `manifest-en.json` written by `tools/fetch/en/fetch.sh`; hashes in `tools/fetch/en/MANIFEST.md`.
- Text overlap: 114 of 2,077 test sentences have identical text in train or dev (127 after folding case and whitespace), mostly one- and two-word fragments; left in. Details in `MANIFEST.md`.
- spaCy's own model selection (`model-best`) uses the dev file only. The test files were read only by the scoring commands after training.

## Training runs

| Pipeline | Machine, queue job | Wall time | Stopped | Best dev | Notes |
|---|---|---|---|---|---|
| tagger, morphologizer, trainable_lemmatizer, parser (**the baseline**) | Apple M2 (arm64, 8 cores, macOS), one core (`THREADS=1`); queue job 595, `--class heavy --cores 1 --max 2h`; spaCy 3.8.18 self-reported | 1,598.9 s (26 min 39 s; user 1,590.6 s, peak RSS 1.25 GB) | early stopping (`patience` 1,600) at step 10,400, epoch 37, of `max_steps` 20,000 | step 8,800 (`model-best`): tag 93.17, pos 93.94, morph 94.32, lemma 94.95, UAS 83.35, LAS 79.57, sents F 75.93 (spaCy dev scores) | complete default run, one uninterrupted job; the config is `tagger_parser.cfg` unchanged, converted data as above (same hashes as the Linux run) |
| ner | x86-64 Linux, AMD Ryzen 9 7950X3D, queue job 3365 | 7 min 50 s | early stopping at step 4,800 | step 3,200: dev ents_f 61.0 (P 67.1, R 55.8) | complete default run; RSS 659 MB |
| parser pipeline, first attempt (superseded) | x86-64 Linux, queue job 3364 | 30 min (cap) | killed by the 30-minute queue limit at step 7,400 | LAS 79.52 | superseded by the arm64 run above; it needed over 30 minutes only because the shared worker was loaded |

The step reached by `model-best` is recorded in its `meta.json`; spaCy keeps training until patience runs out, so the final step is 1,600 past it.

Data and scorer on the arm64 machine were fetched by `tools/fetch/en/fetch.sh` and the six data hashes were verified against `tools/fetch/en/MANIFEST.md` (all match). The converted `.spacy` files have the same hashes as on Linux.

## Results (UD English-EWT r2.18 test; UNER test)

Scores are the CoNLL 2018 script's F1 (`AligndAcc` is in the `scores-*.txt` files); NER is seqeval entity micro-F1. Intervals are 95% paired-bootstrap percentile intervals over sentences from `tools/OneNlp.Eval` (1,000 resamples, seed 1); the point scores equal the CoNLL script's.

| Row | Raw text (spaCy tokenizer + parser sentences) | Gold tokens and sentences |
|---|---|---|
| Tokens | 95.70 [95.14, 96.21] | 100 |
| Sentences | 72.66 [70.48, 74.75] | 100 |
| Words | 97.48 [96.98, 97.94] | 100 |
| UPOS | 92.45 [91.84, 93.06] | 94.29 [93.89, 94.66] |
| XPOS | 91.72 [91.09, 92.34] | 93.43 [93.00, 93.82] |
| UFeats | 93.37 [92.81, 93.92] | 95.18 [94.88, 95.45] |
| Lemmas | 93.69 [93.08, 94.29] | 95.80 [95.42, 96.13] |
| UAS | 81.34 [80.25, 82.37] | 85.40 [84.50, 86.26] |
| LAS | 78.53 [77.46, 79.55] | 81.96 [81.03, 82.83] |

| NER (UNER English-EWT test, gold tokens) | Score (95% CI) |
|---|---|
| micro F1 | **62.32** [59.45, 65.20] |
| micro precision | 67.97 [64.78, 71.35] |
| micro recall | 57.54 [54.25, 60.61] |
| LOC / ORG / PER F1 (seqeval) | 71.43 / 44.33 / 68.05 (no intervals) |

Prediction files (sha256): `pred-ud-raw.conllu` `c1bddcfb076aa76dcfa927355bdbf41f2af0432bc9bb77b822306f644bb839a6`, `pred-ud-gold.conllu` `ed5eeeef0c70090556699a24bd9c370dd40cc91a6069dcad47de6a7e8c774fff`, `pred-ner.tsv` `a37fa9d8a613967e31cf460918094326b380ea319c43e82dfef9c527c8782303`. Regenerate with `run.sh score-ud` and `run.sh score-ner`.

## Notes

- The raw-text rows are limited by spaCy's English tokenizer against EWT conventions (Tokens 95.70); the gold-token rows isolate the trained components.
- The parser baseline ran to spaCy's own stopping point. It was trained on one core of an arm64 machine; the optimizer is deterministic given the seed, but numeric differences between machines can move scores slightly.
