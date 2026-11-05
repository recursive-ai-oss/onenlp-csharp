# Provenance: en-fast-tagging (v2)

- **Model:** en-fast-tagging v2; English fast tier: `FastEncoder.English()` with a tagger (XPOS), a morphologizer (UPOS and UFeats) and an edit-tree lemmatizer, plus the English word tokenizer data and sentencizer. v1 used the first encoder configuration (96 wide, 1-character prefix, small tables, four window layers); v2 is the shared English configuration chosen with the NER head.
- **Recipe:** `recipes/en/fast-tagging/` (`Program.cs`; the recipe constants are in that file; sha256 of `Program.cs` at the time of the run is in the commit that added this file).
- **Seed(s):** training and data order 1, encoder initial weights 1 (head weights are drawn from the training seed plus the head's position), dropout stream derived from the training seed. The dev set is the official dev file, so there is no split seed.
- **Produced:** 2026-11-05; AMD Ryzen 9 7950X3D (16 cores, 32 threads), shared, 4 training threads; wall time 0:22:44 (40 epochs, the full run; best dev at the last step); .NET SDK 10.0.403 (runtime 10.0.14), RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0, this repository.
- **Weights:** `model.onenlp`, 36,958,094 bytes, sha256 `86009eed5179efd38421e1d67cc29416fd29e0e54ad488820d13259269970cb9` (training-state hash printed by `Train`: `411c5bf5c6572a1db78e8d11303ccb5d96ce8ba9b718d70527f7b5e4095160de`). Kept out of git; rebuilt by the recipe. (v1: 12,551,838 bytes, sha256 `c7cb6036f8cc8ccbb6bb75b1d1e568aef2c94889f00c1f6b2d27e207a1c428be`.)
- **SA lineage:** yes; Universal Dependencies English-EWT (CC BY-SA 4.0). Credit: the Universal Dependencies English Web Treebank, <https://github.com/UniversalDependencies/UD_English-EWT>.
- **Licence of the weights:** MIT OR Apache-2.0 for our weights (owner rule on share-alike training data, 2026-10-30); the sources are credited as above.

## Training data

| Dataset | Release | Role | Files | Licence (quoted from the dataset's own licence file) | URL | sha256 |
|---|---|---|---|---|---|---|
| UD English-EWT | r2.18 | train | `en_ewt-ud-train.conllu` | "licensed under a Creative Commons Attribution-ShareAlike 4.0 International License" | <https://github.com/UniversalDependencies/UD_English-EWT/tree/r2.18> | `d68e06122a702464c613076523d56740f047e5bbe89dd90ec32737e04d952143` |
| UD English-EWT | r2.18 | dev | `en_ewt-ud-dev.conllu` | same | same | `39239e0a60db3ae68f4b7036189f11b6692741d10ff8240dd91f74f2760d90f8` |

## Label generator

None: labels are the treebank's gold UPOS, XPOS, UFeats and lemmas. Edit trees and the lower-case flag are derived from the gold (form, lemma) pairs by the lemmatizer's own code.

## Pretrained components

None: trained from scratch.

## Evaluation data

| Dataset | Release | Role | Files | sha256 | Used for |
|---|---|---|---|---|---|
| UD English-EWT | r2.18 | eval | `en_ewt-ud-test.conllu` | `fa024f43dc5da3c5ac02563bc9bd0e974f46cbb1560823976a8f342a37dc494a` | the rows below; read only by `predict` and the scorer |

## Train/test separation

- Manifest: `tools/fetch/en/MANIFEST.md`.
- Text-level overlap: 114 short test fragments also occur in train (listed in the manifest; the official split is kept).
- The test file was not used for early stopping, model selection or any choice of hyperparameter: the recipe was run once with the settings in the README (one earlier 2-epoch smoke run on train and dev only).

## Results

UD English-EWT r2.18 test; paired bootstrap 95% CI over sentences (1,000 resamples, seed 1) from `tools/OneNlp.Eval ud`; reference: spaCy 3.8.18 trained on the same data with its default configuration (`tools/baselines/spacy/PROVENANCE.md`), same input mode, with its own interval. Within noise = the intervals overlap (`docs/measurement.md`).

| Mode | Metric | Ours [95% CI] | spaCy [95% CI] | Within noise |
|---|---|---|---|---|
| gold tokens and sentences | UPOS | 93.89 [93.51, 94.28] | 94.27 [93.90, 94.63] | yes |
| | XPOS | 93.02 [92.59, 93.45] | 93.24 [92.83, 93.65] | yes |
| | UFeats | 94.83 [94.52, 95.13] | 95.16 [94.85, 95.48] | yes |
| | AllTags | 91.01 [90.50, 91.49] | 91.37 [90.88, 91.84] | yes |
| | Lemmas | 96.00 [95.65, 96.32] | 95.70 [95.36, 96.03] | yes |
| raw text | UPOS | 92.56 [92.11, 93.02] | 92.32 [91.70, 92.93] | yes |
| | XPOS | 91.44 [90.94, 91.93] | 91.53 [90.92, 92.15] | yes |
| | UFeats | 93.49 [93.07, 93.88] | 93.32 [92.76, 93.86] | yes |
| | AllTags | 89.35 [88.82, 89.89] | 89.81 [89.14, 90.45] | yes |
| | Lemmas | 94.79 [94.38, 95.18] | 93.62 [92.98, 94.20] | no (ours higher) |

The spaCy column is the full-length parser-pipeline run. v1 against the truncated run was within noise on every row; v2 is at or above v1 on 9 of the 10 rows (XPOS on gold tokens is 0.01 lower).

Raw text means our `UaxWordTokenizer` with the English data and the `sentencizer`; with the multiword-token lines written (`--mwt`), Tokens 99.00, Sentences 58.15, Words 98.68. Catalyst, raw text, for information: UPOS 80.70, Lemmas 83.84.

Reproduce: `recipes/en/fast-tagging/README.md`; the scorer commands are in `docs/measurement.md`.

## Notes

- Our rows are a little under spaCy's on the gold-token tag rows and over on Lemmas; every interval overlaps. No tuning beyond the first full run was done.
- The tagger and the morphologizer both write the token confidence; the later component in the pipeline wins.
