# Saved pipelines

Normative for `PipelineFile` (`RecursiveAI.OneNlp.Neural`). One file holds a trained pipeline: its structure and label sets as JSON, and the weights of its encoders and heads in OneModel's checkpoint encoding.

## Layout (little endian)

| Offset | Size | Field |
|---|---|---|
| 0 | 8 | ASCII `ONENLPPL` |
| 8 | 4 | version, 1 |
| 12 | 4 | flags, 0 |
| 16 | 8 | config length `C` |
| 24 | 8 | weights length `W` |
| 32 | `C` | config, UTF-8 JSON |
| 32 + `C` | `W` | weights, a OneModel checkpoint (`docs/training.md` section 6 of that package) of the state of every encoder and head |

The file length is exactly `32 + C + W`.

## Config

```json
{"format": "onenlp-pipeline", "version": 1, "language": "en", "tokenizer": {"kind": "uax", "data": "english"},
 "encoders": [{"kind": "hash-embedding", "settings": {"width": 64, "buckets": 16384, "seed": 1}}],
 "components": [{"name": "tagger", "type": "head", "kind": "tagger", "encoder": 0,
                 "config": {"column": "Tag", "labels": ["NN", "VB"]}},
                {"name": "sentencizer", "type": "builtin", "settings": {"data": "english"}}]}
```

- `encoders` are the distinct encoders the heads read, in order of first use. Each is rebuilt by `EncoderRegistry.Create(kind, settings)`.
- `components` are in pipeline order. A `head` is rebuilt by `HeadRegistry.Create(kind, name, encoder, config)`; a `builtin` is created by name from `ComponentRegistry`. A component that is neither a trainable head nor registered cannot be saved.
- Weights are named `encoder<i>.…` (encoders in order, those with weights) and `head<j>.…` (trainable heads in pipeline order). Loading checks every name, type and shape against the shapes the config implies.
- `tokenizer` is `{"kind": "rule"}` or `{"kind": "uax", "data": "none"|"neutral"|"english"}`, the segmentation data set the tokenizer uses. Other tokenizers cannot be saved yet.
- A `builtin` component may carry `settings`, an object of at most 64 short strings, written by components that implement `IConfigurableComponent` and passed to `ComponentRegistry.Create` on load (the sentencizer writes `data`).

## Kinds

Encoder kinds (`encoders[i].kind`, with `settings`) and head kinds (`components[j].kind`, with `config`) that the loader knows. Weights of each are named under `encoder<i>.` and `head<j>.`.

| Kind | Settings or config | Weights |
|---|---|---|
| encoder `hash-embedding` | `width`, `buckets`, `seed` | `embed.weight` `[buckets, width]`; `project.weight`, `project.bias` |
| encoder `transformer` | `model`, `tokenizer` (file names of the exported ONNX encoder and its `tokenizer.json`, at most 1,024 characters each), `modelSha256` (optional, 64 hex characters), `pooling` (`first` or `mean`), `maxLength` (2 to 65,536) | none: the encoder is frozen and lives in its own files, which the pipeline file references and does not embed |
| encoder `fast` | `width` (8 to 2048), `depth` (0 to 32), `normRows`, `prefixRows`, `suffixRows`, `shapeRows` (each 1 to 4,194,304, and rows times embedding width over all tables at most 33,554,432 values together), `seed`; optional, absent meaning the first file version: `prefixLength` (1 to 8, default 1), `suffixLength` (1 to 8, default 3), `casedRows` (0 to 4,194,304, default 0: a fifth table for the case-preserving text), `embedWidth` (8 to 2048, default `width`), `hashes` (1 to 4, default 2), `plain` (0 or 1, default 0), `relu` (0 or 1, default 0: ReLU instead of GELU), `neighbourRows` (0 to 4,194,304, default 0: four tables for the lower-cased text and the suffix of the previous and next token: `embed_prev_norm`, `embed_next_norm`, `embed_prev_suffix`, `embed_next_suffix`), `contextRows` (same range, default 0: four tables for the text two tokens before and after and the shape of the previous and next token: `embed_prev2_norm`, `embed_next2_norm`, `embed_prev_shape`, `embed_next_shape`), `bigramRows` (same range, default 0: `embed_bigram_prev`, `embed_bigram_next`); every table counts in `mix.weight`'s input width | `embed_norm.weight`, `embed_prefix.weight`, `embed_suffix.weight`, `embed_shape.weight` (and `embed_cased.weight` when `casedRows` is set) (`[rows, embedWidth]`); `mix.weight` `[width, tables * embedWidth]`, `mix.bias`, and, unless `plain`, `mix_norm.weight`, `mix_norm.bias`; for each layer `i`: `window<i>.weight` `[width, 3 * width]`, `window<i>.bias`, `window_norm<i>.weight`, `window_norm<i>.bias` |
| encoder `transformer-trainable` | `family` (`modernbert` or `postnorm`), `model`, `tokenizer`, `modelSha256` (optional), `pooling`, `maxLength`, `heads` (1 to 1,024), `globalEvery`, `localWindow`, `ropeThetaGlobal`, `ropeThetaLocal`, `normEpsilon`, `positionOffset`, `maxBatchPieces` | the layers: `embedding_norm`, `layers<i>.*`, `final_norm` for `modernbert`; `positions`, `embedding_norm`, `layers<i>.*` for `postnorm`. The token table is not stored: it is read again from the exported model (a pipeline that trains it cannot be saved) |
| head `tagger` | `column` (`Tag`, `Pos` or `Morph`), `labels` | `output.weight` `[labels, width]`, `output.bias` |
| head `morphologizer` | `labels`: joint labels, each `POS=<upos>` or `POS=<upos>\|<features>`, at most 65,536 | `output.weight`, `output.bias` as the tagger |
| head `lemmatizer` | `labels`: classes, each `l` or `r` followed by a serialized edit tree, at most 262,144 | `output.weight`, `output.bias` as the tagger |

The `fast` encoder's optional settings are written only when they differ from the defaults, so a file made with the first-version settings is read and written unchanged. With one hash function only the low half of the 64-bit hash is used; with three or four, the further functions use a re-mixed hash (SplitMix64 of the hash plus the function number). With `plain`, the joined attributes go through a ReLU instead of a GELU and layer normalization. The `fast` encoder's hash is part of the format: FNV-1a over UTF-16 units, seeded per attribute and finished with SplitMix64 (`docs/contracts.md`), so a saved model hashes every token to the same rows on every platform. A serialized edit tree is `M<prefix>,<suffix>(<left>)(<right>)` for a match node and `S<n>:<original><m>:<replacement>` for a substitution, where `<n>` and `<m>` are the lengths in UTF-16 units of the strings that follow them; at most 4,096 characters and 64 levels deep. A label list with a repeated entry, a morphologizer label not starting with `POS=`, a lemmatizer class not starting with `l` or `r`, or a tree that does not parse is an `InvalidDataException`.

## Bounds on read

Config at most 16 MiB with JSON nesting at most 32; weights at most 1 GiB; at most 64 encoders and 1,024 components; label sets at most 1,048,576 entries of at most 4,096 characters; an embedding table at most 67,108,864 values and a head's output layer at most 67,108,864 weights. The declared lengths are compared with the real length of the data before anything is allocated. Any failure is an `InvalidDataException`.

**Referenced encoder files.** A `transformer` encoder is a multi-hundred-megabyte ONNX file plus a `tokenizer.json`. A pipeline file stores their names (and optionally the model's SHA-256), not their bytes: the same encoder serves many pipelines, and a pipeline file stays a few kilobytes. A relative name is looked up in `TransformerEncoder.ModelDirectory` (the current directory when unset); an absolute name is used as is. Loading fails with the file's own `FileNotFoundException` when a file is missing, and `VerifyModelFile` checks the hash on request. The model packages put the encoder files next to the pipeline files they belong to.

## Heads with a two-level output

The morphologizer and lemmatizer may have `headClasses` (0 to 4,096) in their config. With a value above 0, labels are stored in falling order of frequency and the output layer is two Linear layers, `head` (`headClasses + 1` outputs, the last meaning "other") and `tail` (the remaining classes); with 0 or absent it is the single layer `output`. The parser's `labelBilinear` (0 when the label biaffine is left out, absent meaning 1) and the shared-encoder pipelines of `recipes/en/joint` use the same file layout otherwise.

## Inference

At inference the fast encoder with `relu` computes from derived tables (each embedding table multiplied by its slice of `mix.weight`, each window layer as one product); they are rebuilt when a weight changes and are not stored. The encoder keeps, per thread, the result for the last documents it encoded, valid until that thread encodes other documents or any training-mode encoding happens, so the heads of a pipeline share one encoding.
