# RecursiveAI.OneNLP.Models.Multilingual

**spaCy-class NLP for .NET.** The distilled accurate-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): one six-layer multilingual encoder (a student of mmBERT-base, fine-tuned) shared by heads for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese (tags, morphology, lemmas, dependency parse, entities), and an open-type entity recognizer whose entity types you name at run time. Add this package and the `Nlp` verbs use it for those languages whenever no fast-tier package for the language is referenced; a fast-tier package, when present, is used instead. If `RecursiveAI.OneNlp.Models.Multilingual.Large` is referenced too, the accurate tier uses that. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

> **Early preview.** The weights are development models and will be retrained; the API will change. In this preview the accurate-tier rows are information only and several trail the fast tier; `docs/results-table.md` lists every row.

**Measured.** The accurate-tier rows are information, not gated. The fine-tuned student scores UPOS 95.88 [95.55, 96.19] on UD English-EWT r2.18 test (gold tokens), against 93.89 for the English fast tier and the published UDPipe 2 figure of 97.61 (UD 2.17); the student with a frozen encoder and a tuned head scores entity micro F1 75.48 [72.66, 78.03] on UNER English-EWT test, against 61.52 for the English fast tier and the published XLM-R large figure of 85.8. The rows that exist are on English only; no other language has an accurate-tier row in the results table yet. Data lineage: UD r2.18 treebanks and entity corpora (CC BY-SA 4.0 and CC BY), distilled from `jhu-clsp/mmBERT-base` (MIT) on FineWeb-2 and Wikipedia text, so the weights carry share-alike lineage. Size: about 521 MiB of weights across this package and its parts. The open-type entity recognizer has no row yet.


The model is about 480 MB, so it is cut into parts that this package depends on (`...Multilingual.Encoder`, `.FrozenEncoder`, `.Segmenters`); reference only this package. The frozen encoder is also usable on its own, as `RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder` (`FrozenStudentEncoder.Create()`).

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Angela Merkel besuchte im Mai die Stadt Hamburg.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

// Entity types named at run time:
foreach (Span e in Nlp.Entities("Acme Corp. signed the lease with Beta LLC.", "organization"))
{
    Console.WriteLine($"{e.Label} {e}");
}

// Unusual type names score lower; lower the threshold (default 0.5) and check what comes back:
IReadOnlyList<Span> parties = Nlp.Entities("Acme Corp. signed the lease with Beta LLC.", ["contract party"], threshold: 0.1f);
```

Chinese words and Turkish multiword tokens come from small trained tokenizers in the package. The weights are trained on Universal Dependencies treebanks and entity corpora (several CC BY-SA), credited in `THIRD-PARTY-NOTICES.md`; `PROVENANCE.md` lists every dataset, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Measured results against the fast tier and published numbers: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
