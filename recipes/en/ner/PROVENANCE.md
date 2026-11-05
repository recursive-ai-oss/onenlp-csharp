# Provenance: en-ner (tagging and spans)

- **Model:** `en-ner-tagging`, version 0 (development); a named entity recognizer (PER, ORG, LOC) over the library fast encoder (`FastEncoder.English()`), English, fast tier. Format: `docs/provenance.md`.
- **Recipe:** `recipes/en/ner/` (`Program.cs`, `run.sh`); defaults are the configuration: head hidden 256, context 0, dropout 0.4, learning rate 0.003, batch 1000 words, patience 6, at most 40 epochs.
- **Seed(s):** training, data order, initial weights and encoder 7 (`--seed`); the data splits are the official ones.
- **Produced:** 2026-11-05; AMD Ryzen 9 7950X3D (16 cores, 32 threads) shared with other jobs, 4 threads; .NET SDK 10.0.403, runtime 10.0.14; RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0; wall time 0:05:52.
- **Weights:** `model.onenlp` (pipeline file with encoder and head), 35,677,809 bytes, file sha256 `fe3e09746b5fd5245d348760a779cbee0f2ec57e0201a5265a7b41a0399e9cce`, weights hash reported by `Train` `72756de883db4fa6621cb03f2ea50da7ef9ee52e3a749e47f35f902b5c5ac594`. Kept on the build machine; not in git. (Earlier versions: first FastEncoder 12,039,553 bytes `309abffd…0964`; stand-in encoder 9,978,133 bytes `59050e63…2519`.)
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
- The test file was not used for early stopping, model selection or hyperparameter choice: yes (not used). The design (tagging against spans, on an earlier stand-in encoder) and the settings (sweeps of encoder shape, context, dropout and learning rate, about sixty runs) were chosen on the dev split. The test split was read once for each final model (the stand-in encoder with both designs; the first FastEncoder configuration; and the final one, read once).

## Results

Rows, intervals and the reference in `README.md` next to this file. Regenerate: `run.sh <data-root> <work-dir> tagging`, then `eval <work-dir>/tagging/model.onenlp <work-dir>/data/test.jsonl <out> --ref <spaCy predictions>`.

## Notes

- Seeds 8 and 9 were trained for the dev spread only. The spans design was measured only on the earlier stand-in encoder.
- 114 test sentences also occur in train or dev (see above).
