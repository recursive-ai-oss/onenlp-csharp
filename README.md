# OneNLP

**spaCy-class NLP for .NET.** A clean-room library for .NET 10 with no native code, NativeAOT and trim clean. OneNLP is unaffiliated with other projects named OneNLP.

> **Early preview, under construction.** Today the library has the shared types and contracts everything else will build on, and nothing more: no models, no tokenizer families, no recognizers, no language ID. Nothing here claims accuracy, speed or parity with any other library yet, and the API will change.

## What is in it

| Package | Contents today |
|---|---|
| `RecursiveAI.OneNlp` | `Doc`, `Token` and `Span` (character offsets into the original string, zero-copy views), a component `Pipeline` with batch and streaming processing, name-to-factory component registration without reflection, the `Tokenizer` contract (BCL span style), a rule-based word tokenizer, and the `Nlp` statics. Depends on the BCL and `RecursiveAI.OneFont` only. |
| `RecursiveAI.OneNlp.Neural` | The encoder seam, so far one interface. Depends on the core package, `RecursiveAI.OneModel` and `RecursiveAI.OneModel.Training`. |

```csharp
using RecursiveAI.OneNlp;

Doc doc = Nlp.Analyze("Ada Lovelace wrote the first program.");
foreach (Token t in doc)
{
    Console.WriteLine($"{t.Idx}..{t.EndIdx} {t}");   // offsets are into the string you passed in
}

using Pipeline pipeline = Pipeline.Blank(Language.English).Add("norm");
foreach (Doc d in pipeline.ProcessMany(["First text.", "Second text."]))
{
    Console.WriteLine(d[0].Norm);
}
```

The contracts are written down in [docs/contracts.md](docs/contracts.md).

## Building

You need the .NET SDK pinned in `global.json`.

    dotnet build -c Release
    dotnet test --solution OneNlp.slnx -c Release --no-build

Build output goes to `artifacts/` in the repository (ignored by git). Set `ONENLP_ARTIFACTS` to a directory to put it elsewhere; each checkout gets its own folder under it. Machine-specific MSBuild overrides can go in an ignored `Directory.Build.local.props`.

The NativeAOT smoke app is `tests/OneNlp.AotSmoke`; publish it with `dotnet publish tests/OneNlp.AotSmoke -c Release -r <rid>` (it needs the platform's native toolchain, `clang` and `zlib1g-dev` on Linux).

## Layout

- `src/OneNlp`, `src/OneNlp.Neural`: the packages.
- `tests/OneNlp.Tests`, `tests/OneNlp.AotSmoke`: tests and the AOT smoke app.
- `bench/OneNlp.Bench`: BenchmarkDotNet suites.
- `fuzz/OneNlp.Fuzz`: fuzz targets ([fuzz/README.md](fuzz/README.md)).
- `tools/`: scripts for oracles and data ([tools/README.md](tools/README.md)).
- `docs/`: normative notes on behaviour.

## Licence

`MIT OR Apache-2.0`, at your option. See `LICENSE-MIT` and `LICENSE-APACHE`. Copyright (c) Recursive AI, LLC.
