# Provenance: spaCy 3.8.18 default-config baselines for ZH, FR, ES, PT, DE, IT, TR, HI, NL

Comparison baselines, not shipped. Trained with spaCy's released command-line tool on the same data our models of these languages use, with the configuration `spacy init config` writes by default. Format: `docs/provenance.md`. The English baseline is `tools/baselines/spacy/PROVENANCE.md`.

- **Model:** per language, up to two pipelines: (1) `tagger,morphologizer,trainable_lemmatizer,parser` on the language's UD treebank; (2) `ner` on its entity corpus. Both from `spacy init config --lang <lang> --optimize efficiency`, no pretrained vectors or transformers (`vectors = null` in every generated file). Hindi has only the NER pipeline, and Italian's UD pipeline trains on 897 sentences (see `docs/ud-licences.md`).
- **Recipe:** `tools/baselines/spacy/run_lang.sh` and the configs in `tools/baselines/spacy/lang/<lang>/` (`tagger_parser.cfg`, `ner.cfg`; hashes below). Only the data paths are overridden, except for the two `gold_preproc` runs of Chinese described below.
- **Seed(s):** spaCy's default `seed = 0` (training and data order); the splits are the official ones, or the fixed rules in `tools/fetch/<lang>/MANIFEST.md`; bootstrap seed 20261105.
- **Produced:** 2026-11-05. Apple M2 (8 cores, 8 GB, macOS 27.0.1, arm64); one core per job (spaCy's training loop uses one core), up to six jobs at a time, so wall times include contention. spaCy 3.8.18, thinc 8.3.14, numpy 2.5.4, Python 3.13.18; scorer `conll18_ud_eval.py` (sha256 `1072e02af00b1a56205b5e8216d51dee9b8944a104d80744afaccc78859fcb16`, run as a program) and seqeval 1.2.2 (MIT); intervals from `tools/OneNlp.Eval` (`ud` and `span`, 1000 resamples over sentences).
- **Weights:** not distributed; kept in the work directory.
- **SA lineage:** yes. Every treebank is CC BY-SA (AnCora is CC BY, MarkIT CC BY); UNER, Turkish-WikiNER are CC BY-SA; WikiNER, GermEval 2014 and the Naamapadam packaging are CC BY / CC0 (WikiNER's text is Wikipedia, CC BY-SA).
- **Licence of the weights:** not applicable (not released).

## Label generator

None: labels are the datasets' gold annotations (WikiNER and Naamapadam labels are silver, projected by their authors; they are inputs, not our generator).

## Pretrained components

None; trained from scratch (no vectors, no transformer).

## Deviations from the English recipe, each recorded

- **Chinese gold-token rows.** spaCy's default Chinese pipeline segments into single characters (`[nlp.tokenizer] segmenter = "char"`), and `spacy train` builds every training example by tokenizing the text with that tokenizer and aligning the gold annotation to it. A pipeline trained like that is evaluated on gold *words* at a disadvantage (UPOS 61.9, LAS 24.3 on GSDSimp). So Chinese was trained twice: once by default (its raw-text rows are the default-pipeline result: Tokens F1 39.4 because characters are not words) and once with `--corpora.train.gold_preproc true --corpora.dev.gold_preproc true` (a documented option of the training config), which trains and scores on gold words. The `gold_preproc` run is the reference for gold-token rows; it has no raw-text rows (a model trained on words cannot read the character tokens the default tokenizer produces).
- **Multiword tokens.** `spacy convert` trains on syntactic words (French `du` = `de le`), while spaCy's tokenizers keep surface tokens, which costs it in raw-text rows (Words F1 below Tokens F1) in French, Spanish, Portuguese and German.
- **XPOS** is `_` throughout some treebanks (Portuguese-Bosque and Portuguese-PUD have none); the XPOS and AllTags columns are then meaningless for that treebank (0.00 in the Portuguese tables).
- **Portuguese intervals.** OneNlp.Eval first rejected the Portuguese gold files (Bosque writes the contraction `à` as the words `a` + `a`, a one-character surface form with two words). The reader was fixed and the tables below carry 95% intervals; the point estimates equal the shared-task script's F1 values. Raw-text Words (88.5) is low because spaCy's tokenizer does not split contractions into their words.
- **Italian** trains on 897 sentences and uses MarkIT (grammar examples) beside ParlaMint; indicative only.
- **NER on silver corpora** (WikiNER, Naamapadam): scores measure agreement with automatically projected labels, not human judgement; they are comparable between systems on the same files only.

## Training data

Files, URLs, licences (quoted from each source's own licence file), sha256 and split sizes are in `tools/fetch/<lang>/MANIFEST.md`; the per-treebank licence verdicts are in `docs/ud-licences.md`. Converted training files (`spacy convert --converter conllu --n-sents 10` for UD; `prepare_ner.py` for entities):

| Language | UD train / dev `.spacy` | NER train / dev `.spacy` |
|---|---|---|
| zh | `778e5efc…`, `2b8a3581…` | `300be185…`, `430392dd…` |
| fr | `1ee3a60a…`, `94bca6ad…` | `b9d3d095…`, `8c709aa1…` |
| es | `cc62ed45…`, `f1494ea6…` | `f6514660…`, `614e3b36…` |
| pt | `521f54da…`, `6d220d59…` | `05c03458…`, `ecd90205…` |
| de | `1c9a4950…`, `d2ffae2b…` | `5d248389…`, `9399f99d…` |
| it | `7b81a630…`, `b5423d6b…` | `c728e5c9…`, `e739f6c8…` |
| tr | `6bc5c6c0…`, `251df1d6…` | `ecce0212…`, `a6f4bde6…` |
| hi | none | `72905232…`, `58ebccb4…` |
| nl | `9c251ad2…`, `0ab1ed6b…` | `3eca4039…`, `93e398c5…` |

Entity type maps used in training and scoring: German `PER, LOC, ORG, OTH→MISC` (derived and part variants → O); Turkish `PERSON→PER, ORG, GPE→LOC, LOC` (other types → O); the rest keep their corpus's types.

Generated configs (sha256 of `tagger_parser.cfg`, `ner.cfg`):

| Language | `tagger_parser.cfg` | `ner.cfg` |
|---|---|---|
| zh | `1fd14d8e2f4adfe5619ee209acaba5957e9116d100cf700f7377262b0571b712` | `9e7332b5928c8f4fdc6185cdbefe7776495fd6baab909b9ac219acc14acb8369` |
| fr | `ea71eecf846e19905b44612a2cef3e4bce10f2bef178189d576ae611ef69a280` | `237b6bf838dbb69cfae92469dfedb0f0f15da8c89034acef7bb3d4afa00c5efe` |
| es | `7c68fc70b7e6c5ed3d66ca63ed20b01c9065ef088742a0f734244c441ece5718` | `dbefe017bb91a5c6a9ecef4770a12d3b17dc925f31a431012096fab1d6bdfd39` |
| pt | `1da2b07a659ce28ff628d6ae8dee44043993edb28022ec758d6c68e4788e355f` | `33826d4341beec092983b73a4c8cf3714ac0432b1cccd24ba39cb4f94622f402` |
| de | `e5e0f5a507ff199b14086b9aabd4365ceb5b7b3cc9721be87daaa178f944d317` | `a3b9e7b9ce1f64f524a29db068c6ddd285f5de00ea2288799b71d871d6bd8f74` |
| it | `ec6ede9acca32efc991d0955cb549afb90c16c9a27cf11051c03a194ec8ec5fb` | `2963bb7d10ee8e77c1de1f9a49f9e518ad50f5909b4f0fc2b41defc6d911376b` |
| tr | `7862c2061398c0f73a52f5fae50422f9b81ed2b58f491f4752412ff9a57f3ad7` | `c4b81a6c2f6903ea427455241557d12365e082d1af804a5cb1cb276f4cdb5d23` |
| hi | none | `c914dca7257cd7f0cb0a2039e07a5d25c103f67b71ddc6204f4c1f643b88459e` |
| nl | `ada477167ee8896f1e9689b5bcebd62f59694f5d09607542581608b6a2c529ce` | `90b4002c6f3f85226418049751bdf32aaa135ed7552492f08d5623cf2b33b589` |

## Training runs

All on the Apple M2 above, one core each, in a 2-hour limit. Steps are the last step reached; spaCy stops early (patience 1600 steps) or at 20,000 steps. The last two columns are the dev score the final `model-best` had (UPOS / LAS for UD, entity F1 for NER). Peak resident memory is from `/usr/bin/time -l`.

| Language | Pipeline | Training data | Steps | Wall (m:s) | Peak RSS (MB) | Best dev |
|---|---|---|---|---|---|---|
| zh | UD (tagger, morphologizer, lemmatizer, parser) | Chinese-GSDSimp r2.18 | 3,400 (epoch 8) | 13:31 | 828 | 86.2 / 31.7 |
| zh | UD, gold_preproc | same | 9,000 (epoch 75) | 21:47 | 705 | 90.2 / 67.9 |
| zh | NER | UNER Chinese-GSDSIMP | 6,200 (epoch 29) | 7:37 | 727 | 55.1 |
| zh | NER, gold_preproc | same | 3,600 (epoch 21) | 2:38 | 528 | 60.1 |
| fr | UD | French-GSD r2.18 | 16,000 (epoch 11) | 57:19 | 1,460 | 94.3 / 82.9 |
| fr | NER | WikiNER French wp3 | 6,400 (epoch 1) | 12:17 | 2,962 | 78.9 |
| es | UD | Spanish-AnCora r2.18 | 13,800 (epoch 13) | 62:15 | 2,033 | 95.8 / 81.8 |
| es | NER | WikiNER Spanish wp3 | 14,800 (epoch 4) | 27:44 | 1,765 | 84.5 |
| pt | UD | Portuguese-Bosque r2.18 | 8,600 (epoch 23) | 36:23 | 1,132 | 88.4 / 69.7 |
| pt | NER | UNER Portuguese-Bosque | 4,400 (epoch 16) | 6:22 | 823 | 74.0 |
| de | UD | German-GSD r2.18 | 8,600 (epoch 19) | 41:30 | 773 | 92.9 / 76.3 |
| de | NER | GermEval 2014 | 5,200 (epoch 8) | 8:48 | 1,172 | 67.8 |
| it | UD | it_gating (ParlaMint + MarkIT) | 2,400 (epoch 26) | 8:22 | 604 | 88.0 / 65.6 |
| it | NER | WikiNER Italian wp3 | 12,600 (epoch 3) | 20:15 | 1,444 | 86.0 |
| tr | UD | Turkish-BOUN r2.18 | 5,600 (epoch 39) | 32:21 | 794 | 85.9 / 52.7 |
| tr | NER | Turkish-WikiNER | 6,800 (epoch 17) | 10:39 | 651 | 66.9 |
| hi | NER | Naamapadam Hindi (every 10th sentence) | 10,400 (epoch 4) | 15:05 | 1,739 | 73.6 |
| nl | UD | Dutch-Alpino r2.18 | 8,400 (epoch 32) | 39:57 | 767 | 95.7 / 80.9 |
| nl | NER | WikiNER Dutch wp3 | 15,400 (epoch 4) | 22:22 | 2,929 | 88.2 |

## Evaluation data

Test files, their sha256 and sizes are in `tools/fetch/<lang>/MANIFEST.md`. Gating test per language: zh GSDSimp (UNER GSDSIMP for entities), fr GSD, es AnCora, pt Bosque (UNER Bosque), de GSD (GermEval 2014), it ParlaMint, tr BOUN, nl Alpino, hi none (UD) / Naamapadam (entities). Second test sets: the language's PUD (Chinese PUD is Traditional script), Italian MarkIT.

## Train/test separation

- Manifests: `manifest-<lang>.json` written by `tools/fetch/<lang>/fetch.sh` (identical on the two machines that fetched). Hashes were recorded before any model saw the training side.
- Text overlap of the gating test with the training side (exact / folded): zh 0 / 0; fr GSD 1 / 1 of 416; es AnCora 5 / 5 of 1,721; pt Bosque 1 / 2 of 1,167; de GSD 27 / 28 of 977; it ParlaMint 13 / 13 of 375; tr BOUN 6 / 6 of 979; nl Alpino 0 / 0 of 596. Entity tests: fr WikiNER 32 / 33 of 5,723; es 69 / 70 of 6,468; it 87 / 88 of 6,234; nl 35 / 37 of 8,358; hi Naamapadam 53 / 53 of 864; pt Bosque 1 / 2 of 1,167; zh, de and tr 0 / 0. Left in; they are short fragments the corpora repeat. The UD test files were also checked against the entity corpora's training files (the other family): the only exact overlap is one pt Bosque sentence.
- spaCy's `model-best` selection used the dev files only. Test files were read only by the scoring commands after training.

## Results

Seqeval micro F1 (conlleval-style) from `score_ner.py` and the CoNLL 2018 script's tables are in the work directory's `scores-*.txt`; the tables below are OneNlp.Eval's strict values with 95% bootstrap intervals (1000 resamples over sentences, seed 20261105). Prediction files are listed with their sha256 in `tools/baselines/spacy/lang/SHA256SUMS` and are kept in the work directory (not committed). Regenerate: `run_lang.sh <lang> score-ud|score-ner`, then `score_lang.sh`, then `summarize.py`.

### Chinese

**zh: UD r2.18 zh_gsdsimp test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 39.43 [38.62, 40.26] | 46.93 [42.02, 51.81] | 39.43 [38.62, 40.26] | 34.92 [34.17, 35.68] | 35.69 [34.92, 36.45] | 38.38 [37.55, 39.17] | 34.29 [33.53, 35.10] | 39.43 [38.62, 40.26] | 9.12 [8.46, 9.80] | 8.59 [7.94, 9.27] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 61.94 [61.04, 62.84] | 63.16 [62.18, 64.16] | 94.25 [93.78, 94.68] | 58.67 [57.66, 59.64] | 99.33 [99.17, 99.51] | 33.08 [31.77, 34.30] | 24.32 [23.16, 25.37] |
| gold tokens, gold_preproc training | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 90.35 [89.67, 91.00] | 91.11 [90.45, 91.72] | 98.43 [98.20, 98.66] | 89.52 [88.78, 90.21] | 99.91 [99.85, 99.97] | 73.11 [71.57, 74.77] | 68.24 [66.61, 70.02] |

**zh: UD r2.18 zh_pud test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 37.69 [37.06, 38.26] | 51.69 [48.32, 55.02] | 37.69 [37.06, 38.26] | 26.53 [25.93, 27.05] | 29.61 [29.06, 30.14] | 35.16 [34.54, 35.74] | 24.64 [24.07, 25.15] | 37.69 [37.06, 38.26] | 4.60 [4.29, 4.93] | 3.42 [3.16, 3.69] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 54.36 [53.61, 55.13] | 58.52 [57.82, 59.19] | 92.05 [91.68, 92.39] | 49.43 [48.68, 50.20] | 98.56 [98.37, 98.76] | 26.59 [25.77, 27.45] | 16.83 [16.17, 17.55] |
| gold tokens, gold_preproc training | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 69.79 [69.06, 70.55] | 74.55 [73.92, 75.24] | 94.66 [94.32, 94.96] | 66.39 [65.68, 67.14] | 98.84 [98.67, 99.01] | 50.24 [49.07, 51.38] | 34.06 [32.97, 35.06] |

**zh: NER zh_gsdsimp-ud test, gold tokens, gold_preproc training**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 61.86 [57.46, 65.96] | 67.08 [61.81, 71.93] | 57.40 [52.58, 61.68] |

**zh: NER zh_gsdsimp-ud test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 13.30 [10.31, 16.40] | 36.69 [29.49, 43.95] | 8.13 [6.22, 10.20] |

**zh: NER zh_pud-ud test, gold tokens, gold_preproc training**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 34.41 [31.40, 37.25] | 38.66 [35.10, 42.18] | 30.99 [28.12, 33.79] |

**zh: NER zh_pud-ud test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 5.54 [3.74, 7.38] | 18.78 [13.00, 24.64] | 3.25 [2.17, 4.34] |

### French

**fr: UD r2.18 fr_gsd test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 98.85 [98.51, 99.16] | 91.82 [89.00, 94.25] | 94.64 [94.08, 95.22] | 91.80 [91.10, 92.51] | 0.00 [0.00, 0.00] | 91.16 [90.44, 91.90] | 0.00 [0.00, 0.00] | 92.20 [91.48, 92.93] | 81.64 [80.43, 82.84] | 78.10 [76.86, 79.35] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 97.07 [96.71, 97.46] | 0.00 [0.00, 0.00] | 96.41 [95.92, 96.87] | 0.00 [0.00, 0.00] | 97.49 [97.14, 97.84] | 89.10 [88.05, 90.10] | 85.98 [84.89, 87.11] |

**fr: UD r2.18 fr_pud test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.30 [99.13, 99.45] | 92.65 [90.49, 94.58] | 95.67 [95.32, 95.99] | 92.29 [91.87, 92.70] | 0.15 [0.09, 0.22] | 74.99 [74.42, 75.51] | 0.02 [0.00, 0.04] | 93.31 [92.90, 93.70] | 83.67 [82.90, 84.44] | 79.88 [79.10, 80.69] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 96.68 [96.41, 96.96] | 0.15 [0.09, 0.22] | 78.42 [77.85, 78.93] | 0.02 [0.00, 0.04] | 97.63 [97.44, 97.83] | 89.66 [89.07, 90.25] | 86.32 [85.70, 86.96] |

**fr: NER wikiner-fr-wp3 test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 75.50 [74.59, 76.44] | 76.60 [75.70, 77.57] | 74.43 [73.46, 75.42] |

### Spanish

**es: UD r2.18 es_ancora test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.90 [99.85, 99.94] | 97.70 [96.95, 98.43] | 96.60 [96.39, 96.82] | 94.81 [94.56, 95.05] | 85.55 [85.08, 86.01] | 94.57 [94.33, 94.82] | 85.05 [84.59, 85.51] | 95.37 [95.14, 95.60] | 84.87 [84.33, 85.40] | 81.82 [81.25, 82.40] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 98.18 [98.03, 98.31] | 84.97 [84.48, 85.45] | 97.74 [97.58, 97.88] | 84.47 [83.98, 84.95] | 98.72 [98.59, 98.83] | 89.04 [88.56, 89.52] | 85.95 [85.42, 86.47] |

**es: UD r2.18 es_pud test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.63 [99.50, 99.74] | 96.79 [95.60, 97.90] | 96.60 [96.27, 96.90] | 88.50 [88.01, 89.00] | 0.02 [0.00, 0.04] | 76.95 [76.30, 77.62] | 0.02 [0.00, 0.04] | 91.93 [91.45, 92.37] | 82.37 [81.49, 83.30] | 74.65 [73.79, 75.54] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 91.95 [91.54, 92.34] | 0.09 [0.04, 0.15] | 80.11 [79.52, 80.71] | 0.09 [0.04, 0.15] | 95.29 [94.95, 95.60] | 86.24 [85.45, 87.06] | 78.35 [77.57, 79.16] |

**es: NER wikiner-es-wp3 test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 83.35 [82.61, 84.15] | 83.98 [83.23, 84.77] | 82.72 [81.94, 83.53] |

### Portuguese
**pt: UD r2.18 pt_bosque test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.87 [99.80, 99.93] | 84.48 [81.84, 86.96] | 88.51 [88.05, 89.01] | 84.15 [83.64, 84.70] | 0.00 [0.00, 0.00] | 82.16 [81.62, 82.73] | 0.00 [0.00, 0.00] | 85.35 [84.84, 85.84] | 70.23 [69.37, 71.09] | 65.85 [64.97, 66.69] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 95.92 [95.64, 96.20] | 0.00 [0.00, 0.00] | 94.11 [93.79, 94.42] | 0.00 [0.00, 0.00] | 96.90 [96.68, 97.13] | 86.05 [85.44, 86.70] | 81.96 [81.29, 82.68] |

**pt: UD r2.18 pt_pud test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.37 [99.20, 99.51] | 83.93 [81.28, 86.68] | 89.52 [89.05, 90.00] | 79.52 [78.87, 80.21] | 0.11 [0.06, 0.17] | 56.10 [55.45, 56.75] | 0.11 [0.06, 0.17] | 85.83 [85.22, 86.40] | 68.96 [68.08, 69.94] | 61.85 [60.97, 62.89] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 90.31 [89.86, 90.79] | 0.11 [0.06, 0.17] | 62.26 [61.70, 62.83] | 0.11 [0.06, 0.17] | 96.40 [96.13, 96.70] | 82.24 [81.46, 83.00] | 75.46 [74.65, 76.31] |

**pt: NER pt_bosque-ud test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 75.27 [72.62, 77.85] | 76.96 [74.07, 79.61] | 73.66 [70.79, 76.38] |

**pt: NER pt_pud-ud test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 66.30 [63.53, 69.31] | 67.20 [64.42, 70.15] | 65.42 [62.40, 68.57] |

### German

**de: UD r2.18 de_gsd test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 98.03 [97.69, 98.37] | 78.73 [75.95, 81.33] | 95.51 [95.05, 95.97] | 90.13 [89.54, 90.73] | 90.63 [90.05, 91.20] | 80.11 [79.28, 80.92] | 76.75 [75.87, 77.62] | 91.28 [90.77, 91.85] | 75.30 [74.13, 76.41] | 70.77 [69.55, 71.89] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 94.59 [94.25, 94.97] | 95.21 [94.84, 95.56] | 84.71 [84.04, 85.40] | 81.29 [80.55, 82.06] | 95.65 [95.35, 96.00] | 81.88 [80.79, 82.91] | 77.31 [76.20, 78.37] |

**de: UD r2.18 de_pud test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 98.20 [97.91, 98.47] | 85.14 [82.37, 87.99] | 95.85 [95.48, 96.26] | 87.73 [87.15, 88.33] | 17.11 [16.68, 17.57] | 64.53 [63.78, 65.26] | 12.46 [12.05, 12.85] | 91.39 [90.93, 91.89] | 79.27 [78.26, 80.24] | 74.02 [72.96, 75.05] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 91.61 [91.14, 92.04] | 20.95 [20.32, 21.53] | 68.98 [68.28, 69.63] | 16.22 [15.66, 16.74] | 95.47 [95.15, 95.77] | 83.35 [82.37, 84.22] | 77.98 [76.93, 78.91] |

**de: NER germeval-2014 test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 65.77 [64.48, 67.18] | 70.67 [69.32, 72.15] | 61.50 [60.06, 63.10] |

### Italian

**it: UD r2.18 it_markit test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 95.76 [95.28, 96.22] | 91.62 [88.13, 94.89] | 88.66 [87.89, 89.42] | 81.37 [80.40, 82.35] | 81.56 [80.61, 82.63] | 81.05 [80.11, 82.06] | 76.35 [75.31, 77.44] | 73.64 [72.63, 74.74] | 62.66 [61.15, 64.19] | 56.71 [55.16, 58.24] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 92.20 [91.63, 92.76] | 92.54 [92.01, 93.08] | 90.11 [89.47, 90.73] | 85.48 [84.65, 86.24] | 83.80 [83.05, 84.58] | 75.39 [74.22, 76.69] | 69.27 [68.03, 70.57] |

**it: UD r2.18 it_parlamint test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.75 [99.60, 99.87] | 87.55 [83.56, 91.57] | 89.73 [88.91, 90.57] | 83.53 [82.62, 84.50] | 83.20 [82.28, 84.18] | 76.92 [75.92, 77.95] | 73.36 [72.40, 74.45] | 83.03 [82.13, 83.98] | 64.08 [62.60, 65.70] | 58.38 [57.00, 60.00] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 94.10 [93.58, 94.59] | 93.59 [93.04, 94.12] | 87.42 [86.71, 88.12] | 84.08 [83.29, 84.90] | 93.13 [92.61, 93.65] | 77.08 [75.90, 78.35] | 71.39 [70.15, 72.72] |

**it: UD r2.18 it_pud test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.71 [99.61, 99.81] | 79.96 [76.92, 82.85] | 89.61 [89.09, 90.10] | 77.83 [77.21, 78.42] | 2.41 [2.25, 2.59] | 48.06 [47.44, 48.65] | 2.41 [2.25, 2.59] | 79.43 [78.82, 80.05] | 60.70 [59.74, 61.70] | 54.01 [53.01, 54.97] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 88.67 [88.24, 89.12] | 2.39 [2.23, 2.57] | 58.71 [58.03, 59.33] | 2.38 [2.22, 2.57] | 89.93 [89.53, 90.31] | 73.91 [73.08, 74.81] | 67.41 [66.49, 68.32] |

**it: NER wikiner-it-wp3 test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 83.54 [82.84, 84.27] | 83.89 [83.14, 84.65] | 83.20 [82.47, 83.95] |

### Turkish

**tr: UD r2.18 tr_boun test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.42 [99.25, 99.59] | 82.09 [79.26, 84.64] | 97.06 [96.69, 97.41] | 86.82 [86.12, 87.52] | 38.26 [37.43, 39.10] | 77.20 [76.45, 77.99] | 34.52 [33.71, 35.31] | 78.83 [78.07, 79.54] | 63.96 [62.51, 65.41] | 55.86 [54.48, 57.25] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 88.04 [87.40, 88.68] | 38.59 [37.75, 39.41] | 78.30 [77.53, 79.11] | 34.70 [33.86, 35.52] | 80.24 [79.54, 80.94] | 67.44 [65.99, 68.67] | 57.87 [56.53, 59.13] |

**tr: UD r2.18 tr_pud test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.75 [99.63, 99.84] | 94.20 [92.38, 95.90] | 96.64 [96.30, 96.99] | 78.98 [78.29, 79.71] | 9.28 [8.72, 9.88] | 23.07 [22.57, 23.59] | 2.90 [2.67, 3.14] | 78.55 [77.92, 79.23] | 60.12 [58.99, 61.28] | 50.18 [49.13, 51.26] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 80.08 [79.40, 80.76] | 9.41 [8.87, 9.99] | 24.20 [23.67, 24.76] | 2.88 [2.65, 3.12] | 81.11 [80.53, 81.75] | 62.62 [61.50, 63.62] | 51.21 [50.21, 52.19] |

**tr: NER turkish-wikiner test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 65.82 [63.20, 68.35] | 68.49 [65.67, 71.30] | 63.36 [60.61, 66.17] |

### Hindi

**hi: NER naamapadam-hi test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 78.39 [76.38, 80.42] | 78.84 [76.54, 81.10] | 77.94 [75.86, 80.04] |

### Dutch

**nl: UD r2.18 nl_alpino test**

| Input | Tokens | Sentences | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|
| raw text | 99.82 [99.70, 99.93] | 89.60 [86.97, 92.28] | 99.82 [99.70, 99.93] | 94.36 [93.72, 94.97] | 91.63 [90.98, 92.29] | 92.97 [92.32, 93.60] | 90.39 [89.69, 91.14] | 91.65 [91.05, 92.19] | 83.70 [82.53, 84.92] | 79.57 [78.24, 80.89] |
| gold tokens | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 94.42 [93.76, 95.04] | 91.73 [91.07, 92.40] | 93.13 [92.48, 93.74] | 90.51 [89.81, 91.27] | 91.83 [91.23, 92.35] | 84.09 [82.93, 85.30] | 79.85 [78.57, 81.11] |

**nl: NER wikiner-nl-wp3 test, gold tokens**

| micro F1 | micro precision | micro recall |
|---|---|---|
| 86.21 [85.66, 86.75] | 85.96 [85.38, 86.52] | 86.46 [85.87, 87.03] |

## Notes

- Raw-text rows are limited by spaCy's own tokenization and its parser's sentence splits; gold-token rows isolate the trained components. Gate each model against the same mode.
- Reference scores from other systems (Stanza, UDPipe 2, Trankit) are in `docs/references.md`, with the release mismatch noted there. Where spaCy's number and a published number differ by more than the interval, the published number is on another UD release.
- Hindi has no UD pipeline row; it needs training data that the licence rules allow (`docs/ud-licences.md`).
