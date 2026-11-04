# Provenance: spaCy 3.8.18 default-config baselines (English-EWT, UNER-EWT)

Comparison baselines, not shipped. Trained with spaCy's released command-line tool on the same data our English models use. Format: `docs/provenance.md`.

- **Model:** two pipelines. (1) `tagger,morphologizer,trainable_lemmatizer,parser` on UD English-EWT; (2) `ner` on UNER English-EWT. Both from `spacy init config --lang en --optimize efficiency`, no pretrained vectors or transformers.
- **Recipe:** `tools/baselines/spacy/` (`run.sh`, the two `.cfg` files). Config hashes: `tagger_parser.cfg` `0e6bdce9ef61afcd8e9f4e92ad4c81a4b96ebf8560e6806f7e7ca3635719dc68`, `ner.cfg` `a10ef56c6562a685251ab9e428e10434e38cbd88070cdb88394ae8fc8c0dade0`. Only the data paths are overridden.
- **Seed(s):** spaCy's default `seed = 0` (training and data order); the data splits are the official ones.
- **Produced:** 2026-11-04; AMD Ryzen 9 7950X3D (16 cores, 32 threads), shared with other jobs (load average about 12 to 17 while training); spaCy 3.8.18, thinc 8.3.14, numpy 2.5.4, Python 3.13.17; spaCy's training loop used one core (about 100% CPU). Training used 8 reserved cores (parser pipeline) and 4 (NER) in the job queue.
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

| Pipeline | Queue job | Wall time | Stopped | Best dev | Notes |
|---|---|---|---|---|---|
| tagger, morphologizer, trainable_lemmatizer, parser | queue job 3364 | 30 min (cap) | killed by the 30-minute queue limit at step 7,400 of `max_steps` 20,000 (epoch 24); about 0.24 s per step, so a full run is about 80 minutes at this load | step 7,400: tag 93.26, pos 94.04, morph 94.42, lemma 94.94, UAS 83.34, LAS 79.52, sents F 75.44 (spaCy dev scores) | **Truncated**: dev score still rising slowly (LAS 77.5 at step 3,000, 79.5 at 7,400); the default `patience` had not triggered. Not resumed: spaCy 3.8.18's `spacy train` has no resume that keeps optimizer state and step counters, and re-sourcing components would change the config. |
| ner | queue job 3365 | 7 min 50 s | early stopping (`patience` 1,600) at step 4,800 | step 3,200: spaCy dev ents_f 61.0 (P 67.1, R 55.8) | complete default run; RSS 659 MB |

## Results (UD English-EWT r2.18 test, the truncated parser run; UNER test)

Scores are the CoNLL 2018 script's F1 (`AligndAcc` column in the `scores-*.txt` files); NER is seqeval entity micro-F1. Prediction files are kept in the work directory of the run (hashes below). No confidence intervals yet (they need the Scorer).

| Row | Raw text (spaCy tokenizer + parser sentences) | Gold tokens and sentences |
|---|---|---|
| Tokens | 95.70 | 100 |
| Sentences | 71.02 | 100 |
| Words | 97.48 | 100 |
| UPOS | 92.32 | 94.27 |
| XPOS | 91.53 | 93.24 |
| UFeats | 93.32 | 95.16 |
| Lemmas | 93.62 | 95.70 |
| UAS | 81.36 | 85.23 |
| LAS | 78.41 | 81.76 |

| NER (UNER English-EWT test, gold tokens) | Precision | Recall | F1 |
|---|---|---|---|
| LOC | 73.58 | 69.40 | 71.43 |
| ORG | 50.60 | 39.44 | 44.33 |
| PER | 75.20 | 62.14 | 68.05 |
| micro | 67.97 | 57.54 | **62.32** |

Files (sha256): `pred-ud-raw.conllu` `3e187896…2f2`, `pred-ud-gold.conllu` `8310095b…8ca`, `pred-ner.tsv` `a37fa9d8…303`; regenerate with `run.sh score-ud` and `run.sh score-ner`.

## Notes

- The raw-text rows are limited by spaCy's English tokenizer against EWT conventions (Tokens 95.70); the gold-token rows isolate the trained components.
- The parser pipeline numbers are for a model trained for 37% of the default step budget. A gate that cites them must say so until a full run exists.
