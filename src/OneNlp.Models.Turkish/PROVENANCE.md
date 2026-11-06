# Provenance: RecursiveAI.OneNlp.Models.Turkish

The package packs four saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by `recipes/tr/fast` (commands `train-splitter`, `train-tagging`, `train-parser`, `ner-train`), which is in the source repository. Weights are not stored in git.

| File | Components | Data | Seed |
|---|---|---|---|
| `splitter.onenlp` | the pipeline's tokenizer: whole-word Turkish UAX #29 tokenizer plus a trained multiword-token splitter (B/I tagger over characters, fast encoder width 128 with a bidirectional LSTM) | UD Turkish-BOUN r2.18, train and dev | 1 |
| `tagging.onenlp` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder (width 384, 96-wide embeddings, one ReLU window layer and a bidirectional LSTM of 64, Turkish lower-casing) | UD Turkish-BOUN r2.18, train and dev | 1 |
| `parser.onenlp` | biaffine dependency parser with sentence starts, fast encoder and a two-layer bidirectional LSTM | UD Turkish-BOUN r2.18, train and dev | 7 |
| `ner.onenlp` | entity recognizer (BILUO tagging, constrained decoding; PER, ORG, LOC), fast encoder | Turkish-WikiNER, train and dev (PERSON, ORG, GPE and LOC kept; GPE and LOC are both LOC; other types are O) | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts. The tokenizer is the UAX #29 word tokenizer with the whole-word Turkish data (`docs/segmentation.md`) followed by the trained multiword-token splitter.

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD Turkish-BOUN, `tr_boun-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `ae54aef82e69cb7ccc54703a07e2ebcde66c2e4453a5debfac1eb78dfed60e9f` |
| UD Turkish-BOUN, `tr_boun-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `b3531bb26337b3ef258796e6948d2c3469e71704613b9373cd87f341409e2d8d` |
| UD Turkish-BOUN, `tr_boun-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `c7f234f6a7c1b7025f8cab9025239286836e971b653fb1dd3f266a84ac74dbeb` |
| Turkish-WikiNER, `turkish-wikiner-train.iob2` (converted) | HF revision 99df1dd7 | train | CC BY-SA 4.0 | `4c97b5d98f012cc4eb9a9b352c27f60fdd52ffd3ec35e738e71c52bdc2e5da89` |
| Turkish-WikiNER, `turkish-wikiner-dev.iob2` (converted) | HF revision 99df1dd7 | dev | CC BY-SA 4.0 | `dee9ec06448a5ccb6c193ef4ce5058400fbc17073dfe59cd92846466fe1c6017` |
| Turkish-WikiNER, `turkish-wikiner-test.iob2` (converted) | HF revision 99df1dd7 | eval only | CC BY-SA 4.0 | `ee907c75bcc5fdec3c3ac57977bf851d19abadefd71ebad7f8e910cd1a51e084` |

- **Share-alike lineage:** yes (UD Turkish-BOUN, Turkish-WikiNER). Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' gold annotations (Turkish-WikiNER is the dataset authors' annotation).
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read by `predict`, `ner-eval` and the scorer. 6 of the 979 BOUN test sentences also occur in the training file as exact text; the official split is kept (`tools/fetch/tr/MANIFEST.md`).
- **Licence of the weights:** `MIT OR Apache-2.0`.
