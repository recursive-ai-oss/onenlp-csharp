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
