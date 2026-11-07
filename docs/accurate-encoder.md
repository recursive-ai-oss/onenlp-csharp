# The accurate tier's encoder: transformers on OneModel

How an exported transformer becomes an `IEncoder`, what was measured, and what can and cannot be trained. Tools are in `tools/accurate/` and `tools/OneNlp.Accurate/`; the recipe is `recipes/accurate/en-tagging/`.

## Export

`tools/accurate/export_onnx.py` runs PyTorch and Hugging Face `transformers` through their documented APIs (`AutoModel.from_pretrained`, `torch.onnx.export`) and writes one ONNX file per model with

- inputs `input_ids` and `attention_mask`, `int64`, `[batch, sequence]` (both axes dynamic);
- output `last_hidden_state`, `float32`, `[batch, sequence, width]`;
- opset 17, the TorchScript-based exporter (`dynamo=False`), eager attention so attention is plain `MatMul`/`Softmax`;
- a `<name>.export.json` beside it with the repository revision, library versions, file sizes and SHA-256.

`--tiny` builds a small random model from a repository's configuration, and `make_tiny_fixture.py` generates the committed test fixture (`testdata/accurate/`: a random 2-layer BERT-style encoder of width 16, a hand-made WordPiece `tokenizer.json`, and PyTorch's hidden states for fixed ids). The fixture is ours; it is not derived from a released model.

Pins: torch 2.15.0 (CPU), transformers 5.22.1, onnx 1.23.2, onnxruntime 1.31.1.

| Model | Repository revision | ONNX size | SHA-256 |
|---|---|---|---|
| `xlm-roberta-base` | `e73636d4f797dec63c3081bb6ed5c7b0bb3f2089` | 1,110,013,637 | `fb0644d3c12460c1e7dc7c9797224aafb01be59f9e0f9cd94a6e221ffed77b7b` |
| `jhu-clsp/mmBERT-base` | `c5955035435e2bf121cde7f3c8863ef52ff35d82` | 1,228,334,485 | `6a9f485066d95de7eec7fb29c0e3839a9c4ddbefd6f87d46ece33b2cb52a550b` |

## Inference parity

OneModel (fast mode, AVX-512 kernel path) against PyTorch float32 on 200 multilingual texts (150 Tatoeba sentences, 50 UDHR paragraphs, cut at 400 characters; the same records for both models, chosen by `select_texts.py`), tokenized by our own `tokenizer.json` loader, one text per run. Hidden states compared for every token.

| Model | Tokens | max abs | mean abs | min cosine | mean cosine |
|---|---:|---:|---:|---:|---:|
| xlm-roberta-base | 4,764 | 6.3e-5 | 2.9e-7 | 1.00000000 | 1.00000000 |
| mmBERT-base | 5,255 | 3.5e-3 | 2.3e-6 | 0.99999996 | 1.00000000 |

mmBERT's hidden states reach 40.7 in magnitude (XLM-R's 18.6), so its largest difference is about 1e-4 relative. Neither graph needed an operator OneModel lacks.

Indicative latency, 8 threads, sequence length 64, median of 20 after 3 warm-ups, against ONNX Runtime 1.31.1 on the same file (a shared host, load average 2 to 5; not a gated row):

| Model | Batch | OneModel | ONNX Runtime |
|---|---:|---:|---:|
| xlm-roberta-base | 1 | 25.0 ms | 21.8 ms |
| xlm-roberta-base | 8 | 122 ms | 135 ms |
| mmBERT-base | 1 | 42.7 ms | 40.4 ms |
| mmBERT-base | 8 | 268 ms | 203 ms |

## `TransformerEncoder`

`TransformerEncoder(modelPath, tokenizerPath, pooling = Mean, maxLength = 512)` loads the ONNX file and `tokenizer.json`.

1. The whole document text is tokenized once without special tokens; pieces carry UTF-16 offsets into `Doc.Text`.
2. The special tokens the tokenizer wraps a text in are found by encoding a probe text, then added around every window.
3. A document longer than `maxLength` (default 512) is cut into windows that overlap by half. A piece takes its vector from the window whose centre is nearest, so no piece is read near a window edge when another window reads it better.
4. A piece belongs to every token whose character range it overlaps. `Mean` (the default) averages the vectors of those pieces; `FirstPiece` takes the first one. On English-EWT development data a frozen mmBERT tagger scored 96.39 UPOS with `Mean` and 95.62 with `FirstPiece`. A token no piece overlaps (a character the normalizer drops) gets a zero vector.
5. Windows from all documents of a call run together in batches of `WindowBatch` (default 8), longest first so padding stays small.
6. Outputs are cached per `Doc` instance, so repeated epochs over the same documents run the transformer once.

The encoder is **frozen**: `Weights` is `null`, `Train` leaves it alone, and heads train on its output. A saved pipeline **references** the encoder files by name and optional SHA-256 (`docs/model-files.md`).

## Head training on a frozen encoder

`recipes/accurate/en-tagging` trains the existing `Tagger` (UPOS, one linear layer) on UD English-EWT r2.18 (train for training, dev for selection) over the frozen encoder; see its `PROVENANCE.md` for the results.

## Fine-tuning the encoder

The graph runtime and the autodiff tensor layer are separate: `Session.Run` returns tensors with `RequiresGrad == false`, so an imported graph cannot be backpropagated. `TrainableTransformerEncoder` therefore rebuilds the encoder from the exported file's weights in modules written on OneModel's tensor ops, so `Train` updates the encoder together with the head. `TransformerEncoder` (above) stays the frozen, cached inference path.

```csharp
using var encoder = TrainableTransformerEncoder.LoadOnnx("mmbert-base.onnx", "tokenizer.json");   // family detected from the weights
using var pipeline = Pipeline.Blank(Language.English).Add(new Tagger(encoder, TokenColumn.Pos));
pipeline.Train(examples, new TrainOptions { LearningRate = 5e-5f, WarmupSteps = 40, BatchWords = 500 });
PipelineFile.Save(pipeline, "tagger.onenlp");   // kind "transformer-trainable": settings plus the layers' weights
```

### Encoder kinds in a saved pipeline

| Kind | Class | Trains | Weights in the pipeline file |
|---|---|---|---|
| `transformer` | `TransformerEncoder` | no (frozen ONNX graph) | none; file names and hash only |
| `transformer-trainable` | `TrainableTransformerEncoder` | yes | the layers (not the frozen token table); `PipelineFile.Save` to a path replaces it by `transformer` over a written graph (see below) |

Both reference the exported model and tokenizer by name (`TransformerEncoder.ModelDirectory`). The trainable kind reads the token table from the exported model again on load and overwrites the layers from the checkpoint, so the model file must still be there.

### Families

The family is read from the weight names (`TransformerFamily.Auto`) or given. Weights come from graphs written by the PyTorch exporter without dynamo (`torch.onnx.export(..., dynamo=False)`), where each linear layer's weight is the constant of the `MatMul` node named after the module; other graphs are rejected with a message.

- **`ModernBert`** (mmBERT, ModernBERT; `ModernBertModule`). Warner et al. 2024 (arXiv 2412.13663) §2 and the mmBERT paper (arXiv 2509.06888): token embedding, layer norm, then pre-norm layers `x = x + Wo(attention(rotary(Wqkv(norm(x)))))` and `x = x + Wo2(gelu(a) * b)` with `[a, b] = Wi(norm(x))` (GeGLU, Shazeer 2020, arXiv 2002.05202), a final norm. No biases, norms without shift, epsilon 1e-5; the first layer has no attention norm. Layer `i` is global when `i % 3 == 0`; the others see tokens at most 64 positions away (a 128-token window). Rotary embeddings (Su et al. 2021, arXiv 2104.09864) pair coordinate `i` with `i + d/2`, base 160,000 in mmBERT's global and local layers (ModernBERT: 160,000 and 10,000). Details the papers leave open were settled against the exported graph of mmBERT-base and checked against PyTorch: `Wqkv` is laid out `[3, heads, d]`, the first half of `Wi` goes through GELU, the window test is `|i - j| <= 64`, attention scale `1/sqrt(d)`.
- **`PostNorm`** (XLM-R, RoBERTa, BERT; `PostNormModule`). Token and learned position embeddings (the single token-type vector is added into every position row), layer norm, and post-norm layers with exact GELU. XLM-R starts positions at 2 (`PositionOffset`), BERT at 0; epsilon is read from the graph (1e-5 and 1e-12).

The head count is not in the weights: it defaults to width / 64 (set `Heads` otherwise). A graph with two different rotary bases does not say which table is the global one, so `GlobalRopeTheta` and `LocalRopeTheta` must be given for it (the loader refuses to guess); a graph with one base uses it for both.

### Pooling and the embedding table

Pieces become tokens as in `TransformerEncoder` (same tokenizer, windows and `Mean`/`FirstPiece`), but as a differentiable gather: each token's vector is the sum of the rows of its pieces, scaled by `1/count`, so gradients reach every piece. Windows of up to `MaxBatchPieces` padded pieces (default 8,192) run together; padding keys are masked.

`FreezeEmbeddings` (default `true`) keeps the 256,000 x 768 token table (197 million values in mmBERT) out of training and out of the saved weights. Fine-tuning a tagger barely moves it, and dense AdamW over it would add two moment tensors and a 786 MB gradient to every step. With `false` the table trains, and a pipeline holding it cannot be saved (the pipeline file's weights are capped at 1 GiB, and `WriteSettings` says so).

`Train` has one learning rate for the encoder and the head. 5e-5 with a short warm-up is what the recipe uses.

### Tests

`tests/OneNlp.Tests/TrainableTransformerTests.cs` uses two random tiny encoders exported from PyTorch (`tools/accurate/make_tiny_fixture.py`, `make_tiny_modernbert_fixture.py`; the second has alternating global and local layers with a window of 8, two rotary bases and padding): modules against PyTorch's hidden states (max abs below 2e-5), padded batches against single sequences, token vectors against `TransformerEncoder`'s for both poolings and with windowing, finite-difference gradients through rotary, the gated feed-forward and the window mask, a tagger trained through `Train` and reloaded from a saved pipeline, and a resumed run reproducing an uninterrupted one bit for bit.

### Measured

Forward parity of the trainable modules (one text per run, fast mode, the same 200 texts as above; `tools/OneNlp.Accurate trainable-parity`). "Graph runtime" is `TransformerEncoder`'s inference path on the exported file.

| Model | Tokens | Comparison | max abs | mean abs | min cosine |
|---|---:|---|---:|---:|---:|
| mmBERT-base (`ModernBert`) | 5,255 | trainable vs PyTorch | 1.0e-3 (2.6e-5 of the largest value, 40.7) | 2.2e-6 | 1.00000000 |
| | | trainable vs graph runtime | 4.6e-3 | 1.9e-6 | 0.99999993 |
| | | graph runtime vs PyTorch | 3.5e-3 | 2.3e-6 | 0.99999996 |
| xlm-roberta-base (`PostNorm`) | 4,764 | trainable vs PyTorch | 7.3e-5 | 2.7e-7 | 1.00000000 |
| | | trainable vs graph runtime | 4.0e-5 | 2.1e-7 | 1.00000000 |
| | | graph runtime vs PyTorch | 6.3e-5 | 2.9e-7 | 1.00000000 |

Batches of 8 right-padded sequences gave exactly the rows of the sequences run alone. The trainable module is closer to PyTorch than the imported graph is.

One fine-tuning step (24 sequences of 32 pieces, 8 threads, 110,344,721 trainable parameters, frozen token table, AdamW, gradient clipping), C# against plain PyTorch 2.15.0 CPU on the same batch (`step-time` and `tools/accurate/torch_step_time.py`; indicative, shared host at load average about 25, steady-state steps 1 to 3):

| | forward | backward | clip | AdamW | total |
|---|---:|---:|---:|---:|---:|
| OneModel | 0.70 s | 1.27 s | 0.33 s | 0.34 s | 2.62 s |
| PyTorch | 0.47 s | 0.80 s | 0.06 s | 0.20 s | 1.52 s |

The C# step takes 1.7 times the PyTorch step, inside the 3x rule. Gradient clipping (about 6 times) and the AdamW update (1.7 times) are the slowest parts relative to PyTorch. The full recipe ran at about 6.6 s per step on batches of 500 words (about 700 pieces), peak resident memory 11 GB.

Fine-tuning proof (`recipes/accurate/en-tagging`, `--finetune`; `PROVENANCE.md`): one 26-minute chunk, 236 steps (0.58 epoch of UD English-EWT r2.18 train), development UPOS 96.894 at step 200 (the frozen encoder reached 96.39 on development), test UPOS 97.07 [96.79, 97.35], 0.84 [0.62, 1.07] above the frozen encoder's 96.23 and 2.78 [2.44, 3.16] above spaCy's 94.29.

## Fine-tuned weights written back to ONNX

The trainable modules are about three times slower at inference than OneModel's graph runtime on the same weights, so a fine-tuned encoder is not served by them. `TrainableTransformerEncoder.ExportOnnx(path)` writes the fine-tuned weights into an ONNX file of the graph the encoder was loaded from, and `PipelineFile.Save(pipeline, path)` does it for every fine-tuned transformer in the pipeline.

```csharp
PipelineFile.Save(pipeline, "tagger.onenlp");   // also writes tagger.encoder0.onnx beside it
using var loaded = PipelineFile.Load("tagger.onenlp");   // the encoder is a TransformerEncoder on the written graph
```

- **What is written.** A copy of the original export in which only the values of the layers' weights (and the token table, if it trained) differ. The graph, its operators, constants and every other byte are unchanged. The file is as large as the original (about 1.2 GB for mmBERT-base, 142 MB for the student), is written beside the pipeline file as `<name>.encoder<N>.onnx` and is referred to by that plain file name, looked up in the pipeline file's directory. The two files travel together. Loading from a stream cannot find it and says so.
- **How.** `OnnxWeightPatcher` (no dependency) indexes the file's protobuf fields with reads at offsets (`onnx.proto3`: ModelProto.graph, GraphProto.node and .initializer, TensorProto dims, data_type, name, raw_data; the protobuf encoding specification), then writes the output field by field: untouched fields are copied as byte ranges, a replaced initializer is re-encoded with its new `raw_data`. Linear weights go back to the graph's `[in, out]` layout. A BERT-family graph adds one token-type vector to every position row; the position table is written with it folded in and the token-type table zeroed, which gives the graph the same sums. The PyTorch exporter stores equal tensors once and names the others with `Identity` nodes (the zero biases of a fresh model): a replacement for such a name becomes an initializer and the node is dropped; a tensor shared with a name that is not being replaced is refused. Tensors in external data files, non-float32 weights and results over 2 GiB are refused with `NotSupportedException`.
- **Bounds.** Every length is compared with the bytes left in its parent before it is read; names are at most 4 KiB; a node larger than 64 KiB is never read; at most 4 million fields per message; the output is written under a temporary name and renamed, and nothing is left behind on failure.
- **Keeping the weights in the pipeline file.** `PipelineFile.Save(pipeline, path, exportEncoderGraphs: false)`, or saving to a stream, stores them inside the file as before. The pipeline then loads as a trainable encoder: it can be trained further, and it runs on the slower modules. Pipeline files written before this existed still load.
- **Token table.** An encoder that trained its token table can be saved this way too (the table goes into the graph, not the pipeline file), which the 1 GiB cap of the pipeline file previously forbade.

### Measured

Hidden states of the written graph against the trainable modules the weights came from, on the 200 parity texts above (5,255 tokens, one text per run, 8 threads, fast mode), and ONNX Runtime 1.31.1 (CPU, black box) on the same written file against the modules' states (`tools/OneNlp.Accurate export-parity`, `tools/accurate/ort_hidden_compare.py`, `tools/accurate/export-rows.sh`):

| Encoder | Comparison | max abs | largest value | min cosine |
|---|---|---:|---:|---:|
| student, fine-tuned (`r3-upos-en`) | OneModel graph vs modules | 6.5e-5 | 4.5 | 1.00000000 |
| | ONNX Runtime vs modules | 1.9e-4 | 4.5 | 0.99999976 |
| mmBERT-base, fine-tuned 25 steps | OneModel graph vs modules | 6.9e-4 | 40.3 | 0.99999999 |
| | ONNX Runtime vs modules | 1.2e-3 | 40.3 | 0.99999976 |

UPOS on EWT r2.18 test (gold tokens): the student scores 95.88 [95.55, 96.19] through the written graph, as before, with no token whose prediction differs from the inline pipeline's (25,094 tokens). The mmBERT run, 25 steps from the frozen start (a smoke fine-tune, not a recipe), scores 90.67 [90.22, 91.09] through the written graph and also has no differing token.

Indicative speed, tagger over the first 400 sentences of the EWT test split (6,305 tokens), batches of 32 documents, median of 2 repetitions after a warm-up, shared host (load 10 to 16), not a gated row:

| Encoder | Threads | Trainable modules | Written graph | Ratio |
|---|---:|---:|---:|---:|
| student | 1 | 811 tok/s | 2,690 | 3.3 |
| student | 8 | 1,914 | 11,474 | 6.0 |
| mmBERT-base | 1 | 99 | 270 | 2.7 |
| mmBERT-base | 8 | 268 | 983 | 3.7 |

Tests: `tests/OneNlp.Tests/OnnxExportTests.cs` (the three fixture families: perturbed weights give the modules' vectors from the graph, unchanged weights give the original's, a trained table, hostile files, the paired pipeline file and its inline alternative). Fuzz target `onnx-patch` (`fuzz/README.md`).

## The distilled student

The default small encoder is distilled from mmBERT-base (MIT) by `tools/distill/`, with PyTorch as development tooling only. Nothing there ships; the result is one ONNX file, its `<name>.config.json` and mmBERT's `tokenizer.json`, treated by the library like any other exported encoder.

**Architecture** (`tools/distill/student.py`): six post-norm transformer layers of width 384 (12 heads, feed-forward 1536, exact GELU), learned absolute positions (512), and a factorised token embedding: a 256,000 x 96 table lifted to 384 by a linear map (Lan et al. 2020, section 3.1), so the vocabulary costs 24.6 M of the 35.5 M weights. It uses mmBERT's tokenizer unchanged, so token ids equal the teacher's for every text and no vocabulary is pruned.

**Objective** (`tools/distill/distill.py`): hidden-state distillation (Jiao et al. 2020, section 3.1, Eq. 8). Each student layer's output goes through its own learned linear map and is matched by mean squared error to a teacher layer: the uniform mapping (section 4.6) sends student layers 1 to 5 to teacher hidden states 4, 7, 11, 15 and 18, and the last layer to the teacher's final hidden state, standardised per feature, with a cosine term. Intermediate targets are layer-normalised per token because the teacher's residual stream has a few very large dimensions. The token table starts from the teacher's, reduced to 96 columns by principal components. Data: FineWeb-2 and Wikipedia text in 113 languages, sampled with language weights proportional to the square root of the amount of text, windows of 128, 256 and 512 pieces (`recipes/accurate/student-v1/PROVENANCE.md`).

**Export** (`tools/distill/export_student.py`): the same graph shape as the other exported encoders (inputs `input_ids`, `attention_mask`; output `last_hidden_state`; opset 17, attention as MatMul/Softmax), written from the exponential moving average of the weights. Every linear weight is a named initializer (`model.encoder.layer.N.attention.query.weight` and so on, stored `[in, out]`) and the head count is in `<name>.config.json`.

**Fine-tuning.** `TrainableTransformerEncoder` (family `PostNorm`) reads this layout as well as the PyTorch exporter's: when the graph has `embeddings.projection.weight` the table is factorised (`PostNormConfig.EmbeddingWidth` = 96, a `projection` linear layer), the weights are read by name, positions start at 0, and the head count comes from `Heads` or from the `.config.json` beside the model (a student graph without either is refused; a saved pipeline stores it). `tools/OneNlp.Accurate trainable-parity --family postnorm --position-offset 0` on the 200 parity texts (5,255 tokens): trainable modules against PyTorch max abs 2.6e-5 (largest value 4.4), graph runtime against PyTorch 2.4e-5, padded batches identical to single sequences.
