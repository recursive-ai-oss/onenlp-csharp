# English fast-tier tagging recipe

Trains the English tagger (XPOS), morphologizer (UPOS and UFeats) and edit-tree lemmatizer together on one `FastEncoder`, with the public `Train` API, on Universal Dependencies English-EWT.

- **Model:** `FastEncoder` (width 96, 4 window layers; tables of 20,000 / 2,000 / 5,000 / 2,500 rows for the normalized form, prefix, suffix and shape) and three heads. 12.5 MB saved.
- **Training:** AdamW at 2e-3 with 200 warm-up steps, dropout 0.1 on the encoder output, batches of about 1,000 words, gradient clipping at 1, up to 40 epochs, evaluation on the development set after every epoch (mean of UPOS, XPOS, UFeats and Lemmas, given gold tokens), stop after 6 evaluations without a new best, best weights restored.
- **Data:** `en_ewt-ud-train.conllu` to train, `en_ewt-ud-dev.conllu` to select. The test file is only read by `predict`.

```
dotnet run -c Release --project recipes/en/fast-tagging -- train \
    --train en_ewt-ud-train.conllu --dev en_ewt-ud-dev.conllu --out model-dir --epochs 40 --seed 1 --threads 8
dotnet run -c Release --project recipes/en/fast-tagging -- predict \
    --model model-dir/model.onenlp --gold en_ewt-ud-test.conllu --mode gold --out pred-gold.conllu
dotnet run -c Release --project recipes/en/fast-tagging -- predict \
    --model model-dir/model.onenlp --gold en_ewt-ud-test.conllu --mode raw --out pred-raw.conllu
```

`train` writes a checkpoint every 100 steps (`--checkpoint-every`). Stop it any time, or give it `--budget-seconds`; running the same command again continues, and the final weights are identical to those of an uninterrupted run (same machine and thread count). Score the predictions with `tools/OneNlp.Eval ud`.

Other options: `--heads tagger|morphologizer|lemmatizer` trains one head alone (for timing), `--patience N`. `bench` reports inference throughput.

See `PROVENANCE.md` for the data, the seeds, the weights hash and the results.
