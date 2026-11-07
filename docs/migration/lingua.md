# Moving from Lingua (SearchPioneer.Lingua)

Lingua is a language detector for short and long text; SearchPioneer.Lingua is its .NET port. OneNLP's `LanguageDetector` (namespace `RecursiveAI.OneNlp.LanguageId`) does the same jobs. This page maps the calls. It is written from Lingua's README and from running `SearchPioneer.Lingua` 2.0.0 and `lingua-language-detector` 2.2.0 as black boxes ([language-id.md](../language-id.md)); nothing comes from any Lingua implementation's source. There is no shim.

## At a glance

| | Lingua | OneNLP |
|---|---|---|
| Build a detector | `LanguageDetectorBuilder.FromAllLanguages().Build()`, then `WithPreloadedLanguageModels()` to load eagerly | `LanguageDetector.Default`: 75 languages in one 3.6 MB model embedded in the core package, loaded in about 10 ms (`results.md`) |
| A subset of languages | `FromLanguages(English, French, German)` | `LanguageDetector.Default.Restrict([Language.English, Language.French, Language.German])` |
| Accuracy mode | `WithLowAccuracyMode()` | none: one model, one mode |
| Detect | `detector.DetectLanguageOf(text)`; `Language.Unknown` when it cannot tell | `detector.Detect(text)`; `Language.Unknown` when the text has no letter of a supported script |
| Confidence values | `ComputeLanguageConfidenceValues(text)`, sorted | `detector.Confidences(text)`; probabilities sum to 1 |
| Mixed text | `DetectMultipleLanguagesOf(text)`: results with a language, a word count and a character `Range` | `detector.DetectSpans(text, switchPenalty = 9)`: runs that tile the text from 0 to its length |
| Minimum relative distance | `WithMinimumRelativeDistance(d)` | not needed: restrict the languages, or read `Confidences` |
| Language names | an enum with a member per language | `Language` is a code: `Language.English`, ...; the others by `Language.FromCode("sv")` (two-letter codes of `tools/fetch/langid/languages.tsv`) |
| Happy path | none | `Nlp.DetectLanguage(text)` |

## Before and after

Lingua:

```csharp
LanguageDetector detector = LanguageDetectorBuilder
    .FromLanguages(Language.English, Language.French, Language.German)
    .WithPreloadedLanguageModels()
    .Build();

Language language = detector.DetectLanguageOf("languages are awesome");
var confidences = detector.ComputeLanguageConfidenceValues("languages are awesome");
var runs = detector.DetectMultipleLanguagesOf("Parlez-vous français? Ich spreche Französisch nur ein bisschen.");
```

OneNLP (`samples/OneNlp.Samples/MigrationSamples.cs`, which builds and runs):

```csharp
// LanguageDetectorBuilder.FromLanguages(...).Build()  becomes  Restrict(...) on the detector that ships with the library.
LanguageDetector detector = LanguageDetector.Default.Restrict([Language.English, Language.French, Language.German]);

Language language = detector.Detect("languages are awesome");                         // DetectLanguageOf
IReadOnlyList<LanguageConfidence> ranked = detector.Confidences("languages are awesome"); // ComputeLanguageConfidenceValues
IReadOnlyList<LanguageSpan> runs = detector.DetectSpans(                              // DetectMultipleLanguagesOf
    "Parlez-vous français? Ich spreche Französisch nur ein bisschen.");
foreach (LanguageSpan run in runs)
{
    Console.WriteLine($"{run.Language} {run.Start}..{run.End} {run.Confidence:F2}");
}
```

Or, with no setup, `Language language = Nlp.DetectLanguage(text);`.

## Differences that matter when you port

- **Two `Language` types.** If a file uses both libraries, alias one (`using LinguaLanguage = Lingua.Language;`). OneNLP's is a small struct of the language code, so any language in the model can be named without an enum member.
- **Languages.** OneNLP identifies 75 languages (the list is in `tools/fetch/langid/languages.tsv`); SearchPioneer.Lingua 2.0.0 has 79, and our comparison runs it restricted to the same 75. Restricting to your languages helps most on short text.
- **Short text.** On single words, word pairs and sentences from the held-out Leipzig and Tatoeba text, OneNLP's accuracy is above Lingua's high-accuracy mode in every bucket; the hard cases are the closely related Latin-script pairs (Croatian and Bosnian, Indonesian and Malay, Norwegian Bokmål, Nynorsk and Danish), where some languages score below Lingua. The per-language rows are in [results-table.md](../results-table.md).
- **Cost.** One model of 3.6 MB that loads in about 10 ms, about 1.4 KB allocated per text, and no per-language models to preload or accuracy modes to choose. The load, memory and throughput rows against Lingua are in [results.md](../results.md).
- **Offsets** in `DetectSpans` are UTF-16 offsets into your string; a run begins at the first word of a new language, so spaces and punctuation after a run belong to it.
- **Thread safety.** A detector is immutable and safe to share.
