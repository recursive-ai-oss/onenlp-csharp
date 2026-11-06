# Sentence embeddings

`IEmbedder` (core) turns text into a fixed-length vector; `SentenceEmbedder` (`RecursiveAI.OneNlp.Neural`) runs a sentence-transformers-format model on OneModel. `Nlp.Embed` is the happy path over the default model in `RecursiveAI.OneNlp.Models.Embeddings`.

```csharp
float[] v = Nlp.Embed("how do I reset my password");          // needs RecursiveAI.OneNlp.Models.Embeddings
using SentenceEmbedder e = SentenceEmbedder.Load("my-model-dir");   // any sentence-transformers export
float[][] batch = e.EmbedBatch(texts);                              // sorted by length internally, results in input order
foreach (float[] x in e.EmbedMany(lazyTexts)) { }                   // streaming, in batches of e.BatchSize
```

## Model directory

`SentenceEmbedder.Load(dir, modelFile = "model.onnx", prefix = "")` reads, as data, what sentence-transformers publishes:

| File | Used for |
|---|---|
| `model.onnx` | the encoder: inputs `input_ids`, `attention_mask` (and `token_type_ids`, fed zeros), first output `[batch, sequence, width]` |
| `tokenizer.json` | the tokenizer, with its post-processor's special tokens |
| `modules.json` | the module chain. Supported: Transformer, Pooling, Normalize. Anything else (a Dense projection, say) throws `NotSupportedException` naming the module |
| `1_Pooling/config.json` | exactly one of CLS, mean, max, mean-sqrt-length. Several at once (concatenation) is not supported |
| `sentence_bert_config.json` | `max_seq_length` (2 to 65,536; default 512 when absent) |

All files are bounded (1 MiB each for the JSON files), the pooling folder must lie inside the model directory, and a damaged file throws `InvalidDataException`. Export a PyTorch model to ONNX with `tools/accurate/export_onnx.py` (first output = last hidden states). `Load(Stream, Stream, EmbeddingConfig)` takes weights from memory or an embedded resource.

## Behaviour

- **Pooling** follows Reimers and Gurevych 2019 (Sentence-BERT) §3 over the tokens the attention mask keeps. **Normalization** scales to unit L2 length (a floor of 1e-12 on the norm) when `modules.json` has a Normalize module.
- **Truncation:** inputs are cut to `max_seq_length` tokens including the special tokens, keeping the closing special token. Longer text is never an error.
- **Prefix:** `prefix` is put in front of every input (E5-family models need `query: ` or `passage: `). The default is none, which is what sentence-transformers does.
- **Batching:** a batch is sorted longest first and run in groups of `BatchSize` (default 32); padding never changes a text's vector beyond float rounding. Calls are serialized per embedder.
- **int8 (opt-in, never the default):** pass an int8-quantized ONNX file of the same model as `modelFile`. `tools/embeddings/quantize_int8.py` makes one with ONNX Runtime's dynamic quantization. OneModel runs the integer operators exactly. Deltas are below.

### SentencePiece models and white space

For models whose tokenizer has a lone `▁` piece (XLM-R and other SentencePiece Unigram vocabularies) sentence-transformers 6.2.0 (transformers 5.22.1) does not give the same pieces as the bare `tokenizer.json` pipeline in a few white-space cases. Observed in its output with `multilingual-e5-small` and reproduced here:

1. Characters the normalizer maps to a space are collapsed to one space (`a<U+2003><U+2003>b` is `a b`).
2. White space at the end of the text, or before a special token written in the text (`<s>`, `</s>`, `<pad>`, `<unk>`), gives no `▁` piece. White space after a special token is kept.
3. U+0085 (next line) acts as a space.

`SentenceEmbedder` applies these three rules for such tokenizers; WordPiece models are unaffected. Without them 91 of 2,103 parity texts differed in token ids (all texts containing trailing or exotic white space or special-token look-alikes); with them none do.

## Parity with sentence-transformers

Reference: sentence-transformers 6.2.0, torch 2.15.0+cpu, CPU, float32. Inputs: 2,103 texts from the multilingual parity corpus (all scripts of the Universal Declaration of Human Rights, Tatoeba sentences, and the synthetic classes: CJK, emoji and ZWJ, combining marks, whitespace runs, special-token look-alikes, URLs and code; 62 to 65 of them longer than 512 tokens). Selection is deterministic (`tools/embeddings/parity_reference.py`, seed in the script). Token ids come from `SentenceTransformer.tokenize`.

| Model (revision) | Texts | Token-id mismatches | Max abs diff | Min cosine | Mean cosine |
|---|---|---|---|---|---|
| bge-small-en-v1.5 (5c38ec7c) | 2,103 | 0 | 4.0e-7 | 0.99999988 | 1.0000000 |
| multilingual-e5-small (614241f6), `query: ` prefix | 2,103 | 0 | 3.2e-7 | 0.99999988 | 1.0000000 |

The vectors match to float tolerance (differences are float32 summation order). int8 against the float reference, for information: bge-small-en-v1.5 max abs diff 0.124, min cosine 0.848, mean cosine 0.966 on this deliberately hostile set.

## MTEB

Harness: `mteb` 2.27.1, run black-box through `mteb.evaluate` with a small custom model class (`tools/embeddings/mteb_run.py`); the same class wraps sentence-transformers for the same-harness reference, so the two columns differ only in who computes the vectors. Scores are main scores x 100. "Published" is the model card's `model-index` (the metric each task type uses as its main score; for the multilingual tasks the mean over the subsets that were run). Prompts: bge-small-en-v1.5 uses its retrieval query instruction on SciFact queries and nothing elsewhere; multilingual-e5-small uses `query: ` for queries and symmetric tasks and `passage: ` for documents. Multilingual runs cover STS22 in en, de, es, fr, it, tr, zh and the cross-lingual pairs among them, and MTOPDomain in en, de, es, fr.

### bge-small-en-v1.5

| Task | Type | Published | sentence-transformers (same harness) | Ours | Ours − ST | Ours − published | int8 | int8 − ours |
|---|---|---|---|---|---|---|---|---|
| STSBenchmark | STS | 85.86 | 85.86 | 85.86 | 0.00 | 0.00 | 85.53 | −0.33 |
| Banking77Classification | Classification | 85.74 | 81.75 | 81.75 | 0.00 | **−3.99** | 80.71 | −1.04 |
| TwentyNewsgroupsClustering | Clustering | 48.52 | 48.63 | 48.63 | 0.00 | +0.10 | 45.72 | −2.91 |
| TwitterSemEval2015 | Pair classification | 73.33 | 73.24 | 73.24 | 0.00 | −0.09 | 71.59 | −1.65 |
| AskUbuntuDupQuestions | Reranking | 62.59 | 62.58 | 62.58 | 0.00 | 0.00 | 61.71 | −0.87 |
| SciFact | Retrieval | 71.28 | 71.27 | 71.27 | 0.00 | 0.00 | 69.65 | −1.62 |

### multilingual-e5-small

| Task | Type | Published | sentence-transformers (same harness) | Ours | Ours − ST | Ours − published | int8 | int8 − ours |
|---|---|---|---|---|---|---|---|---|
| STSBenchmark | STS | 84.01 | 84.11 | 84.11 | 0.00 | +0.10 | 83.64 | −0.47 |
| STS22 (multilingual) | STS | 66.01 | 65.13 | 65.13 | 0.00 | −0.88 | 63.93 | −1.20 |
| Banking77Classification | Classification | 79.42 | 70.44 | 70.44 | 0.00 | **−8.97** | 69.72 | −0.72 |
| MTOPDomainClassification (en, de, es, fr) | Classification | 88.01 | 85.59 | 85.59 | 0.00 | **−2.42** | 83.94 | −1.65 |
| TwentyNewsgroupsClustering | Clustering | 33.22 | 32.81 | 32.81 | 0.00 | −0.41 | 31.67 | −1.14 |
| TwitterSemEval2015 | Pair classification | 70.95 | 70.75 | 70.75 | 0.00 | −0.20 | 69.33 | −1.42 |
| AskUbuntuDupQuestions | Reranking | 57.88 | 56.42 | 56.42 | 0.00 | **−1.46** | 55.25 | −1.18 |
| SciFact | Retrieval | 67.70 | 67.70 | 67.70 | 0.00 | 0.00 | 63.86 | −3.84 |

Reading the tables:

- **Ours equals sentence-transformers** in the same harness on every task to two decimals (the vectors agree to 1e-7, so the scores cannot differ).
- **Published numbers:** STS, pair classification, clustering, retrieval and (for bge) reranking reproduce within 0.5. The bold rows do not, **and they do not for sentence-transformers either**. Banking77Classification, MTOPDomainClassification and AskUbuntuDupQuestions (e5) are where `mteb` 2.27.1 scores the same vectors differently from the older release that produced the cards: running mteb's own implementation of multilingual-e5-small (revision fd1525a9, its per-model prompts) gives 70.64 on Banking77Classification and 56.42 on AskUbuntuDupQuestions, as our wrapper does, and running bge through mteb's own sentence-transformers path gives 81.75 on Banking77 (81.76 on its `.v2`). The gap is the benchmark version, not the vectors.
- **int8** is smaller (bge 133 MB to 34 MB) and about as fast; quality drops 0.3 to 3.8 points depending on the task (clustering and retrieval most). It is information only; fp32 is what ships.

## Speed (indicative)

One run on the shared worker (AMD Ryzen 9 7950X3D, 4 threads for both sides, load average about 35 to 41 during the run, so treat as a rough guide), 204 English sentences of 30 to 250 characters, tokenization included, the same exported model. Reference: ONNX Runtime 1.31.1 with `tokenizers` and numpy pooling. There is no managed comparator, so this row passes with that reason recorded; it is not a gate.

| Model | Batch | Ours (texts/s, two runs) | ONNX Runtime (texts/s) |
|---|---|---|---|
| bge-small-en-v1.5 | 1 | 81, 82 | 100 |
| bge-small-en-v1.5 | 32 | 119, 111 | 76 |
| multilingual-e5-small | 1 | 59, 82 | 112 |
| multilingual-e5-small | 32 | 83, 80 | 81 |
| bge-small-en-v1.5 int8 | 32 | 121 | |

## Reproducing

`tools/embeddings/`: `setup_venv.sh` (sentence-transformers, mteb, ONNX Runtime), `fetch_model.py` and `tools/accurate/export_onnx.py` (pinned revision to ONNX), `parity.sh`, `mteb_chunk.sh`, `speed_ort.py`, `quantize_int8.py`, and the `OneNlp.EmbedTool` console project that embeds a text file or serves vectors over a pipe.
