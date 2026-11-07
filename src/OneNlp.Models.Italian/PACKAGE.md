# RecursiveAI.OneNLP.Models.Italian

**spaCy-class NLP for .NET.** The Italian fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Italian text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change. The tagger and parser learn from only 897 sentences, because the larger Italian treebanks are non-commercial; expect them to be weaker than the other languages' models.

**Measured.** UD Italian-ParlaMint r2.18 test (and WikiNER Italian test for entities), against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 94.44 against 94.10, lemmas 95.18 against 93.13 and UFeats 89.52 against 87.42 (UPOS within noise; UFeats and lemmas ahead), LAS 72.57 against 71.39 (within noise). Raw text: UPOS 94.01 against 83.53 and LAS 72.77 against 58.38 (ahead). Entities micro F1 86.35 against 83.54 (ahead). Misses: the tagger and parser learn from 897 sentences, and the absolute LAS (72.57) is the lowest of the fast-tier languages. Data lineage: UD Italian-ParlaMint (CC BY-SA 4.0) and Italian-MarkIT (CC BY 4.0), and WikiNER (CC BY 4.0 labels over Wikipedia text, so share-alike lineage); the weights carry share-alike lineage where the data does. Size: 105.8 MiB of weights. Speed against the managed reference is measured at the release gate.

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
