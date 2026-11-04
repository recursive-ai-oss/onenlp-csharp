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
    .Add(new EntityRecognizer(encoder, NerDecoding.Tagging, context: 2, hidden: 256) { OwnsEncoder = true });
pipeline.Train(JsonlReader.Read("train.jsonl").ToList(), new TrainOptions { DevExamples = dev, Score = DevScores.Entities(), Seed = 7 });
PipelineFile.Save(pipeline, "model.onenlp");
```

The recipe's word encoder (`LexicalEncoder.cs`) hashes each token's lower-cased form, 3-character prefix and suffix, and shape into one embedding
table. It stands in until the library's fast encoder is used, which swaps in with one line.

`torch_ner.py` is the same tagging recipe in PyTorch on CPU, for the training-time comparison.

## Design

`EntityRecognizer` (kind `ner`) reads each token's encoder vector with `context` neighbours on each side, passes them through one hidden ReLU
layer (a window classifier, Collobert et al. 2011), and decodes in one of two ways:

- **Tagging** (default): a softmax over BILUO tags (Lample et al. 2016, tagging scheme), decoded with a Viterbi search that allows only well-formed tag sequences.
- **Spans**: every span of up to `maxSpanLength` tokens is classified from its two end vectors and its width, with a "none" class; spans are chosen by probability, without overlap.

Entities already in the document (for example from `entity_ruler` placed before the recognizer) stay as they are and the model predicts around them.
Rules placed after it follow `EntityRuler.OverwriteEnts`. An entity's confidence is the mean tag probability over its tokens (tagging) or its span probability (spans).

## Results (UNER English-EWT, test split, gold tokens)

Seed 7, encoder `LexicalEncoder`. The reference is spaCy 3.8.18 trained on the same data with its default configuration (`tools/baselines/spacy/`).
Intervals are paired bootstrap 95% (1000 resamples over sentences, seed 20260101).

| Row | Tagging | Spans | spaCy |
|---|---|---|---|
| micro F1 | 66.24 [63.33, 69.35] | 63.93 [60.76, 67.02] | 62.32 [59.42, 65.33] |
| precision | 71.66 | 71.35 | 67.97 |
| recall | 61.58 | 57.90 | 57.54 |
| LOC F1 | 73.50 [68.95, 77.83] | 68.80 [63.89, 73.39] | 71.43 [66.56, 76.16] |
| ORG F1 | 48.47 [43.06, 53.99] | 46.57 [40.66, 52.67] | 44.33 [39.12, 49.82] |
| PER F1 | 72.62 [68.53, 76.50] | 70.11 [66.27, 73.88] | 68.05 [63.91, 71.83] |
| raw text, character spans, micro F1 (information) | 64.73 [61.70, 67.75] | 62.56 [59.52, 65.57] | |

Development F1 (the selection set): tagging 62.07 (seeds 7, 8, 9: 62.07, 63.80, 63.03), spans 61.48.

Training (4 threads, one worker core group): tagging 93 s (12 epochs, stopped early), spans 425 s (22 epochs). Indicative inference on 2,077 test sentences (25,097 tokens),
one thread: tagging 81,000 words/s, spans 26,000 words/s.
