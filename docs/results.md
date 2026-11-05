# Results

Measured rows. **Every number on this page is indicative** until the publishable run replaces it.

## Tokenizer speed against Microsoft.ML.Tokenizers

Both libraries encode the same text to token IDs, count tokens and decode IDs. Before a row is timed the harness checks that the
IDs from both are identical on that input (and that both decode to the same text); a row whose outputs differ is shown as such and
not timed. Our correctness reference is Hugging Face `tokenizers` and `tiktoken` (see `parity.md`); where the comparator
differs from them that is information, not a failure of ours.

- **Speedup** is comparator time divided by our time; 1.00x or more passes. It is the median per operation (5 to 25 samples after
  a half-second warm-up).
- **Alloc** is bytes allocated per operation on the calling thread (the comparator's `EncodeToIds(string)` returns a list; ours
  writes into a caller's buffer, which is the call each library offers for the job).
- **Inputs** (from the Tatoeba sentence export): short = one English sentence (59 chars); medium = eight English sentences (513 chars);
  long = every 600th sentence of the export, 990,287 bytes of UTF-8 across all the languages it holds.
- **Operations:** `encode-to-ids` without special tokens; `count` = token count; `decode` of the IDs without special tokens.
- **Peak extra RSS** (growth of the resident set high-water mark during the samples) is in the raw table written by the harness; it
  is dominated by the garbage collector's timing and is not tabulated here.

### Machine and pins

- x64: AMD Ryzen 9 7950X3D (16 cores, 32 threads), Linux 7.0, .NET SDK 10.0.403 / runtime 10.0.x, Release, non-concurrent GC, ICU on.
  The host was shared: load average 10 to 16 before, during and after the run (indicative only). One thread.
- Comparator: Microsoft.ML.Tokenizers 2.0.1 with Microsoft.ML.Tokenizers.Data.{Gpt2,Cl100kBase,O200kBase} 2.0.1.
- Information column: `tokenizers` and `tiktoken` as pinned in `parity.md` (Python, one thread, same inputs).
- Run with `bench/scripts/tokenizer-rows.sh`.

### Families not yet timed

WordPiece (bert-base-uncased), Unigram (t5-small) and SentencePiece BPE with byte fallback (TinyLlama) are in the harness but
cannot be timed yet: their tokenizers do not load in this build (the `tokenizer.json` stage types they need, `BertNormalizer`,
`Precompiled` and `TemplateProcessing`, are not implemented here yet). Their rows are added when they do.

### Rows

| family | operation | size | ours | Microsoft.ML.Tokenizers | speedup | alloc ours | alloc comparator | native (info) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| Byte-level BPE (gpt2) | load | - | 58.09 ms | 57.53 ms | 0.99x | | | |
| Byte-level BPE (gpt2) | encode-to-ids | short | 559 ns | 820 ns | 1.47x | 408 B | 312 B | 11.94 µs |
| Byte-level BPE (gpt2) | count | short | 536 ns | 901 ns | 1.68x | 408 B | 96 B | 11.92 µs |
| Byte-level BPE (gpt2) | decode | short | 127 ns | 213 ns | 1.68x | 144 B | 176 B | 1.38 µs |
| Byte-level BPE (gpt2) | encode-to-ids | medium | 5.41 µs | 7.30 µs | 1.35x | 1.3 KB | 1.2 KB | 89.93 µs |
| Byte-level BPE (gpt2) | count | medium | 5.19 µs | 6.39 µs | 1.23x | 1.3 KB | 96 B | 88.29 µs |
| Byte-level BPE (gpt2) | decode | medium | 653 ns | 1.12 µs | 1.72x | 1.0 KB | 1.1 KB | 9.78 µs |
| Byte-level BPE (gpt2) | encode-to-ids | long | 55.42 ms | 121.49 ms | 2.19x | 8.8 MB | 14.0 MB | 475.57 ms |
| Byte-level BPE (gpt2) | count | long | 54.59 ms | 88.94 ms | 1.63x | 8.8 MB | 10.0 MB | 503.41 ms |
| Byte-level BPE (gpt2) | decode | long | 5.56 ms | 8.39 ms | 1.51x | 1.6 MB | 1.6 MB | 49.94 ms |
| Tiktoken cl100k_base | load | - | 23.16 ms | 22.48 ms | 0.97x | | | |
| Tiktoken cl100k_base | encode-to-ids | short | 559 ns | 755 ns | 1.35x | 408 B | 312 B | 2.10 µs |
| Tiktoken cl100k_base | count | short | 501 ns | 670 ns | 1.34x | 408 B | 96 B | 2.18 µs |
| Tiktoken cl100k_base | decode | short | 77 ns | 117 ns | 1.51x | 144 B | 176 B | 397 ns |
| Tiktoken cl100k_base | encode-to-ids | medium | 5.73 µs | 6.70 µs | 1.17x | 1.3 KB | 1.2 KB | 18.40 µs |
| Tiktoken cl100k_base | count | medium | 5.12 µs | 6.15 µs | 1.20x | 1.3 KB | 96 B | 18.82 µs |
| Tiktoken cl100k_base | decode | medium | 567 ns | 1.14 µs | 2.01x | 1.0 KB | 1.1 KB | 1.58 µs |
| Tiktoken cl100k_base | encode-to-ids | long | 58.40 ms | 105.24 ms | 1.80x | 8.8 MB | 12.5 MB | 82.23 ms |
| Tiktoken cl100k_base | count | long | 57.28 ms | 96.89 ms | 1.69x | 8.8 MB | 8.5 MB | 65.20 ms |
| Tiktoken cl100k_base | decode | long | 4.74 ms | 7.71 ms | 1.63x | 1.6 MB | 1.6 MB | 10.12 ms |
| Tiktoken o200k_base | load | - | 56.13 ms | 79.91 ms | 1.42x | | | |
| Tiktoken o200k_base | encode-to-ids | short | 666 ns | 862 ns | 1.29x | 408 B | 312 B | 1.39 µs |
| Tiktoken o200k_base | count | short | 698 ns | 761 ns | 1.09x | 408 B | 96 B | 1.32 µs |
| Tiktoken o200k_base | decode | short | 73 ns | 117 ns | 1.61x | 144 B | 176 B | 391 ns |
| Tiktoken o200k_base | encode-to-ids | medium | 6.67 µs | 7.30 µs | 1.09x | 1.3 KB | 1.2 KB | 10.09 µs |
| Tiktoken o200k_base | count | medium | 6.58 µs | 6.90 µs | 1.05x | 1.3 KB | 96 B | 10.25 µs |
| Tiktoken o200k_base | decode | medium | 540 ns | 996 ns | 1.84x | 1.0 KB | 1.1 KB | 1.64 µs |
| Tiktoken o200k_base | encode-to-ids | long | 65.87 ms | 108.32 ms | 1.64x | 8.8 MB | 10.2 MB | 55.19 ms |
| Tiktoken o200k_base | count | long | 67.75 ms | 107.30 ms | 1.58x | 8.8 MB | 6.2 MB | 57.05 ms |
| Tiktoken o200k_base | decode | long | 4.62 ms | 9.13 ms | 1.97x | 1.6 MB | 1.6 MB | 8.40 ms |

## Language-ID and recognizer speed

Single thread, one process per system and input set, after a warm-up over 200 inputs; three timed repetitions, and the table shows
the median repetition (the first repetition of every system is slower while the JIT tiers up, and the comparators' first
recognizer repetition is slower still). Run with `bench/scripts/langid-recognizer-rows.sh`; each machine ran alone in the benchmark
queue. These are publishable rows for the two components.

- **Speedup** is comparator time divided by ours; 1.00x or more passes. **Alloc** is bytes allocated on the calling thread. **Peak RSS**
  is the process's peak resident size (one process per system, so it includes that system's model load).
- **Language ID** inputs: every 15th line of the held-out `main` evaluation set per bucket (`tools/bench/make-langid-bench.sh`):
  4,970 words, 4,872 pairs, 5,000 sentences. Ours: the 75-language model (3.6 MB). Comparator: SearchPioneer.Lingua 2.0.0 built for the same
  75 languages with its models preloaded, in high and low accuracy mode. Accuracy rows are in `recipes/langid/results/`.
- **Recognizers** inputs: English text of 11,558 lines, 9,979,226 characters, built from the development split only
  (`tools/recognizers-gold/make_bench_text.py`). Row 1 compares the comparator's number, ordinal, percentage, currency, dimension,
  temperature, age, e-mail, URL, IP, phone, hashtag, mention and GUID models with our rules for the same types (the rule files without date/time). Row 2
  adds the date/time model on both sides; because the comparator runs it at about 0.1 M chars/s, that row uses the first quarter of
  the same text (2,889 lines, 2,492,312 characters) so a benchmark group fits its time limit. Both systems see the same lines; the
  comparator is Microsoft.Recognizers.Text 1.8.13 (en-US). The two systems return different numbers of results (conventions differ); this table
  compares cost, and the quality rows compare results.

### Machines and pins

| | x64 | arm64 |
|---|---|---|
| CPU | AMD Ryzen 9 7950X3D, 16 cores / 32 threads (avx2, avx512f, fma, bmi2) | Apple M2, 8 cores |
| OS, SDK | Linux 7.0, .NET SDK 10.0.403, Release | macOS, .NET SDK 10.0.403, Release |
| Load average (1/5/15 min) before | 1.22 / 2.48 / 6.93 | 1.81 / 1.82 / 1.81 |
| 1-minute load after the Lingua rows, after the first recognizer row, at the end | 1.5, 3.1, 4.8 | 2.3, 2.2, 2.3 |
| Run | 2026-11-05 | 2026-11-05 |

OneModel is not used by either component. Language-ID weights: `langid.bin`, sha256 `9d06cc77632083274ae5ca5fbb0f3e676abfc63aa6a2d702cbdf9d558cba9d6d`.
The runs queue behind other work, then hold the machine alone; the load averages above are the 1-minute values read between rows.

### Language ID (median repetition)

| bucket | system | x64 µs/text | x64 texts/s | x64 speedup | arm64 µs/text | arm64 texts/s | arm64 speedup | alloc per text |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| words | ours | 7.88 | 126,970 | | 7.96 | 125,657 | | 1.4 KB |
| words | Lingua high | 76.28 | 13,110 | 9.7x | 84.37 | 11,853 | 10.6x | 79 KB |
| words | Lingua low | 68.79 | 14,536 | 8.7x | 60.72 | 16,468 | 7.6x | 75 KB |
| pairs | ours | 5.05 | 198,104 | | 8.08 | 123,636 | | 1.4 KB |
| pairs | Lingua high | 90.87 | 11,005 | 18.0x | 85.99 | 11,629 | 10.6x | 84 KB |
| pairs | Lingua low | 64.42 | 15,524 | 12.8x | 55.67 | 17,964 | 6.9x | 80 KB |
| sentences | ours | 9.06 | 110,422 | | 10.75 | 93,058 | | 1.4 KB |
| sentences | Lingua high | 375.10 | 2,666 | 41.4x | 294.61 | 3,394 | 27.4x | 126 KB |
| sentences | Lingua low | 250.14 | 3,998 | 27.6x | 212.59 | 4,704 | 19.8x | 123 KB |

Our repetitions of 15 to 50 ms vary more than the comparator's (x64 pairs: 8.3, 5.1 and 3.0 µs/text as the JIT tiers up); the median is shown and the speedup
is at least 7.6x on every row either way.

| | ours | Lingua high | Lingua low |
|---|---:|---:|---:|
| Load, x64 / arm64 | 10.7 ms / 8.2 ms | 3,850 ms / 4,238 ms | 326 ms / 321 ms |
| Peak RSS, x64 / arm64 | 95 MB / 65 MB | 4,791 MB / 2,747 MB | 367 MB / 253 MB |
| Model on disk | 3.6 MB | n/a (embedded in the package) | |

### Recognizers (median repetition)

| row | system | x64 time | x64 M chars/s | x64 speedup | arm64 time | arm64 M chars/s | arm64 speedup | alloc | peak RSS x64 / arm64 | load x64 / arm64 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| number, unit, currency, sequence (10.0 M chars) | ours | 0.497 s | 20.08 | | 0.559 s | 17.86 | | 387 MB | 113 / 84 MB | 21 / 18 ms |
| number, unit, currency, sequence (10.0 M chars) | Recognizers.Text 1.8.13 | 27.12 s | 0.37 | 54.6x | 32.18 s | 0.31 | 57.6x | 8,795 MB (x64), 8,827 MB (arm64) | 345 / 299 MB | 266 / 259 ms |
| with date/time (2.5 M chars) | ours | 0.620 s | 4.02 | | 0.558 s | 4.47 | | 556 MB | 101 / 67 MB | 39 / 31 ms |
| with date/time (2.5 M chars) | Recognizers.Text 1.8.13 | 27.16 s | 0.09 | 43.8x | 28.18 s | 0.09 | 50.5x | 5,024 MB (x64), 5,026 MB (arm64) | 334 / 303 MB | 630 / 424 ms |

Pass/fail: every row is at or faster than its managed comparator, on both machines.
