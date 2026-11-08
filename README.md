# OneNLP

**spaCy-class NLP for .NET.** A clean-room library for .NET 10 with no native code, NativeAOT and trim clean, built on [OneModel](https://www.nuget.org/packages/RecursiveAI.OneModel). OneNLP is unaffiliated with other projects named OneNLP.

Text in, structure out: tokens, sentences, tags, morphology, lemmas, dependency parses and entities through fast and accurate model tiers; token-exact Hugging Face tokenizers; numbers, units, currency, e-mail and URLs, and dates and times resolved to values in ten languages; language identification for 75 languages; text classification, span categorization and open-type entity recognition; sentence embeddings. You can train every one of the neural parts yourself with the API that trained the shipped models.

> Every quality and speed claim here is a row in [docs/results-table.md](docs/results-table.md), with its reference, its interval and its verdict, including the rows we lose.

## What is in it

| Package | Contents |
|---|---|
| `RecursiveAI.OneNlp` | The core; depends on the .NET base class library and [`RecursiveAI.OneFont`](https://www.nuget.org/packages/RecursiveAI.OneFont) only. `Doc`, `Token`, `Span` with character offsets into your string; `Pipeline` with batch and streaming processing; tokenizers that match Hugging Face `tokenizers` byte for byte (WordPiece, BPE, byte-level BPE, Unigram/SentencePiece, Tiktoken; `tokenizer.json`, SentencePiece `.model`, `vocab.txt` and `.tiktoken` loaders); UAX #29 word and sentence segmentation; `Matcher`, `PhraseMatcher`, `EntityRuler`; recognizers for numbers, ordinals, percentages, units, currency, e-mail, URL, IP, phone-like sequences, hashtags, mentions, GUIDs and dates and times, with rules as data, in English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi and Chinese; language ID for 75 languages (3.6 MB); JSON and binary serialization of documents; the `Nlp` statics. |
| `RecursiveAI.OneNlp.Neural` | Depends on the core, `RecursiveAI.OneModel` and `RecursiveAI.OneModel.Training`. The encoders (the fast hash-embedding encoder, transformer encoders loaded from exported ONNX weights, fine-tunable), the heads (tagger, morphologizer, edit-tree lemmatizer, biaffine dependency parser, entity recognizer, text classifier, span categorizer, and, as trained word tokenizers, a multiword splitter and a word segmenter), `Train` with checkpoints and early stopping, `PipelineFile`, and the sentence embedder. |
| `RecursiveAI.OneNlp.Models.English`, `.French`, `.German`, `.Spanish`, `.Portuguese`, `.Dutch`, `.Italian`, `.Chinese`, `.Turkish`, `.Hindi` | Ten fast-tier languages: tokenization, tags, morphology, lemmas, dependency parse and entities (Hindi has no learned lemmas or XPOS; see below). Add the package and `Nlp.Analyze` and `Nlp.Entities` use it. |
| `RecursiveAI.OneNlp.Models.Embeddings` | The default English sentence embedder (bge-small-en-v1.5, MIT) for `Nlp.Embed`. |
| `RecursiveAI.OneNlp.Models.Embeddings.Multilingual` | multilingual-e5-small (MIT), shipped in shards that depend on one another; reference this one package. |
| `RecursiveAI.OneNlp.Models.Multilingual` | Distilled accurate tier: syntax and entities for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese (nine languages), plus open-type NER (labels named at runtime). |
| `RecursiveAI.OneNlp.Models.Multilingual.Large` | Base-size accurate tier: syntax for 84 languages, entities for 19, with trained word segmentation for unspaced scripts. About 3.5 GB across 19 packages; reference the root package. Deploy the complete publish directory, including the copied head assets. |

**Hindi** has no treebank we may train on (Hindi-HDTB is non-commercial), so `RecursiveAI.OneNlp.Models.Hindi` is trained on Hindi web text that the accurate tier labelled by transfer from other languages, Marathi and Sindhi above all. It has UPOS, features, a parse and PER/ORG/LOC entities, no real lemmas (`Token.Lemma` is the token text) and no XPOS, and its scores (UPOS 83.2, LAS 52.8 on Hindi-PUD, gold tokens) are below the other languages'; see [docs/results-table.md](docs/results-table.md). Hindi UD is not available for an accurate-tier model.

Nothing is ever downloaded. A model is delivered with its package; without the package, a verb that needs it throws an exception that names the package to add (see [docs/model-packages.md](docs/model-packages.md)).

## Install

    dotnet add package RecursiveAI.OneNlp
    dotnet add package RecursiveAI.OneNlp.Models.English      // a model package per language you analyze
    dotnet add package RecursiveAI.OneNlp.Models.Embeddings   // for Nlp.Embed

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
- **The base-size accurate tier** (84 languages) is measured against spaCy 3.8.18 trained on the same data: 117 of 144 rows ahead, 14 within noise, 13 behind (listed under known gaps). Published research systems such as Stanza, UDPipe 2 and Trankit, which train a larger model per treebank, are ahead on most rows; their numbers are shown beside ours as information.
- **Text classification and span categorization** are ahead of spaCy's defaults on accuracy and F1; span precision is within noise at the default threshold.
- **Embeddings** equal sentence-transformers in the same harness; the model cards' own numbers are shown beside them, with the cells neither reproduces.

The English tagging path is compared with Catalyst at matched or better measured accuracy; see the platform-specific speed rows in the results table.

## Known gaps

Stated plainly; each is also in [docs/results-table.md](docs/results-table.md).

- **Accurate-tier coverage:** no model for Ancient Hebrew, Basque, Belarusian, Bulgarian, Chintang, Classical Armenian, Estonian, Gothic, Hungarian, Kadiweu, Khoekhoe, Maghrebi Arabic French, Manx, Mbya Guarani, Naga, Nheengatu, Occitan, Old Church Slavonic, Pomak, Ruuli, Shanghainese, South Levantine Arabic, Turkish German, Urdu or Xibe (25 languages). Their available training data is non-commercial, platform-restricted or has unresolved terms; see [the treebank licence inventory](docs/ud-licences.md). Hindi is a separate test-only UD case, with the transferred-label fast tier described above.
- **Accurate-tier quality:** 13 of 144 rows are behind spaCy trained on the same data: Chinese UPOS, UFeats, lemmas, UAS and LAS (LAS 61.42 against 68.24); Spanish UPOS, UFeats and lemmas (by 0.2–1.0); Dutch WikiNER entities (F1 83.70 against 86.64); German raw tokenization; Turkish raw sentence boundaries. Published research systems are ahead on most rows, typically by 3–9 LAS points (1,205 of 1,314 best-published comparisons; 534 of 650 against Stanza 1.5.1). See [the full accurate-tier rows](docs/accurate-tier-rows.md).
- **Open-type NER:** all five CrossNER zero-shot rows are 19–35 F1 points below the published GLiNER-S model, and protocol equivalence is unverified. No zero-shot quality pass is claimed.
- **Hindi:** measured on Hindi-PUD only; gold UPOS 83.19 and LAS 52.79, raw UPOS behind the same-label spaCy baseline. No learned lemmatizer or XPOS, and entity scores are information from a small hand-labelled sample, not a treebank gate.
- English raw-text MLAS is behind spaCy (65.30 against 69.05; an information row).
- Embeddings: five MTEB cells are below the model cards for sentence-transformers itself, and ours equal it. Language ID is below Lingua on some languages and short-text buckets (listed in the results).
- Italian's fast tier is trained on 897 sentences, since the larger Italian treebanks are non-commercial. Catalyst has English models only, so the other fast-tier languages have no managed speed comparator.

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
