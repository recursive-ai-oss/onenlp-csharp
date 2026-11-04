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
