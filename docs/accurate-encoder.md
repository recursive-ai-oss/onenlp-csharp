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
| `transformer-trainable` | `TrainableTransformerEncoder` | yes | the layers (not the frozen token table) |

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
