The multilingual accurate-tier recipe: one fine-tuned mmBERT-base encoder shared by per-language heads.

```
dotnet RecursiveAI.OneNlp.Recipes.Accurate.dll train --langs de,tr --data DATA --onnx mmbert-base.onnx --tokenizer tokenizer.json --out OUT [--steps 500] [--lr 1e-4]
```

`run-chunk.sh` runs one resumable time-boxed chunk; `run-chain.sh` repeats chunks through a job queue until the run completes. Each language gets a tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer and biaffine parser, trained only on that language's sentences (`ScopedHead`); the encoder sees every language. A single language is plain per-language fine-tuning. A new treebank is a row in `Treebanks`.

Shared versus per-language, development sets (UD r2.18 German-GSD and Turkish-BOUN), 500 steps, 400 words per language per step, learning rate 1e-4, seed 1:

| Run | de UPOS | de UFeats | de XPOS | de Lemmas | de LAS | tr UPOS | tr UFeats | tr Lemmas | tr LAS |
|---|---|---|---|---|---|---|---|---|---|
| per language | 95.20 | 86.96 | 95.42 | 92.06 | 84.13 | 91.50 | 82.50 | 81.54 | 70.92 |
| shared | 95.07 | 86.62 | 95.14 | 92.18 | 84.67 | 91.20 | 81.23 | 79.55 | 69.95 |

Single runs, no confidence intervals: indicative only.

## Continuing a run, lemma tables, the packaged model

- `--init last.ckpt --init-steps N` starts a new run from the weights and optimizer state of an earlier one (the learning-rate schedule is shifted by N steps, so a half cosine given for the new steps starts at its peak). `--repeat zh=1.8,it=3.5,de=0.45` sets how many copies of each listed language's treebank the training list holds (a fraction takes evenly spaced sentences), `--ner-use N` limits the entity documents of each language. The label sets of the heads are always built from the list as `--balance` leaves it, so a checkpoint of an earlier run fits.
- `--lexicon tr=1:0.6,de=1:0.6` builds a lemma table from each listed language's training treebank (minimum count : minimum share) in front of its edit trees; `tools/accurate/lexicon_dev.py` picks the settings on development predictions (`--predict-split dev --predict-limit 400`).
- `--zh-segmenter FILE` puts a trained Chinese word segmenter (a saved pipeline whose tokenizer is a `NeuralWordSegmenter`) in front of the Chinese pipeline, in exported files and in raw-text predictions.
- `--export DIR` writes the shipped files: the heads, the encoder as an ONNX graph (`encoder.onnx`; cut into pieces by `tools/accurate/split_graph.py`) and the trainable layers' checkpoint. `predict-package --export DIR --data DATA --split test --raw 1 --out DIR` predicts a split with the packaged model; `tools/accurate/score_ml.sh` scores it.

## Corpus-driven runs

`run-84.sh` reads `../common/corpus.json`: all allowed training treebanks, the held-out development splits, entity sets and unspaced-script flags. It warm-starts the encoder and existing heads by tensor name, initializes the other heads, and uses temperature sampling (`--plan 0.3`) with parser loss weight 1.5. The reproducible run defaults are seed 3, 1,200 cosine steps from 6e-5, 3,000 words per batch, and evaluation every 150 steps on at most 10 four-sentence documents per language (100 entity sentences). These small development samples make selection noisy. Checkpoints keep the optimizer at chunk boundaries and the best model at evaluations. Each training budget is 1,100 seconds to leave room for loading, evaluation and checkpoint I/O inside a 30-minute queue job.

- `--tune-lexicons 1` chooses a lemma table for each language after restoring the best checkpoint. The grid is minimum count 1 or 2 and minimum share 0.6, 0.8, 0.9 or 1.0. It uses the model's predicted UPOS on the development documents and the complete, pre-sampling training references (with held-out sentences excluded). A table must improve lemma accuracy strictly; ties prefer no table, then the first grid setting. Languages with no development data keep the edit trees alone. `OUT/lexicon-dev.tsv` records the choices; no evaluation data is consulted.
- To finalize a budget-stopped run without another update, resume it with `--steps 1` and the **same language order, corpus, sampling and initialization settings**. The existing step already exceeds this bound, so training restores its best weights without taking a step. Use `--tune-lexicons 1 --export NEW_DIRECTORY` to export them. Use a new export directory: an existing `encoder.onnx` is deliberately not overwritten.
- `predict-package` and `verify-export` accept `--spec`, `--segmenter-dir` and `--zh-segmenter`, just like training. The four additional segmenter pipelines are `DIRECTORY/{ja,lzh,th,vi}/model.onenlp`. The exported language heads embed their tokenizer, including a segmenter when present.
- `tools/accurate/score_corpus.py SPEC DATA PREDICTIONS EVAL_DLL SCORES` scores all spec-defined gating test files. It resumes between datasets and does not report XPOS where the gold annotation is absent.
- `tools/accurate/compare_refs.py SCORES docs/references.tsv --spec SPEC --before PREVIOUS_SCORES` writes both reference views: best published per matching row and Stanza 1.5.1 pretrained/charlm without a transformer. Input modes and treebanks are never crossed, and missing references are not passes. `--treebanks` supplies the catalogue used to identify the actual test treebank of combined training entries.
- `tools/accurate/stage_large_weights.py EXPORT WEIGHTS --repo CHECKOUT --spec SPEC` copies the exported weights, cuts the encoder into six byte-preserving pieces, and generates bounded language-head parts, manifests and static dispatch. Each extra head part holds at most 200 MB uncompressed. No tensor is quantized or removed to fit a package.
