# RecursiveAI.OneNLP.Models.Spanish

**spaCy-class NLP for .NET.** The Spanish fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Spanish text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

**Measured.** UD Spanish-AnCora r2.18 test (and WikiNER Spanish test for entities), against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 98.11 against 98.18, UFeats 97.71 against 97.74 and lemmas 98.84 against 98.72 (within noise), LAS 86.41 against 85.95 (within noise). Raw text: UPOS 97.86 against 94.81 and LAS 86.15 against 81.82 (ahead). Entities micro F1 84.81 against 83.35 (ahead, paired +1.46 [0.84, 2.14]). PER entity F1 is 91.57 against 90.45 (ahead). Data lineage: UD Spanish-AnCora (CC BY 4.0) and WikiNER (CC BY 4.0 labels over Wikipedia text, so share-alike lineage); the weights carry share-alike lineage where the data does. Size: 105.1 MiB of weights. Speed against the managed reference is measured at the release gate.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Pedro Almodóvar viene del norte de España y quiere verlo mañana.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Pedro Almodóvar vive en Madrid."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

The contractions `del` and `al`, and verbs with enclitic pronouns (`verlo`, `acercarse`, `situándose`), are several tokens, as UD Spanish writes them as multiword tokens: `del` is the tokens `de` and `l` (the words `de el`), `verlo` is `ver` and `lo`. `SegmentationData.Spanish.TryMatchMultiword` gives the words.

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `SpanishModel.Register()` once.

The weights are trained on Universal Dependencies Spanish-AnCora (CC BY 4.0) and WikiNER (CC BY 4.0 labels over Wikipedia text, CC BY-SA), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
