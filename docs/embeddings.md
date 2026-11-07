# Sentence embeddings

`IEmbedder` (core) turns text into a fixed-length vector; `SentenceEmbedder` (`RecursiveAI.OneNlp.Neural`) runs a sentence-transformers-format model on OneModel. `Nlp.Embed` is the happy path over the default model in `RecursiveAI.OneNlp.Models.Embeddings`.

```csharp
float[] v = Nlp.Embed("how do I reset my password");          // needs RecursiveAI.OneNlp.Models.Embeddings
using SentenceEmbedder e = SentenceEmbedder.Load("my-model-dir");   // any sentence-transformers export
float[][] batch = e.EmbedBatch(texts);                              // sorted by length internally, results in input order
foreach (float[] x in e.EmbedMany(lazyTexts)) { }                   // streaming, in batches of e.BatchSize
```

## Multilingual: `RecursiveAI.OneNlp.Models.Embeddings.Multilingual`

`intfloat/multilingual-e5-small` (MIT, about 100 languages, 384 dimensions, mean pooling). Its ONNX file is 470 MB, over the NuGet size limit, so it is cut into three equal byte ranges carried by `...Embeddings.Multilingual.Shard1`, `.Shard2` and `.Shard3`; the thin package (tokenizer, configuration, registration, the API below) depends on all three. The ranges are joined in memory by a read-only seekable stream and handed to the loader, with no temporary file and no joined copy on disk (`tools/embeddings/make_shards.sh` cuts the file and prints the manifest rows).

- **Default for `Nlp.Embed`.** The multilingual package registers with a higher priority than the English one, so when it is referenced `Nlp.Embed` uses it, also when `RecursiveAI.OneNlp.Models.Embeddings` (bge) is referenced too. To use bge anyway, call `EmbeddingsModel.Load()` from the English package and keep the embedder.
- **Prefixes.** e5 models were trained with `query: ` in front of a search query (and of both texts of a similarity task) and `passage: ` in front of a passage. `Nlp.Embed` and `E5Embedder.Embed` use `query: `, which is right for similarity. For retrieval, `MultilingualEmbeddingsModel.Load()` returns an `E5Embedder` with `EmbedQuery`, `EmbedPassage` and their batch forms `EmbedQueries`, `EmbedPassages`. The helpers live on the model, not on `Nlp`.
- **Checks.** Parity with sentence-transformers on the joined shards: see the table below. The consumer application (JIT and NativeAOT) works with the multilingual package alone and with both packages.

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
| multilingual-e5-small, through the shipped package (three shards joined in memory) | 2,103 | 0 | 3.2e-7 | 0.99999988 | 1.0000000 |

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
- **Published numbers:** STS, pair classification, clustering, retrieval and (for bge) reranking reproduce within 0.5. The bold rows do not, **and they do not for sentence-transformers either**. See the next section for what was tried.
- **int8** is smaller (bge 133 MB to 34 MB) and about as fast; quality drops 0.3 to 3.8 points depending on the task (clustering and retrieval most). It is information only; fp32 is what ships.

### Card-era harness (mteb 1.x), for the cells that miss

The cells that miss under mteb 2.27.1 were re-run, ours and sentence-transformers 6.2.0 side by side, under two 1.x releases of `mteb` (each in its own venv; `tools/embeddings/mteb1_run.py`, `mteb1_chunk.sh`): 1.1.1 (the first release with these tasks, the closest to the era of the cards) and 1.12.94 (the last 1.12 release), both with datasets 3.2.0 and scikit-learn 1.9.1. The cards state no mteb version, so "the version that produced the card" is not knowable from the card; the datasets' revisions in the cards equal the ones these releases use for Banking77. Per-subset scores are in the result files. Under 1.1.1 STS22 and MTOPDomain would not load (the pinned dataset revisions are loader scripts, which were not run); they were run under 1.12.94 only.

| Model and cell | Published | ST 2.27.1 | Ours 2.27.1 | ST 1.1.1 | Ours 1.1.1 | ST 1.12.94 | Ours 1.12.94 | Ours (1.12.94) − published |
|---|---|---|---|---|---|---|---|---|
| bge Banking77Classification | 85.74 | 81.75 | 81.75 | 81.75 | 81.75 | 81.75 | 81.75 | **−3.99** |
| e5 Banking77Classification | 79.42 | 70.44 | 70.44 | 70.44 | 70.44 | 70.44 | 70.44 | **−8.97** |
| e5 AskUbuntuDupQuestions | 57.88 | 56.42 | 56.42 | 56.42 | 56.42 | 56.42 | 56.42 | **−1.46** |
| e5 STS22 (12 subsets) | 66.01 | 65.13 | 65.13 | not loadable | not loadable | 66.10 | 66.10 | +0.09 |
| e5 MTOPDomainClassification (en, de, es, fr) | 88.01 | 85.59 | 85.59 | not loadable | not loadable | 85.54 | 85.54 | **−2.47** |

- **STS22 now reproduces** (66.10 against 66.01): the per-subset scores agree with the card to about 0.1 on most subsets; the 2.x difference came from the harness. **Ours equals sentence-transformers** on every cell and release (STS22 differs by 7e-5 in the mean, from one subset).
- **The other four cells do not move with the mteb release** (identical to four decimals across 1.1.1, 1.12.94 and 2.27.1 for the three classification and reranking cells). The harness release is therefore not the cause of the gap, and these cells stay below the card for sentence-transformers itself. MTOPDomain per subset, ours (ST identical): en 88.99, de 86.15, es 85.53, fr 81.50 against the card's 91.07, 87.47, 89.27, 84.23. Not investigated further: the card's numbers cannot be reproduced from the published model with the released harness, whoever computes the vectors.

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

`tools/embeddings/`: `setup_venv.sh` (sentence-transformers, mteb, ONNX Runtime), `fetch_model.py` and `tools/accurate/export_onnx.py` (pinned revision to ONNX), `parity.sh`, `make_shards.sh`, `mteb_chunk.sh`, `mteb1_chunk.sh` (venvs: mteb 1.1.1 or 1.12.94 with `pyarrow==18.1.0 datasets==3.2.0`), `speed_ort.py`, `quantize_int8.py`, and the `OneNlp.EmbedTool` console project that embeds a text file or serves vectors over a pipe.
