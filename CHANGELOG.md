# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `RecursiveAI.OneNlp.Models.English`: the English fast-tier model package (tags, morphology, lemmas, dependency parse, PER/ORG/LOC entities) with weights embedded; `ModelPackages` (the registry model packages use), `Nlp.Entities`, `Nlp.Lemmas`, `Nlp.AnalyzeMany`, and `Nlp.Analyze` filling every annotation when a model package is referenced. See `docs/model-packages.md`.
- Recognizers (`Recognizer`, `RecognizerComponent`, `Quantity`, `SequenceMatch`, `Nlp.Quantities`, `Nlp.Numbers`, `Nlp.Sequences`): numbers and ordinals in digits and words, percentages, measures (length, mass, volume, area, speed, information, temperature, age), money with ISO 4217 codes, e-mail, URL, IPv4/IPv6, phone-like sequences (unvalidated), hashtags, mentions and GUIDs, from per-language rule data that is embedded in the assembly or loaded from files. English rules are included; see `docs/recognizers.md`.
- Repository skeleton: `RecursiveAI.OneNlp` and `RecursiveAI.OneNlp.Neural` projects, tests, a NativeAOT smoke app, a benchmark project and a fuzz harness.
- `Doc`, `Token` and `Span`: columnar documents with character offsets into the original string, interned attribute columns, entities, sentences and confidences.
- `Pipeline`, `IPipelineComponent`, `IWordTokenizer` and `ComponentRegistry`: ordered components, batch and streaming processing, and reflection-free registration by name.
- `Tokenizer`, `TokenEncoding` and the format loaders' entry points (`Tokenizer.FromFile`, `Tokenizer.FromStream`): the contract the tokenizer families will implement.
- `RuleWordTokenizer` and the `Nlp.Analyze` static.
- `IEncoder`, the first shape of the encoder seam, in `RecursiveAI.OneNlp.Neural`.
- `Doc.ToJson`/`Doc.FromJson` in the shape spaCy documents for `Doc.to_json` (offsets in code points, extra fields for norm, confidences and sentence boundaries), and a compact, versioned, checksummed binary form (`Doc.ToBytes`/`Doc.FromBytes`), plus `DocBin` and a streaming `DocBinReader` that share one string table. Readers reject malformed, truncated and oversized input with `InvalidDataException`. Formats are specified in `docs/json-format.md` and `docs/binary-format.md`.
