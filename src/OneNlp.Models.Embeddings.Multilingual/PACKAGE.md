# RecursiveAI.OneNLP.Models.Embeddings.Multilingual

**spaCy-class NLP for .NET.** A multilingual sentence embedder for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and `Nlp.Embed` turns text in about 100 languages into a 384-value, unit-length vector. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The API will change.

**Measured.** Vectors match sentence-transformers 6.2.0 to a maximum absolute difference of 3e-7 over 2,103 texts, and `mteb` 2.27.1 scores equal the same-harness sentence-transformers scores on every task. Against the model card, four cells are lower for sentence-transformers itself too: Banking77Classification 70.44 against 79.42, MTOPDomainClassification (en, de, es, fr) 85.59 against 88.01, AskUbuntuDupQuestions 56.42 against 57.88, and STS22 65.13 against 66.01 (66.10 under `mteb` 1.12.94); the others are within 0.5 of the card (STSBenchmark 84.11 against 84.01, TwentyNewsgroupsClustering 32.81 against 33.22, TwitterSemEval2015 70.75 against 70.95, SciFact 67.70 against 67.70). Size: 448.4 MiB of weights across three shard packages plus 16.3 MiB here. Lineage: the authors' weights (MIT); nothing is trained here.


```csharp
using RecursiveAI.OneNlp;

float[] a = Nlp.Embed("Wie setze ich mein Passwort zurück?");
float[] b = Nlp.Embed("How do I reset my password?");
float cosine = a.Zip(b, (x, y) => x * y).Sum();   // vectors have unit length
```

The model is `intfloat/multilingual-e5-small` (MIT), exported to ONNX from the authors' published weights and run on OneModel; nothing is trained here. The 470 MB of weights are split by byte range into three `...Embeddings.Multilingual.Shard1..3` packages (NuGet's size limit), which this package depends on; the shards are joined in memory at load time. When this package and `RecursiveAI.OneNlp.Models.Embeddings` (English, bge) are both referenced, `Nlp.Embed` uses this one; call `EmbeddingsModel.Load()` for the English model.

The model expects `"query: "` / `"passage: "` prefixes: `Nlp.Embed` uses `"query: "` (right for similarity); for retrieval load `MultilingualEmbeddingsModel.Load()` and call `EmbedQuery` and `EmbedPassage`. `PROVENANCE.md` states the licence and the authors' training data.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>

Copyright (c) Recursive AI, LLC.
