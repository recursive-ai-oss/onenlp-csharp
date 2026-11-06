# Provenance: RecursiveAI.OneNlp.Models.English

The package packs one saved pipeline, `english.onenlp` (`weights.manifest.props` lists its size and sha256; a pack fails when it differs). It holds three separately trained models, each with its own encoder, put into one file by `recipes/en/assemble` (no weights change; each part predicts what it predicted alone, checked on the test sets). All three were trained from scratch in C# with the `Train` API by the recipes in the source repository. Weights are not stored in git.

| Part | Recipe | Components | Data | Seed | Part file sha256 |
|---|---|---|---|---|---|
| tagging | `recipes/en/selftrain` (narrow encoder, 96 wide, no window layer) | tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer | UD English-EWT r2.18 train and dev, plus 290k sentences of FineWeb and Wikipedia text labelled by the previous tagging model (`recipes/en/selftrain/PROVENANCE.md`) | 1 | `f52cb26243c8e16c3fbc45924d51cdaef234c6a057ffd1e5cf15c72baa645dae` (51,981,603 bytes) |
| parser | `recipes/en/parser` | biaffine dependency parser with sentence starts, two bidirectional LSTM layers of 192; its own fast encoder (96 wide, four window layers) | UD English-EWT r2.18, train and dev | training 7, encoder 1 | `612101ebeb4c62b6239a94c11727a2536537bfdb60245f337075eae1e9ddfccc` (22,108,789 bytes) |
| entities | `recipes/en/selftrain` (128 wide, no window layer) | entity recognizer (BILUO tagging, PER, ORG, LOC) | UNER English-EWT commit 8ed072de train and dev, plus 120k labelled sentences of the same text | 1 | `3b38c0ec5d4a2def130aed26eb663f431a0130c55c9d958ef8c8a22462cbba55` (52,112,428 bytes) |

Assembled file: `english.onenlp`, 126,202,180 bytes, sha256 `1414ec7920de0c25b679a557a5508dc7e10429aeeff319aa71f5ba173ec61e48`. The rule sentencizer saved with the tagging part is left out: the parser sets sentence starts. Settings and results: the `PROVENANCE.md` next to each recipe. A single-encoder alternative (`recipes/en/joint`, 23.7 MB, faster, lower scores) is documented in `docs/results.md` and is not packed.

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
- **Label generator:** the tagging and entity parts also train on text labelled by the previous English model of this package (our own labels; share-alike lineage through its UD and UNER training data). The unlabelled text is FineWeb (ODC-By 1.0, Common Crawl terms of use) and English Wikipedia (CC BY-SA 3.0, GFDL).
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read only by each recipe's `predict` step and the scorer. Each recipe's `PROVENANCE.md` has the scores and the train/test separation.
- **Licence of the weights:** `MIT OR Apache-2.0`.

These are development weights; retraining replaces the rows in `weights.manifest.props` and this file together.
