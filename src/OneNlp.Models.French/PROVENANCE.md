# Provenance: RecursiveAI.OneNlp.Models.French

The package packs three saved pipelines (`weights.manifest.props` lists name, size and sha256 of each; a pack fails when one differs). Each was trained from scratch in C# with the `Train` API by the recipe named in the manifest, which is in the source repository. Weights are not stored in git.

| File | Recipe | Components | Data | Seed |
|---|---|---|---|---|
| `tagging.onenlp` | `recipes/fr/fast-tagging` | sentencizer, morphologizer (UPOS, UFeats), edit-tree lemmatizer, on a fast hash-embedding encoder | UD French-GSD r2.18, train and dev | 1 |
| `parser.onenlp` | `recipes/fr/parser` | biaffine dependency parser with sentence starts, fast encoder and one LSTM layer of 128 units per direction, arc 128, label 64, product label features (`docs/parser.md`) | UD French-GSD r2.18, train and dev | 7 |
| `ner.onenlp` | `recipes/fr/ner` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG, MISC), fast encoder | WikiNER French wp3, train and dev | 7 |

The sentencizer saved in `tagging.onenlp` is not used by the packaged pipeline; the parser predicts sentence starts. The tokenizer is the UAX #29 word tokenizer with the French data (`docs/segmentation.md`).

The datasets, their hashes and licences, the share-alike lineage (yes), the label generator (none), the train/test separation and the results are in `recipes/fr/PROVENANCE.md`. Licence of the weights: `MIT OR Apache-2.0`.
