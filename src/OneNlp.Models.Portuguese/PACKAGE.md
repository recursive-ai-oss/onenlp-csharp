# RecursiveAI.OneNLP.Models.Portuguese

**spaCy-class NLP for .NET.** The Portuguese fast-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): add this package and the `Nlp` verbs analyze Portuguese text with tags, morphology, lemmas, a dependency parse and named entities. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Maria Silva visitou a cidade de Lisboa em maio.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Maria Silva mora em Lisboa."))
{
    Console.WriteLine($"{e.Label} {e}");
}
```

Tokens follow Universal Dependencies Portuguese-Bosque. A contraction of a preposition with an article or pronoun (`do`, `na`, `pelo`, `nesse`) is two tokens, because the treebank writes it as a multiword token of two words: `do` is the tokens `d` and `o` (the words `de o`). A pronoun after a verb is its own token with its hyphen: `trata-se` is `trata` and `-se`. `à` is one token (the words `a a`). Bosque has no XPOS column, so `Token.Tag` is empty; `Pos` and `Morph` come from the morphologizer.

The package adds a small source file to the project that references it; that file registers the model when the program starts. Programs that cannot compile it call `PortugueseModel.Register()` once.

The weights are trained on Universal Dependencies Portuguese-Bosque and Universal NER Portuguese-Bosque (both CC BY-SA 4.0), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against spaCy and others: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
