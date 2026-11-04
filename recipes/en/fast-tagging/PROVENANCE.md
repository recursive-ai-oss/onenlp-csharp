# Provenance: en-fast-tagging (v1)

- **Model:** en-fast-tagging v1; English fast tier: `FastEncoder` with a tagger (XPOS), a morphologizer (UPOS and UFeats) and an edit-tree lemmatizer, plus the English word tokenizer data and sentencizer.
- **Recipe:** `recipes/en/fast-tagging/` (`Program.cs`; the recipe constants are in that file; sha256 of `Program.cs` at the time of the run is in the commit that added this file).
- **Seed(s):** training and data order 1, encoder initial weights 1 (head weights are drawn from the training seed plus the head's position), dropout stream derived from the training seed. The dev set is the official dev file, so there is no split seed.
- **Produced:** 2026-11-04; AMD Ryzen 9 7950X3D (16 cores, 32 threads; AVX2, AVX-512, FMA), 8 training threads; wall time 0:02:47 (15 epochs, early stop at epoch 15, best at epoch 9); .NET SDK 10.0.403 (runtime 10.0.14), RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0, this repository.
- **Weights:** `model.onenlp`, 12,551,838 bytes, sha256 `c7cb6036f8cc8ccbb6bb75b1d1e568aef2c94889f00c1f6b2d27e207a1c428be` (training-state hash printed by `Train`: `894da664538b3ffaaff287a9f2ffae2f3078b8a95d01dbfd7cef1556c218847f`). Kept out of git; rebuilt by the recipe.
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
| gold tokens and sentences | UPOS | 93.72 [93.29, 94.10] | 94.29 [93.89, 94.66] | yes |
| | XPOS | 93.03 [92.60, 93.45] | 93.43 [93.00, 93.82] | yes |
| | UFeats | 94.79 [94.47, 95.09] | 95.18 [94.88, 95.45] | yes |
| | AllTags | 90.97 [90.48, 91.42] | 91.53 [91.05, 91.97] | yes |
| | Lemmas | 95.90 [95.57, 96.23] | 95.80 [95.42, 96.13] | yes |
| raw text | UPOS | 92.33 [91.83, 92.79] | 92.45 [91.84, 93.06] | yes |
| | XPOS | 91.25 [90.73, 91.74] | 91.72 [91.09, 92.34] | yes |
| | UFeats | 93.43 [93.02, 93.82] | 93.37 [92.81, 93.92] | yes |
| | AllTags | 89.23 [88.67, 89.77] | 89.99 [89.32, 90.62] | yes |
| | Lemmas | 94.67 [94.27, 95.07] | 93.69 [93.08, 94.29] | yes |

Raw text means our `UaxWordTokenizer` with the English data and the `sentencizer`; with the multiword-token lines written (`--mwt`), Tokens 99.00, Sentences 58.15, Words 98.68. Catalyst, raw text, for information: UPOS 80.70, Lemmas 83.84.

Reproduce: `recipes/en/fast-tagging/README.md`; the scorer commands are in `docs/measurement.md`.

## Notes

- Our rows are a little under spaCy's on the gold-token tag rows and over on Lemmas; every interval overlaps. No tuning beyond the first full run was done.
- The tagger and the morphologizer both write the token confidence; the later component in the pipeline wins.
