# RecursiveAI.OneNLP.Models.Chinese

**spaCy-class NLP for .NET.** The Chinese fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Chinese text (Simplified script) with word segmentation, tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("李明在北京大学学习计算机科学。他明天去上海。");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("李明在北京大学学习。"))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

Chinese is written without spaces, so the tokens are words cut by a trained segmenter (a character tagger, `NeuralWordSegmenter`), not by Unicode word rules; offsets are into your original string. The segmentation is the Universal Dependencies one (UD Chinese-GSDSimp), so "北京大学" may be one word or two as that treebank writes it.

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `ChineseModel.Register()` once.

The weights are trained on Universal Dependencies Chinese-GSDSimp (CC BY-SA 4.0) and Universal NER Chinese-GSDSIMP (CC BY-SA 4.0), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`. The training text is Simplified-script; Traditional-script text is segmented and tagged with lower accuracy.

- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
