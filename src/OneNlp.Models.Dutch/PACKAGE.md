# RecursiveAI.OneNLP.Models.Dutch

**spaCy-class NLP for .NET.** The Dutch fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Dutch text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

**Measured.** UD Dutch-Alpino r2.18 test (and WikiNER Dutch test for entities), against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 93.83 against 94.42, UFeats 92.94 against 93.13, lemmas 91.61 against 91.83 and LAS 80.28 against 79.85 (all within noise). Raw text: UPOS 93.09 against 94.36 and LAS 79.48 against 79.57 (within noise). Entities micro F1 87.47 against 86.21 (ahead, paired +1.26 [0.76, 1.72]). No headline row in the results table is behind spaCy. Data lineage: UD Dutch-Alpino (CC BY-SA 4.0) and WikiNER (CC BY 4.0 labels over Wikipedia text, so share-alike lineage); the weights carry share-alike lineage where the data does. Size: 106.3 MiB of weights. Speed against the managed reference is measured at the release gate.

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

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
