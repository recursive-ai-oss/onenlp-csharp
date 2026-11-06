# RecursiveAI.OneNLP.Models.Embeddings.Multilingual

**spaCy-class NLP for .NET.** A multilingual sentence embedder for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and `Nlp.Embed` turns text in about 100 languages into a 384-value, unit-length vector. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The API will change.

```csharp
using RecursiveAI.OneNlp;

float[] a = Nlp.Embed("Wie setze ich mein Passwort zurück?");
float[] b = Nlp.Embed("How do I reset my password?");
float cosine = a.Zip(b, (x, y) => x * y).Sum();   // vectors have unit length
```

The model is `intfloat/multilingual-e5-small` (MIT), exported to ONNX from the authors' published weights and run on OneModel; nothing is trained here. The 470 MB of weights are split by byte range into three `...Embeddings.Multilingual.Shard1..3` packages (NuGet's size limit), which this package depends on; the shards are joined in memory at load time. When this package and `RecursiveAI.OneNlp.Models.Embeddings` (English, bge) are both referenced, `Nlp.Embed` uses this one; call `EmbeddingsModel.Load()` for the English model.

The model expects `"query: "` / `"passage: "` prefixes: `Nlp.Embed` uses `"query: "` (right for similarity); for retrieval load `MultilingualEmbeddingsModel.Load()` and call `EmbedQuery` and `EmbedPassage`. `PROVENANCE.md` states the licence and the authors' training data.

- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>

Copyright (c) Recursive AI, LLC.
