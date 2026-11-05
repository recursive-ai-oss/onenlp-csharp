# Provenance: RecursiveAI.OneNlp.Models.German

The package packs three saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by `recipes/de/fast` (commands `train-tagging`, `train-parser`, `ner-train`), which is in the source repository. Weights are not stored in git.

| File | Components | Data | Seed |
|---|---|---|---|
| `tagging.onenlp` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder (`FastEncoder.English()` configuration) | UD German-GSD r2.18, train and dev | 1 |
| `parser.onenlp` | biaffine dependency parser with sentence starts, fast encoder and a two-layer bidirectional LSTM | UD German-GSD r2.18, train and dev | 7 |
| `ner.onenlp` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG, MISC), fast encoder | GermEval 2014, train and dev | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts. The tokenizer is the UAX #29 word tokenizer with the German data (`docs/segmentation.md`).

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD German-GSD, `de_gsd-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `6cb1b600029292fb7bf60531a1d6d7824544791d732c0f7abae8102a2ac337bf` |
| UD German-GSD, `de_gsd-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `01e8e674973592747ffe9a8c77fcf9d2f5936a8484e731ad4f76254318a8952c` |
| UD German-GSD, `de_gsd-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `595070aa50b706a91dc66f17c296f7a9a25cbc75269f177c27680fb1c21528ab` |
| GermEval 2014, `germeval-2014-train.iob2` (converted) | 2014 | train | CC BY 4.0 | `958b1743dc888696062df4eeafdeacdeabb2e6560a64d8691ff82f610866a45f` |
| GermEval 2014, `germeval-2014-dev.iob2` (converted) | 2014 | dev | CC BY 4.0 | `d0c6bb759a5cfa6beb419067e16e925c52ad820f372094770343eab37e3ddca9` |
| GermEval 2014, `germeval-2014-test.iob2` (converted) | 2014 | eval only | CC BY 4.0 | `3aac959b721df30ec8e49113cf25f061c98a04a1730be433b9706d8a2c292df3` |

- **Share-alike lineage:** yes (UD German-GSD). Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' gold annotations.
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read by `predict` and `ner-eval` and the scorer. 27 of the 977 GSD test sentences also occur in the training file as short exact text overlaps; the official split is kept (`tools/fetch/de/MANIFEST.md`).
- **Licence of the weights:** `MIT OR Apache-2.0`.
