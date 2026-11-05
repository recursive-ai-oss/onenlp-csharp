# RecursiveAI.OneNLP.Models.Dutch

**spaCy-class NLP for .NET.** The Dutch fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Dutch text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Mark Rutte bezocht in mei de stad Amsterdam.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Mark Rutte woont in Den Haag."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

Dutch words with an apostrophe stay whole (`zo'n`, `auto's`), as do hyphenated compounds (`Zuid-Afrika`); the short forms `'t`, `'s` and `'n` are tokens of their own.

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `DutchModel.Register()` once.

The weights are trained on Universal Dependencies Dutch-Alpino (CC BY-SA 4.0) and WikiNER Dutch (CC BY 4.0), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
