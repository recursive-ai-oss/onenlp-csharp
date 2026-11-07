# RecursiveAI.OneNLP.Models.Multilingual.Large

**spaCy-class NLP for .NET.** The base-size accurate-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): one fine-tuned mmBERT-base encoder shared by heads for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese (tags, morphology, lemmas, dependency parse, entities). Add this package and the `Nlp` verbs use it for those languages whenever no fast-tier package for the language is referenced; a fast-tier package, when present, is used instead. No downloads, no native code. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

The model is about 1.3 GB, so it is cut into parts that this package depends on (`...Large.Encoder1`, `Encoder2`, `TokenTable1` to `TokenTable4`); reference only this package. Loading it reads about 1.3 GB into memory the first time a verb needs it.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Angela Merkel besuchte im Mai die Stadt Hamburg.");
foreach (Span e in doc.Ents)
{
    Console.WriteLine($"{e.Label} {e}");
}
```
