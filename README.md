# OneNLP

**spaCy-class NLP for .NET.** A clean-room library for .NET 10 with no native code, NativeAOT and trim clean. OneNLP is unaffiliated with other projects named OneNLP.

> **Early preview.** The API will change. Only components whose measured rows are in [docs/results.md](docs/results.md) are listed as working; speed rows are indicative until the first release.

## What is in it

| Package | Contents |
|---|---|
| `RecursiveAI.OneNlp` | `Doc`, `Token`, `Span` (character offsets into the original string); the component `Pipeline` with batch and streaming processing; tokenizers matching Hugging Face `tokenizers` byte for byte (WordPiece, BPE, byte-level BPE, Unigram/SentencePiece, Tiktoken); UAX #29 word and sentence segmentation; `Matcher`, `PhraseMatcher`, `EntityRuler`; English recognizers for numbers, units, currency, sequences (e-mail, URL, ...) and dates and times; language ID for 75 languages; JSON and binary serialization; the `Nlp` statics. Depends on the BCL and `RecursiveAI.OneFont` only. |
| `RecursiveAI.OneNlp.Neural` | The encoder seam, the fast hash-embedding encoder, the heads (tagger, morphologizer, edit-tree lemmatizer, biaffine dependency parser, entity recognizer), and `Train`, the API the shipped models are trained with. Depends on the core package, `RecursiveAI.OneModel` and `RecursiveAI.OneModel.Training`. |
| `RecursiveAI.OneNlp.Models.English` | The English fast-tier model: tags, morphology, lemmas, dependency parse and entities, from three separately trained models in one file (94.7 MB; `recipes/en/fast-tagging`, `parser`, `ner`, put together by `recipes/en/assemble`). `recipes/en/joint` trains a single shared encoder instead (23.7 MB, much faster, weaker rows); it is an option, not the packaged model. |

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Ada Lovelace wrote the first program in London.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t} {t.Pos} {t.Lemma} {t.Dep}");
}

foreach (Span e in Nlp.Entities("Ada Lovelace lived in London."))
{
    if (e.Label == EntityLabels.Person)
    {
        Console.WriteLine(e);   // offsets are into the string you passed in
    }
}
```

The packaged English model is within the noise of spaCy 3.8.18 (trained on the same data) on every UD English-EWT row gated here (gold and raw tokens: UPOS, XPOS, UFeats, AllTags, lemmas, UAS, LAS, CLAS, sentences) and on the UNER entity F1 (61.52 against 62.32); MLAS on raw text is below spaCy. Its speed is far below Catalyst's on the tagging path (about 17,000 tokens/s against about 300,000, indicative); the single-encoder option is closer. Every row is in [docs/results.md](docs/results.md).

The contracts are written down in [docs/contracts.md](docs/contracts.md).

## Building

You need the .NET SDK pinned in `global.json`.

    dotnet build -c Release
    dotnet test --solution OneNlp.slnx -c Release --no-build

Build output goes to `artifacts/` in the repository (ignored by git). Set `ONENLP_ARTIFACTS` to a directory to put it elsewhere; each checkout gets its own folder under it. Machine-specific MSBuild overrides can go in an ignored `Directory.Build.local.props`.

The NativeAOT smoke app is `tests/OneNlp.AotSmoke`; publish it with `dotnet publish tests/OneNlp.AotSmoke -c Release -r <rid>` (it needs the platform's native C toolchain and `zlib1g-dev` on Linux).

## Layout

- `src/OneNlp`, `src/OneNlp.Neural`, `src/OneNlp.Models.English`: the packages.
- `recipes/`: the training recipes users can run.
- `tests/OneNlp.Tests`, `tests/OneNlp.AotSmoke`: tests and the AOT smoke app.
- `bench/OneNlp.Bench`: BenchmarkDotNet suites.
- `fuzz/OneNlp.Fuzz`: fuzz targets ([fuzz/README.md](fuzz/README.md)).
- `tools/`: scripts for oracles and data ([tools/README.md](tools/README.md)).
- `docs/`: normative notes on behaviour.

## Licence

`MIT OR Apache-2.0`, at your option. See `LICENSE-MIT` and `LICENSE-APACHE`. Copyright (c) Recursive AI, LLC.
