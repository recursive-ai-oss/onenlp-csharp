# RecursiveAI.OneNLP.Models.German

**spaCy-class NLP for .NET.** The German fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze German text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Angela Merkel besuchte im Mai die Stadt Hamburg.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Angela Merkel lebt in Berlin."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

A preposition and article that have grown together (`im`, `zum`, `vom`) are two tokens, as UD German writes them as a multiword token of two words: `im` is the tokens `i` and `m` (the words `in dem`).

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `GermanModel.Register()` once.

The weights are trained on Universal Dependencies German-GSD (CC BY-SA 4.0) and GermEval 2014 (CC BY 4.0), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
