# Results

Measured rows. The tokenizer speed rows below are final: they were taken by one `wq --class bench` group per machine, alone on the
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

- **x64:** AMD Ryzen 9 7950X3D (16 cores, 32 threads), Linux 7.0.0-38-generic, .NET SDK 10.0.403, Release, non-concurrent GC, ICU on. Queue job `the x64 machine.exo0l57k` (host queue job 003988, class `bench`), 2026-11-05 03:27 UTC. Load average before the run 3.6 (1 min), 15.5 (5 min), 17.4 (15 min), the tail of an earlier job; during the run (10 s samples) it fell from 3.6 to about 1.5; the machine was otherwise idle (`wq` reported the CPU 0% busy at the start).
- **arm64:** Apple M2 (8 cores), macOS (Darwin 27.0.0), .NET SDK 10.0.403 (user-local install), Release, non-concurrent GC, ICU on. Queue job `the arm64 machine.9itrnzi3` (host queue job 000653, class `bench`), 2026-11-05 04:40 UTC. Load average before the run 2.7 / 3.9 / 12.4; during the run 2.4 to 3.3 (the harness's own thread and the OS).
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

