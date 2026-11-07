# RecursiveAI.OneNLP.Models.Embeddings

**spaCy-class NLP for .NET.** The default English sentence embedder for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and `Nlp.Embed` turns text into a 384-value, unit-length vector. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The API will change.

**Measured.** Vectors match sentence-transformers 6.2.0 to a maximum absolute difference of 4e-7 over 2,103 texts, with no token-ID mismatches, and `mteb` 2.27.1 scores equal the same-harness sentence-transformers scores on every task: STSBenchmark 85.86 (model card 85.86), TwentyNewsgroupsClustering 48.63 (48.52), TwitterSemEval2015 73.24 (73.33), AskUbuntuDupQuestions 62.58 (62.59), SciFact 71.27 (71.28). Banking77Classification is 81.75 against the model card's 85.74: sentence-transformers itself misses the card by the same amount on that task, so the gap is in the card's number, not in the runtime. Size: 127.6 MiB of weights. Lineage: the authors' weights (MIT); nothing is trained here.


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
