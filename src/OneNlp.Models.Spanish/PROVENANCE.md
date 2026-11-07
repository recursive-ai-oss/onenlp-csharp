# Provenance: RecursiveAI.OneNlp.Models.Spanish

The package packs three saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by `recipes/es/fast` (commands `train-tagging`, `train-parser`, `ner-train`), which is in the source repository. Weights are not stored in git.

| File | Components | Data | Seed |
|---|---|---|---|
| `tagging.onenlp` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder with one window layer and one 64-unit bidirectional LSTM layer (ReLU, no layer normalization; `recipes/es/fast/PROVENANCE.md`) | UD Spanish-AnCora r2.18, train and dev | 1 |
| `parser.onenlp` | biaffine dependency parser with sentence starts, fast encoder and a one LSTM layer of 128 units per direction, arc 128, label 64, product label features (`docs/parser.md`)  | UD Spanish-AnCora r2.18, train and dev | 7 |
| `ner.onenlp` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG, MISC), fast encoder with one window layer and one 64-unit bidirectional LSTM layer | WikiNER Spanish wp3, train and dev, `del`, `al` and verbs with enclitics cut into the tokenizer's pieces (`ner-convert --pieces`) | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts. The tokenizer is the UAX #29 word tokenizer with the Spanish data (`docs/segmentation.md`).

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD Spanish-AnCora, `es_ancora-ud-train.conllu` | r2.18 | train | CC BY 4.0 | `47b6fc49d6ed48a016e7fa5ad9f5c7ecf3ec21e9db005df3ea14d10f07c6deb6` |
| UD Spanish-AnCora, `es_ancora-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY 4.0 | `1a132068062edf2654fb45b41cd4c59ba7e5c18e7c5f642ed5a9cc351fd9f0fe` |
| UD Spanish-AnCora, `es_ancora-ud-test.conllu` | r2.18 | eval only | CC BY 4.0 | `679f1c05ca654aa26801c80319df45f8f97606718fb534b91755b48f3e67fa6f` |
| WikiNER Spanish, `wikiner-es-wp3-train.iob2` (converted) | wp3 | train | CC BY 4.0 (text CC BY-SA) | `40720c77b9856888db2c191efb42abd49781bd8027e330e89c32e397aa1599bf` |
| WikiNER Spanish, `wikiner-es-wp3-dev.iob2` (converted) | wp3 | dev | CC BY 4.0 (text CC BY-SA) | `eeeed400ef4f9722d16529277eeb1af848775af449404bd747579a285be64d77` |
| WikiNER Spanish, `wikiner-es-wp3-test.iob2` (converted) | wp3 | eval only | CC BY 4.0 (text CC BY-SA) | `1d25596ad6f64e64dbfb86aa485b52a9df8a36b73500143e910f17506257c4c6` |

- **Share-alike lineage:** yes (the entity recognizer, through WikiNER's Wikipedia text). Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' annotations (WikiNER's are silver labels projected from Wikipedia links).
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read by `predict` and `ner-eval` and the scorer. 5 of the 1,721 AnCora test sentences also occur in the training file, and 69 of the 6,468 WikiNER test sentences in its training file, as short exact text overlaps; the official splits are kept (`tools/fetch/es/MANIFEST.md`).
- **Licence of the weights:** `MIT OR Apache-2.0`.
