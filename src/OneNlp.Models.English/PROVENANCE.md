# Provenance: RecursiveAI.OneNlp.Models.English

The package packs one saved pipeline, `english.onenlp` (`weights.manifest.props` lists its size and sha256; a pack fails when it differs). It was trained from scratch in C# with the `Train` API by `recipes/en/joint`, which is in the source repository. Weights are not stored in git.

| File | Recipe | Components | Data | Seed |
|---|---|---|---|---|
| `english.onenlp` | `recipes/en/joint` | tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, biaffine dependency parser with sentence starts (one bidirectional LSTM layer of 192), entity recognizer (BILUO tagging, PER, ORG, LOC); one shared fast hash-embedding encoder (128 wide, one window layer, ReLU, neighbour and wider-context hash tables) | UD English-EWT r2.18 and UNER English-EWT commit 8ed072de, train and dev | 1 |

sha256 `6bb03663e81f6d874f83bffde41c65b06e376c028050dcd92f73bfe615dc50b9`, 23,749,404 bytes. Settings and results: `recipes/en/joint/PROVENANCE.md`.

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD English-EWT, `en_ewt-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `d68e06122a702464c613076523d56740f047e5bbe89dd90ec32737e04d952143` |
| UD English-EWT, `en_ewt-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `39239e0a60db3ae68f4b7036189f11b6692741d10ff8240dd91f74f2760d90f8` |
| UD English-EWT, `en_ewt-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `fa024f43dc5da3c5ac02563bc9bd0e974f46cbb1560823976a8f342a37dc494a` |
| UNER English-EWT, `en_ewt-ud-train.iob2` | 8ed072de | train | CC BY-SA 4.0 | `647b296624433d2b0f304f81974404fb1d5ed1520cc05649b4646f5777a5497d` |
| UNER English-EWT, `en_ewt-ud-dev.iob2` | 8ed072de | dev | CC BY-SA 4.0 | `0cafd881cf31d39897b6d588420fab8fbbe4423d271725c59921ac9dd49c0713` |
| UNER English-EWT, `en_ewt-ud-test.iob2` | 8ed072de | eval only | CC BY-SA 4.0 | `821ad826d5f503ac03b4edc3fbc2174f21fbb005ec25863caf2051c434616d6e` |

- **Share-alike lineage:** yes. Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' gold annotations.
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read only by each recipe's `predict` step and the scorer. Each recipe's `PROVENANCE.md` has the scores and the train/test separation.
- **Licence of the weights:** `MIT OR Apache-2.0`.

These are development weights; retraining replaces the rows in `weights.manifest.props` and this file together.
