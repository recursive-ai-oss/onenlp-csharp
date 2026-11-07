# Provenance: en-textcat-multilabel (LexGLUE EUR-LEX)

- **Model:** `en-textcat-eurlex`, version 0 (development); a multi-label text classifier (100 EuroVoc concepts) over the library fast encoder (`FastEncoder.English()`), mean-pooled, no hidden layer. English, fast tier. Format: `docs/provenance.md`.
- **Recipe:** `recipes/en/textcat-multilabel/` (`Program.cs`); defaults are the configuration (dropout 0.2, learning rate 0.003, batch 2000 words, patience 6, no hidden layer) with `--epochs 6`.
- **Seed(s):** training, data order, initial weights and encoder 7 (`--seed`); the data splits are the official ones.
- **Produced:** 2026-11-06; shared 32-core x64 worker, 4 threads, in resumable 25-minute chunks (checkpoint every 200 steps); .NET SDK 10.0.403; RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0; 92,932 steps over 6 epochs, about 3 hours.
- **Weights:** `model.onenlp`, 35,504,475 bytes, file sha256 `e7c9471c5095d22c9d88778eb351adfa2ff4b2b828f0a56c7d9cd439640f41af`, weights hash reported by `Train` `d186d97ad83354ce8d94bf4043d6e86ba4193ec076514f22dcdc317221d02f06`. Kept on the build machine; not in git. Best development micro-F1 70.357.
- **SA lineage:** no. The labels are the EU Publications Office's, the texts are EUR-Lex documents, and the dataset card says CC BY 4.0.
- **Licence of the weights:** not released; development model.

## Training data

| Dataset | Release | Role | Files | Licence | URL | sha256 |
|---|---|---|---|---|---|---|
| LexGLUE EUR-LEX (Chalkidis et al. 2022), from MultiEURLEX (Chalkidis, Fergadiotis, Androutsopoulos 2021) | `coastalcph/lex_glue` revision `c23fdff1a6bf74e0e1a71cb86f1e781d37da888c`, `eurlex/train-00000-of-00001.parquet` | train (55,000 documents, first 512 whitespace words each) | `train.jsonl` | dataset card: `cc-by-4.0`; EUR-Lex documents reusable under Commission Decision 2011/833/EU Art. 6 ("without restrictions", conditions may include acknowledging the source); (c) European Union | huggingface.co/datasets/coastalcph/lex_glue | `08a1456d98edd7afc236b84bea5f273f5965ec2580252db83bd519b45438fa31` |
| same | same, `eurlex/validation-...parquet` | dev (5,000; early stopping and best-epoch choice) | `dev.jsonl` | same | same | `67e39cb5ae1f1584590698b5d61b48a6e3a7ffbafd7ba5b461273d924f2e13a9` |

The raw Parquet hashes are in each directory's `MANIFEST.json` written by `tools/fetch/textcat/fetch.py eurlex`.

Credit: EU Publications Office (EuroVoc annotation of EUR-Lex documents, (c) European Union); Chalkidis, Fergadiotis and Androutsopoulos 2021, "MultiEURLEX", EMNLP; Chalkidis, Jana, Hartung, Bommarito, Androutsopoulos, Katz and Aletras 2022, "LexGLUE", ACL. Changes made: plain-text documents cut to their first 512 words, labels renamed `c00` to `c99` by EuroVoc concept order of the release.

## Label generator

None: labels are the dataset's gold annotations.

## Pretrained components

None; trained from scratch.

## Evaluation data

| Dataset | Release | Role | Files | sha256 | Used for |
|---|---|---|---|---|---|
| LexGLUE EUR-LEX | same revision, `eurlex/test-00000-of-00001.parquet` (5,000 documents) | eval | `test.jsonl` | `442659c8e93585e9cf02d15da22465711df0f76773098d7c5c1d611b541d1e6c` | reported rows, once for the final model |

## Train/test separation

- The split is the dataset's official one (chronological: training 1958-2010, development 2010-2012, test 2012-2016 per the MultiEURLEX card). The test hash was recorded before training. The fetch script writes train and dev to one directory and test to another, with a `MANIFEST.json` in each; training is given only the first.
- The test file was not used for early stopping, model selection or the choice of settings: the 6-epoch cap was set from the speed of the run, and the checkpoint kept is the best on dev. It was read once for the final model.

## Results

`README.md` next to this file; predictions `ours-pred-test.jsonl` sha256 `61e38cb65eed12da30135ac64028b243a21a2abe9a1d39cd9304d316026a59b0`, spaCy's `ae10e8de9c7f2c57774655b60446681841232fb883947cd99f8885e36763a5e9`.

## Notes

- The spaCy reference is spaCy 3.8.18 `textcat_multilabel` with `spacy init config` defaults (config sha256 `e0cc5397abbc1bcd83acfe7090137e96ac4b9c84e4e4f90cd768024ca958e810`), trained on the same files on the Mac mini with one core, 627 s.
