# RecursiveAI.OneNLP.Models.Multilingual

**spaCy-class NLP for .NET.** The distilled accurate-tier model for [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): one six-layer multilingual encoder (a student of mmBERT-base, fine-tuned) shared by heads for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese (tags, morphology, lemmas, dependency parse, entities), and an open-type entity recognizer whose entity types you name at run time. Add this package and the `Nlp` verbs use it for those languages whenever no fast-tier package for the language is referenced; a fast-tier package, when present, is used instead. If `RecursiveAI.OneNlp.Models.Multilingual.Large` is referenced too, the accurate tier uses that. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

**Measured.** The distilled tier is an information tier, not the base-size accurate-tier quality gate. On English, the fine-tuned student scores UPOS 95.88 [95.55, 96.19] on UD English-EWT r2.18 test (gold tokens); the frozen-encoder entity path scores micro F1 75.48 [72.66, 78.03] on UNER English-EWT test. The published UDPipe 2 UPOS figure is 97.61 (UD 2.17) and XLM-R large entity F1 is 85.8; these are different-release/model references, not paired comparisons. The full language rows and protocol notes are in `docs/results-table.md` and the accurate-tier recipe results. Data lineage: UD r2.18 and entity corpora (CC BY-SA and CC BY); mmBERT-base (MIT) distilled on FineWeb-2 and Wikipedia, so the weights carry share-alike lineage.

Open-type NER on CrossNER zero-shot test scores F1 41.27 (politics), 36.50 (science), 26.01 (music), 30.38 (literature) and 21.10 (AI), 19–35 points below the published GLiNER-S numbers. Protocol equivalence is unverified and no zero-shot quality pass is claimed. See `docs/open-ner.md` for the audit.

Weights total about 505 MiB across this package and its three parts (`...Multilingual.Encoder`, `.FrozenEncoder`, `.Segmenters`); reference only this package. The frozen encoder is also usable on its own, as `RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder` (`FrozenStudentEncoder.Create()`).

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
