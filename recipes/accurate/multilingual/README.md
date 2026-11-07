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
