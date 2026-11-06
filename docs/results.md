# Results

Measured rows. The tokenizer speed rows below are final: they were taken in one benchmark-queue group per machine, alone on the
machine, with `bench/scripts/tokenizer-rows.sh`. Nothing else on this page is a final number yet.

## Tokenizer speed against Microsoft.ML.Tokenizers

Both libraries encode the same text to token IDs, count tokens and decode IDs, one thread. Before a row is timed the harness checks
that the IDs from both are identical on that input (and that both decode to the same text); a row whose outputs differ is shown as
such and not timed. Our correctness reference is Hugging Face `tokenizers` and `tiktoken` (see `parity.md`); where the comparator
differs from them that is information, not a failure of ours.

- **Gate:** ours at or faster than the managed reference on every timed row. **Result: all 47 timed rows on x64 and all 47 on arm64 pass** (speedup 1.00x or more). Load time is reported, not gated.
- **Speedup** is comparator time divided by ours: the median per operation (5 to 25 samples after a half-second warm-up).
- **Alloc** is bytes allocated per operation on the calling thread. Ours writes into a caller's buffer (`EncodeToIds(text, ids)`); the comparator returns a list (`EncodeToIds(string)`), which is the call each library offers for the job. Decode allocates the result string in both.
- **Inputs** (from the Tatoeba sentence export, fixed by the first run and reused on the second machine): short = one English sentence (59 chars); medium = eight English sentences (513 chars); long = every 600th sentence of the export, 990,287 bytes of UTF-8 across all the languages it holds.
- **Operations:** `encode-to-ids` without special tokens; `count` = token count; `decode` of the IDs without special tokens.
- **Families and files:** WordPiece = bert-base-uncased (`tokenizer.json`; the comparator reads `vocab.txt` with lowercasing and accent stripping on, as the Hugging Face file has them); Unigram = xlm-roberta-base (`tokenizer.json`; the comparator reads `sentencepiece.bpe.model`); SentencePiece BPE with byte fallback = TinyLlama (`tokenizer.json`); byte-level BPE = gpt2 (`tokenizer.json`); Tiktoken = `cl100k_base` and `o200k_base` (`.tiktoken` files, comparator `TiktokenTokenizer.CreateForModel`).
- **Peak extra RSS** (growth of the resident-set high-water mark during the samples) is in the harness output (`rows.md`); it follows the garbage collector's timing and is not tabulated here. It is only measured on Linux.

### Rows not timed, and why

- **WordPiece, medium decode:** the comparator's decoded text differs from ours at character 74 (lengths 525 and 519); it does not apply the Hugging Face `cleanup` rules to spaces before punctuation and contractions. Ours matches Hugging Face (see `parity.md`).
- **WordPiece, long (encode, count, decode):** the IDs differ from the first difference at token 159 (ours 1456, the comparator 100, its unknown token): the comparator gives the unknown token for text that the Hugging Face tokenizer, and ours, split into pieces. 357,483 tokens (ours) against 356,166.
- **Unigram, long (encode, count, decode):** the IDs differ at token 30,076 (ours 49,729, the comparator 1,759); 250,378 tokens against 251,115. On short and medium the IDs are identical once the comparator's numbering is mapped to the Hugging Face file's: the comparator numbers the pieces of `sentencepiece.bpe.model`, and the Hugging Face file puts four control tokens first, so a piece ID p is token p + 1 (the three control pieces swap places).
- **t5-small** is not a Unigram row: the comparator throws `IndexOutOfRangeException` while building its Unigram model from t5-small's `spiece.model`.

### Machines and pins

- **x64:** AMD Ryzen 9 7950X3D (16 cores, 32 threads), Linux 7.0.0-38-generic, .NET SDK 10.0.403, Release, non-concurrent GC, ICU on. Run on 2026-11-05 at 03:27 UTC. Load average before the run 3.6 (1 min), 15.5 (5 min), 17.4 (15 min), the tail of an earlier job; during the run (10 s samples) it fell from 3.6 to about 1.5; the machine was otherwise idle (the queue checked that the CPU was idle before starting).
- **arm64:** Apple M2 (8 cores), macOS (Darwin 27.0.0), .NET SDK 10.0.403 (user-local install), Release, non-concurrent GC, ICU on. Run on 2026-11-05 at 04:40 UTC. Load average before the run 2.7 / 3.9 / 12.4; during the run 2.4 to 3.3 (the harness's own thread and the OS).
- **Versions:** Microsoft.ML.Tokenizers 2.0.1 with Microsoft.ML.Tokenizers.Data.{Gpt2, Cl100kBase, O200kBase} 2.0.1; OneNLP at the commit of this page; information column: `tokenizers` 0.22.1 and `tiktoken` 0.14.0 (Python, one thread, x64 only).
- **Tokenizer files:** the Hugging Face model files named in `parity.md`; the `.tiktoken` files from the published encodings.
- **Run with** `bench/scripts/tokenizer-rows.sh MODELS INPUTS OUT`; `INPUTS` is the Tatoeba export or a directory with `short.txt`, `medium.txt` and `long.txt`.

### Rows

Times are medians per operation. `native` is the information column: the Hugging Face `tokenizers` / `tiktoken` Python package on the x64 machine, same text.

#### x64

| family | operation | size | ours | Microsoft.ML.Tokenizers | speedup | alloc ours | alloc comparator | native (info, x64) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| WordPiece (bert-base-uncased) | load | - | 31.88 ms | 19.66 ms | 0.62x | | | |
| WordPiece (bert-base-uncased) | encode-to-ids | short | 281 ns | 873 ns | 3.10x | 0 B | 456 B | 15.00 µs |
| WordPiece (bert-base-uncased) | count | short | 283 ns | 835 ns | 2.96x | 0 B | 240 B | 14.82 µs |
| WordPiece (bert-base-uncased) | decode | short | 70 ns | 161 ns | 2.31x | 144 B | 176 B | 5.52 µs |
| WordPiece (bert-base-uncased) | encode-to-ids | medium | 2.85 µs | 7.81 µs | 2.74x | 0 B | 2.3 KB | 108.94 µs |
| WordPiece (bert-base-uncased) | count | medium | 2.80 µs | 7.51 µs | 2.68x | 0 B | 1.1 KB | 108.24 µs |
| WordPiece (bert-base-uncased) | decode | medium | not timed | not timed | - | | | |
| WordPiece (bert-base-uncased) | encode-to-ids | long | not timed | not timed | - | | | |
| WordPiece (bert-base-uncased) | count | long | not timed | not timed | - | | | |
| WordPiece (bert-base-uncased) | decode | long | not timed | not timed | - | | | |
| Unigram (xlm-roberta-base) | load | - | 226.30 ms | 705.90 ms | 3.12x | | | |
| Unigram (xlm-roberta-base) | encode-to-ids | short | 199 ns | 1.35 µs | 6.79x | 0 B | 216 B | 12.97 µs |
| Unigram (xlm-roberta-base) | count | short | 200 ns | 1.43 µs | 7.14x | 0 B | 0 B | 13.25 µs |
| Unigram (xlm-roberta-base) | decode | short | 69 ns | 146 ns | 2.11x | 144 B | 176 B | 1.02 µs |
| Unigram (xlm-roberta-base) | encode-to-ids | medium | 1.67 µs | 11.31 µs | 6.76x | 0 B | 2.2 KB | 106.73 µs |
| Unigram (xlm-roberta-base) | count | medium | 1.59 µs | 12.12 µs | 7.63x | 0 B | 0 B | 104.94 µs |
| Unigram (xlm-roberta-base) | decode | medium | 651 ns | 1.38 µs | 2.13x | 1.0 KB | 1.1 KB | 10.47 µs |
| Unigram (xlm-roberta-base) | encode-to-ids | long | not timed | not timed | - | | | |
| Unigram (xlm-roberta-base) | count | long | not timed | not timed | - | | | |
| Unigram (xlm-roberta-base) | decode | long | not timed | not timed | - | | | |
| SentencePiece BPE, byte fallback (TinyLlama) | load | - | 48.44 ms | 10.91 ms | 0.23x | | | |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | short | 89 ns | 4.14 µs | 46.76x | 0 B | 1.6 KB | 7.37 µs |
| SentencePiece BPE, byte fallback (TinyLlama) | count | short | 88 ns | 4.11 µs | 46.67x | 0 B | 1.4 KB | 7.48 µs |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | short | 83 ns | 148 ns | 1.78x | 144 B | 176 B | 2.97 µs |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | medium | 12.05 µs | 42.03 µs | 3.49x | 0 B | 12.3 KB | 77.08 µs |
| SentencePiece BPE, byte fallback (TinyLlama) | count | medium | 12.28 µs | 42.40 µs | 3.45x | 0 B | 11.1 KB | 78.32 µs |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | medium | 671 ns | 1.52 µs | 2.27x | 1.0 KB | 1.1 KB | 24.45 µs |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | long | 86.57 ms | 243.17 ms | 2.81x | 1.8 MB | 21.5 MB | 359.33 ms |
| SentencePiece BPE, byte fallback (TinyLlama) | count | long | 88.74 ms | 241.62 ms | 2.72x | 1.8 MB | 17.5 MB | 358.47 ms |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | long | 4.48 ms | 6.70 ms | 1.49x | 1.6 MB | 1.6 MB | 71.05 ms |
| Byte-level BPE (gpt2) | load | - | 20.22 ms | 26.44 ms | 1.31x | | | |
| Byte-level BPE (gpt2) | encode-to-ids | short | 438 ns | 677 ns | 1.55x | 0 B | 312 B | 11.05 µs |
| Byte-level BPE (gpt2) | count | short | 441 ns | 633 ns | 1.43x | 0 B | 96 B | 11.08 µs |
| Byte-level BPE (gpt2) | decode | short | 67 ns | 113 ns | 1.70x | 144 B | 176 B | 1.23 µs |
| Byte-level BPE (gpt2) | encode-to-ids | medium | 3.99 µs | 6.21 µs | 1.56x | 0 B | 1.2 KB | 86.19 µs |
| Byte-level BPE (gpt2) | count | medium | 3.93 µs | 5.68 µs | 1.45x | 0 B | 96 B | 83.95 µs |
| Byte-level BPE (gpt2) | decode | medium | 450 ns | 978 ns | 2.17x | 1.0 KB | 1.1 KB | 9.14 µs |
| Byte-level BPE (gpt2) | encode-to-ids | long | 28.97 ms | 63.26 ms | 2.18x | 0 B | 14.0 MB | 382.68 ms |
| Byte-level BPE (gpt2) | count | long | 28.90 ms | 61.19 ms | 2.12x | 0 B | 10.0 MB | 380.77 ms |
| Byte-level BPE (gpt2) | decode | long | 4.59 ms | 6.78 ms | 1.48x | 1.6 MB | 1.6 MB | 43.94 ms |
| Tiktoken cl100k_base | load | - | 18.31 ms | 19.22 ms | 1.05x | | | |
| Tiktoken cl100k_base | encode-to-ids | short | 475 ns | 704 ns | 1.48x | 408 B | 312 B | 2.07 µs |
| Tiktoken cl100k_base | count | short | 520 ns | 644 ns | 1.24x | 408 B | 96 B | 2.03 µs |
| Tiktoken cl100k_base | decode | short | 67 ns | 111 ns | 1.65x | 144 B | 176 B | 364 ns |
| Tiktoken cl100k_base | encode-to-ids | medium | 5.02 µs | 6.32 µs | 1.26x | 1.3 KB | 1.2 KB | 16.57 µs |
| Tiktoken cl100k_base | count | medium | 5.15 µs | 6.05 µs | 1.17x | 1.3 KB | 96 B | 16.66 µs |
| Tiktoken cl100k_base | decode | medium | 508 ns | 1.01 µs | 1.99x | 1.0 KB | 1.1 KB | 1.49 µs |
| Tiktoken cl100k_base | encode-to-ids | long | 47.85 ms | 65.20 ms | 1.36x | 8.8 MB | 12.5 MB | 51.54 ms |
| Tiktoken cl100k_base | count | long | 47.77 ms | 63.95 ms | 1.34x | 8.8 MB | 8.5 MB | 52.25 ms |
| Tiktoken cl100k_base | decode | long | 4.47 ms | 6.07 ms | 1.36x | 1.6 MB | 1.6 MB | 7.22 ms |
| Tiktoken o200k_base | load | - | 27.60 ms | 43.85 ms | 1.59x | | | |
| Tiktoken o200k_base | encode-to-ids | short | 564 ns | 793 ns | 1.41x | 408 B | 312 B | 1.21 µs |
| Tiktoken o200k_base | count | short | 555 ns | 740 ns | 1.33x | 408 B | 96 B | 1.22 µs |
| Tiktoken o200k_base | decode | short | 71 ns | 111 ns | 1.56x | 144 B | 176 B | 386 ns |
| Tiktoken o200k_base | encode-to-ids | medium | 5.25 µs | 6.83 µs | 1.30x | 1.3 KB | 1.2 KB | 9.38 µs |
| Tiktoken o200k_base | count | medium | 5.26 µs | 6.48 µs | 1.23x | 1.3 KB | 96 B | 9.45 µs |
| Tiktoken o200k_base | decode | medium | 519 ns | 943 ns | 1.82x | 1.0 KB | 1.1 KB | 1.45 µs |
| Tiktoken o200k_base | encode-to-ids | long | 44.38 ms | 68.68 ms | 1.55x | 8.8 MB | 10.2 MB | 37.04 ms |
| Tiktoken o200k_base | count | long | 43.92 ms | 65.53 ms | 1.49x | 8.8 MB | 6.2 MB | 37.88 ms |
| Tiktoken o200k_base | decode | long | 3.99 ms | 5.38 ms | 1.35x | 1.6 MB | 1.6 MB | 5.63 ms |

#### arm64

| family | operation | size | ours | Microsoft.ML.Tokenizers | speedup | alloc ours | alloc comparator | native (info, x64) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| WordPiece (bert-base-uncased) | load | - | 33.32 ms | 19.15 ms | 0.57x | | | |
| WordPiece (bert-base-uncased) | encode-to-ids | short | 305 ns | 971 ns | 3.18x | 0 B | 456 B |  |
| WordPiece (bert-base-uncased) | count | short | 306 ns | 955 ns | 3.12x | 0 B | 240 B |  |
| WordPiece (bert-base-uncased) | decode | short | 89 ns | 165 ns | 1.85x | 144 B | 176 B |  |
| WordPiece (bert-base-uncased) | encode-to-ids | medium | 2.77 µs | 10.49 µs | 3.79x | 0 B | 2.3 KB |  |
| WordPiece (bert-base-uncased) | count | medium | 2.77 µs | 10.42 µs | 3.77x | 0 B | 1.1 KB |  |
| WordPiece (bert-base-uncased) | decode | medium | not timed | not timed | - | | | |
| WordPiece (bert-base-uncased) | encode-to-ids | long | not timed | not timed | - | | | |
| WordPiece (bert-base-uncased) | count | long | not timed | not timed | - | | | |
| WordPiece (bert-base-uncased) | decode | long | not timed | not timed | - | | | |
| Unigram (xlm-roberta-base) | load | - | 206.07 ms | 757.73 ms | 3.68x | | | |
| Unigram (xlm-roberta-base) | encode-to-ids | short | 171 ns | 1.45 µs | 8.51x | 0 B | 216 B |  |
| Unigram (xlm-roberta-base) | count | short | 168 ns | 1.57 µs | 9.38x | 0 B | 0 B |  |
| Unigram (xlm-roberta-base) | decode | short | 98 ns | 148 ns | 1.51x | 144 B | 176 B |  |
| Unigram (xlm-roberta-base) | encode-to-ids | medium | 1.32 µs | 13.04 µs | 9.90x | 0 B | 2.2 KB |  |
| Unigram (xlm-roberta-base) | count | medium | 1.30 µs | 14.68 µs | 11.25x | 0 B | 0 B |  |
| Unigram (xlm-roberta-base) | decode | medium | 751 ns | 1.57 µs | 2.10x | 1.0 KB | 1.1 KB |  |
| Unigram (xlm-roberta-base) | encode-to-ids | long | not timed | not timed | - | | | |
| Unigram (xlm-roberta-base) | count | long | not timed | not timed | - | | | |
| Unigram (xlm-roberta-base) | decode | long | not timed | not timed | - | | | |
| SentencePiece BPE, byte fallback (TinyLlama) | load | - | 48.96 ms | 11.15 ms | 0.23x | | | |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | short | 108 ns | 3.63 µs | 33.63x | 0 B | 1.6 KB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | count | short | 116 ns | 3.65 µs | 31.42x | 0 B | 1.4 KB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | short | 98 ns | 174 ns | 1.78x | 144 B | 176 B |  |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | medium | 13.70 µs | 44.19 µs | 3.22x | 0 B | 12.3 KB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | count | medium | 13.64 µs | 43.78 µs | 3.21x | 0 B | 11.1 KB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | medium | 687 ns | 1.99 µs | 2.89x | 1.0 KB | 1.1 KB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | long | 102.25 ms | 265.79 ms | 2.60x | 1.8 MB | 21.5 MB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | count | long | 102.62 ms | 263.03 ms | 2.56x | 1.8 MB | 17.5 MB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | long | 4.48 ms | 7.12 ms | 1.59x | 1.6 MB | 1.6 MB |  |
| Byte-level BPE (gpt2) | load | - | 26.90 ms | 32.47 ms | 1.21x | | | |
| Byte-level BPE (gpt2) | encode-to-ids | short | 396 ns | 638 ns | 1.61x | 0 B | 312 B |  |
| Byte-level BPE (gpt2) | count | short | 392 ns | 620 ns | 1.58x | 0 B | 96 B |  |
| Byte-level BPE (gpt2) | decode | short | 74 ns | 120 ns | 1.63x | 144 B | 176 B |  |
| Byte-level BPE (gpt2) | encode-to-ids | medium | 3.72 µs | 5.84 µs | 1.57x | 0 B | 1.2 KB |  |
| Byte-level BPE (gpt2) | count | medium | 3.71 µs | 5.66 µs | 1.53x | 0 B | 96 B |  |
| Byte-level BPE (gpt2) | decode | medium | 426 ns | 1.13 µs | 2.66x | 1.0 KB | 1.1 KB |  |
| Byte-level BPE (gpt2) | encode-to-ids | long | 24.74 ms | 66.39 ms | 2.68x | 0 B | 14.0 MB |  |
| Byte-level BPE (gpt2) | count | long | 24.92 ms | 65.01 ms | 2.61x | 0 B | 10.0 MB |  |
| Byte-level BPE (gpt2) | decode | long | 4.28 ms | 7.94 ms | 1.85x | 1.6 MB | 1.6 MB |  |
| Tiktoken cl100k_base | load | - | 19.96 ms | 19.20 ms | 0.96x | | | |
| Tiktoken cl100k_base | encode-to-ids | short | 439 ns | 675 ns | 1.54x | 408 B | 312 B |  |
| Tiktoken cl100k_base | count | short | 430 ns | 622 ns | 1.45x | 408 B | 96 B |  |
| Tiktoken cl100k_base | decode | short | 61 ns | 116 ns | 1.92x | 144 B | 176 B |  |
| Tiktoken cl100k_base | encode-to-ids | medium | 4.17 µs | 6.01 µs | 1.44x | 1.3 KB | 1.2 KB |  |
| Tiktoken cl100k_base | count | medium | 4.05 µs | 5.88 µs | 1.45x | 1.3 KB | 96 B |  |
| Tiktoken cl100k_base | decode | medium | 447 ns | 1.12 µs | 2.51x | 1.0 KB | 1.1 KB |  |
| Tiktoken cl100k_base | encode-to-ids | long | 51.07 ms | 68.65 ms | 1.34x | 8.8 MB | 12.5 MB |  |
| Tiktoken cl100k_base | count | long | 50.61 ms | 67.49 ms | 1.33x | 8.8 MB | 8.5 MB |  |
| Tiktoken cl100k_base | decode | long | 3.80 ms | 6.42 ms | 1.69x | 1.6 MB | 1.6 MB |  |
| Tiktoken o200k_base | load | - | 23.05 ms | 50.26 ms | 2.18x | | | |
| Tiktoken o200k_base | encode-to-ids | short | 501 ns | 816 ns | 1.63x | 408 B | 312 B |  |
| Tiktoken o200k_base | count | short | 493 ns | 762 ns | 1.55x | 408 B | 96 B |  |
| Tiktoken o200k_base | decode | short | 60 ns | 116 ns | 1.92x | 144 B | 176 B |  |
| Tiktoken o200k_base | encode-to-ids | medium | 4.86 µs | 7.56 µs | 1.56x | 1.3 KB | 1.2 KB |  |
| Tiktoken o200k_base | count | medium | 4.80 µs | 7.41 µs | 1.54x | 1.3 KB | 96 B |  |
| Tiktoken o200k_base | decode | medium | 432 ns | 1.09 µs | 2.52x | 1.0 KB | 1.1 KB |  |
| Tiktoken o200k_base | encode-to-ids | long | 45.61 ms | 70.04 ms | 1.54x | 8.8 MB | 10.2 MB |  |
| Tiktoken o200k_base | count | long | 45.55 ms | 67.84 ms | 1.49x | 8.8 MB | 6.2 MB |  |
| Tiktoken o200k_base | decode | long | 3.54 ms | 5.78 ms | 1.63x | 1.6 MB | 1.6 MB |  |


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

## German fast tier

UD German-GSD r2.18 test and GermEval 2014 test against spaCy 3.8.18 trained with its default configuration on the same data; the table, the settings and the weights are in `recipes/de/fast/PROVENANCE.md`. Raw text: every row ahead of spaCy or within noise (tokens 99.91 vs 98.03, UPOS 94.22 vs 90.13, UFeats 85.32 vs 80.11, LAS 76.75 vs 70.77). Gold tokens: UPOS (94.56 vs 94.59), XPOS (95.35 vs 95.21), lemmas (95.95 vs 95.65), UAS, LAS and entities (66.28 vs 65.77) within noise; UFeats (86.25 vs 84.71) and AllTags (82.78 vs 81.29) ahead. The tagging heads sit on an encoder with one bidirectional LSTM layer after one window layer (`recipes/de/fast/PROVENANCE.md`). The package is 109 MB; the consumer app runs on JIT and NativeAOT.

## French fast tier

UD French-GSD r2.18 test and WikiNER French (wp3) test against spaCy 3.8.18 trained with its default configuration on the same data, paired bootstrap 95% CI over sentences (1,000 resamples, seed 1); the table, the settings and the weights are in `recipes/fr/PROVENANCE.md`. Gold tokens: UPOS (96.77 vs 97.07), UFeats (96.34 vs 96.41), lemmas (97.51 vs 97.49), UAS (90.22 vs 89.10) and LAS (86.69 vs 85.98) within noise. Raw text: every row ahead of spaCy, whose tokenizer keeps `du` and `au` as one token where the treebank has two words (tokens 99.88 vs 98.85, words 99.48 vs 94.64, UPOS 96.06 vs 91.80, LAS 85.97 vs 78.10); sentences 94.79 vs 91.82, within noise. Entities, gold tokens: micro F1 82.48 vs 75.50 (+6.97 [6.12, 7.78]) and each of LOC, MISC, ORG, PER ahead. French-GSD has no XPOS. The package is 95 MB.

## Portuguese fast tier

UD Portuguese-Bosque r2.18 test and Universal NER Portuguese-Bosque test against spaCy 3.8.18 trained with its default configuration on the same data; the table, the settings and the weights are in `recipes/pt/fast/PROVENANCE.md`. Bosque has no XPOS, so there is no tagger and no XPOS or AllTags row. Raw text: every row ahead of spaCy except tokens (99.86 vs 99.87, within noise) (words 99.74 vs 88.51, UPOS 95.35 vs 84.15, LAS 82.09 vs 65.85, sentences 92.72 vs 84.48). Gold tokens: UPOS (95.89 vs 95.92), UFeats (94.51 vs 94.11), lemmas (97.30 vs 96.90), UAS (86.93 vs 86.05) and LAS (82.74 vs 81.96) within noise. The tagging heads sit on an encoder with one bidirectional LSTM layer after one window layer (`recipes/pt/fast/PROVENANCE.md`). Entities 73.13 vs 75.27 (paired difference -2.14 [-4.26, 0.39], within noise). The package is 118 MB.
## Fast-tier tagging speed in the other languages (indicative)

The tagging model of each language alone (sentencizer, tagger where present, morphologizer, lemmatizer), raw test text of the language's gating treebank, one thread, one document per call, three runs of four passes; build before the faster kernels and the LSTM-free encoder prediction against the current build, in the same job, x64 worker with load 16 to 25. Predictions of the current build equal the earlier build's on every file (dumps of the tagging, parser and entity files of each language: 0 differing lines, plus the Dutch entity confidence noted under English).

| Language, file | Before, tokens/s | Now, tokens/s | Allocation now | Peak working set now |
|---|---|---|---|---|
| German (GSD) | 13,300 to 15,800 | 16,100 to 17,200 | 3,227 B/token | 1,150 MB |
| Portuguese (Bosque) | 13,500 to 17,400 | 16,000 to 21,500 | 1,893 B/token | 691 MB |
| Dutch (Alpino) | 10,300 to 11,200 | 12,900 to 15,100 | 5,102 B/token | 1,055 MB |
| French (GSD) | 23,900 to 24,000 | 30,500 to 31,100 | 2,238 B/token | 745 MB |
| Turkish (BOUN) | 3,150 to 3,170 | 4,280 to 4,290 | 6,310 B/token | 1,900 MB |

German, Portuguese and Dutch (the recurrent-layer models) gain 15 to 40 percent; the rest of their time is in the three wide heads and the 256-wide encoders, which need the same retraining as English to be faster. Allocation went up for them because the recurrent layers now return one array per call. First-call times of the tagging models: German 1,419 ms, Dutch 1,258 ms, Portuguese 226 ms (load 125 to 171 ms). A French and a Turkish narrow student were trained on web text labelled by the shipped models; their text had not been checked against the evaluation sets, so they are not used.

## English fast tier (shipped: self-trained tagging and entity models, earlier parser)

Model `english.onenlp` in `RecursiveAI.OneNlp.Models.English` (126.2 MB, sha256 `1414ec79...1e48`): the tagging model (tagger, morphologizer, lemmatizer; 96-wide encoder with no window layer), the parser model (`recipes/en/parser`, unchanged) and the entity model (128-wide encoder with no window layer), each with its own encoder, put into one pipeline file by `recipes/en/assemble`. The tagging and entity models are trained by `recipes/en/selftrain`: the treebank (or UNER) plus web text labelled by the previous packaged model, after removing every sentence that matches a train, development or test sentence of the evaluation data (`recipes/en/selftrain/results/dedup-report.jsonl`). Provenance: `recipes/en/selftrain/PROVENANCE.md`.

UD English-EWT r2.18 test (2,077 sentences, 25,131 tokens) and UNER English-EWT test, each read once for the final configuration; the development sets chose every setting. Paired bootstrap 95% CI over sentences (1,000 resamples, seed 1) from `tools/OneNlp.Eval`; reference spaCy 3.8.18 trained on the same data with its default configuration, same input mode, with its own interval. "Within noise" means the intervals overlap (`docs/measurement.md`). The rows are in `recipes/en/selftrain/results/`.

### Quality, gold tokens and sentences

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 94.16 [93.79, 94.54] | 94.29 [93.89, 94.66] | yes |
| XPOS | 93.21 [92.79, 93.61] | 93.43 [93.00, 93.82] | yes |
| UFeats | 95.15 [94.86, 95.47] | 95.18 [94.88, 95.45] | yes |
| AllTags | 91.29 [90.83, 91.75] | 91.53 [91.05, 91.97] | yes |
| Lemmas | 96.11 [95.77, 96.44] | 95.80 [95.42, 96.13] | yes |
| UAS | 85.77 [84.91, 86.55] | 85.40 [84.50, 86.26] | yes |
| LAS | 81.68 [80.82, 82.49] | 81.96 [81.03, 82.83] | yes |
| CLAS | 76.76 [75.76, 77.75] | 77.28 [76.18, 78.29] | yes |

### Quality, raw text

Tagging and dependency rows below: ten consecutive sentences per call. The parser file is the one of the earlier model and its heads and relations are identical to that model's, so its whole-document rows are unchanged: Sentences 76.15 [74.25, 78.08] against spaCy 72.66, UAS 81.80, LAS 78.13 [77.18, 79.06] against 78.53, CLAS 73.04 against 74.34 (all within noise or ahead).

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.00 [98.80, 99.19] | 95.70 [95.14, 96.21] | ahead |
| UPOS | 92.90 [92.42, 93.35] | 92.45 [91.84, 93.06] | yes |
| XPOS | 91.91 [91.38, 92.38] | 91.72 [91.09, 92.34] | yes |
| UFeats | 93.87 [93.48, 94.26] | 93.37 [92.81, 93.92] | yes |
| AllTags | 90.04 [89.49, 90.57] | 89.99 [89.32, 90.62] | yes |
| Lemmas | 95.03 [94.61, 95.43] | 93.69 [93.08, 94.29] | ahead |
| UAS | 81.40 [80.41, 82.30] | 81.34 [80.25, 82.37] | yes |
| LAS | 77.76 [76.82, 78.70] | 78.53 [77.46, 79.55] | yes |
| CLAS | 72.63 [71.53, 73.75] | 74.34 [73.14, 75.47] | yes |

MLAS, information: 65.61 [64.46, 66.72] over ten-sentence calls, against spaCy's 69.05 [67.81, 70.21]; it is outside the interval, as for every earlier English model, and is not one of the gated rows.

### Entities (UNER English-EWT test, gold tokens)

| Row | Ours | spaCy | Paired difference |
|---|---|---|---|
| micro F1 | 63.09 [60.01, 65.89] | 62.32 [59.42, 65.33] | 0.77 [-1.92, 3.33] |
| precision | 70.95 [67.54, 74.27] | 67.97 [64.80, 71.11] | 2.98 [-0.18, 6.06] |
| recall | 56.80 [53.58, 60.13] | 57.54 [54.26, 60.91] | -0.74 [-3.42, 1.83] |
| LOC | 71.56 [67.09, 75.67] | 71.43 [66.56, 76.16] | 0.13 [-3.83, 3.97] |
| ORG | 43.87 [38.29, 48.99] | 44.33 [39.12, 49.82] | -0.46 [-5.77, 4.70] |
| PER | 69.40 [64.96, 73.26] | 68.05 [63.91, 71.83] | 1.35 [-2.51, 5.16] |

Every row is within noise.

### Speed (indicative, not a gate row)

Raw EWT test text (25,131 tokens, repeated four times per pass, 100,524 tokens), one document per call, twelve passes after the process starts, medians of the last ten; Catalyst 26.10.6514 with its English models on the same documents, run back to back in the same job. Machine: x64 worker (Ryzen 9 7950X3D, 32 threads) with other jobs running (load average 22 to 30 before and after, so the numbers are noisy), .NET SDK 10.0.403, OneModel 1.0.0. The gate run is made once, on a quiet machine, at the 1.0 gate.

| | 1 thread, tokens/s (min to max) | allocation | 8 threads, tokens/s |
|---|---|---|---|
| Catalyst, sentences + tokens + POS + lemmas | 545,000 (409,000 to 733,000) | not measured in this job (307 B/token before) | not run |
| Ours, sentences + tokens + UPOS/UFeats + lemmas | 366,000 (336,000 to 464,000) | 329 B/token | 1,460,000 |
| Ours, sentences + entities | 221,000 (184,000 to 314,000) | 322 B/token | 361,000 |
| Ours, parser path (the parser sets sentences) | 5,200 (4,500 to 5,400) | 4,940 B/token | 5,700 |
| Ours, full pipeline | 5,600 (4,800 to 5,800) | 5,480 B/token | 5,600 |

The ratio on the tagging path is **0.67** in this job (the same model against Catalyst in an earlier job with the same method: 0.94 for a 96-wide variant of the same recipe before the final retraining, 0.06 for the previous packaged model), which meets the 0.5 bar. Peak working set 1,241 MB (1 thread) and 1,360 MB (8 threads) for the full pipeline; model file 126.2 MB; load 416 ms and the first call 365 ms (previous model: 328 ms and 434 ms). Full pipeline in the single-file mode used by `Nlp.Analyze` (one document per call): 5,100 to 5,500 tokens/s against 3,000 to 3,500 for the previous model. The parser is now the limit of the full pipeline (about 190 us per token, an LSTM over a 192-wide state), and its prediction is serialized per head, so eight threads do not help it; the tagging and entity paths scale (4.0 and 1.6 times at eight threads).

How the speed was reached without changing a row: the encoders are narrower and trained on more text (above); GELU, the dense layers and the parser's LSTM and label scores run on dedicated row kernels (`RowKernels`, `LstmInference`) with bit-identical or last-digit-identical results (prediction dumps of every shipped file before and after: 0 differing lines on English, German, Portuguese, Dutch, Turkish and French except one Dutch entity confidence that rounds 0.901 to 0.902); the encoder runs outside the head lock.


## English fast tier, option: one shared encoder (not shipped)

A single encoder shared by every head, trained by `recipes/en/joint` (23.7 MB, sha256 `6bb03663...50b9`): four times smaller and more than ten times faster on the tagging path, with the rows below. It is not the packaged model because its UFeats, AllTags, entity and raw CLAS and MLAS rows are weaker. Rows measured on the model file `english.onenlp` of that recipe. UD English-EWT r2.18 test, 2,077 sentences, 25,131 tokens; UNER English-EWT test. Paired bootstrap 95% CI over sentences (1,000 resamples, seed 1); reference spaCy 3.8.18 trained on the same data with its default configuration, same input mode. "Within noise" means the intervals overlap. The development sets chose the configuration; the test files were read once.

### Quality, gold tokens and sentences

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 93.58 [93.17, 93.99] | 94.29 [93.91, 94.68] | yes |
| XPOS | 92.81 [92.36, 93.24] | 93.43 [93.00, 93.87] | yes |
| UFeats | 94.40 [94.07, 94.73] | 95.18 [94.86, 95.48] | **no** |
| AllTags | 90.45 [89.98, 90.95] | 91.53 [91.05, 91.99] | **no** |
| Lemmas | 95.84 [95.50, 96.17] | 95.80 [95.46, 96.13] | yes |
| UAS | 84.98 [84.12, 85.80] | 85.40 [84.48, 86.28] | yes |
| LAS | 81.31 [80.47, 82.16] | 81.96 [81.00, 82.91] | yes |

### Quality, raw text

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.00 [98.81, 99.19] | 95.70 [95.16, 96.17] | ahead |
| Sentences | 72.76 [70.97, 74.66] | 72.66 [70.46, 74.70] | yes |
| UPOS | 92.53 [92.09, 93.01] | 92.45 [91.83, 93.05] | yes |
| XPOS | 91.60 [91.11, 92.11] | 91.72 [91.10, 92.34] | yes |
| UFeats | 93.39 [92.97, 93.79] | 93.37 [92.84, 93.91] | yes |
| AllTags | 89.35 [88.80, 89.89] | 89.99 [89.34, 90.64] | yes |
| Lemmas | 94.68 [94.29, 95.07] | 93.69 [93.07, 94.28] | ahead |
| UAS | 80.41 [79.47, 81.36] | 81.34 [80.25, 82.34] | yes |
| LAS | 76.93 [75.98, 77.92] | 78.53 [77.49, 79.54] | yes |
| CLAS | 71.81 [70.67, 72.97] | 74.34 [73.19, 75.49] | **no** |
| MLAS | 65.14 [63.97, 66.36] | 69.05 [67.85, 70.21] | **no** |

### Entities (UNER English-EWT test, gold tokens)

| Row | Ours | spaCy | Paired difference |
|---|---|---|---|
| micro F1 | 59.38 [56.06, 62.60] | 62.32 [59.42, 65.33] | -2.93 [-5.81, -0.15] |
| LOC | 65.55 [60.45, 70.29] | 71.43 [66.56, 76.16] | -5.88 [-9.82, -1.94] |
| ORG | 45.19 [39.35, 50.48] | 44.33 [39.12, 49.82] | 0.86 [-3.94, 5.38] |
| PER | 64.19 [59.35, 68.55] | 68.05 [63.91, 71.83] | -3.86 [-8.32, 0.72] |

The intervals overlap on every entity row; the paired differences for micro F1 and LOC exclude zero.

### Speed (indicative, not a gate row)

Raw EWT test text (25,131 tokens, repeated four times per pass), one thread, sentences + tokens + UPOS/UFeats + lemmas; Catalyst 26.10.6514 with its English models on the same documents, run back to back in the same job. Machine: x64 worker (Ryzen 9 7950X3D) while other jobs were running (load 9 to 15), .NET SDK 10.0.403. The gate run is made once, on a quiet machine, at the 1.0 gate.

| | tokens/s | allocation | note |
|---|---|---|---|
| Catalyst | 530,000 to 590,000 | 307 B/token | UPOS 80.7, lemmas 83.8 on this text |
| Ours, one document per call | 215,000 to 270,000 | 413 B/token | UPOS 92.5, lemmas 94.7 |
| Ours, `ProcessMany` batches of 64 | 226,000 to 259,000 | 327 B/token | |
| Ours, entities path | 209,000 to 257,000 | 393 B/token | |
| Ours, parser path | 5,300 to 7,900 | 856 B/token | the bidirectional LSTM is the cost |
| Ours, full pipeline | 5,500 to 7,900 | 1,377 B/token | |

The ratio on the tagging path is about 0.4 (**misses**). Peak working set 640 MB for the full pipeline; model file 23.7 MB; first `Nlp.Analyze` (loads the weights) 325 ms on x64. Where the time goes (microseconds per token, one document per call): tokenizer 0.2, sentencizer 0.05, encoder about 2 (lookups 0.6, window-layer product 1.2), lemmatizer 0.9, morphologizer 1.0. A smaller configuration of the same recipe (48 wide, no window layer) ran the same path at 500,000 to 690,000 tokens/s (about 1.6 to 2.3 times Catalyst in the same conditions) with a development tagging mean 0.6 points lower; it is not the shipped model.

## Dutch fast tier

UD Dutch-Alpino r2.18 test and WikiNER Dutch (wp3) test against spaCy 3.8.18 trained with its default configuration on the same data, paired bootstrap 95% CI over sentences (1,000 resamples, seed 1); the table, the settings and the weights are in `recipes/nl/fast/PROVENANCE.md`. Every row is within noise of spaCy in both modes. Gold tokens (the treebank's own words and sentences): UPOS 93.83 vs 94.42, XPOS 91.52 vs 91.73, UFeats 92.94 vs 93.13, AllTags 90.30 vs 90.51, lemmas 91.61 vs 91.83, UAS 85.18 vs 84.09, LAS 80.28 vs 79.85. Raw text: tokens 99.66 vs 99.82, sentences 91.31 vs 89.60, UPOS 93.09 vs 94.36, XPOS 90.94 vs 91.63, UFeats 92.47 vs 92.97, AllTags 89.52 vs 90.39, lemmas 91.19 vs 91.65, UAS and LAS (79.48 vs 79.57). The tagging heads sit on an encoder with one bidirectional LSTM layer after one window layer (`recipes/nl/fast/PROVENANCE.md`). Entities (gold tokens): micro F1 87.47 vs 86.21 (difference +1.26 [0.76, 1.72], ahead), every type within noise or ahead.
## Turkish fast tier

UD Turkish-BOUN r2.18 test and Turkish-WikiNER test against spaCy 3.8.18 trained with its default configuration on the same data, paired bootstrap 95% CI over sentences (1,000 resamples, seed 1); the table, the settings and the weights are in `recipes/tr/fast/PROVENANCE.md`. Gold words: UPOS (88.42 vs 88.04), UFeats (77.48 vs 78.30), UAS, LAS, CLAS and BLEX within noise; XPOS, AllTags and lemmas (84.88 vs 80.24) ahead; MLAS behind (36.36 vs 38.85). Raw text: tokens (99.56 vs 99.42) and words (96.71 vs 97.06) within noise, sentences ahead (87.46 vs 82.09), UFeats, UAS and LAS within noise; UPOS (85.46 vs 86.82), CLAS and MLAS behind, because the multiword tokens of the treebank (`vardır` = `var` + `dır`) cannot be predicted from the spelling. Entities (gold tokens): 67.40 vs 65.82, within noise. The package is 134 MB; the consumer app runs on JIT and NativeAOT.

## Spanish fast tier

UD Spanish-AnCora r2.18 test and WikiNER Spanish (wp3) test against spaCy 3.8.18 trained with its default configuration on the same data, paired bootstrap 95% CI over sentences (1,000 resamples, seed 1); the table, the settings and the weights are in `recipes/es/fast/PROVENANCE.md`. Gold words and sentences: UPOS (98.11 vs 98.18), UFeats (97.71 vs 97.74) and lemmas (98.84 vs 98.72) within noise; XPOS (86.73 vs 84.97), AllTags (86.09 vs 84.47), UAS (90.05 vs 89.04) and LAS (87.00 vs 85.95) ahead. Raw text: every row within noise or ahead (spaCy's tokenizer keeps `del` and `verlo` as one token where the treebank has several words; words 99.83 vs 96.60, UPOS 97.86 vs 94.81, LAS 86.70 vs 81.82); tokens 99.84 vs 99.90 and sentences 98.14 vs 97.70 within noise. Entities, gold tokens: micro F1 82.88 vs 83.35 (difference -0.46 [-1.14, 0.20], within noise); ORG ahead (+4.96), PER behind (-1.96 [-2.83, -1.13]). The entity model reads the tokenizer's pieces of `del` and `al`: trained on whole tokens it scored 81.71 (-1.64, outside the interval). Indicative cost on one thread of the shared worker: tagging heads 44 us/token, whole pipeline about 190 us/token. The package is 109 MB; the consumer app runs on JIT and NativeAOT.

## Chinese fast tier

UD Chinese-GSDSimp r2.18 test and UNER Chinese-GSDSIMP test against spaCy 3.8.18 trained with its default configuration on the same data, paired bootstrap 95% CI over sentences (1,000 resamples, seed 1); the settings and weights are in `recipes/zh/fast/PROVENANCE.md`. spaCy's default Chinese tokenizer cuts single characters, so its raw-text pipeline cannot reach word-level scores (Tokens 39.43); the gold-token reference is the `gold_preproc` training run described in `tools/baselines/spacy/lang/PROVENANCE.md`, which trains and scores on gold words. The pipeline cuts words with a trained character tagger (`docs/segmentation.md`), tags, lemmatizes, parses and finds entities.

Gold words and sentences (spaCy `gold_preproc`):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 90.41 [89.70, 91.07] | 90.35 [89.70, 90.99] | yes |
| XPOS | 91.37 [90.75, 91.98] | 91.11 [90.45, 91.70] | yes |
| UFeats | 98.38 [98.13, 98.59] | 98.43 [98.19, 98.66] | yes |
| AllTags | 89.49 [88.76, 90.17] | 89.52 [88.79, 90.20] | yes |
| Lemmas | 99.87 [99.79, 99.94] | 99.91 [99.85, 99.96] | yes |
| UAS | 74.94 [73.46, 76.38] | 73.11 [71.46, 74.68] | yes |
| LAS | 68.76 [67.12, 70.32] | 68.24 [66.47, 69.94] | yes |
| CLAS | 65.85 [64.15, 67.51] | 65.49 [63.52, 67.27] | yes |
| MLAS | 56.94 [55.08, 58.69] | 58.87 [56.85, 60.72] | yes |

Raw text (spaCy's default pipeline, character tokens):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens, Words | 89.62 [88.87, 90.42] | 39.43 [38.60, 40.23] | no (ahead) |
| Sentences | 99.40 [98.70, 99.90] | 46.93 [42.22, 51.79] | no (ahead) |
| UPOS | 81.90 [80.99, 82.86] | 34.92 [34.11, 35.68] | no (ahead) |
| XPOS | 82.73 [81.85, 83.67] | 35.69 [34.86, 36.47] | no (ahead) |
| UFeats | 88.24 [87.49, 89.01] | 38.38 [37.56, 39.16] | no (ahead) |
| AllTags | 81.00 [80.09, 81.99] | 34.29 [33.48, 35.06] | no (ahead) |
| Lemmas | 89.56 [88.82, 90.35] | 39.43 [38.60, 40.23] | no (ahead) |
| UAS | 62.50 [60.81, 64.17] | 9.12 [8.44, 9.79] | no (ahead) |
| LAS | 57.88 [56.21, 59.63] | 8.59 [7.94, 9.28] | no (ahead) |

Entities, UNER Chinese-GSDSIMP test, gold tokens (paired with spaCy `gold_preproc`): micro F1 62.80 [58.46, 67.23] vs 61.86 [57.66, 66.12] (difference 0.94 [-2.18, 4.40], within noise); precision 62.44 vs 67.08 (-4.64 [-8.52, -0.50], inside the noise rule); recall 63.17 vs 57.40. Per type: LOC 69.86 vs 68.15, ORG 47.91 vs 45.00, PER 57.69 vs 59.08, all within noise. On raw text (our segmenter's words, character-span F1) the micro F1 is 52.88 [48.60, 57.61]; spaCy's default model reaches 13.30 on gold tokens only, and no raw-text spaCy row exists.

For information (`docs/references.md`): Stanza 1.5.1 raw text UPOS 88.82, LAS 70.65; UDPipe 2 gold tokens UPOS 95.73, LAS 83.60; Trankit base raw UPOS 94.17, LAS 81.96 (an older annotation). Our fast tier is below them, as expected of a fast tier with no pretrained embeddings; the gate is spaCy's.

**Misses:** none. Everything is within noise or ahead; MLAS (-1.93) is inside the noise rule.

Indicative speed (1 thread, load average 8-13 on the shared worker, raw development text; Catalyst has no Chinese model, so there is no managed row): segmenter alone 22-23k words/s (3.7 kB allocated per word); segmenter and tagging 16-17k words/s; the whole pipeline (segmenter, tagging, parser, entities) 4.5-4.6k words/s.
