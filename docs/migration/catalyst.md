# Moving from Catalyst

Catalyst is a managed spaCy-style pipeline for .NET. This page maps its calls to OneNLP's. It is written from Catalyst's published README and from running its released packages as a black box (versions 26.10.6514 and English models 26.9.5316); nothing here comes from its source. There are no compatibility shims: the calls differ, and the table says how.

## At a glance

| | Catalyst | OneNLP |
|---|---|---|
| Models | `Catalyst.Models.English.Register()` and a storage folder; model packages per language | reference the model package (`RecursiveAI.OneNlp.Models.English`, ...); nothing to register, nothing downloaded |
| Pipeline | `Pipeline.ForAsync(Language.English)` | `Nlp.Analyze(text)`, or `Pipeline.Blank(language).Add(...)`, or `PipelineFile.Load(path)` |
| Text object | `new Document(text, Language.English)` | `Doc` made by the pipeline; it keeps your string |
| Processing | `nlp.ProcessSingle(doc)` | `nlp.Process(text)`; `nlp.ProcessMany(texts)` for batches |
| Sentences | `doc.Spans` | `doc.Sentences` (`Span` values) |
| Tokens | `span.Tokens` | `foreach (Token t in doc)` |
| Token text | `token.Value` (may be normalized: `'ve` as `have`) | `token.Text` (a string cut from your input; `token.TextSpan` is the copy-free view); `token.Norm` for a normalized form |
| Offsets | `token.Begin`, `token.End` (the end is inclusive) | `token.Idx`, `token.EndIdx` (the end is exclusive); UTF-16 units into the string you passed |
| Part of speech | `token.POS` (an enum) | `token.Pos` (a Universal Dependencies tag string such as `PROPN`) and `token.Tag` (the treebank's own tag) |
| Lemma | `token.Lemma` | `token.Lemma` |
| Entities | an extra model (`AveragePerceptronEntityRecognizer`) and `token.EntityTypes` | `doc.Ents` or `Nlp.Entities(text)`; `span.Label` is `PER`, `ORG`, `LOC` or `MISC` |
| Dependency parse | none | `token.Dep`, `token.Head` |
| Morphology | none | `token.Morph` |

## Before and after

Catalyst, as its README shows it:

```csharp
Catalyst.Models.English.Register();
Storage.Current = new DiskStorage("catalyst-models");
var nlp = await Pipeline.ForAsync(Language.English);

var doc = new Document("Marie Curie won the Nobel Prize in Paris.", Language.English);
nlp.ProcessSingle(doc);

foreach (var span in doc.Spans)
{
    foreach (var token in span.Tokens)
    {
        Console.WriteLine($"{token.Begin}..{token.End} {token.Value} {token.POS} {token.Lemma}");
    }
}
```

OneNLP (`samples/OneNlp.Samples/MigrationSamples.cs`, which builds and runs):

```csharp
// Reference the model package, then call the verb. Nothing is registered by hand and nothing is downloaded.
Doc doc = Nlp.Analyze("Marie Curie won the Nobel Prize in Paris.");
foreach (Token token in doc)
{
    Console.WriteLine($"{token.Idx} {token} {token.Pos} {token.Lemma}");
}

foreach (Span entity in doc.Ents)
{
    Console.WriteLine($"{entity.Label} {entity}");
}
```

## Differences that matter when you port

- **Tokenization** follows Universal Dependencies and UAX #29: contractions are split as treebanks split them (`don't` becomes `do` and `n't`), and in French, Spanish, Portuguese, Italian, German, Dutch and Turkish the contractions that are multiword tokens in the treebanks are cut into pieces the model tags. Expect different token counts than Catalyst's.
- **Sentence splitting** is set by the parser when the model has one, otherwise by the rule sentencizer.
- **Languages.** The Catalyst English models are the only managed comparison for English; the other eight fast-tier languages have no Catalyst counterpart here.
- **Entity labels** are the UNER scheme (`PER`, `ORG`, `LOC`), plus `MISC` where the training data has it, not Catalyst's WikiNER types. The recognizers add `CARDINAL`, `ORDINAL`, `PERCENT`, `MONEY` and `QUANTITY` through `Nlp.Quantities` ([recognizers](../recognizers.md)).
- **Training.** Catalyst trains averaged perceptrons; OneNLP trains neural heads with `Train` ([training.md](../training.md)). A model trained for Catalyst does not load here; retrain on the same data.
- **Threading.** A `Pipeline` may be shared between threads for `Process`; documents are independent.

How the two compare on accuracy and speed is in [results.md](../results.md) and [results-table.md](../results-table.md): the English UPOS and lemma rows (on raw text, 92.90 and 95.03 against Catalyst's 80.70 and 83.84 in its black-box run, `tools/oracles/Catalyst/README.md`) and the speed run, which is repeated at the release gate.
