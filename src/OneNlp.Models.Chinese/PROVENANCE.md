# Provenance: RecursiveAI.OneNlp.Models.Chinese

The package packs four saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by `recipes/zh/fast` (commands `train-segmenter`, `train-tagging`, `train-parser`, `ner-train`), which is in the source repository. Weights are not stored in git.

| File | Components | Data | Seed |
|---|---|---|---|
| `segmenter.onenlp` | the pipeline's tokenizer: a character B/I tagger (`NeuralWordSegmenter`) on a fast hash-embedding encoder with neighbour, wider-context and character-pair tables, two window layers and two 96-unit bidirectional LSTM layers (`recipes/zh/fast/PROVENANCE.md`) | UD Chinese-GSDSimp r2.18, train and dev | 1 |
| `tagging.onenlp` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast encoder with one window layer and one 64-unit bidirectional LSTM layer, one-character prefix and suffix | UD Chinese-GSDSimp r2.18, train and dev | 1 |
| `parser.onenlp` | biaffine dependency parser with sentence starts, fast encoder and a two-layer bidirectional LSTM | UD Chinese-GSDSimp r2.18, train and dev | 7 |
| `ner.onenlp` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG), fast encoder with two hashes per attribute | UNER Chinese-GSDSIMP, train and dev | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts.

## Datasets

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD Chinese-GSDSimp, `zh_gsdsimp-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `956636fe612a1166e8b19e7413fee2e73d68231aca2f0455be2c616b947d629d` |
| UD Chinese-GSDSimp, `zh_gsdsimp-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `d03f1eeb93b16071bfbbe6c76b971554be87c9a2307b3f3a820dd7c07f73fb63` |
| UD Chinese-GSDSimp, `zh_gsdsimp-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `3af8046a6f32477b4d5cf3dd06bbf38682a380fe77aade3f68de97e51ab94900` |
| UNER Chinese-GSDSIMP, `zh_gsdsimp-ud-train.iob2` | commit 1f030759 | train | CC BY-SA 4.0 | `a03b5260c3d960aa711ff5f12b797de5c7d7f412563593c173cd0e7135001db5` |
| UNER Chinese-GSDSIMP, `zh_gsdsimp-ud-dev.iob2` | commit 1f030759 | dev | CC BY-SA 4.0 | `8aeab3ac662468e258a65f887844c50cee8311cb080b60a421ff7aa8a05e8032` |
| UNER Chinese-GSDSIMP, `zh_gsdsimp-ud-test.iob2` | commit 1f030759 | eval only | CC BY-SA 4.0 | `f1d8faa790d914fe01a24627874c8e7fad9c40b54742f5aefab5ba3f7c8edede` |

- **Share-alike lineage:** yes, every component (CC BY-SA 4.0 training data). Credit is in `THIRD-PARTY-NOTICES.md`.
- **Label generator:** none; labels are the datasets' annotations. The segmenter's labels are the treebank's word boundaries.
- **Pretrained components:** none.
- **Evaluation sets:** the test files above, read by `predict` and `ner-eval` and the scorer. The traditional-script UD Chinese-GSD is not used: its sentences are the same as GSDSimp's, so it would put test sentences in training.
- **Licence of the weights:** `MIT OR Apache-2.0`.
