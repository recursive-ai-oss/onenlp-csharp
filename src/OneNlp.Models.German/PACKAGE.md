# RecursiveAI.OneNLP.Models.German

**spaCy-class NLP for .NET.** The German fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze German text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

**Measured.** UD German-GSD r2.18 test (and GermEval 2014 test for entities), against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 94.56 against 94.59, lemmas 95.95 against 95.65 and LAS 76.66 against 77.31 (within noise), UFeats 86.25 against 84.71 (ahead). Raw text: UPOS 94.22 against 90.13 and LAS 74.87 against 70.77 (ahead; spaCy keeps contractions such as `zum` whole). Entities micro F1 66.28 against 65.77 (within noise). No headline row in the results table is behind spaCy. Data lineage: UD German-GSD (CC BY-SA 4.0) and GermEval 2014 NER (CC BY); the weights carry share-alike lineage where the data does. Size: 105.4 MiB of weights. Speed against the managed reference is measured at the release gate.

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
