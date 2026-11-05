# Provenance: fr-fast (French fast tier)

- **Model:** three saved pipelines on the fast hash-embedding encoder (`FastEncoder.English()` configuration, unchanged): `tagging.onenlp` (sentencizer, morphologizer for UPOS and UFeats, edit-tree lemmatizer), `parser.onenlp` (biaffine parser with sentence starts, two-layer BiLSTM) and `ner.onenlp` (BILUO tagging with constrained decoding: PER, LOC, ORG, MISC). All use the French word tokenizer data (`docs/segmentation.md`). French-GSD has no XPOS, so there is no tagger head.
- **Recipe:** `recipes/fr/fast-tagging`, `recipes/fr/parser`, `recipes/fr/ner` (adapted from the English recipes; README beside them).
- **Seed(s):** tagging 1; parser 7; NER 7 (training and data order; encoder initial weights are drawn from the same seed). Dev sets are the official UD dev file and our WikiNER dev split, so there is no split seed other than the article-index rule in `tools/fetch/fr/MANIFEST.md`.
- **Produced:** 2026-11-05; shared 32-core worker, 8 training threads; .NET SDK 10.0.403, RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0. Training time (excluding queue waits): tagging 20 epochs in 18.8 min (one chunk); parser 18 epochs in two chunks, about 40 min; NER 20 epochs in six chunks of at most 30 min, about 2.5 h. Early stopping with patience 6 on dev stopped all three; the best dev weights were restored.
- **Weights:** `weights.manifest.props` of `src/OneNlp.Models.French` (tagging 36,881,404 bytes sha256 `412f2e57…`, parser 22,574,800 bytes `3d19adad…`, NER 35,681,975 bytes `08875648…`). Kept out of git; on the worker under `~/onenlp/models/fr-fast/u26/pack/`.
- **SA lineage:** yes; UD French-GSD (CC BY-SA 4.0) and WikiNER French (labels CC BY 4.0, text from Wikipedia, CC BY-SA). Credit is in the package's `THIRD-PARTY-NOTICES.md`.
- **Licence of the weights:** MIT OR Apache-2.0.

## Training data

| Dataset | Release | Role | File | Licence | sha256 |
|---|---|---|---|---|---|
| UD French-GSD | r2.18 | train | `fr_gsd-ud-train.conllu` | CC BY-SA 4.0 | `4b9a87b1b7e6e7003219df1d3ccca87ffaca0f00a6055b12b1857df5373589d2` |
| UD French-GSD | r2.18 | dev | `fr_gsd-ud-dev.conllu` | CC BY-SA 4.0 | `9221e5084cc6a1b1540671cfbd2fc0456efc223b2fe8dd2146c2bad921be02d4` |
| WikiNER French wp3 | figshare 5462500 | train (119,460 sentences) | `wikiner-fr-wp3-train.iob2` | CC BY 4.0 (text: Wikipedia, CC BY-SA) | `6c3bbfae6dc8d4ea22298140c776791b9dacea8802a9e069a72deaf5d55d8ab7` |
| WikiNER French wp3 | same | dev (1,769 sentences) | `wikiner-fr-wp3-dev.iob2` | same | `112116cdf110fe89fd3caa7868ce913b61c12c97f8df2b633fc76286624674a9` |

Sequoia and FQB (allowed with credit) were not used; the GSD test file is the gate and no dev result called for more data. Their absence is not a licence question.

## Label generator

None: labels are the datasets' gold (UD) and silver (WikiNER, projected from Wikipedia links) annotations. The tokenizer's list of words after which `des` is the plural article is counted from the training split only (`tools/fetch/fr/des_context.py`); `et` was added to it after the development split showed 13 article against 8 contraction readings (train: 91 against 92).

## Pretrained components

None: trained from scratch.

## Evaluation data

| Dataset | Release | Role | File | sha256 | Used for |
|---|---|---|---|---|---|
| UD French-GSD | r2.18 | eval | `fr_gsd-ud-test.conllu` | `eee5a599b429658b6ee8582fae9993eb07161247fc64bad57d16b2050ed4eb1a` | the UD rows below, read once for the final models |
| WikiNER French wp3 | same | eval (5,723 sentences) | `wikiner-fr-wp3-test.iob2` | `bcabfe5c4120855d389b8013663faa8b24c08a687cfa5e946b69138812ce870f` | the NER rows below |

## Train/test separation

- Manifest: `tools/fetch/fr/MANIFEST.md`. Text-level overlap: one GSD test sentence also occurs in the GSD training text; 32 (33 folded) WikiNER test sentences also occur in its training text; they are short fragments and the splits are kept.
- Early stopping and every setting were chosen on the dev files. The tokenizer data was tuned on the GSD dev split. The test files were read once, after the models were final.

## Results

UD French-GSD r2.18 test. Paired bootstrap 95% CI over sentences (1,000 resamples, seed 1) from `tools/OneNlp.Eval`; reference: spaCy 3.8.18 trained on the same data with its default configuration (`tools/baselines/spacy/lang/PROVENANCE.md`), same input mode. "Within noise" means the intervals overlap. Raw text is our tokenizer with the multiword tokens written as CoNLL-U range lines; spaCy keeps `du` as one token, which costs it Words and every row after it. UPOS, UFeats and Lemmas come from `tagging.onenlp`, UAS, LAS and Sentences (parser) from `parser.onenlp`. French-GSD has no XPOS, so there are no XPOS or AllTags rows.

| Mode | Metric | Ours [95% CI] | spaCy 3.8.18 [95% CI] | Within noise |
|---|---|---|---|---|
| gold tokens and sentences | UPOS | 96.77 [96.33, 97.19] | 97.07 [96.68, 97.47] | yes |
| | UFeats | 96.34 [95.89, 96.81] | 96.41 [95.91, 96.88] | yes |
| | Lemmas | 97.51 [97.16, 97.84] | 97.49 [97.14, 97.84] | yes |
| | UAS | 90.22 [89.21, 91.14] | 89.10 [88.01, 90.11] | yes |
| | LAS | 86.69 [85.60, 87.81] | 85.98 [84.80, 87.12] | yes |
| raw text | Tokens | 99.88 [99.78, 99.96] | 98.85 [98.52, 99.18] | ours higher |
| | Words | 99.48 [99.30, 99.65] | 94.64 [94.06, 95.26] | ours higher |
| | Sentences (parser) | 94.79 [92.66, 96.71] | 91.82 [89.11, 94.33] | yes |
| | Sentences (rule sentencizer) | 90.64 [87.82, 93.27] | 91.82 [89.11, 94.33] | yes |
| | UPOS | 96.06 [95.60, 96.53] | 91.80 [91.07, 92.55] | ours higher |
| | UFeats | 95.76 [95.26, 96.27] | 91.16 [90.42, 91.94] | ours higher |
| | Lemmas | 96.77 [96.37, 97.19] | 92.20 [91.50, 92.94] | ours higher |
| | UAS | 89.48 [88.42, 90.42] | 81.64 [80.41, 82.87] | ours higher |
| | LAS | 85.97 [84.73, 87.08] | 78.10 [76.77, 79.43] | ours higher |

WikiNER French wp3, our test articles (article index mod 20 = 0), gold tokens, strict span F1; reference spaCy 3.8.18 on the same data:

| Metric (gold tokens) | Ours [95% CI] | spaCy 3.8.18 [95% CI] | Paired difference [95% CI] |
|---|---|---|---|
| micro F1 | 82.48 [81.74, 83.28] | 75.50 [74.68, 76.46] | +6.97 [6.12, 7.78] |
| micro precision | 82.61 [81.81, 83.46] | 76.60 [75.72, 77.61] | +6.01 [5.07, 6.84] |
| micro recall | 82.34 [81.62, 83.15] | 74.43 [73.58, 75.39] | +7.91 [7.06, 8.72] |
| F1 LOC | 82.79 [81.75, 83.92] | 76.27 [75.03, 77.56] | +6.53 [5.39, 7.65] |
| F1 MISC | 70.25 [68.12, 72.38] | 59.38 [56.96, 61.70] | +10.87 [8.45, 13.03] |
| F1 ORG | 78.45 [76.30, 80.76] | 68.00 [65.19, 70.66] | +10.46 [8.04, 12.79] |
| F1 PER | 89.39 [88.47, 90.34] | 84.42 [83.34, 85.50] | +4.97 [3.98, 6.00] |

The published WikiNER numbers (Stanza 92.9, Trankit 92.3 to 92.9) are on another split and are information only (`docs/references.md`). Dev scores (for reference): tagging mean of UPOS, UFeats and Lemmas 97.12; parser LAS 89.56 (gold tokens); NER F1 83.49.

## Notes

- The three models have separate encoders, as in English. A shared encoder is a later change.
- C#/PyTorch training-time ratio: the architectures are the English ones (tagging 1.7x, morphologizer 1.5x, lemmatizer 0.47x, parser 1.14x, NER 1.25x), so the French recipes were not timed again.
- `Words` and the tag rows on raw text depend on the contraction pieces: `des` is cut by the word before it (87% right on dev), the others always. Words 99.48 on test.
