# RecursiveAI.OneNLP.Neural

**spaCy-class NLP for .NET.** The neural layer of [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp), running on [OneModel](https://www.nuget.org/packages/RecursiveAI.OneModel) with no native code. Unaffiliated with other projects named OneNLP.

- **Encoders** behind one seam: the fast hash-embedding encoder, and transformer encoders (ModernBERT and BERT/XLM-R families) loaded from exported ONNX weights, which you can fine-tune.
- **Heads** on any encoder: tagger, morphologizer, edit-tree lemmatizer, biaffine dependency parser, entity recognizer, text classifier (multi-class and multi-label), span categorizer, multiword splitter and word segmenter.
- **`Train`**: the API the shipped models are trained with. AdamW, early stopping, atomic checkpoints that resume to the same weights, JSON Lines and CoNLL-U readers. The recipes in the repository are small console projects over it, so "retrain our entity recognizer on your data" means running our recipe.
- **`PipelineFile`** saves and loads a whole pipeline as one file, and `SentenceEmbedder` runs sentence-embedding models.

```csharp
using RecursiveAI.OneNlp;

Pipeline nlp = Pipeline.Blank(Language.English)
    .Add(new TextClassifier(FastEncoder.English(7)) { OwnsEncoder = true });
nlp.Train(examples, new TrainOptions { Epochs = 10, Seed = 7 });     // examples: from JsonlReader or ConllUReader
PipelineFile.Save(nlp, "model.onenlp");
```

Models come in separate packages, one per language ([RecursiveAI.OneNlp.Models.English](https://www.nuget.org/packages/RecursiveAI.OneNlp.Models.English), `.French`, `.German`, `.Spanish`, `.Portuguese`, `.Dutch`, `.Italian`, `.Chinese`, `.Turkish`) and for embeddings (`.Embeddings`, `.Embeddings.Multilingual`).

- Training and recipes: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/training.md>
- Measured results: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Licensed `MIT OR Apache-2.0`. Copyright (c) Recursive AI, LLC.
