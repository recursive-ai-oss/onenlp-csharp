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

## Bounds on read

Config at most 16 MiB with JSON nesting at most 32; weights at most 1 GiB; at most 64 encoders and 1,024 components; label sets at most 1,048,576 entries of at most 4,096 characters; an embedding table at most 67,108,864 values and a head's output layer at most 67,108,864 weights. The declared lengths are compared with the real length of the data before anything is allocated. Any failure is an `InvalidDataException`.
