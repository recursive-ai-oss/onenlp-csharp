# RecursiveAI.OneNLP.Models.Chinese

**spaCy-class NLP for .NET.** The Chinese fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Chinese text (Simplified script) with word segmentation, tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

**Measured.** UD Chinese-GSDSimp r2.18 test (and UNER Chinese-GSDSIMP test for entities); the reference is spaCy trained on gold words (`gold_preproc`) for gold rows, against spaCy 3.8.18 trained with its default configuration on the same data (95% paired bootstrap intervals are in the results table). Gold words: UPOS 90.41 against 90.35, UFeats 98.38 against 98.43, lemmas 99.87 against 99.91, LAS 68.76 against 68.24 and MLAS 56.94 against 58.87 (all within noise). Raw text: words 89.62 against 39.43, UPOS 81.90 against 34.92 and LAS 57.88 against 8.59 (ahead; spaCy's default Chinese tokenizer cuts single characters). Entities micro F1 62.80 against 61.86 (within noise). Misses: entity precision is 62.44 against 67.08 (paired -4.64 [-8.52, -0.50]) while recall is 63.17 against 57.40. Data lineage: UD Chinese-GSDSimp and UNER Chinese-GSDSIMP (CC BY-SA 4.0); the weights carry share-alike lineage where the data does. Size: 162.9 MiB of weights. Speed against the managed reference is measured at the release gate.

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

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
