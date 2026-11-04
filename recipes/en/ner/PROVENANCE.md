# Provenance: en-ner (tagging and spans)

- **Model:** `en-ner-tagging` and `en-ner-spans`, version 0 (development); a named entity recognizer (PER, ORG, LOC) over a hash-embedding word encoder, English, fast tier. Format: `docs/provenance.md`.
- **Recipe:** `recipes/en/ner/` (`Program.cs`, `LexicalEncoder.cs`, `run.sh`); defaults are the configuration: width 256, hidden 256, context 2, dropout 0.4, learning rate 0.003, batch 1000 words, patience 6, at most 40 epochs.
- **Seed(s):** training, data order and initial weights 7 (`--seed`); the data splits are the official ones. Seeds 8 and 9 were trained for the dev spread only.
- **Produced:** 2026-11-04; AMD Ryzen 9 7950X3D (16 cores, 32 threads) shared with other jobs, 4 threads; .NET SDK 10.0.403, runtime 10.0.14; RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0; wall time tagging 1:33, spans 7:05.
- **Weights:** `model.onenlp` (pipeline file with encoder and head). Tagging 9,978,133 bytes, file sha256 `59050e63939423e34ffd6910d99166d550dc1734fddfe9040d5bb87278222519`, weights hash reported by `Train` `dbfd2fca8edc12b5ba624a9e124bf8a5984498257ee2f508a1c6eae113981f10`. Spans 10,514,963 bytes, file sha256 `bc7f8b66333e343a6eebe49c7e0d9577f190c1c3e5099905ef728ba3256d7357`, weights hash `3e080b3b5e9fc197876fe2b9402745df1fc6fe30d68744e416434a4bb6e9fd37`. Kept on the build machine under `models/en-fast/u16/`; not in git. Retraining at the same seed gives the same weights hash (checked twice for tagging).
- **SA lineage:** yes. UNER English-EWT is CC BY-SA 4.0 (and sits on UD English-EWT, also CC BY-SA 4.0). Credit: Universal NER (Mayhew et al. 2024) and Universal Dependencies English Web Treebank.
- **Licence of the weights:** not released; development model.

## Training data

| Dataset | Release | Role | Files | Licence (quoted from the dataset's own licence file) | URL | sha256 |
|---|---|---|---|---|---|---|
| UNER English-EWT | commit `8ed072de2fd24022cc62458997fe96a8fe191ea4` (Universal NER v1) | train | `en_ewt-ud-train.iob2` | CC BY-SA 4.0 (`LICENSE.txt`: "Attribution-ShareAlike 4.0 International") | github.com/UniversalNER/UNER_English-EWT | `647b296624433d2b0f304f81974404fb1d5ed1520cc05649b4646f5777a5497d` |
| UNER English-EWT | same | dev (early stopping, choice of design and settings) | `en_ewt-ud-dev.iob2` | same | same | `0cafd881cf31d39897b6d588420fab8fbbe4423d271725c59921ac9dd49c0713` |

## Label generator

None: labels are the datasets' gold annotations.

## Pretrained components

None; trained from scratch (no vectors, no transformer).

## Evaluation data

| Dataset | Release | Role | Files | sha256 | Used for |
|---|---|---|---|---|---|
| UNER English-EWT | commit `8ed072de` | eval | `en_ewt-ud-test.iob2` | `821ad826d5f503ac03b4edc3fbc2174f21fbb005ec25863caf2051c434616d6e` | reported rows, once per final model |

## Train/test separation

- Manifest: `tools/fetch/en/MANIFEST.md`, which lists the six files and their roles.
- Text-level overlap of the test file with train and dev: 114 identical sentence texts (127 after case and whitespace folding), almost all one- and two-word fragments; the official split is kept (`tools/fetch/en/MANIFEST.md`).
- The test file was not used for early stopping, model selection or hyperparameter choice: yes (not used). The design (tagging against spans) and the settings (sweep of dropout, context, hidden and encoder width, six runs) were chosen on the dev split. The test split was read once for the final configuration, for both designs.

## Results

Rows, intervals and the reference in `README.md` next to this file. Regenerate: `run.sh <data-root> <work-dir> tagging`, then `eval <work-dir>/tagging/model.onenlp <work-dir>/data/test.jsonl <out> --ref <spaCy predictions>`.

## Notes

- The encoder is the recipe's stand-in (`LexicalEncoder`); the rows are to be repeated on the library's fast encoder.
- 114 test sentences also occur in train or dev (see above).
