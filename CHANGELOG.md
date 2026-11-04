# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Repository skeleton: `RecursiveAI.OneNlp` and `RecursiveAI.OneNlp.Neural` projects, tests, a NativeAOT smoke app, a benchmark project and a fuzz harness.
- `Doc`, `Token` and `Span`: columnar documents with character offsets into the original string, interned attribute columns, entities, sentences and confidences.
- `Pipeline`, `IPipelineComponent`, `IWordTokenizer` and `ComponentRegistry`: ordered components, batch and streaming processing, and reflection-free registration by name.
- `Tokenizer`, `TokenEncoding` and the format loaders' entry points (`Tokenizer.FromFile`, `Tokenizer.FromStream`): the contract the tokenizer families will implement.
- `RuleWordTokenizer` and the `Nlp.Analyze` static.
- `IEncoder`, the first shape of the encoder seam, in `RecursiveAI.OneNlp.Neural`.
