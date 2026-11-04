# RecursiveAI.OneNLP

**spaCy-class NLP for .NET.** A clean-room library for .NET 10 with no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview, under construction.** This package holds the shared types and contracts only: `Doc`, `Token` and `Span` with character offsets into the original string, a component `Pipeline`, the `Tokenizer` contract and the `Nlp` statics. There are no models or tokenizer families yet, and the API will change. No accuracy, speed or parity claims are made.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Ada Lovelace wrote the first program.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t.Idx}..{t.EndIdx} {t}");
}
```

It depends on the .NET base class library and [RecursiveAI.OneFont](https://www.nuget.org/packages/RecursiveAI.OneFont) only.

- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Changelog: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/CHANGELOG.md>
- Home: <https://recursiveai.org/onenlp/>

Licensed `MIT OR Apache-2.0`. Copyright (c) Recursive AI, LLC.
