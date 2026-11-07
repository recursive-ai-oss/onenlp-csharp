# RecursiveAI.OneNLP.Models.English

**spaCy-class NLP for .NET.** The English fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze English text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Ada Lovelace wrote the first program in London.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Ada Lovelace lived in London."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `EnglishModel.Register()` once.

The weights are trained on Universal Dependencies English-EWT and Universal NER English-EWT (both CC BY-SA 4.0), and on web text (FineWeb, ODC-By 1.0; English Wikipedia, CC BY-SA) labelled by our own previous English model, all credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
