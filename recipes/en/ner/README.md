# English named entity recognizer

Trains a named entity recognizer on any JSON Lines file of examples and scores it. The same commands work for your own data and your own labels.

```
dotnet run -c Release --project recipes/en/ner -- convert en_ewt-ud-train.iob2 train.jsonl     # UNER .iob2 -> JSON Lines
dotnet run -c Release --project recipes/en/ner -- train train.jsonl dev.jsonl out/ --decoding tagging
dotnet run -c Release --project recipes/en/ner -- eval out/model.onenlp test.jsonl results/ [--ref reference.tsv] [--raw]
dotnet run -c Release --project recipes/en/ner -- speed out/model.onenlp test.jsonl 1
```

`run.sh <data-root> <work-dir> <tagging|spans>` converts the UNER files and trains. Each line of the data file is
`{"text": "...", "tokens": [[s, e], ...], "entities": [[s, e, "LABEL"], ...]}` (`docs/data-formats.md`); `tokens` is optional, and any set of
labels is learned. Everything is the public API:

```csharp
Pipeline pipeline = Pipeline.Blank(Language.English)
    .Add(new EntityRecognizer(encoder, NerDecoding.Tagging, context: 0, hidden: 256) { OwnsEncoder = true });
pipeline.Train(JsonlReader.Read("train.jsonl").ToList(), new TrainOptions { DevExamples = dev, Score = DevScores.Entities(), Seed = 7 });
PipelineFile.Save(pipeline, "model.onenlp");
```

The word encoder is the library's `FastEncoder` (width 96, 4 window layers), so the head adds no context of its own by default (`--context 0`).

`torch_ner.py` is the PyTorch twin of the earlier tagging recipe (hashed features, window head) used for a training-time comparison; it does not model `FastEncoder`.

## Design

`EntityRecognizer` (kind `ner`) reads each token's encoder vector with `context` neighbours on each side, passes them through one hidden ReLU
layer (a window classifier, Collobert et al. 2011), and decodes in one of two ways:

- **Tagging** (default): a softmax over BILUO tags (Lample et al. 2016, tagging scheme), decoded with a Viterbi search that allows only well-formed tag sequences.
- **Spans**: every span of up to `maxSpanLength` tokens is classified from its two end vectors and its width, with a "none" class; spans are chosen by probability, without overlap.

Entities already in the document (for example from `entity_ruler` placed before the recognizer) stay as they are and the model predicts around them.
Rules placed after it follow `EntityRuler.OverwriteEnts`. An entity's confidence is the mean tag probability over its tokens (tagging) or its span probability (spans).

## Results (UNER English-EWT, test split, gold tokens)

Tagging decoding, seed 7, `FastEncoder`. The reference is spaCy 3.8.18 trained on the same data with its default configuration (`tools/baselines/spacy/`).
Intervals are paired bootstrap 95% (1000 resamples over sentences, seed 20260101).

| Row | Ours | spaCy | Difference |
|---|---|---|---|
| micro F1 | 58.92 [55.71, 62.02] | 62.32 [59.42, 65.33] | -3.40 [-6.01, -0.95] |
| precision | 64.38 [60.69, 67.76] | 67.97 [64.80, 71.11] | -3.59 [-6.80, -0.47] |
| recall | 54.32 [51.05, 57.65] | 57.54 [54.26, 60.91] | -3.22 [-5.97, -0.56] |
| LOC F1 | 66.99 [61.96, 71.43] | 71.43 [66.56, 76.16] | -4.44 [-8.74, -0.22] |
| ORG F1 | 42.63 [36.95, 48.31] | 44.33 [39.12, 49.82] | -1.70 [-6.77, 3.74] |
| PER F1 | 63.99 [59.90, 67.85] | 68.05 [63.91, 71.83] | -4.06 [-7.57, -0.38] |
| raw text, character spans, micro F1 (information) | 57.44 [54.37, 60.45] | | |

The intervals of ours and spaCy's overlap on every row, but the paired difference excludes zero on micro F1, precision, recall, LOC and PER.

Development F1 (selection set): 59.97 (seeds 7, 8, 9: 59.97, 59.12, 59.06). A dev sweep of context (0 to 2), encoder width (96 to 256), depth (2 to 6),
norm-table rows, dropout and learning rate stayed between 57.4 and 60.1, within seed spread, so the defaults are the simplest setting at the top of the range.
The earlier stand-in encoder (hashed lower-case, prefix, suffix and shape features joined and projected to 256, with a window of 2 in the head) reached 62.07 on dev
and 66.24 [63.33, 69.35] on test, so the encoder, not the head, sets the level.

Training: 122 s on 4 threads (11 epochs, early stop). Indicative inference on 2,077 test sentences (25,097 tokens), one thread: about 71,000 words/s.
