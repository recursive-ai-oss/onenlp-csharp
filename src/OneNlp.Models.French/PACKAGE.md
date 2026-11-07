# RecursiveAI.OneNLP.Models.French

**spaCy-class NLP for .NET.** The French fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze French text with French tokenization (elision, inversion clitics, the contractions `du`, `au`, `aux`, `des`), morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

**Measured.** UD French-GSD r2.18 test (and WikiNER French test for entities), against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 96.77 against 97.07, UFeats 96.34 against 96.41, lemmas 97.51 against 97.49 and LAS 86.69 against 85.98 (all within noise). Raw text: UPOS 96.06 against 91.80 and LAS 85.97 against 78.10 (ahead). Entities micro F1 82.48 against 75.50 (ahead). No headline row in the results table is behind spaCy. Data lineage: UD French-GSD (CC BY-SA 4.0) and WikiNER (CC BY 4.0 labels over Wikipedia text, so share-alike lineage); the weights carry share-alike lineage where the data does. Size: 81.9 MiB of weights. Speed against the managed reference is measured at the release gate.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Angela Merkel a visité Lyon avec le président du Conseil.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Marie Curie est née à Varsovie."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

The contractions `du` (de + le), `des` (de + les), `au` (à + le) and `aux` (à + les) are multiword tokens in Universal Dependencies. The tokenizer cuts them into the pieces of the token (`d` + `u`), which the model tags and lemmatizes as the words they stand for; `SegmentationData.French.TryMatchMultiword` gives the words (`de`, `le`) for output in CoNLL-U form.

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `FrenchModel.Register()` once.

The weights are trained on Universal Dependencies French-GSD (CC BY-SA 4.0) and WikiNER French (CC BY 4.0 labels over Wikipedia text, CC BY-SA), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
