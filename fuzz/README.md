# Fuzzing

`fuzz/OneNlp.Fuzz` holds the fuzz targets. One executable, one target per input surface, chosen with the `ONENLP_FUZZ_TARGET` environment variable.

| Target | Input | Checks |
|---|---|---|
| `word-tokenizer` | arbitrary bytes, read as UTF-8 (or, when the first byte is odd, as raw UTF-16 units, which reaches lone surrogates) | the document built by `Nlp.Analyze`: tokens are non-empty, ordered slices of the text with only whitespace between them, and `FindToken` and `TryGetCharSpan` agree with the offsets |
| `tokenizer-json` | bytes read as a `tokenizer.json`, with a one-token-per-character toy model in place of the subword model | loading raises only `InvalidDataException`/`NotSupportedException`; when it loads, single and pair encodings keep parallel arrays and in-range offsets, decoding does not throw, and a split pattern that costs too much ends in `InvalidOperationException` |
| `tokenizer-json-models` | the same, with the real WordPiece, BPE, Unigram and WordLevel models behind the stages | the same rules |
| `code-point-regex` | a regular expression, a NUL byte, then a text | parsing raises only `InvalidDataException`; matching raises only `InvalidOperationException` (step budget or depth); matches are ordered non-empty ranges inside the text |
| `charsmap` | bytes used as the data of a SentencePiece character map normalizer | construction raises only `InvalidDataException`; normalizing never throws |
| `segmentation` | arbitrary bytes, as above | word tokenizer and sentencizer with no data, neutral data and English data: offsets ascending and in range, every non-white-space character inside a token, sentences partition the tokens |
| `doc-json` | arbitrary bytes | `Doc.FromJson`: only `InvalidDataException` may escape; a document that reads must write to binary and back to the same bytes, and to JSON and back (unless it holds an unpaired surrogate, which JSON cannot carry) |
| `doc-binary` | byte 0 is a mode, the rest a serialized document (bit 0 rewrites the header's flags and length to fit the input, bit 1 also appends a valid checksum, so mutations reach the section decoders) | `Doc.FromBytes`: only `InvalidDataException`; a document that reads writes and reads back to the same bytes |
| `docbin` | same framing, for a bin | `DocBin.FromBytes` and `DocBinReader`: only `InvalidDataException`; the streaming reader yields the same documents as the whole-bin reader |
| `langid-detect` | arbitrary bytes, as above | `LanguageDetector.Default`: confidences are probabilities summing to 1, the best language is the first confidence, a restricted detector never answers outside its set, mixed-language runs tile the text |
| `langid-model` | arbitrary bytes | `LanguageDetector.Load`: only `InvalidDataException`; a model that loads can detect |
| `recognizers` | arbitrary bytes, read as UTF-8 (or raw UTF-16 units when the first byte is odd) | `Recognizer.English`: results ordered, in range, without overlap, with their own text; the pipeline component accepts them |
| `recognizer-rules` | arbitrary bytes as rule data | `Recognizer.FromRules`: only `RecognizerRulesException`; rules that load must run on a sample text with the same checks |

Only documented exceptions may escape a library call. Any other exception, a failed invariant, a hang or a memory blow-up is a finding.

Without a libFuzzer host:

    dotnet run -c Release --project fuzz/OneNlp.Fuzz -- random word-tokenizer 20000
    dotnet run -c Release --project fuzz/OneNlp.Fuzz -- replay word-tokenizer some-input...

To fuzz with coverage, use `fuzz/run.sh <target> <seconds>`: it builds the harness, writes seed inputs (`seeds <target> <dir>`), instruments `RecursiveAI.OneNlp.dll` with the `SharpFuzz.CommandLine` tool and runs the SharpFuzz libFuzzer host. `ONENLP_FUZZ_WORK` (outside the checkout) and `ONENLP_FUZZ_TOOLS` (holding `libfuzzer-dotnet`, and `sharpfuzz` unless `SHARPFUZZ` names it) are required; findings land in `$ONENLP_FUZZ_WORK/findings/<target>/`. Replay one with `fuzz/run.sh replay <target> <file>`.

`conllu`, `jsonl` and `pattern-json` read CoNLL-U, JSON Lines examples and pattern files. `fuzz/run-many.sh <seconds> <target>...` runs several targets side by side, each with its own work directory and a copy of the seed folders (`fuzz/seeds`, `fuzz/corpus`). The `langid-model` seed is the shipped model re-emitted with at most 2^8 buckets per group: valid, small, and with a weight section to mutate; set `ONENLP_LANGID_WEIGHTS` when building so `langid-detect` runs the real model. The libFuzzer host needs a .NET 9 runtime for the instrumenter only.

Targets for the neural layer: `edit-tree` (the edit-tree text format), `fast-settings` (the fast encoder's settings object), `pipeline-config` (a pipeline config inside a valid header, no weights: reaches encoder and head construction) and `pipeline-file` (a whole saved pipeline, which must also save and load back to the same bytes). `run.sh` instruments both library assemblies.

To add a target, write `Run(ReadOnlySpan<byte> data)` and add one line to the table in `Program.cs`. Minimized findings go under `tests/` as fixtures (under 1 MB) with a test that feeds them.
