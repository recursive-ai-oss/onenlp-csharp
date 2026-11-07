# RecursiveAI.OneNLP.Models.Embeddings

**spaCy-class NLP for .NET.** The default English sentence embedder for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and `Nlp.Embed` turns text into a 384-value, unit-length vector. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The API will change.

```csharp
using RecursiveAI.OneNlp;

float[] a = Nlp.Embed("how do I reset my password");
float[] b = Nlp.Embed("I forgot my login credentials");
float cosine = a.Zip(b, (x, y) => x * y).Sum();   // vectors have unit length
```

The model is BAAI `bge-small-en-v1.5` (MIT), exported to ONNX from the authors' published weights and run on OneModel; nothing is trained here. Vectors match sentence-transformers to float tolerance (`docs/embeddings.md` in the source repository has the parity and MTEB results). `PROVENANCE.md` states the licence and the authors' training data.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>

Copyright (c) Recursive AI, LLC.
