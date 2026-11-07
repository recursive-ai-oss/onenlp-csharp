# RecursiveAI.OneNLP.Models.Italian

**spaCy-class NLP for .NET.** The Italian fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Italian text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change. The tagger and parser learn from only 897 sentences, because the larger Italian treebanks are non-commercial; expect them to be weaker than the other languages' models.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Mario Draghi vive nel centro di Roma e vuole vederlo domani.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Mario Draghi vive a Roma."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

Articulated prepositions (`del`, `nella`, `dall'`) and verbs with enclitic pronouns (`farlo`, `vedersi`) are several tokens, as UD Italian writes them as multiword tokens: `del` is the tokens `de` and `l` (the words `di il`), `vederlo` is `veder` and `lo`. `SegmentationData.Italian.TryMatchMultiword` gives the words.

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `ItalianModel.Register()` once.

The weights are trained on Universal Dependencies Italian-ParlaMint (CC BY-SA 4.0) and Italian-MarkIT (CC BY 4.0), 897 sentences in all, and WikiNER (CC BY 4.0 labels over Wikipedia text, CC BY-SA), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
