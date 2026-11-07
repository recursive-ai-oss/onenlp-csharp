# Moving from Microsoft.ML.Tokenizers

Microsoft.ML.Tokenizers loads one tokenizer type per model family. OneNLP's `Tokenizer` loads Hugging Face `tokenizer.json`, SentencePiece `.model`, `vocab.txt` and `.tiktoken` files through one type and matches Hugging Face `tokenizers` and `tiktoken` byte for byte. This page maps the calls. It is written from Microsoft.ML.Tokenizers 2.0.1's published API and from running it as a black box in the speed comparison ([results.md](../results.md)); nothing comes from its source. There is no shim.

## At a glance

| Microsoft.ML.Tokenizers | OneNLP |
|---|---|
| `BertTokenizer.Create(vocabStream, options)` (WordPiece; options for lower-casing and accent stripping) | `Tokenizer.FromFile("tokenizer.json")`; the normalizer settings come from the file. `Tokenizer.FromFile("vocab.txt")` reads a bare vocabulary |
| `SentencePieceTokenizer.Create(stream, addBeginningOfSentence, addEndOfSentence)`; `LlamaTokenizer.Create` | `Tokenizer.FromFile("tokenizer.json")` or `Tokenizer.FromFile("tokenizer.model")`; the special tokens come from the post-processor of the `tokenizer.json` |
| `TiktokenTokenizer.CreateForModel("gpt-4o")` (the ranks come with the `Microsoft.ML.Tokenizers.Data.*` packages) | `Tokenizer.FromFile("o200k_base.tiktoken")`: you supply the ranks file; the encoding is taken from the file name or rank count (`r50k_base`, `p50k_base`, `cl100k_base`, `o200k_base`) |
| `tokenizer.EncodeToIds(text)` returns `IReadOnlyList<int>` | `tokenizer.EncodeToIds(text, ids, out written, out consumed)` writes into your buffer and returns an `OperationStatus` (`Done` or `DestinationTooSmall`); `Encode(text)` returns a `TokenEncoding` with ids, tokens, offsets, the special-token mask and word ids |
| `tokenizer.CountTokens(text)` | `tokenizer.CountTokens(text)` |
| `tokenizer.GetIndexByTokenCount(text, n, out normalized, out count)` and `GetIndexByTokenCountFromEnd` | `tokenizer.TrimToTokenBudget(text, n, TokenizerSide.Left or Right)` returns the slice of your text covered by the first or last *n* tokens |
| `tokenizer.Decode(ids)` | `tokenizer.Decode(ids)`, or `Decode(ids, destination, out idsConsumed, out charsWritten)` into your buffer |
| `EncodedToken` values with `Offset` | `TokenEncoding.Offsets` (`Start`, `End`) |

## Before and after

Microsoft.ML.Tokenizers (the calls its README shows):

```csharp
Tokenizer tokenizer = BertTokenizer.Create(File.OpenRead("vocab.txt"));
IReadOnlyList<int> ids = tokenizer.EncodeToIds("a text to encode");
int count = tokenizer.CountTokens("a text to encode");
string text = tokenizer.Decode(ids);
int index = tokenizer.GetIndexByTokenCount("a text to encode", 2, out string? normalized, out int tokenCount);
```

OneNLP (`samples/OneNlp.Samples/MigrationSamples.cs`, which builds and runs; it was run on a BERT `tokenizer.json`):

```csharp
// BertTokenizer.Create / TiktokenTokenizer.CreateForModel / SentencePieceTokenizer.Create  become one type that reads the file's own format.
Tokenizer tokenizer = Tokenizer.FromFile(tokenizerJson);

int[] ids = new int[512];
OperationStatus status = tokenizer.EncodeToIds("a text to encode", ids, out int written, out int consumed);   // EncodeToIds(text)
int count = tokenizer.CountTokens("a text to encode");                                                      // CountTokens(text)
string text = tokenizer.Decode(ids.AsSpan(0, written));                                                     // Decode(ids)
ReadOnlySpan<char> head = tokenizer.TrimToTokenBudget("a text to encode", 2);                               // GetIndexByTokenCount
```

## Differences that matter when you port

- **Special tokens.** `EncodeToIds`, `Encode`, `CountTokens` add the model's special tokens by default (`addSpecialTokens: true`), as Hugging Face `tokenizers` does; pass `false` for none. Check counts against your old code with the flag set the same way.
- **Equality with the Hugging Face reference.** On the parity corpus the ids, tokens, offsets and decoded text equal Hugging Face `tokenizers` and `tiktoken`. Where Microsoft.ML.Tokenizers differs from those references, ours follows them. The speed run lists the rows where the two libraries' outputs differ and so were not timed: WordPiece text with characters the comparator maps to its unknown token, long Unigram text, and the cleanup of spaces before punctuation when decoding WordPiece ([results.md](../results.md), "Rows not timed, and why").
- **Unigram numbering.** SentencePiece `.model` files and Hugging Face `tokenizer.json` files number the control tokens differently; ours follows the file it loads. If you compare ids with Microsoft.ML.Tokenizers on a SentencePiece model, map the numbering first (the same page explains the offset).
- **Offsets** count UTF-16 units for `char` input and UTF-8 bytes for byte input; special tokens carry `(0, 0)`.
- **Truncation and padding** are properties of the `Tokenizer` (loaded from `tokenizer.json`) and apply to `Encode` and `EncodeBatch`.
- **Allocation.** The span forms (`EncodeToIds` into your buffer, `CountTokens`) allocate nothing for WordPiece, Unigram, SentencePiece-BPE and byte-level BPE inputs in the speed run; Tiktoken allocates a little (408 B for a short sentence, 1.3 KB for eight).
- **Speed.** At or faster than Microsoft.ML.Tokenizers 2.0.1 on every timed row on x64 and arm64; load time is slower for WordPiece and SentencePiece-BPE (31.88 ms against 19.66 ms, and 48.44 ms against 10.91 ms on x64) and faster for Unigram and byte-level BPE; it is reported, not gated ([results.md](../results.md)).
