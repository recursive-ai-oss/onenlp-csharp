# RecursiveAI.OneNLP

**spaCy-class NLP for .NET.** The model-free core of OneNLP: a clean-room library for .NET 10 with no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

It holds everything that needs no trained model:

- `Doc`, `Token` and `Span`, with character offsets into the string you passed in, and a component `Pipeline` with batch and streaming processing.
- Tokenizers that match Hugging Face `tokenizers` byte for byte: WordPiece, BPE, byte-level BPE, Unigram/SentencePiece and Tiktoken, loaded from `tokenizer.json`, SentencePiece `.model`, `vocab.txt` or `.tiktoken` files, with normalizers, pre-tokenizers, post-processors, padding, truncation, special tokens and count/trim-to-budget helpers, in the BCL span style.
- UAX #29 word and sentence segmentation, `Matcher`, `PhraseMatcher` and `EntityRuler`.
- Recognizers that resolve numbers, ordinals, percentages, units, currency, e-mail, URL, IP, phone-like sequences, hashtags, mentions, GUIDs, dates, times, ranges, durations and recurrences to values, in English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi and Chinese. The rules are data, per language.
- Language identification for 75 languages in a 3.6 MB model, with per-word-run mixed-language spans.
- JSON and binary serialization of documents.

```csharp
using RecursiveAI.OneNlp;

string text = "Angela Merkel met Emmanuel Macron in Paris on 5 March 2026. The deal cost $5m.";

Language language = Nlp.DetectLanguage(text);                       // Language.English
DateTimeOffset today = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);
DateRange? when = Nlp.ParseDate("the first Monday after Easter 2027", today);
IEnumerable<Quantity> money = Nlp.Quantities(text);                 // 5000000 USD

Tokenizer tokenizer = Tokenizer.FromFile("tokenizer.json");         // a Hugging Face tokenizer.json
int tokens = tokenizer.CountTokens("How long is this text, in tokens?");
```

Tagging, lemmas, parses and entities need a model: add [RecursiveAI.OneNlp.Neural](https://www.nuget.org/packages/RecursiveAI.OneNlp.Neural) and a model package such as [RecursiveAI.OneNlp.Models.English](https://www.nuget.org/packages/RecursiveAI.OneNlp.Models.English). Without one, `Nlp.Entities` throws an exception that names the package to add; nothing is downloaded.

The package depends on the .NET base class library and [RecursiveAI.OneFont](https://www.nuget.org/packages/RecursiveAI.OneFont) only.

- Measured results against the leaders: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/docs/results-table.md>
- Documentation and source: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Changelog: <https://github.com/recursive-ai-oss/onenlp-csharp/blob/main/CHANGELOG.md>
- Home: <https://recursiveai.org/onenlp/>

Licensed `MIT OR Apache-2.0`. Copyright (c) Recursive AI, LLC.
