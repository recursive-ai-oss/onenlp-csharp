# Fuzzing

`fuzz/OneNlp.Fuzz` holds the fuzz targets. One executable, one target per input surface, chosen with the `ONENLP_FUZZ_TARGET` environment variable.

| Target | Input | Checks |
|---|---|---|
| `word-tokenizer` | arbitrary bytes, read as UTF-8 (or, when the first byte is odd, as raw UTF-16 units, which reaches lone surrogates) | the document built by `Nlp.Analyze`: tokens are non-empty, ordered slices of the text with only whitespace between them, and `FindToken` and `TryGetCharSpan` agree with the offsets |

Only documented exceptions may escape a library call. Any other exception, a failed invariant, a hang or a memory blow-up is a finding.

Without a libFuzzer host:

    dotnet run -c Release --project fuzz/OneNlp.Fuzz -- random word-tokenizer 20000
    dotnet run -c Release --project fuzz/OneNlp.Fuzz -- replay word-tokenizer some-input...

To fuzz with coverage, instrument `RecursiveAI.OneNlp.dll` with the `SharpFuzz.CommandLine` tool and run the executable under the SharpFuzz libFuzzer host.

To add a target, write `Run(ReadOnlySpan<byte> data)` and add one line to the table in `Program.cs`. Minimized findings go under `tests/` as fixtures (under 1 MB) with a test that feeds them.
