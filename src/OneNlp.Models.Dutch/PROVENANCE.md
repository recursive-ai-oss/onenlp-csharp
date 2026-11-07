# Provenance: RecursiveAI.OneNlp.Models.Dutch

The package packs three saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by `recipes/nl/fast` (commands `train-tagging`, `train-parser`, `ner-train`), which is in the source repository. Weights are not stored in git.

| File | Components | Data | Seed |
|---|---|---|---|
| `tagging.onenlp` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder (ReLU, one window layer, one bidirectional LSTM layer of 64 units per direction; `recipes/nl/fast/PROVENANCE.md`) | UD Dutch-Alpino r2.18, train and dev | 1 |
| `parser.onenlp` | biaffine dependency parser with sentence starts, fast encoder and a one LSTM layer of 128 units per direction, arc 128, label 64, product label features (`docs/parser.md`)  | UD Dutch-Alpino r2.18, train and dev | 7 |
| `ner.onenlp` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG, MISC), fast encoder | WikiNER Dutch wp3, train and dev | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts. The tokenizer is the UAX #29 word tokenizer with the Dutch data (`docs/segmentation.md`).

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD Dutch-Alpino, `nl_alpino-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `d6612a025ae77a8f58494458b987be6f39fdcaa40c94b5e2a5522e50c4166cfb` |
| UD Dutch-Alpino, `nl_alpino-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `0444fc2590d7f522b5f201473940a40f0f1790770fcb568450581f12bcbcffd9` |
| UD Dutch-Alpino, `nl_alpino-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `20d0f718cdc73b51dbb2744386c9d4ecb1a9df6a076efa81b4c008ace3801fc0` |
| WikiNER Dutch wp3, `wikiner-nl-wp3-train.iob2` (converted, 163,725 sentences) | figshare 5462500 | train | CC BY 4.0 (text: Wikipedia, CC BY-SA) | `d8b5aec809b4ceddae16ad9facb93ec9892756f8bdaa3079c3815964c6328a7d` |
| WikiNER Dutch wp3, `wikiner-nl-wp3-dev.iob2` (converted, 1,645 sentences) | same | dev | same | `4328b8bb4a9d5a7edfdc5c6d3ed4d5536520dac12e3a944d4f3002b63a5750fe` |
| WikiNER Dutch wp3, `wikiner-nl-wp3-test.iob2` (converted, 8,358 sentences) | same | eval only | same | `6c2bc736a5789664662d3dec98822dc06370a9cd3c4b5b0484c7b10ed83801a2` |

- **Share-alike lineage:** yes (UD Dutch-Alpino; the WikiNER text is Wikipedia). Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the UD gold annotations and WikiNER's silver labels (projected from Wikipedia links).
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read by `predict` and `ner-eval` and the scorer. No Alpino test sentence occurs in the training file; 35 of the 8,358 WikiNER test sentences also occur in its training text as short repeated fragments, and the official article split is kept (`tools/fetch/nl/MANIFEST.md`).
- **Licence of the weights:** `MIT OR Apache-2.0`.
