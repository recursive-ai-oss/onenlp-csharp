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

The graph runtime and the autodiff tensor layer are separate: `Session.Run` returns tensors with `RequiresGrad == false`, so an imported graph cannot be backpropagated. What does work for BERT-style models is moving the weights into OneModel.Training's own modules. `tools/OneNlp.Accurate` (`finetune-probe`) builds `Embedding` + `LayerNorm` + 12 `TransformerEncoderLayer` for XLM-R from the ONNX initializers (the bias and norm initializers keep their PyTorch names; each linear layer's weight is the unnamed initializer of the `MatMul` node with the matching path), and measures:

- output of the modules against the graph runtime: max abs difference 1.4e-5;
- five AdamW steps (learning rate 2e-5) on one 26-token sequence with an eight-way linear head: loss 2.392, 2.287, 2.209, 2.091, 1.947, nonzero gradients in layer 0, 2.1 to 2.7 s per step on 8 threads (277,057,536 parameters).

mmBERT (ModernBERT: pre-norm, bias-free norms, rotary positions, gated-GELU feed-forward, alternating local and global attention) has no module in `OneModel.Training.Layers`; the tensor ops it needs exist. It was not built. What it needs: a pre-norm encoder layer, bias-free norm and linear layers, rotary position embedding, a gated-GELU feed-forward block, and sliding-window masks.
