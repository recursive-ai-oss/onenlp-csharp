# RecursiveAI.OneNLP.Models.Turkish

**spaCy-class NLP for .NET.** The Turkish fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Turkish text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

**Measured.** UD Turkish-BOUN r2.18 test (and Turkish-WikiNER test for entities), against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 88.97 against 88.04 and UFeats 78.15 against 78.30 (within noise), lemmas 85.11 against 80.24 and LAS 62.80 against 57.87 (ahead). Raw text: UPOS 86.02 against 86.82 and LAS 57.53 against 55.86 (within noise). Entities micro F1 67.40 against 65.82 (within noise). No headline row in the results table is behind spaCy. Data lineage: UD Turkish-BOUN and Turkish-WikiNER (CC BY-SA 4.0); the weights carry share-alike lineage where the data does. Size: 154.7 MiB of weights. Speed against the managed reference is measured at the release gate.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Mustafa Kemal Atatürk 1881 yılında Selanik'te doğdu.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Ankara Türkiye'nin başkentidir."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

A suffix written after a name or a number with an apostrophe (`Ankara'da`, `1882'den`) stays in its token, as in the treebank. UD Turkish also writes a copula or a suffix such as `-ken` that is spelled onto its word as a word of its own (`yaparken` is `yapar` + `ken`); a small trained model decides those cuts from the characters of the word and its sentence. The treebank is not consistent about them, so about half of the cuts are ones the treebank does not make and a third of its cuts are missed (`docs/segmentation.md`).

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `TurkishModel.Register()` once.

The weights are trained on Universal Dependencies Turkish-BOUN (CC BY-SA 4.0) and Turkish-WikiNER (CC BY-SA 4.0), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
