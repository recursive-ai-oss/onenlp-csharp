# Provenance: en-spancat (UNER English-EWT)

- **Model:** `en-spancat-uner`, version 0 (development); a span categorizer (PER, ORG, LOC) over the library fast encoder (`FastEncoder.English()`), English, fast tier. Format: `docs/provenance.md`.
- **Recipe:** `recipes/en/spancat/` (`Program.cs`); defaults are the configuration: sizes 1, 2, 3, hidden 256, threshold 0.5, dropout 0.2, learning rate 0.003, batch 1000 words, patience 6, at most 40 epochs.
- **Seed(s):** training, data order, initial weights and encoder 7 (`--seed`); the data splits are the official ones.
- **Produced:** 2026-11-06; shared 32-core x64 worker, 4 threads; .NET SDK 10.0.403; RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0; 650 s.
- **Weights:** `model.onenlp`, 36,191,861 bytes, weights hash reported by `Train` `96d1938dfc5785a19159ed1549dc73fbc7698794d17db0a7d203d5f0f8b06508`. Kept on the build machine; not in git. Best development span F1 59.600.
- **SA lineage:** yes. UNER English-EWT is CC BY-SA 4.0 (and sits on UD English-EWT, also CC BY-SA 4.0). Credit: Universal NER (Mayhew et al. 2024) and Universal Dependencies English Web Treebank.
- **Licence of the weights:** not released; development model.

## Training data

| Dataset | Release | Role | Files | Licence | URL | sha256 |
|---|---|---|---|---|---|---|
| UNER English-EWT | commit `8ed072de2fd24022cc62458997fe96a8fe191ea4` (Universal NER v1) | train | `en_ewt-ud-train.iob2`, converted by `recipes/en/spancat convert` to `train.jsonl` (12,543 sentences) | CC BY-SA 4.0 (`LICENSE.txt`) | github.com/UniversalNER/UNER_English-EWT | iob2 `647b296624433d2b0f304f81974404fb1d5ed1520cc05649b4646f5777a5497d`; `train.jsonl` `67eac704bb3cdf83a4020dd2d922bcbcc7975cd5142e9d7db475833ffd113332` |
| UNER English-EWT | same | dev (early stopping, choice of settings) | `en_ewt-ud-dev.iob2` -> `dev.jsonl` (2,001) | same | same | iob2 `0cafd881cf31d39897b6d588420fab8fbbe4423d271725c59921ac9dd49c0713`; `dev.jsonl` `fccc1a3898de7e5d1bc3c54e3372afd3d122dbaf4e468a6d9528a3b95b8da17f` |

Each entity mention becomes one span of the group `sc`; the dataset's mentions are flat, so overlapping spans are exercised only by the synthetic unit test.

## Label generator

None: labels are the dataset's gold annotations.

## Pretrained components

None; trained from scratch.

## Evaluation data

| Dataset | Release | Role | Files | sha256 | Used for |
|---|---|---|---|---|---|
| UNER English-EWT | commit `8ed072de` | eval | `en_ewt-ud-test.iob2` -> `test.jsonl` (2,077) | iob2 `821ad826d5f503ac03b4edc3fbc2174f21fbb005ec25863caf2051c434616d6e`; `test.jsonl` `4282e1ac709709dbaeab7a2e338775e571127e1a76a6ac52733e0fad217d64f5` | reported rows, once per final model |

## Train/test separation

- Manifest: `tools/fetch/en/MANIFEST.md`. The test hash was recorded before training. 114 test sentence texts also occur in train or dev (almost all one- and two-word fragments); the official split is kept.
- The test file was not used for early stopping, model selection or the choice of settings; it was read once for the final model.

## Results

`README.md` next to this file.
