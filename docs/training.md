# Training and recipes

Everything a user can train, the shipped models were trained with the same public `Train` API, on [OneModel](https://www.nuget.org/packages/RecursiveAI.OneModel) (automatic differentiation, AdamW, checkpoints), in C#. A recipe is a small console project over that API; running ours on your data is "retraining our model". Behaviour of `Train`, the heads and the encoders is written down in [contracts.md](contracts.md); file formats in [data-formats.md](data-formats.md) and [model-files.md](model-files.md).

## The call

The code below is `samples/OneNlp.Samples/TrainSample.cs`, which builds and runs.

```csharp
string[] lines =
[
    "{\"text\": \"my card has not arrived\", \"cats\": {\"card\": 1, \"pin\": 0}}",
    "{\"text\": \"where is my new card\", \"cats\": {\"card\": 1, \"pin\": 0}}",
    "{\"text\": \"the card never came in the post\", \"cats\": {\"card\": 1, \"pin\": 0}}",
    "{\"text\": \"I forgot my pin\", \"cats\": {\"card\": 0, \"pin\": 1}}",
    "{\"text\": \"how do I change my pin\", \"cats\": {\"card\": 0, \"pin\": 1}}",
    "{\"text\": \"my pin is not working\", \"cats\": {\"card\": 0, \"pin\": 1}}",
];
using Pipeline nlp = Pipeline.Blank(Language.English)
    .Add(new TextClassifier(FastEncoder.English(7)) { OwnsEncoder = true });
List<Example> examples = [.. JsonlReader.Read(new StringReader(string.Join('\n', lines)), nlp)];

// A fixed seed makes the run repeatable; with CheckpointDirectory set, an interrupted run resumes from its last checkpoint.
TrainResult result = nlp.Train(examples, new TrainOptions { Epochs = 30, Seed = 7, Dropout = 0f, DevExamples = examples });

string path = Path.Combine(Path.GetTempPath(), "onenlp-sample-" + Guid.NewGuid().ToString("N") + ".onenlp");
PipelineFile.Save(nlp, path);                                  // one file: tokenizer, encoder, heads, labels
using Pipeline loaded = PipelineFile.Load(path);
Doc doc = loaded.Process("I lost my pin");
// doc.Cats maps each label to a probability
```

(The sample uses its training set as the development set so that it finishes in seconds; real runs keep them apart.)

- **Adding labels.** A head without weights is built from the labels of the training examples. A head that already has weights (a packaged pipeline) keeps them: an `EntityRecognizer` appends the labels the examples add, with fresh weights for the new outputs, so `Pipeline.Create(Language.English).Add<EntityRecognizer>()` followed by `Train` on examples with an `ORDER` label learns `ORDER` beside `PER`, `ORG` and `LOC` (include examples of the old labels to keep them). A `Tagger` or `TextClassifier` has a fixed label set and throws `ArgumentException` naming the values it does not have.
- **Data.** `JsonlReader` reads JSON Lines (`text`, optional `tokens`, `entities`, `spans`, `cats`); `ConllUReader` reads Universal Dependencies files. `Example` pairs a predicted `Doc` with a reference `Doc`; when the tokenizations differ it aligns them. For data already in memory, `Example.Create(nlp, text, [(start, end, "LABEL")])` takes character offsets and tokenizes with the pipeline. A training program needs `using RecursiveAI.OneNlp;` (pipeline, `Example`, `TrainOptions`), `.Neural` (`PipelineFile`), `.Neural.Components` and `.Neural.Encoders`.
- **Heads.** `Tagger`, `Morphologizer`, `EditTreeLemmatizer`, `Parser`, `EntityRecognizer`, `TextClassifier`, `SpanCategorizer`, on `FastEncoder` or a transformer encoder; and, as trained word tokenizers, `NeuralMultiwordSplitter` (Turkish and similar) and `NeuralWordSegmenter` (Chinese). Heads that share one encoder instance share its work and add their losses.
- **Options.** `TrainOptions` has epochs, batch size in words, learning rate and schedule, warm-up, weight decay, clipping, dropout, seed, a frozen-encoder switch, the development set (`DevExamples` or a split), the score function (`DevScores.Ud(...)`, `DevScores.Entities()`, `DevScores.Accuracy()`), evaluation interval, patience, `CheckpointDirectory` and a progress callback. The library never writes to the console.
- **Repeatable.** The batches depend only on the seed and the epoch. A resumed run reaches the same weights as an uninterrupted one (same machine and thread count). The best development checkpoint is restored at the end.
- **Fine-tuning a transformer.** `TrainableTransformerEncoder` loads exported ONNX weights (ModernBERT and BERT/XLM-R families) as trainable modules and fine-tunes them with the same call; the written-back weights run in the graph runtime. See [accurate-encoder.md](accurate-encoder.md).

## Recipes

`recipes/<language>/...` and `recipes/en/...` hold one console project per trained part. Each has a `README.md` with its commands, a `PROVENANCE.md` and the results it produced:

| Recipe | Trains | Data |
|---|---|---|
| `recipes/en/fast-tagging`, `parser`, `ner`, `assemble`, `selftrain` | the English tagging, parsing and entity models, assembled into one pipeline file; the self-training that made the packaged encoders narrower | UD English-EWT, UNER English-EWT, self-labelled web text |
| `recipes/en/joint` | one shared encoder for all English heads (an option; smaller and faster, weaker rows) | UD English-EWT, UNER English-EWT |
| `recipes/en/textcat`, `textcat-multilabel`, `spancat` | the text classifier (multi-class and multi-label) and the span categorizer | BANKING77, EUR-LEX, UNER English-EWT |
| `recipes/fr`, `de/fast`, `es/fast`, `pt/fast`, `nl/fast`, `it/fast`, `tr/fast`, `zh/fast` | the fast tier of each language: tagging, parsing, entities, and the multiword splitter or word segmenter where the language needs one | UD treebanks and the named-entity sets listed per recipe |
| `recipes/langid` | the language-ID model | held-out multilingual text, listed in its manifest |
| `recipes/accurate/en-tagging`, `probe`, `student-v1` | heads on a frozen or fine-tuned transformer encoder; the distilled student's results | UD English-EWT, UNER English-EWT |

Conventions every recipe follows:

- **Data is fetched by script and checked by hash.** `tools/fetch/` fetches treebanks and pins them; the manifest of each recipe lists every dataset with its role (`train`, `dev` or `eval`) and licence. A recipe reads only its `train` and `dev` files; the test files are read by `predict` and the scorer.
- **Resumable.** Every `train` writes checkpoints and continues when run again with the same arguments, so a limited job is chained until the model file appears. `--budget-seconds` stops cleanly.
- **Provenance.** `PROVENANCE.md` records the datasets (version, URL, hash, licence), the label generator if any, the recipe and seed, the evaluation sets and the output weights' hash.
- **Scoring.** `predict` writes CoNLL-U or entity files that `tools/OneNlp.Eval` scores with the CoNLL 2018 definitions and paired bootstrap intervals ([measurement.md](measurement.md)).

A model you train this way loads with `PipelineFile.Load` and plugs into `Pipeline` like ours. Models from elsewhere load too: any exported ONNX encoder through `TransformerEncoder` and `TrainableTransformerEncoder`, and any sentence-embedding model through `SentenceEmbedder.Load`. The licence rules for what we ship (permissive weights only) do not limit what you load.

## Speed of training

The C# recipes were timed against the same recipes in plain PyTorch on CPU (same data, architecture, batch size and threads); the ratios are in `tools/ratio/RESULTS.md` and the step timing of a transformer fine-tune in [accurate-encoder.md](accurate-encoder.md).
