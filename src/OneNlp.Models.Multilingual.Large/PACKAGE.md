# RecursiveAI.OneNLP.Models.Multilingual.Large

**spaCy-class NLP for .NET.** The base-size accurate-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): one fine-tuned mmBERT-base encoder shared by heads for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese (tags, morphology, lemmas, dependency parse, entities). Add this package and the `Nlp` verbs use it for those languages whenever no fast-tier package for the language is referenced; a fast-tier package, when present, is used instead. No downloads, no native code. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change.

The encoder is a 1.2 GB ONNX graph, so it is cut into six pieces that this package depends on (`...Large.Encoder1`, `Encoder2`, `TokenTable1` to `TokenTable4`, in that order) and one for the Chinese heads and word segmenter (`...Large.Chinese`); reference only this package. The first verb that needs the model reads the graph into memory once, and the heads of a language when that language is first used.

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Angela Merkel besuchte im Mai die Stadt Hamburg.");
foreach (Span e in doc.Ents)
{
    Console.WriteLine($"{e.Label} {e}");
}
```
