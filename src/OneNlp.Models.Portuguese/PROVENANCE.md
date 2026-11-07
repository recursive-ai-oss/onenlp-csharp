# Provenance: RecursiveAI.OneNlp.Models.Portuguese

The package packs three saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by `recipes/pt/fast` (commands `train-tagging`, `train-parser`, `ner-train`), which is in the source repository. Weights are not stored in git.

| File | Components | Data | Seed |
|---|---|---|---|
| `tagging.onenlp` | sentencizer, morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder (ReLU, one window layer, one bidirectional LSTM layer of 64 units per direction; `recipes/pt/fast/PROVENANCE.md`) | UD Portuguese-Bosque r2.18, train and dev | 1 |
| `parser.onenlp` | biaffine dependency parser with sentence starts, fast encoder and a one LSTM layer of 128 units per direction, arc 128, label 64, product label features (`docs/parser.md`)  | UD Portuguese-Bosque r2.18, train and dev | 7 |
| `ner.onenlp` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG), fast encoder with one window layer and one 64-unit bidirectional LSTM layer | Universal NER Portuguese-Bosque, train and dev | 7 |

The treebank has no XPOS column, so there is no tagger and `Token.Tag` is empty. The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts. The tokenizer is the UAX #29 word tokenizer with the Portuguese data (`docs/segmentation.md`). The training files are the treebank rewritten by `recipes/pt/fast prepare` (the tokens the tokenizer makes), and the entity files are the Universal NER annotation carried onto those tokens (`ner-convert`).

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD Portuguese-Bosque, `pt_bosque-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `b56d7124af821e2968302c38298904ec67af342304a33ae8a9dc4fa57df89648` |
| UD Portuguese-Bosque, `pt_bosque-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `f8a67abae12fbab85a3995204a6459d1958c11065062d4ea5349d6b914e78f81` |
| UD Portuguese-Bosque, `pt_bosque-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `9a824650b7a02cf411f6e09b39e8fb423c85e1a5fb3a55b87e7f4c0c2ea5b3bb` |
| Universal NER Portuguese-Bosque, `pt_bosque-ud-train.iob2` | commit f2c2ad67 | train | CC BY-SA 4.0 | `1d8762040667fc15f4ec83801e20d14d10ebea2b90c16c312f3ba03da2d1f860` |
| Universal NER Portuguese-Bosque, `pt_bosque-ud-dev.iob2` | commit f2c2ad67 | dev | CC BY-SA 4.0 | `0e6cb1db280e6677e2fd4380d695039791b5cb70bb5100376166aae5690da01a` |
| Universal NER Portuguese-Bosque, `pt_bosque-ud-test.iob2` | commit f2c2ad67 | eval only | CC BY-SA 4.0 | `45cf232ff7677cb6f4020dad96dbf8412d1f2cca38d81164ac96ee8c03b6b09f` |

- **Share-alike lineage:** yes (UD Portuguese-Bosque, Universal NER). Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' gold annotations.
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read by `predict` and `ner-eval` and the scorer. One Bosque test sentence also occurs in the training file as exact text (two folded); the official split is kept (`tools/fetch/pt/MANIFEST.md`).
- **Licence of the weights:** `MIT OR Apache-2.0`.
