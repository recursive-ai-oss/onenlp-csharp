# Provenance: RecursiveAI.OneNlp.Models.Italian

The package packs three saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by `recipes/it/fast` (commands `train-tagging`, `train-parser`, `ner-train`), which is in the source repository. Weights are not stored in git. Settings, seeds, development choices and results: `recipes/it/fast/PROVENANCE.md`.

| File | Components | Data | Seed |
|---|---|---|---|
| `tagging.onenlp` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder with one window layer and one 64-unit bidirectional LSTM layer (ReLU, no layer normalization) | UD Italian-ParlaMint and Italian-MarkIT r2.18, derived training and development files | 1 |
| `parser.onenlp` | biaffine dependency parser with sentence starts, fast encoder and a 128-unit bidirectional LSTM | the same | 7 |
| `ner.onenlp` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG, MISC), fast encoder | WikiNER Italian wp3, train and dev, articulated prepositions and verbs with enclitics cut into the tokenizer's pieces (`ner-convert --pieces`) | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts. The tokenizer is the UAX #29 word tokenizer with the Italian data (`docs/segmentation.md`).

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD Italian-ParlaMint `it_parlamint-ud-train.conllu` (7 sentences in 8 for training, the rest for development) | r2.18 | train, dev | CC BY-SA 4.0 | `2b83d41e5eacfab1511245e9032a12a412d273cbb153bcdef2cc5ac793d91aea` |
| UD Italian-MarkIT `it_markit-ud-train.conllu` | r2.18 | train | CC BY 4.0 | `4f6fc3423acb00d49e1e167bd21b11ebbc23e22275fd6f5eeb4d75cfbbccc5c6` |
| UD Italian-MarkIT `it_markit-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY 4.0 | `633b6186f47ecfebfcff4ff9c7e19a0cbffd1f655f14fc1e9265b12bda597f97` |
| derived `it_gating-ud-train.conllu` (897 sentences) | | train | CC BY-SA 4.0 and CC BY 4.0 | `909763cc8f5d2b15eb42f8f874482f455cf6c975827414c3e3266b8406685938` |
| derived `it_gating-ud-dev.conllu` (381 sentences) | | dev | CC BY-SA 4.0 and CC BY 4.0 | `53e038adcbc5f93978a1523abcd16ee91a12dee496683a4d00c972ea2efffc1c` |
| UD Italian-ParlaMint `it_parlamint-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `aa0e0fc94683fef4d4ee73a387e747c31b1639c1123570b4a536174af2327229` |
| WikiNER Italian `wikiner-it-wp3-train.iob2` (converted) | wp3 | train | CC BY 4.0 (text CC BY-SA) | `d04c5d4e2a9689c0a7a145ae262bd85674fbd94bc174b3082327a909a032ab66` |
| WikiNER Italian `wikiner-it-wp3-dev.iob2` (converted) | wp3 | dev | CC BY 4.0 (text CC BY-SA) | `e805005685c85f422263716b2015b3fbfcd3c39a56c94d39f0e34184a9eaaa66` |
| WikiNER Italian `wikiner-it-wp3-test.iob2` (converted) | wp3 | eval only | CC BY 4.0 (text CC BY-SA) | `6dcab55fd030dc93fd7be266e28504bcc4c41be5f362cc175cdc6931a9bd7429` |

- **Share-alike lineage:** yes (ParlaMint annotations are CC BY-SA 4.0; the entity recognizer also through WikiNER's Wikipedia text). Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' annotations (WikiNER's are silver labels projected from Wikipedia links).
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read by `predict` and `ner-eval` and the scorer. 13 of the 375 ParlaMint test sentences and 87 of the 6,234 WikiNER test sentences also occur in the training files, as short exact text overlaps; the official splits are kept (`tools/fetch/it/MANIFEST.md`).
- **Licence of the weights:** `MIT OR Apache-2.0`.
