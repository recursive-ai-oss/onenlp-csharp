# Provenance: en-textcat (BANKING77, multi-class)

- **Model:** `en-textcat-banking77`, version 0 (development); a multi-class text classifier (77 intents) over the library fast encoder (`FastEncoder.English()`), mean-pooled, no hidden layer. English, fast tier. Format: `docs/provenance.md`.
- **Recipe:** `recipes/en/textcat/` (`Program.cs`); defaults are the configuration: dropout 0.2, learning rate 0.003, batch 2000 words, patience 6, at most 40 epochs, no hidden layer.
- **Seed(s):** training, data order, initial weights and encoder 7 (`--seed`); the data splits are fixed by the fetch script.
- **Produced:** 2026-11-06; shared 32-core x64 worker, 4 threads; .NET SDK 10.0.403; RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0; 1,736 steps, 321 s.
- **Weights:** `model.onenlp`, 35,481,999 bytes, weights hash reported by `Train` `45b78a4e51821571bd3819e2b38b76b77961748523b3697401418d446c0785dd`. Kept on the build machine; not in git. Best development accuracy 91.887.
- **SA lineage:** no. BANKING77 is CC BY 4.0.
- **Licence of the weights:** not released; development model.

## Training data

| Dataset | Release | Role | Files | Licence | URL | sha256 |
|---|---|---|---|---|---|---|
| BANKING77 (Casanueva et al. 2020, PolyAI) | `PolyAI-LDN/task-specific-datasets` master, `banking_data/train.csv` | train (8,980 texts: every training text except the development tenth) | `train.jsonl` | CC BY 4.0 (the repository's `LICENSE`, sha256 `7e7170e3…`) | github.com/PolyAI-LDN/task-specific-datasets | raw CSV `b06e26ac…`; `train.jsonl` `a9ae1551afa7f777633e11c7920ff6bf01159c25f819d7daf3d8f4786cd690ec` |
| BANKING77 | same | dev (1,023 texts: every training text whose SHA-256 gives bucket 0 of 10; early stopping and choice of settings) | `dev.jsonl` | same | same | `786ed2dac8dff0aef9c2e5679de9888c95496cde3cd7cc8cc101c53dec9f4554` |

## Label generator

None: labels are the dataset's gold intents.

## Pretrained components

None; trained from scratch.

## Evaluation data

| Dataset | Release | Role | Files | sha256 | Used for |
|---|---|---|---|---|---|
| BANKING77 | `banking_data/test.csv` (official, 3,080 texts) | eval | `test.jsonl` | `4626e974412c704ba961d6107e3f3c43a1b276e068275f3c74a53cc6ec16e2c4` (raw CSV `d12d6e3b…`) | reported rows, once per final model |

## Train/test separation

- The test hash was recorded before any training. `tools/fetch/textcat/fetch.py banking77` writes the train and dev files to one directory and the test file to another, with a `MANIFEST.json` in each; training is given only the first.
- The test file was not used for early stopping, model selection or the choice of settings: hidden-layer variants A, B and C were compared on dev only (they scored 87.2 to 87.5 on dev against 91.9 without, so the default has no hidden layer). The test split was read once, for the final model.

## Results

`README.md` next to this file.

## Notes

- Credit: PolyAI Ltd. and Casanueva, Temčinas, Gerz, Henderson, Vulić 2020, "Efficient Intent Detection with Dual Sentence Encoders" (NLP4ConvAI at ACL 2020).
