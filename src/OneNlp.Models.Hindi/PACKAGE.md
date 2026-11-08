# RecursiveAI.OneNLP.Models.Hindi

**spaCy-class NLP for .NET.** The Hindi fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Hindi text with part-of-speech tags, morphological features, a dependency parse and named entities (PER, ORG, LOC). No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Weaker than the other languages' models.** There is no Hindi treebank whose licence allows training a model that is shipped (Hindi-HDTB is non-commercial). This model was trained on Hindi web text (FineWeb-2) that the multilingual accurate-tier model labelled by cross-lingual transfer from other languages (Marathi, Sindhi and five large languages), keeping the sentences where its sources agree. Its labels are therefore noisier than a treebank's, and its scores on the UD Hindi-PUD test set (gold tokens: UPOS 83.2, LAS 52.8) are lower than the English or Spanish models' on their treebanks. It has **no lemmatizer**: `Token.Lemma` is the token's own text, which is wrong for inflected words, and there are no XPOS tags (`Token.Tag` is empty), because no transferable source for them exists. The entity recognizer was never scored against Hindi entity gold, because none that may be used exists; see `PROVENANCE.md` for the measurements that were possible.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("प्रधानमंत्री नरेंद्र मोदी ने दिल्ली में कल एक बैठक की। उन्होंने कहा कि सरकार तैयार है।");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Dep}");
}

foreach (Span e in Nlp.Entities("नरेंद्र मोदी दिल्ली में हैं।"))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

Words are separated by spaces and the danda `।` is a token of its own; Hindi has no multiword tokens. The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `HindiModel.Register()` once.

The weights are derived from Hindi web text (FineWeb-2, ODC-By 1.0, Common Crawl terms) and from the accurate-tier model, which was trained on Universal Dependencies treebanks (CC BY-SA and CC BY) and named-entity corpora; every source is credited in `THIRD-PARTY-NOTICES.md`, and `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
