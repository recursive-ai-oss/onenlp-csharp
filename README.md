# OneNLP

**spaCy-class NLP for .NET.** A clean-room library for .NET 10 with no native code, NativeAOT and trim clean, built on [OneModel](https://www.nuget.org/packages/RecursiveAI.OneModel). OneNLP is unaffiliated with other projects named OneNLP.

Text in, structure out: tokens, sentences, tags, morphology, lemmas, dependency parses and entities in nine languages; token-exact Hugging Face tokenizers; numbers, units, currency, e-mail and URLs, and dates and times resolved to values in ten languages; language identification for 75 languages; text classification and span categorization; sentence embeddings. You can train every one of the neural parts yourself with the API that trained the shipped models.

> **Pre-release.** The packages are published on nuget.org with the 1.0 release; until then build from source (below). The API may still change. Every quality and speed claim here is a row in [docs/results-table.md](docs/results-table.md), with its reference, its interval and its verdict, including the rows we lose.

## What is in it

| Package | Contents |
|---|---|
| `RecursiveAI.OneNlp` | The core; depends on the .NET base class library and [`RecursiveAI.OneFont`](https://www.nuget.org/packages/RecursiveAI.OneFont) only. `Doc`, `Token`, `Span` with character offsets into your string; `Pipeline` with batch and streaming processing; tokenizers that match Hugging Face `tokenizers` byte for byte (WordPiece, BPE, byte-level BPE, Unigram/SentencePiece, Tiktoken; `tokenizer.json`, SentencePiece `.model`, `vocab.txt` and `.tiktoken` loaders); UAX #29 word and sentence segmentation; `Matcher`, `PhraseMatcher`, `EntityRuler`; recognizers for numbers, ordinals, percentages, units, currency, e-mail, URL, IP, phone-like sequences, hashtags, mentions, GUIDs and dates and times, with rules as data, in English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi and Chinese; language ID for 75 languages (3.6 MB); JSON and binary serialization of documents; the `Nlp` statics. |
| `RecursiveAI.OneNlp.Neural` | Depends on the core, `RecursiveAI.OneModel` and `RecursiveAI.OneModel.Training`. The encoders (the fast hash-embedding encoder, transformer encoders loaded from exported ONNX weights, fine-tunable), the heads (tagger, morphologizer, edit-tree lemmatizer, biaffine dependency parser, entity recognizer, text classifier, span categorizer, and, as trained word tokenizers, a multiword splitter and a word segmenter), `Train` with checkpoints and early stopping, `PipelineFile`, and the sentence embedder. |
| `RecursiveAI.OneNlp.Models.English`, `.French`, `.German`, `.Spanish`, `.Portuguese`, `.Dutch`, `.Italian`, `.Chinese`, `.Turkish`, `.Hindi` | The fast-tier model of one language: tokenization rules of the language, tags, morphology, lemmas, dependency parse and entities. Add the package and `Nlp.Analyze` and `Nlp.Entities` use it. |
| `RecursiveAI.OneNlp.Models.Embeddings` | The default English sentence embedder (bge-small-en-v1.5, MIT) for `Nlp.Embed`. |
| `RecursiveAI.OneNlp.Models.Embeddings.Multilingual` | multilingual-e5-small (MIT), shipped in shards that depend on one another; reference this one package. |
| `RecursiveAI.OneNlp.Models.Multilingual`, `.Multilingual.Large` | **In progress.** The accurate tier: one multilingual transformer encoder with heads for many languages. Not released; no claims are made for it yet. |

Not packaged yet and therefore not listed as working: open-type NER (entity types named at runtime), the accurate-tier packages above, and the lemmas of the Hindi model (below). See [docs/results-table.md](docs/results-table.md) for the gaps.

**Hindi** has no treebank we may train on (Hindi-HDTB is non-commercial), so `RecursiveAI.OneNlp.Models.Hindi` is trained on Hindi web text that the accurate tier labelled by transfer from nine other languages. It has UPOS, features, a parse and PER/ORG/LOC entities, no lemmas and no XPOS, and its scores (UPOS 70.9, LAS 31.9 on Hindi-PUD, gold tokens) are far below the other languages'; see [docs/results-table.md](docs/results-table.md). Hindi UD is not available for an accurate-tier model.

Languages without an accurate-tier model: Basque, Bulgarian, Classical Armenian, Gothic, Hungarian, Kadiweu, Nheengatu, Old Church Slavonic, Pomak, Shanghainese, South Levantine Arabic and Urdu have no Universal Dependencies treebank licensed for commercial use (each is non-commercial only), and Hindi's only such treebank is a test set. Language ID covers these languages where it lists them; we train no tagger, parser or entity model on non-commercial data. See [docs/ud-licences.md](docs/ud-licences.md).

Nothing is ever downloaded. A model lives inside its package; without the package, a verb that needs it throws an exception that names the package to add (see [docs/model-packages.md](docs/model-packages.md)).

## Install

    dotnet add package RecursiveAI.OneNlp
    dotnet add package RecursiveAI.OneNlp.Models.English      // a model package per language you analyze
    dotnet add package RecursiveAI.OneNlp.Models.Embeddings   // for Nlp.Embed

Until the first release, build the packages from source and add them from a local folder feed.

## The happy path

Static task verbs, no options objects; the language is detected for you. The code below is `samples/OneNlp.Samples/HappyPath.cs`, which builds and runs.

```csharp
using RecursiveAI.OneNlp;

string text = "Angela Merkel met Emmanuel Macron in Paris on 5 March 2026. The deal cost $5m.";

Language language = Nlp.DetectLanguage(text);                    // Language.English
IReadOnlyList<Span> sentences = Nlp.Sentences(text);             // sentence spans, offsets into text
Token[] words = Nlp.Tokens(text);                                // word tokens
string[] lemmas = Nlp.Lemmas("The cats were running.");          // "the", "cat", "be", "run", "."
IReadOnlyList<Span> entities = Nlp.Entities(text);               // "Angela Merkel" PER, "Paris" LOC, ...

DateTimeOffset today = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);
DateRange? when = Nlp.ParseDate("the first Monday after Easter 2027", today);
IEnumerable<Quantity> money = Nlp.Quantities(text);              // 5000000 USD

Doc doc = Nlp.Analyze(text);   // tokens, sentences, tags, lemmas, the parse and the entities in one call
```

Offsets are always UTF-16 offsets into the string you passed in. Relative dates need a reference date, which you pass; time zones are explicit. [docs/reads-like.md](docs/reads-like.md) shows the happy path next to Apple's NaturalLanguage, the low-level `Pipeline` next to spaCy, and `Tokenizer` next to Hugging Face `tokenizers`.

## Moving from another library

Call-by-call maps, with no compatibility shims:

- [Catalyst](docs/migration/catalyst.md)
- [Microsoft.Recognizers.Text](docs/migration/recognizers-text.md)
- [Lingua (SearchPioneer.Lingua)](docs/migration/lingua.md)
- [Microsoft.ML.Tokenizers](docs/migration/ml-tokenizers.md)

## Measured against the leaders

[docs/results-table.md](docs/results-table.md) has one table: component, language, metric, our score with its interval, the reference, and whether the difference is within noise. In short:

- **Tokenizers** are byte-exact against Hugging Face `tokenizers` and `tiktoken` on twelve model families, and at least as fast as Microsoft.ML.Tokenizers on all 47 timed rows (rows the comparator cannot reproduce are listed in the results).
- **Language ID** is more accurate than Lingua on words, word pairs and sentences, and faster on every row (a few rows below it per language are listed).
- **Recognizers** score ahead of Microsoft.Recognizers.Text on our hand-labelled sets in ten languages, and faster.
- **The fast tier** is within noise of spaCy 3.8.18 trained on the same data on the gated UD and entity rows, or ahead of it; the few rows behind are listed by name.
- **Text classification and span categorization** are ahead of spaCy's defaults on accuracy and F1; span precision is within noise at the default threshold.
- **Embeddings** equal sentence-transformers in the same harness; the model cards' own numbers are shown beside them, with the cells neither reproduces.

Speed against Catalyst on the tagging path is measured at the release gate.

## Known gaps

Stated plainly; each is also in [docs/results-table.md](docs/results-table.md).

- English raw-text MLAS is behind spaCy (65.61 against 69.05; an information row). Spanish PER entities, Portuguese and Chinese entity precision are behind spaCy's per type or per metric while the micro F1 is within noise.
- Speed against Catalyst on the tagging path is measured at the release gate; Catalyst has English models only, so the other languages have no managed speed comparator.
- The accurate-tier packages and open-type entity recognition are in progress and have no gated rows; the accurate-tier rows that exist are on English only.
- Embeddings: five MTEB cells are below the model cards for sentence-transformers itself, and ours equal it.
- Language ID is below Lingua on some languages and short-text buckets (listed in the results).
- Italian is trained on 897 sentences, since the larger Italian treebanks are non-commercial.

## Documentation

- Start with [docs/reads-like.md](docs/reads-like.md); the contracts every component follows are in [docs/contracts.md](docs/contracts.md).
- Components: [tokenizers](docs/parity.md), [segmentation](docs/segmentation.md), [Matcher and EntityRuler](docs/matching.md), [serialization](docs/json-format.md), [recognizers](docs/recognizers.md), [language ID](docs/language-id.md), [the parser](docs/parser.md), [embeddings](docs/embeddings.md), [the accurate-tier encoder](docs/accurate-encoder.md), [model packages](docs/model-packages.md), [model files](docs/model-files.md), [data formats](docs/data-formats.md) and [training and recipes](docs/training.md).
- How things are measured: [docs/measurement.md](docs/measurement.md), [docs/references.md](docs/references.md), [docs/provenance.md](docs/provenance.md).

## Building

You need the .NET SDK pinned in `global.json`.

    dotnet build -c Release
    dotnet test --solution OneNlp.slnx -c Release --no-build

Build output goes to `artifacts/` in the repository (ignored by git). Set `ONENLP_ARTIFACTS` to a directory to put it elsewhere; each checkout gets its own folder under it. Machine-specific MSBuild overrides can go in an ignored `Directory.Build.local.props`. Weights are not in git: the model projects embed them from a directory named by an environment variable (for example `ONENLP_ENGLISH_WEIGHTS`) and check each file against a committed hash; see each project's `PROVENANCE.md`.

The NativeAOT smoke app is `tests/OneNlp.AotSmoke`; publish it with `dotnet publish tests/OneNlp.AotSmoke -c Release -r <rid>` (it needs a native C linker: on Linux clang by default, or gcc with `-p:CppCompilerAndLinker=gcc`, and the zlib development package). Run the samples with `dotnet run --project samples/OneNlp.Samples -- all <path to a tokenizer.json>`.

## Layout

- `src/`: the packages (`OneNlp`, `OneNlp.Neural`, one project per model package).
- `recipes/`: the training recipes; each is a small console project over the public `Train` API, with its data manifest, provenance and results.
- `samples/`: the code shown in the docs.
- `tests/`, `bench/`, `fuzz/`: tests, the AOT smoke app, benchmarks and fuzz targets.
- `tools/`: scripts for data, oracles and evaluation ([tools/README.md](tools/README.md)).
- `docs/`: behaviour written down.

## Licence

`MIT OR Apache-2.0`, at your option. See `LICENSE-MIT` and `LICENSE-APACHE`. Copyright (c) Recursive AI, LLC. Third-party notices: `THIRD-PARTY-NOTICES.md`.
