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

The word encoder is the library's `FastEncoder.English()` (the same configuration the tagging recipe uses), so the head adds no context of its own by default (`--context 0`). Any encoder option (`--width`, `--depth`, `--prefix`, `--hashes` ...) builds a `FastEncoder` of that shape instead.

`torch_ner.py` is the PyTorch twin of an earlier tagging recipe (hashed features, window head) used for a training-time comparison; it does not model `FastEncoder`.

## Design

`EntityRecognizer` (kind `ner`) reads each token's encoder vector with `context` neighbours on each side, passes them through one hidden ReLU
layer (a window classifier, Collobert et al. 2011), and decodes in one of two ways:

- **Tagging** (default): a softmax over BILUO tags (Lample et al. 2016, tagging scheme), decoded with a Viterbi search that allows only well-formed tag sequences.
- **Spans**: every span of up to `maxSpanLength` tokens is classified from its two end vectors and its width, with a "none" class; spans are chosen by probability, without overlap.

Entities already in the document (for example from `entity_ruler` placed before the recognizer) stay as they are and the model predicts around them.
Rules placed after it follow `EntityRuler.OverwriteEnts`. An entity's confidence is the mean tag probability over its tokens (tagging) or its span probability (spans).

## Results (UNER English-EWT, test split, gold tokens)

Tagging decoding, seed 7, `FastEncoder.English()`. The reference is spaCy 3.8.18 trained on the same data with its default configuration (`tools/baselines/spacy/`).
Intervals are paired bootstrap 95% (1000 resamples over sentences, seed 20260101).

| Row | Ours | spaCy | Difference |
|---|---|---|---|
| micro F1 | 61.52 [58.57, 64.31] | 62.32 [59.42, 65.33] | -0.80 [-3.48, 1.85] |
| precision | 67.10 [64.08, 70.31] | 67.97 [64.80, 71.11] | -0.87 [-4.21, 2.12] |
| recall | 56.80 [53.54, 59.91] | 57.54 [54.26, 60.91] | -0.74 [-3.63, 1.95] |
| LOC F1 | 66.77 [61.91, 71.09] | 71.43 [66.56, 76.16] | -4.66 [-8.70, -0.76] |
| ORG F1 | 46.02 [40.63, 51.01] | 44.33 [39.12, 49.82] | 1.69 [-3.58, 6.59] |
| PER F1 | 68.65 [64.45, 72.54] | 68.05 [63.91, 71.83] | 0.60 [-3.51, 4.72] |
| raw text, character spans, micro F1 (information) | 60.13 [57.08, 62.93] | | |

The paired difference includes zero on micro F1, precision, recall, ORG and PER; only LOC is lower, by 4.7 points.

Development F1 (selection set, three seeds 7, 8, 9): 59.1 to 62.1 for the chosen configuration, against 57.4 to 60.7 for the first encoder configuration.
Settings were chosen on dev over about sixty runs: affix length 3 and wider, lower-collision tables mattered most (about +1.5 dev F1 each); case-preserving text as an extra
attribute, a joining layer without layer normalization, more hash functions, and larger or smaller windows did not help beyond seed spread (about 1.5 points between seeds).
The stand-in encoder that was used before reached 62.07 on dev and 66.24 on test; this encoder does not match it (the shared configuration also serves the tagging heads).

Training: 352 s on 4 threads (13 epochs, early stop; machine shared). Indicative inference on 2,077 test sentences (25,097 tokens), one thread: about 56,000 words/s.
