# Accurate-tier tagging recipe (frozen transformer encoder)

Trains the `Tagger` (UPOS) on a **frozen** exported transformer (`TransformerEncoder`) with the public `Train` API, on Universal Dependencies English-EWT. It proves the accurate tier's seam: ONNX encoder in, head trained in C# on top.

- **Model:** `TransformerEncoder` over `mmbert-base.onnx` (exported by `tools/accurate/export_onnx.py`) with its `tokenizer.json`, subword pooling `mean`, and one linear layer (768 to the UPOS labels). The saved pipeline is 53 KB: it references the encoder files by name.
- **Training:** AdamW at 3e-3 with 100 warm-up steps, dropout 0.1 on the encoder output, batches of about 1,000 words, gradient clipping at 1, up to 20 epochs, UPOS on the development set after every epoch, stop after 4 evaluations without a new best, best weights restored.
- **Cache:** the encoder runs once per document (the first epoch takes the time of encoding the training and development sets; later epochs are the head alone, about 20 s each on 8 threads). The cache is in memory, so a resumed chunk encodes again.
- **Data:** `en_ewt-ud-train.conllu` to train, `en_ewt-ud-dev.conllu` to select. The test file is only read by `predict` and the scorer.

```
dotnet run -c Release --project recipes/accurate/en-tagging -- train \
    --onnx mmbert-base.onnx --tokenizer tokenizer.json \
    --train en_ewt-ud-train.conllu --dev en_ewt-ud-dev.conllu --out model-dir --seed 1 --threads 8
dotnet run -c Release --project recipes/accurate/en-tagging -- predict \
    --model model-dir/model.onenlp --gold en_ewt-ud-test.conllu --out pred-gold.conllu
```

`train` checkpoints every 100 steps; `--budget-seconds` stops after that long, and running the same command again continues (`run-chunk.sh` is one chunk). `--pooling first` takes the first piece of each token instead of the mean. `encode` times the encoder alone over a file. Score predictions with `tools/OneNlp.Eval ud --metrics UPOS`.

See `PROVENANCE.md` for data, seeds, hashes and results.

## Fine-tuning the encoder

`--finetune` trains the encoder with the head through `TrainableTransformerEncoder` (learning rate 5e-5, token table frozen unless `--train-embeddings`). `run-finetune-chunk.sh` runs one resumable chunk (`--budget-seconds`, checkpoint every 15 steps, development UPOS every 200 steps); a batch of 500 words takes about 6.6 s on 8 threads and the process needs about 11 GB. Running the same command again continues. Stopping early: resume with `--max-steps` at the number of steps done, which restores the best development checkpoint and writes `model.onenlp`.
