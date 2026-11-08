# RecursiveAI.OneNLP.Models.English

**spaCy-class NLP for .NET.** The English fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze English text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

**Measured.** UD English-EWT r2.18 test (and UNER English-EWT test for entities), against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 94.16 against 94.29, UFeats 95.15 against 95.18, lemmas 96.11 against 95.80 and LAS 81.53 against 81.96 (all within noise). Raw text: UPOS 92.90 against 92.45 and LAS 77.26 against 78.53 (within noise), lemmas 95.03 against 93.69 (ahead). Entities micro F1 63.09 against 62.32 (within noise). Misses: raw MLAS is 65.30 against spaCy's 69.05 (behind; an information row, not a gated one). Data lineage: UD English-EWT and UNER English-EWT (CC BY-SA 4.0), plus web text (FineWeb, ODC-By 1.0; English Wikipedia, CC BY-SA) labelled by our previous English model; the weights carry share-alike lineage where the data does. Size: 112.0 MiB of weights. Speed against the managed reference is measured at the release gate.

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
