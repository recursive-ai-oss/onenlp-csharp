# RecursiveAI.OneNLP.Models.Turkish

**spaCy-class NLP for .NET.** The Turkish fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Turkish text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

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
