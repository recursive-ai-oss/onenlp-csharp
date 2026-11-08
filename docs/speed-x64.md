# x64 release speed rows

Measured 2026-11-08 on an AMD Ryzen 9 7950X3D (16 physical cores, 32 logical),
x64 Linux 7.0.0-38-generic, .NET SDK 10.0.403, runtime 10.0.14. Available ISA:
SSE4.2, AVX2, AVX-512F/BW/VL, FMA and BMI2. CPU inference only; no GPU used.
OneNLP 0.4.0-preview.1 source build, OneFont 1.4.0, OneModel and
OneModel.Training 1.0.0. Comparators: Microsoft.ML.Tokenizers 2.0.1,
SearchPioneer.Lingua 2.0.0, Microsoft.Recognizers.Text 1.8.13,
Catalyst 26.10.6514 with Catalyst.Models.English 26.9.5316.

Three exclusive benchmark-queue jobs, each bounded to 20 minutes, ran once after
prebuilding the tools. Actual timed-group wall times: tokenizers 2m23s,
language ID/recognizers 4m40s, Catalyst 1m16s (about 8m19s total).
No training, builds or other queued heavy work overlapped these jobs.
The methods are in [measurement.md](measurement.md) and the repository's bench
scripts. Values below are medians, not best-case samples.

| Group | Queue job | Load before (1/5/15 min) | Load during | Load after |
|---|---|---|---|---|
| Tokenizers | 007311 | 1.39 / 1.74 / 3.38 | 1-min samples 1.34–2.05, every 10 s | 1.74 / 1.79 / 3.17 |
| Language ID and recognizers | 007317 | 1.47 / 1.73 / 3.13 | Between operations: 1.47, 1.72, 1.66, 1.61 (1-min) | 1.71 / 1.68 / 2.74 |
| Catalyst and English model | 007321 | 2.85 / 2.04 / 2.80 | 1-min samples 1.78–2.85, every 10 s | 1.71 / 1.87 / 2.68 |

## Tokenizers

[Every cell](tokenizer-speed-x64.md) includes time, managed allocation per
operation and extra resident high-water growth during the samples. All **47
matched encode/count/decode rows pass** (1.13–48.55× the managed comparator).
Seven operation rows are not timed because the comparator produces different
IDs or decoded text: WordPiece medium decode and long encode/count/decode;
Unigram long encode/count/decode. They are not passes. Six load rows are
information only; WordPiece, TinyLlama, gpt2 and cl100k_base load more slowly.
Total process peak RSS was 400 MiB. Native HF timing was not requested here;
exactness against HF is reported separately in [parity.md](parity.md).

Each operation warms up for about 500 ms, then measures 5–25 batches of about
40 ms (1,000 ms sampling budget); the reported time is the median batch's
per-operation time. Allocation uses the thread counter. The six model families
use the pinned files in `tools/parity/families.json` and `Families.All` in the
benchmark. Short (59 characters), medium (513), and long (990,287 UTF-8 bytes)
inputs are byte-identical to the arm64 run:

| Input | SHA-256 |
|---|---|
| short.txt | b15e7e0dd880d275e4d21144fc05f857eb0670669a9d61a55188cb751cdb0ed9 |
| medium.txt | 8235943c7f5f0f8475095d4d99a5d5912eebe7bb7d8baae34133b9c8c986df85 |
| long.txt | 1f6178675374c4ba5ea26a16556b1a05f605e6333585d9fe881d7a6dd8bbd5ce |

## Language ID

Single-thread scoring, every 15th held-out main-set line per bucket. Warm-up
200 inputs, then three repetitions. Median microseconds and bytes per text:

| Bucket | Texts | Ours µs | Lingua high µs | Low µs | Speedup high / low | Allocation ours / high / low B | Verdict |
|---|---:|---:|---:|---:|---|---|---|
| word | 4,970 | 8.83 | 81.62 | 69.05 | 9.24× / 7.82× | 1,454 / 79,278 / 75,495 | pass |
| word pair | 4,872 | 3.58 | 96.29 | 65.58 | 26.90× / 18.32× | 1,467 / 84,283 / 80,446 | pass |
| sentence | 5,000 | 9.06 | 392.54 | 246.83 | 43.33× / 27.24× | 1,454 / 125,503 / 123,283 | pass |

Load: ours 12.6 ms, high 3,670.6 ms, low 282.0 ms. Process peak RSS: 95, 5,042,
and 360 MiB respectively. Our model is 3,588,959 bytes, SHA-256
`9d06cc77632083274ae5ca5fbb0f3e676abfc63aa6a2d702cbdf9d558cba9d6d`.
Lingua's model size was not separately measured (it is delivered by its package).
Input SHA-256:

- words: `c878bfb1403c9f70b4616771397674fe46d1dbbc6008b4cfb24a7e9e61d7a421`
- pairs: `f5123bbf28cf9423d3f179c208f3a427c8902fbb4150a29b93fbdaf1b6817473`
- sentences: `956a081df9d16a2d1b825b782857dd13e3bb0ac680250ef7deedee8c6aecd127`

## English recognizers

Same generated input lines and reference date for both systems (2026-03-18
10:30 UTC). Three repetitions; managed allocations below are the median total
per repetition, not bytes per expression. UTF-16 character throughput excludes
line terminators. Model size: data rules, no trained weights; rule byte size not
separately measured. Different result counts reflect the quality differences
reported in [results-table.md](results-table.md), not different input work.

| Rules | Lines / characters | Ours s | Comparator s | Speedup | Allocated MB ours / comparator | Peak RSS MiB ours / comparator | Load ms ours / comparator | Verdict |
|---|---|---:|---:|---:|---|---|---|---|
| Number/unit/currency/sequence | 11,557 / 9,980,266 | 0.542 | 26.140 | 48.23× | 402.3 / 8,794.7 | 113 / 344 | 30.2 / 356.5 | pass |
| All, including date/time | 2,889 / 2,492,392 | 0.621 | 21.948 | 35.34× | 615.7 / 4,877.2 | 103 / 325 | 47.7 / 642.4 | pass |

Date/time uses the first quarter of the full text to keep the group bounded;
both systems receive those same lines. Turkish and Hindi timing is not a gate.

## English fast tier against Catalyst

One thread, batch 1; UD English-EWT r2.18 test raw documents, repeated four times
as one input workload. Each tool makes 12 passes; discard the first two and take
the median of the remaining ten (internal samples, not repeated benchmark jobs).
Ours emits 100,524 tokens per pass, Catalyst 101,724, because tokenizers differ.

| Path | Ours tokens/s | Catalyst tokens/s | Ratio | Allocation B/token ours / Catalyst | Verdict |
|---|---:|---:|---:|---|---|
| Sentences + UPOS/UFeats + lemmas vs tagging | 833,483 | 1,049,762.5 | **0.794×** | 330 / 307 | **pass ≥0.5×** |
| With XPOS too vs tagging | 661,659.5 | 1,049,762.5 | 0.630× | 425 / 307 | information |
| Sentences + entities vs tagging + WikiNER | 443,997.5 | 471,738 | 0.941× | 322 / 699 | information, not matched work |
| Dependency parser, predicts sentences | 45,235.5 | — | — | 2,530 / — | no managed parser comparator |
| Full pipeline | 38,313 | — | — | 3,070 / — | information |

The tagging row counts under matched or better measured accuracy on raw EWT
(test F1/accuracy): Tokens 99.00 vs 94.47, Sentences 71.08 vs 64.57, UPOS 92.90 vs
80.70, Lemmas 95.03 vs 83.84. UNER character-span entity F1 is 63.09 vs 57.77.
The NER speed row is not a gate: Catalyst's NER needs its tagging pipeline,
whereas ours runs just the sentencizer and entity component.

Process peak RSS: ours 1,240 MiB (whole multi-path job); Catalyst 383 MiB
(316 MiB at the end of tagging alone). English model file: 117,451,750 bytes,
SHA-256 `ac0080adcb1a9832fe4c5c7b80e22a54da51bfcdac88e0d53986c80e6ce7ed9b`.
Cold English model load was not measured in this run. The quality rows are
unchanged; no model was retrained or selected using these timings.

## Accurate tiers: indicative, not a speed gate

A separate **heavy**, four-core-reservation job (007347), not an exclusive
benchmark, measured one English pipeline from each package. `TensorCompute.Threads`
is 1 and `DOTNET_PROCESSOR_COUNT` is 4. Other heavy jobs were running; load was
13.39 / 13.20 / 9.11 before, 12.62–13.77 (1-min) during, and
13.77 / 13.25 / 9.44 after. These figures must not be compared as publishable
speed gates or as a no-load regression measurement.

Input: the first 100 `# text` sentences of UD English-EWT r2.18 test, one per
line, 2,206 emitted word tokens including punctuation; SHA-256
`1d719e08e95c2d852234bcb59f284b0306a60a6358ac932b7adbf337e30fbaa6`.
Each throughput process warms up on 20 documents and takes the median of three
passes. Cold load uses a separate fresh process and the first sentence, with
an explicit English language (no language-detection work). Models are the exact
manifest-verified package payloads; no quantization or tensors were changed.

| Package | Installed weights | Median word tokens/s | Model load ms | First result including load ms | Second call ms | Peak RSS cold / throughput MiB |
|---|---:|---:|---:|---:|---:|---|
| `.Models.Multilingual` | 529,158,395 bytes | 462 | 3,512 | 3,992 | 151.8 | 809 / 814 |
| `.Models.Multilingual.Large` | 3,508,907,198 bytes | 150 | 3,134 | 3,406 | 54.1 | 3,904 / 4,055 |

Only English heads load. Allocation per token was not instrumented for this
information run. The quality tables and known published-reference gaps are
unchanged; these timings do not select a model or relax any quality bar.
