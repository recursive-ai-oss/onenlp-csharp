# Provenance: RecursiveAI.OneNlp.Models.English

The package packs three saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by the recipe named below, which is in the source repository. Weights are not stored in git.

| File | Recipe | Components | Data | Seed |
|---|---|---|---|---|
| `tagging.onenlp` | `recipes/en/fast-tagging` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder | UD English-EWT r2.18, train and dev | 1 |
| `parser.onenlp` | `recipes/en/parser` | biaffine dependency parser with sentence starts, fast encoder and a bidirectional LSTM | UD English-EWT r2.18, train and dev | 1 |
| `ner.onenlp` | `recipes/en/ner` | entity recognizer (BILUO tagging, constrained decoding; PER, ORG, LOC), fast encoder | UNER English-EWT commit 8ed072de, train and dev | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts.

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD English-EWT, `en_ewt-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `d68e06122a702464c613076523d56740f047e5bbe89dd90ec32737e04d952143` |
| UD English-EWT, `en_ewt-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `39239e0a60db3ae68f4b7036189f11b6692741d10ff8240dd91f74f2760d90f8` |
| UD English-EWT, `en_ewt-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `fa024f43dc5da3c5ac02563bc9bd0e974f46cbb1560823976a8f342a37dc494a` |
| UNER English-EWT, `en_ewt-ud-train.iob2` | 8ed072de | train | CC BY-SA 4.0 | `647b296624433d2b0f304f81974404fb1d5ed1520cc05649b4646f5777a5497d` |
| UNER English-EWT, `en_ewt-ud-dev.iob2` | 8ed072de | dev | CC BY-SA 4.0 | `0cafd881cf31d39897b6d588420fab8fbbe4423d271725c59921ac9dd49c0713` |
| UNER English-EWT, `en_ewt-ud-test.iob2` | 8ed072de | eval only | CC BY-SA 4.0 | `821ad826d5f503ac03b4edc3fbc2174f21fbb005ec25863caf2051c434616d6e` |

- **Share-alike lineage:** yes, all three files. Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' gold annotations.
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read only by each recipe's `predict` step and the scorer. Each recipe's `PROVENANCE.md` has the scores and the train/test separation.
- **Licence of the weights:** `MIT OR Apache-2.0`.

These are development weights; retraining replaces the rows in `weights.manifest.props` and this file together.
