# English fast-tier tagging recipe

Trains the English tagger (XPOS), morphologizer (UPOS and UFeats) and edit-tree lemmatizer together on one `FastEncoder`, with the public `Train` API, on Universal Dependencies English-EWT.

- **Model:** `FastEncoder.English()` (64-wide hash embeddings, one hash function and 32,768 rows for each of the lower-cased form, 3-character prefix, 3-character suffix and shape, mixed to 256 and two window layers) and three heads. 37 MB saved.
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
