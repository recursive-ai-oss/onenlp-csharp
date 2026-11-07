# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed (breaking)

- `Nlp.Entities(text)`, `Nlp.Lemmas(text)` and `Nlp.Analyze(text)`: when the language detector is unsure (confidence below 0.8) and has no model for its answer, the text is read in the most likely of the languages that have a model, so "Paris", "Ok" or a name is read by the model you referenced instead of throwing from one verb and silently losing entities in the other. Confident answers are unchanged: French text without a French model is still an error naming the package.
- A missing model throws `ModelNotAvailableException` (still an `InvalidOperationException`) with `Language`, `Tier` and `PackageIds`; messages name only packages that exist.
- `Nlp` verbs and `Pipeline.Process`/`ProcessMany` refuse text longer than `Nlp.MaxLength` / `Pipeline.MaxLength` (1,000,000 characters by default) with an `ArgumentOutOfRangeException`. Texts above that size that used to be accepted now need a raised limit.
- `Tokenizer.FromFile("vocab.txt")` no longer reads letters without a case pair (such as U+211D, which `bert-base-uncased` contains) as evidence of a cased vocabulary: that vocabulary is now uncased, as it is.
- `Recognizer.Recognize(text, context = default)` is two overloads, `Recognize(text)` and `Recognize(text, context)`; a `default(DateTimeOffset)` reference date throws `ArgumentException` here and in `Nlp.Dates` and `Nlp.ParseDate`.
- `Language.Unknown.ToString()` is `"Unknown"` (its `Code` is still empty).
- `PatternFormatException` and `RecognizerRulesException` carry `LineNumber` (and `BytePositionInLine` for patterns); pattern messages no longer end in `(Parameter 'name')`, and rule-line messages no longer repeat "Line N" inside the text they pass to the constructor.

### Added

- `Nlp.Entities`, `Nlp.Analyze`, `Nlp.AnalyzeMany` and `Nlp.Lemmas` overloads that take a `Language`; `Nlp.MaxLength`; `Pipeline.MaxLength`.
- `Tokenizer.FromWordPieceVocabulary(path or stream, WordPieceVocabularyOptions)` states the casing of a `vocab.txt`; `Tokenizer.EncodeToIds(text, addSpecialTokens)` returns an `int[]`.
- `ModelNotAvailableException`.
- Documentation for the first release: a rewritten README and package readmes, `docs/results-table.md` (every quality row with its reference and interval), side-by-side "reads like" samples (`docs/reads-like.md`), migration pages for Catalyst, Microsoft.Recognizers.Text, Lingua and Microsoft.ML.Tokenizers, guides for training, the fast tier and classification, and `samples/OneNlp.Samples`, which builds and runs every sample the docs show.
- Recognizer comparison files for Turkish and Hindi test sets, and refreshed English ones that include date/time.

- Sentence embeddings: `IEmbedder`, `SentenceEmbedder` (sentence-transformers-format ONNX models, CLS/mean/max pooling, normalization, batching, opt-in int8), `Nlp.Embed`/`Nlp.EmbedMany`, `EmbedderPackages`, `Tokenizer.IsSpecialToken`, and the `RecursiveAI.OneNlp.Models.Embeddings` package (bge-small-en-v1.5, MIT). Parity and MTEB results: `docs/embeddings.md`.
- French: `SegmentationData.French` (elision, inversion clitics, `du`/`au`/`aux`/`des` as multiword-token pieces, digit groups) with `TryMatchMultiword`, the recipes in `recipes/fr` and the `RecursiveAI.OneNlp.Models.French` package.
- Dutch: `SegmentationData.Dutch` (apostrophe words and short forms, abbreviations, dash amounts), the recipe in `recipes/nl/fast` and the `RecursiveAI.OneNlp.Models.Dutch` package (tagging, morphology, lemmas, parser, entities on UD Dutch-Alpino and WikiNER Dutch).
- Language identification: `LanguageDetector` names the language of a text (75 languages, a 3.6 MB model embedded at build time from `ONENLP_LANGID_WEIGHTS`) from one word to a document, with confidences, `Restrict(languages)` and mixed-language runs (`DetectSpans`). `Nlp.DetectLanguage` is the happy path, and `Nlp.Analyze`, `Nlp.Tokens` and `Nlp.Sentences` now use the detected language's segmentation data. Method, model file and measurements are in `docs/language-id.md`; the recipe and provenance log are in `recipes/langid`.
- `RecursiveAI.OneNlp.Models.English`: the English fast-tier model package (tags, morphology, lemmas, dependency parse, PER/ORG/LOC entities) with weights embedded; `ModelPackages` (the registry model packages use), `Nlp.Entities`, `Nlp.Lemmas`, `Nlp.AnalyzeMany`, and `Nlp.Analyze` filling every annotation when a model package is referenced. See `docs/model-packages.md`.
- Recognizers (`Recognizer`, `RecognizerComponent`, `Quantity`, `SequenceMatch`, `Nlp.Quantities`, `Nlp.Numbers`, `Nlp.Sequences`): numbers and ordinals in digits and words, percentages, measures (length, mass, volume, area, speed, information, temperature, age), money with ISO 4217 codes, e-mail, URL, IPv4/IPv6, phone-like sequences (unvalidated), hashtags, mentions and GUIDs, from per-language rule data that is embedded in the assembly or loaded from files. English rules are included; see `docs/recognizers.md`.
- Recognizers: a number in words repeated in brackets is one result (`sixty (60) days`, `five thousand dollars ($5,000)`; a disagreeing numeral lowers the confidence and is not merged); durations of business days (`ten business days`, `P10BD`), `calendar` units, and `TemporalMatch.Count` and `TemporalMatch.Unit` (`DurationUnit`) on every duration; `in 3 business days` resolves to a date; compound measures (`5 feet 3 inches` is 63 inches); Indian digit grouping (`12,34,567`) and scientific notation (`6.02e23`, `1.5×10^3`); `1,000th` as an ordinal; `1,000 pounds` is money in a money context; `wait a second` is a duration; `no one`, `one of them` and `for the first time` are not numbers; the ISO 4217 code list was checked against the published List One.
- Repository skeleton: `RecursiveAI.OneNlp` and `RecursiveAI.OneNlp.Neural` projects, tests, a NativeAOT smoke app, a benchmark project and a fuzz harness.
- `Doc`, `Token` and `Span`: columnar documents with character offsets into the original string, interned attribute columns, entities, sentences and confidences.
- `Pipeline`, `IPipelineComponent`, `IWordTokenizer` and `ComponentRegistry`: ordered components, batch and streaming processing, and reflection-free registration by name.
- `Tokenizer`, `TokenEncoding` and the format loaders' entry points (`Tokenizer.FromFile`, `Tokenizer.FromStream`): the contract the tokenizer families will implement.
- `RuleWordTokenizer` and the `Nlp.Analyze` static.
- `IEncoder`, the first shape of the encoder seam, in `RecursiveAI.OneNlp.Neural`.
- `Doc.ToJson`/`Doc.FromJson` in the shape spaCy documents for `Doc.to_json` (offsets in code points, extra fields for norm, confidences and sentence boundaries), and a compact, versioned, checksummed binary form (`Doc.ToBytes`/`Doc.FromBytes`), plus `DocBin` and a streaming `DocBinReader` that share one string table. Readers reject malformed, truncated and oversized input with `InvalidDataException`. Formats are specified in `docs/json-format.md` and `docs/binary-format.md`.

### Fixed

- `bench/OneNlp.Bench` runs out of process under BenchmarkDotNet (the assembly name now matches the project file); `tools/oracles/bench_recognizers_lang.sh` builds its own tools, uses small inputs where the comparator is slow (Turkish, Hindi) and times out each run.
