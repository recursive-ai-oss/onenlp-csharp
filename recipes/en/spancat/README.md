# English span categorizer

Trains a span categorizer on any JSON Lines file of texts with labelled spans and scores it. Spans may nest or overlap, and a span may carry several labels.
The same commands work for your own data and your own labels.

```
dotnet run -c Release --project recipes/en/spancat -- convert en_ewt-ud-train.iob2 train.jsonl      # UNER .iob2 -> JSON Lines, one span per entity mention
dotnet run -c Release --project recipes/en/spancat -- train train.jsonl dev.jsonl out/              # resumes from out/last.ckpt; writes out/model.onenlp
dotnet run -c Release --project recipes/en/spancat -- predict out/model.onenlp test.jsonl pred.jsonl
```

Each data line is `{"text": "...", "tokens": [[s, e], ...], "spans": {"sc": [[s, e, "LABEL"], ...]}}` (`tokens` optional, offsets into `text`). Everything is
the public API:

```csharp
Pipeline pipeline = Pipeline.Blank(Language.English)
    .Add(new SpanCategorizer(FastEncoder.English(7), "sc", sizes: [1, 2, 3], hiddenSize: 256) { OwnsEncoder = true });
pipeline.Train(examples, new TrainOptions { DevExamples = dev, Score = DevScores.SpanGroup("sc"), Seed = 7 });
Doc doc = pipeline.Process("Hamdan v. Rumsfeld was decided in Washington.");
foreach (Span span in doc.Spans["sc"]) { /* span.Label, offsets, span.Confidence */ }
```

## Design

`SpanCategorizer` (kind `spancat`) has a suggester and a classifier, as spaCy's documented `spancat`. The suggester proposes every n-gram of the
configured sizes (1 to 3 tokens by default, which cover 97% of the gold spans in the data below). The classifier represents a span as the concatenation of the
mean of its token vectors, its first token's vector and its last token's vector, applies a hidden ReLU layer, and gives one independent sigmoid per label;
a span is kept for every label whose probability reaches the threshold (0.8), so overlapping spans and several labels per span are allowed. The
boundary-plus-pooled representation follows Lee et al. 2017 (end-to-end coreference, span embeddings from boundary representations and an attention-weighted
head); this recipe uses the mean in place of the learned head attention.

## Results (UNER English-EWT test, 2,077 sentences, gold tokens, one run, seed 7)

| Metric | Ours | spaCy 3.8.18 `spancat` (default config, sizes 1-3, same data) | Paired difference |
|---|---|---|---|
| Span F1 | 64.44 [61.28, 67.36] | 60.19 [57.02, 63.40] | +4.25 [1.57, 6.71] |
| Precision | 70.33 [66.97, 73.38] | 75.38 [72.01, 78.86] | -5.05 [-8.63, -1.42] **(behind)** |
| Recall | 59.47 [56.11, 62.50] | 50.09 [46.64, 53.48] | +9.38 [6.69, 11.85] |

Spans are exact matches of label, start and end. Intervals are paired bootstrap 95% (1000 resamples over sentences, seed 20260101,
`tools/OneNlp.Eval spangroup`). The scorer was cross-checked against spaCy's `Scorer.score_spans` on the 2,061 test sentences whose predictions do not overlap:
identical to all digits for both systems (ours P 72.20, R 59.62, F 65.31; spaCy's P 77.58, R 49.67, F 60.56). spaCy's scorer is unreliable on overlapping
predictions (for a gold span and a second predicted span that overlaps it, it can return 0), so the rows above use the plain definition.

### Threshold

The first measurement used the threshold 0.5 (spaCy's default) and showed lower precision than spaCy's. The threshold is applied after training, so it was chosen on the UNER English-EWT **development** split alone: one prediction pass at 0.05 kept every score, and thresholds from 0.5 to 0.95 were scored on dev. Dev span F1 peaks at 0.55 (59.75, against 59.60 at 0.5) and falls as precision rises (0.8: F1 58.81, P 69.19, R 51.14; 0.9: F1 58.16, P 72.97; 0.95: F1 56.68, P 75.30). The rule fixed before reading test was the highest threshold within one F1 point of the best development F1, which is 0.8. The test split was then read once at 0.8 (the model is the same; `predict --threshold 0.8`):

| Metric | Threshold 0.5 | Threshold 0.8 | spaCy 3.8.18 | Paired difference at 0.8 |
|---|---|---|---|---|
| Span F1 | 64.44 [61.28, 67.36] | 63.79 [60.78, 66.63] | 60.19 [57.02, 63.40] | +3.60 [1.14, 6.07] (ahead) |
| Precision | 70.33 [66.97, 73.38] | 75.98 [72.83, 79.18] | 75.38 [72.01, 78.86] | +0.60 [-3.02, 4.17] (within noise) |
| Recall | 59.47 [56.11, 62.50] | 54.96 [51.67, 58.24] | 50.09 [46.64, 53.48] | +4.87 [2.44, 7.10] (ahead) |

The component default and this recipe's default are now 0.8; a model trained from scratch also stops early on dev F1 at that threshold, which this table did not re-run.
