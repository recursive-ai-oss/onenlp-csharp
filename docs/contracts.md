# Contracts

Behaviour that every component and tokenizer family relies on. Language-neutral and normative.

## Offsets

- Every offset a `Doc`, `Token` or `Span` reports is a UTF-16 code unit offset into the string the caller passed in (`Doc.Text`). Nothing is reported against a normalized copy.
- A token has `Idx` (first character) and `EndIdx` (just past the last). Spans cover whole tokens; `StartChar` is the first token's `Idx` and `EndChar` the last token's `EndIdx`. An empty span has `StartChar == EndChar`.
- `Token.Text` and `Span.Text` are slices of `Doc.Text`; they copy nothing. A span's text includes the whitespace between its tokens.
- `Doc.TryGetCharSpan` makes a span from character offsets only when both fall on token boundaries. Nothing is rounded.
- Tokenizer encodings (`TokenOffset`) count UTF-16 units for text passed as `char`, and bytes for text passed as UTF-8. Tokens that stand for no input text (added special tokens, padding) have the offsets `(0, 0)`.

## Doc

- Token boundaries are fixed when the document is created. They are non-empty, ordered and non-overlapping; what lies between tokens is the caller's whitespace.
- Annotations are columns indexed by token: strings as IDs in a shared `StringStore` (ID 0 is the empty string, meaning "not set"), heads as token indexes (a token that is its own head is a root), sentence boundaries as `Unset`/`Start`/`Inside`, entities as an ordered list of non-overlapping spans with a per-token IOB and type, and a confidence per token.
- A new document is unannotated: empty strings, every token its own head, boundaries unset, `EntIob.Unset`, confidence 1.
- A document with no sentence boundaries set is one sentence. After `SetEntities` (even with an empty list) every token has an `EntIob`.
- `Token.Confidence` is the confidence of the token's tag annotation. Entity and span confidences live on the span. Rule-based results have confidence 1.
- Reads may run concurrently; writes need exclusive use. The `StringStore` is thread-safe and only grows.

## Pipeline and components

- A pipeline is a word tokenizer followed by components in the order they were added. Component names are unique within a pipeline.
- A component only fills annotations; it never changes token boundaries or the text.
- `Pipeline.Process` may be called from several threads when the components allow it: per-call state stays in locals, shared state is read-only.
- `ProcessMany` reads its source lazily, tokenizes one batch, runs each component over the whole batch (`IPipelineComponent.ProcessBatch`, which defaults to calling `Process` per document), then yields the batch in input order. `ProcessManyAsync` is the same over an `IAsyncEnumerable<string>`; only reading the source awaits.
- Components are created by name from `ComponentRegistry`. Registration is by explicit call (a package's module initializer or a public `Register` method), never by scanning assemblies. An unknown name throws and lists the registered names.

## Tokenizers

- `Tokenizer` is the one type callers hold; the families (WordPiece, BPE, byte-level BPE, Unigram, Tiktoken) derive from it and implement `EncodeCore`, `Decode`, `IdToToken`, `TokenToId` and `VocabularySize`.
- `Encode` returns a `TokenEncoding`: ids, token strings, offsets, special-token mask, type ids, attention mask and word ids, all one entry per token. Truncation is the family's job (it must count special tokens against the limit); padding is applied by the base class afterwards.
- `Truncation` and `Padding` apply to `Encode` and `EncodeBatch`. `EncodeToIds`, `CountTokens` and `TrimToTokenBudget` ignore them.
- `EncodeToIds` writes IDs into the caller's buffer and returns `Done` or `DestinationTooSmall`. After `DestinationTooSmall` the written IDs are an exact prefix of the full encoding, and `consumed` is the end offset of the last written token; encoding the remainder separately is not guaranteed to continue the same sequence.
- `TrimToTokenBudget` returns the slice of the text covered by the first (or last) *n* tokens of the full encoding without special tokens.
- Invalid UTF-8 is read as U+FFFD, one per invalid sequence, and offsets still count the original bytes.
- `Tokenizer.FromFile` picks the format from the extension (`.json`, `.model`, `.txt`, `.tiktoken`); `FromStream` takes the format explicitly. A format without a loader throws `NotSupportedException`.

## Tokenizer.json pipeline (internal seam)

`Tokenizer.FromFile`/`FromStream(HuggingFaceJson)` returns a `HuggingFaceTokenizer`, composed as the published `tokenizer.json` format describes: **normalizer → pre-tokenizer → model → post-processor**, with a **decoder** for decoding, **added tokens** beside the model vocabulary, and the file's **truncation** and **padding** copied to `Tokenizer.Truncation` and `Padding`. All of it lives in `src/OneNlp/Tokenizers/Hf/` and is internal.

- **Alignment.** `NormalizedString` holds the current text and, for each UTF-16 unit, the range of the original input it came from. Normalizers rewrite it with `NormalizedStringBuilder` (inserted or replaced units share the origin of the unit they stand for). `PreTokenizedString` is a list of such pieces; a piece's index is the word index of its tokens. A range of a piece maps back to the original input from the origin start of its first unit to the origin end of its last.
- **Stages.** `INormalizer.Normalize(NormalizedString)`, `IPreTokenizer.PreTokenize(PreTokenizedString)`, `IPostProcessor.AddedTokenCount(isPair)` and `Process(encoding, pair, addSpecialTokens)`, `ITokenDecoder.DecodeChain(List<string>)`. Stages are immutable and thread-safe.
- **Model seam.** `ITokenizerModel.Tokenize(ReadOnlySpan<char> piece, List<ModelToken> output)` appends `(Id, Value, Start, End)` with offsets relative to the piece, plus `TokenToId`, `IdToToken` and `VocabularySize` (model entries only). The piece is the text after normalization and pre-tokenization (for byte-level BPE, the byte-mapped characters). Byte fallback, `ignore_merges`, unknown-token fusing and rank lookups are the model's business.
- **Registry.** `StageRegistry` maps a `type` string to a factory per kind. A model or stage is added from its own file by implementing the matching optional partial method of `StageRegistry` and calling `AddModel`/`AddNormalizer`/`AddPreTokenizer`/`AddPostProcessor`/`AddDecoder`: `RegisterWordPiece`, `RegisterUnigram`, `RegisterBpe`, `RegisterTiktoken`, `RegisterNormalizers`, `RegisterPreTokenizers`, `RegisterPostProcessors`, `RegisterDecoders`. Registering an existing type replaces it. Every documented type is registered first as a stub that throws `NotSupportedException` ("... is not implemented yet"); an undocumented type throws "... is not supported". A model object without `type` is read as `BPE`.
- **JSON access.** Factories read their object through `StageConfig` (typed accessors that throw `InvalidDataException` naming the entry and field; `Element` for bulk reads such as a vocabulary). The loader reads at most 512 MiB and parses with a nesting limit of 32.
- **Truncation and pair encoding** in the composed tokenizer: the room reserved for the template's special tokens is `IPostProcessor.AddedTokenCount`, counted only when special tokens are added; pairs are truncated longest-first.

## Encoders, heads and training (`RecursiveAI.OneNlp.Neural`)

- **Encoder seam.** `IEncoder.Encode(ReadOnlySpan<Doc>)` returns an `EncodedBatch`: one float32 row per token, the documents' rows stacked in order (`DocCount`, `Start(i)`, `Length(i)`, `Rows(i)`). One call serves both modes: with recording on and trainable `Weights` the matrix carries the autodiff graph into the encoder; inside `GradMode.NoGrad()` (inference, a frozen encoder) it is plain values. `Weights` (an `NnModule`, or `null` for an encoder with nothing to train or save, such as an imported ONNX model) fixes the state order for checkpoints. `Kind` and `WriteSettings` name the encoder in a saved pipeline (`EncoderRegistry`). `Dispose` is idempotent; heads that share an encoder may each dispose it.
- **Head contract.** `ITrainableComponent : IPipelineComponent` has `Encoder`, `Initialize(examples, seed)` (fixes the label set from the training examples' reference documents, in an order that depends only on their content, and builds the weights), `Loss(EncodedBatch, examples)` (one rank-0 tensor; the heads' losses are added and backpropagated once), `Predict(EncodedBatch, docs)` (writes into the documents), `Weights`, `Kind` and `WriteConfig` (labels and settings; rebuilt by `HeadRegistry`). `TrainableHead` implements the pipeline side: it skips empty documents, runs the encoder and `Predict` without gradients in evaluation mode, and serializes calls on one head. A head sees whole batches, so a softmax tagger, a transition or biaffine parser, span or transition NER and a pooled classifier all fit.
- **`Example`** (`RecursiveAI.OneNlp.Data`) pairs a predicted `Doc` with a reference `Doc`, plus gold `Cats` and `Spans`. `Alignment` maps tokens when the tokenizations differ (rules in `docs/data-formats.md`); a loss uses `GetAligned(column)`, where `null` means "no gold label".
- **`Train`.** `pipeline.Train(examples, TrainOptions, CancellationToken)` trains every trainable head and the encoders they read with AdamW. Batches are made by token count from a shuffle that depends only on the seed and the epoch. Dropout is applied to the encoder output. The dev set is `DevExamples` or a deterministic split of `DevFraction`; the dev score is `Score` (a delegate over the dev examples with their predicted documents annotated; `DevScores.Ud(metric)` and `DevScores.Entities()` wrap the scorers) or else the negative mean dev loss. `Patience` ends the run after that many evaluations without a new best; the best weights are put back. With `CheckpointDirectory`, `last.ckpt` (weights, optimizer, dropout counter, position, best score, patience) and `best.ckpt` are written atomically, and a later call resumes from `last.ckpt` to exactly the weights an uninterrupted run reaches (same machine and thread count). Progress goes to `IProgress<TrainProgress>`; the library never writes to the console.
- **Saved pipelines** are one file (`docs/model-files.md`).
- **Readers** (`RecursiveAI.OneNlp.Data`): `ConllUReader`/`ConllUWriter` and `JsonlReader` (`docs/data-formats.md`). `UdCorpus.Read` in `Scoring` reads through `ConllUReader`.
