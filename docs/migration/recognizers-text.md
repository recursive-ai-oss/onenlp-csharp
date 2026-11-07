# Moving from Microsoft.Recognizers.Text

Microsoft.Recognizers.Text finds numbers, units, currency, sequences and date/time expressions and returns `ModelResult` values. OneNLP's recognizers do the same job with one engine and rules kept as data per language. This page maps the calls. It is written from the package's published usage and from running version 1.8.13 as a black box on our own gold sentences ([recognizer-gold.md](../recognizer-gold.md)); nothing comes from its source or its spec files. There is no shim.

## At a glance

| | Microsoft.Recognizers.Text | OneNLP |
|---|---|---|
| Entry point | one static class per area: `NumberRecognizer`, `NumberWithUnitRecognizer`, `SequenceRecognizer`, `DateTimeRecognizer`; one method per model | `Recognizer.For(language).Recognize(text, context)` for everything; or the `Nlp` verbs |
| Culture | a culture string, `Culture.English` (`"en-us"`), `"fr-fr"`, ... | a `Language`, `Recognizer.English`, `Recognizer.For(Language.French)`: English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi, Chinese |
| Result | `ModelResult`: `Text`, `Start`, `End`, `TypeName`, `Resolution` (a dictionary of strings) | a typed record: `Quantity`, `SequenceMatch` or `TemporalMatch`, each with `Start`, `End`, `Text`, `Label`, `Confidence` |
| End offset | **inclusive** (`End` is the last character) | **exclusive** (`End - Start == Text.Length`) |
| Reference time | the `refTime` argument of `RecognizeDateTime` | `RecognitionContext(referenceDate)`; relative expressions without one are not reported |
| Resolved values | strings in `Resolution` (`"value"`, `"unit"`, `"timex"`, `"start"`, `"end"`) | a `decimal Value`, `Unit`, `Dimension`, `Currency`; a `DateRange` with `DateTimeOffset` `Start` and `End`, a `Granularity` and an `Approximate` flag |

## Which call becomes which

| Recognizers.Text call | OneNLP result |
|---|---|
| `NumberRecognizer.RecognizeNumber` | `Quantity` with `Kind == QuantityKind.Cardinal` (`Nlp.Numbers(text)`) |
| `NumberRecognizer.RecognizeOrdinal` | `Quantity`, `Kind == Ordinal` |
| `NumberRecognizer.RecognizePercentage` | `Quantity`, `Kind == Percent`; `Value` is in percent (5, not 0.05) |
| `NumberWithUnitRecognizer.RecognizeCurrency` | `Quantity`, `Kind == Money`; `Currency` is the ISO 4217 code and `Value` is in that currency (50 cents is 0.5) |
| `NumberWithUnitRecognizer.RecognizeDimension`, `RecognizeTemperature`, `RecognizeAge` | `Quantity`, `Kind == Measure`; `Dimension` is `length`, `mass`, `volume`, `area`, `speed`, `information`, `temperature` or `age`; `Unit` is the canonical English unit name in every language |
| `SequenceRecognizer.RecognizeEmail`, `RecognizeURL`, `RecognizeIpAddress`, `RecognizePhoneNumber`, `RecognizeHashtag`, `RecognizeMention`, `RecognizeGUID` | `SequenceMatch` with `Kind` `Email`, `Url`, `IpAddress`, `PhoneLike`, `Hashtag`, `Mention`, `Uuid` (`Nlp.Sequences(text)`); phone-like is syntax only and unvalidated |
| `DateTimeRecognizer.RecognizeDateTime` | `TemporalMatch` with `Kind` `Date`, `Time`, `DateTime`, `DateRange`, `TimeRange`, `DateTimeRange`, `Duration` or `Set` (`Nlp.Dates(text, reference)`, `Nlp.ParseDate(text, reference)`) |

## Before and after

Recognizers.Text:

```csharp
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.NumberWithUnit;
using Microsoft.Recognizers.Text.Sequence;

var reference = new DateTime(2026, 3, 18, 10, 30, 0);
string text = "Pay $20 to bob@example.com by 5 March 2026, or within two weeks.";

var money = NumberWithUnitRecognizer.RecognizeCurrency(text, Culture.English);
var email = SequenceRecognizer.RecognizeEmail(text, Culture.English);
var dates = DateTimeRecognizer.RecognizeDateTime(text, Culture.English, DateTimeOptions.None, reference);
foreach (var r in money.Concat(email).Concat(dates))
{
    Console.WriteLine($"{r.TypeName} [{r.Start},{r.End}] {r.Text}");      // End is inclusive
}
```

OneNLP (`samples/OneNlp.Samples/MigrationSamples.cs`, which builds and runs):

```csharp
Recognizer english = Recognizer.English;                              // Recognizer.For(Language.French), ... for the other nine
DateTimeOffset reference = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);

foreach (Recognition r in english.Recognize("Pay $20 to bob@example.com by 5 March 2026, or within two weeks.", new RecognitionContext(reference)))
{
    string detail = r switch
    {
        Quantity q => $"{q.Kind} {q.Value} {q.Unit ?? q.Currency}",     // ModelResult.Resolution["value"], ["unit"]
        TemporalMatch t => $"{t.Kind} {t.Value} {t.Range?.Start:yyyy-MM-dd}", // Resolution["values"][..]["timex"], ["start"], ["end"]
        SequenceMatch s => $"{s.Kind}",
        _ => string.Empty,
    };
    Console.WriteLine($"{r.Label,-10} [{r.Start},{r.End}) {r.Text}  {detail}");   // TypeName, Start, End (inclusive in the old library)
}
```

## Differences that matter when you port

- **Spans do not overlap.** One call returns every type in text order, and where two readings cover the same characters the engine keeps one ([recognizers.md](../recognizers.md), "Overlaps"). Recognizers.Text returns each model's results separately, so the same characters can appear as a number and as part of a date.
- **Ranges end where they say.** A `DateRange` has an inclusive `Start` and an **exclusive** `End`: `from April 6 to April 10` ends at the start of April 11. Recognizers.Text states the end of such day ranges as the end day itself. Both conventions are named in [recognizer-gold.md](../recognizer-gold.md).
- **Several candidates.** For an ambiguous expression Recognizers.Text lists several values (past and future Friday, am and pm). OneNLP resolves to one, by rules written in [recognizers.md](../recognizers.md) (a bare weekday is the first such day on or after the reference day; `next Friday` is Friday of next week).
- **Time zones are explicit.** A stated zone (`PST`, `UTC+2`, a trailing `Z`) is kept in `Value`, `UtcOffset` and the offset of `Range`. There is no time zone database, so no daylight-saving arithmetic.
- **Approximate expressions** (`spring 2021`, `early 2021`, `late 2023`) resolve to a range with a granularity and an `Approximate` flag.
- **Region.** `RecognitionContext.Region` (`us` or `gb`) picks the order of numeric dates and the holidays.
- **Rules are data.** The words, symbols, units, currencies, holidays and patterns of a language are rule files, not code; the grammar that combines them is code ([recognizers.md](../recognizers.md), "Rule data" says where the line falls). `Recognizer.FromFiles(...)` loads rules from your own files, so you can add a unit, a currency or a holiday without a new build.
- **Values are exact.** `Quantity.Value` is a `decimal`; money does not round-trip through `double`.

On our hand-labelled test sets, in each of eight languages, every type is within noise of or ahead of Recognizers.Text, and the engine is faster; the rows are in [results-table.md](../results-table.md) and [results.md](../results.md).
