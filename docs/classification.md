# Text classification and span categorization

Two heads for labels you define: `TextClassifier` gives a document one or several categories, and `SpanCategorizer` labels spans of a document, including spans that overlap or nest. Both sit on any encoder (the fast encoder by default), train with `Train` ([training.md](training.md)) and are scored in [results-table.md](results-table.md).

## TextClassifier

`TextClassifier` (kind `textcat`) averages the encoder's vectors over the document's tokens and applies a linear layer, with a softmax for exclusive categories (multi-class) or independent sigmoids for any number of categories per document (`multiLabel: true`). A hidden ReLU layer is available (`hiddenSize`) and off by default. Scores land in `doc.Cats` (label to probability); multi-class scores sum to 1. The design follows the sentence-classification setups of Kim 2014 and Joulin et al. 2016.

```csharp
Pipeline nlp = Pipeline.Blank(Language.English)
    .Add(new TextClassifier(FastEncoder.English(7)) { OwnsEncoder = true });      // multiLabel: true for several labels per document
nlp.Train(examples, new TrainOptions { DevExamples = dev, Score = DevScores.Accuracy(), Seed = 7 });
Doc doc = nlp.Process("my card has not arrived");        // doc.Cats: label -> probability
```

Data is JSON Lines, one `{"text": "...", "cats": {"label_a": 1, "label_b": 0}}` per document (a label missing from a line counts as 0); see [data-formats.md](data-formats.md). The recipes `recipes/en/textcat` (BANKING77) and `recipes/en/textcat-multilabel` (EUR-LEX) run it end to end, with `train`, `predict` and `bench`.

## SpanCategorizer

`SpanCategorizer` (kind `spancat`) has a suggester and a classifier, as spaCy documents its `spancat`. The suggester proposes every n-gram of the configured sizes (1 to 3 tokens by default). The classifier represents a span as the concatenation of the mean of its token vectors, its first token's vector and its last token's vector (boundary-plus-pooled span embeddings after Lee et al. 2017), applies a hidden ReLU layer and gives one independent sigmoid per label; a span is kept for every label whose probability reaches the threshold (0.5). Results are in `doc.Spans[key]`, each a `Span` with a label and a confidence.

```csharp
Pipeline nlp = Pipeline.Blank(Language.English)
    .Add(new SpanCategorizer(FastEncoder.English(7), "sc", sizes: [1, 2, 3], hiddenSize: 256) { OwnsEncoder = true });
nlp.Train(examples, new TrainOptions { DevExamples = dev, Score = DevScores.SpanGroup("sc"), Seed = 7 });
Doc doc = nlp.Process("Hamdan v. Rumsfeld was decided in Washington.");
foreach (Span span in doc.Spans["sc"]) { /* span.Label, span.StartChar, span.EndChar, span.Confidence */ }
```

Data: `{"text": "...", "spans": {"sc": [[start, end, "LABEL"], ...]}}`, character offsets, with `tokens` optional. The span precision of the shipped recipe is below spaCy's default at the default threshold while recall and F1 are above ([results-table.md](results-table.md)); tune the threshold on your development set.

## Entities from the entity recognizer, and rules

`EntityRecognizer` labels non-overlapping entities by tagging tokens; use the span categorizer when spans can overlap. Entities from rules (`EntityRuler`, the recognizers) coexist in `doc.Ents` with model entities ([matching.md](matching.md), [recognizers.md](recognizers.md)).
