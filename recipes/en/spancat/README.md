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
a span is kept for every label whose probability reaches the threshold (0.5), so overlapping spans and several labels per span are allowed. The
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

The precision miss is reported as measured; the threshold (`--threshold`, default 0.5, as spaCy) was not tuned.
