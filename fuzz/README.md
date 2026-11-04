# Fuzzing

`fuzz/OneNlp.Fuzz` holds the fuzz targets. One executable, one target per input surface, chosen with the `ONENLP_FUZZ_TARGET` environment variable.

| Target | Input | Checks |
|---|---|---|
| `word-tokenizer` | arbitrary bytes, read as UTF-8 (or, when the first byte is odd, as raw UTF-16 units, which reaches lone surrogates) | the document built by `Nlp.Analyze`: tokens are non-empty, ordered slices of the text with only whitespace between them, and `FindToken` and `TryGetCharSpan` agree with the offsets |
| `tokenizer-json` | bytes read as a `tokenizer.json`, with a one-token-per-character toy model in place of the subword model | loading raises only `InvalidDataException`/`NotSupportedException`; when it loads, single and pair encodings keep parallel arrays and in-range offsets, decoding does not throw, and a split pattern that costs too much ends in `InvalidOperationException` |
| `code-point-regex` | a regular expression, a NUL byte, then a text | parsing raises only `InvalidDataException`; matching raises only `InvalidOperationException` (step budget or depth); matches are ordered non-empty ranges inside the text |
| `charsmap` | bytes used as the data of a SentencePiece character map normalizer | construction raises only `InvalidDataException`; normalizing never throws |

Only documented exceptions may escape a library call. Any other exception, a failed invariant, a hang or a memory blow-up is a finding.

Without a libFuzzer host:

    dotnet run -c Release --project fuzz/OneNlp.Fuzz -- random word-tokenizer 20000
    dotnet run -c Release --project fuzz/OneNlp.Fuzz -- replay word-tokenizer some-input...

`fuzz/run.sh <target> <seconds>` builds, instruments and runs a target under libFuzzer; it needs the libFuzzer .NET host and the SharpFuzz instrumenter (see the script header).

To fuzz with coverage by hand, instrument `RecursiveAI.OneNlp.dll` with the `SharpFuzz.CommandLine` tool and run the executable under the SharpFuzz libFuzzer host.

To add a target, write `Run(ReadOnlySpan<byte> data)` and add one line to the table in `Program.cs`. Minimized findings go under `tests/` as fixtures (under 1 MB) with a test that feeds them.
