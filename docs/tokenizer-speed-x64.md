# Tokenizer speed: x64

See [speed-x64.md](speed-x64.md) for machine, input hashes, versions and method.
Speedup is comparator time divided by ours. Load is information, not gated.

Inputs: short 59 chars, medium 513 chars, long 990287 bytes (UTF-8).

| family | operation | size | ours | comparator | speedup | alloc ours | alloc comparator | peak extra RSS ours | peak extra RSS comparator | note |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| WordPiece (bert-base-uncased) | load | - | 37.15 ms | 22.89 ms | 0.62x | - | - | - | - | load time; ours from tokenizer.json |
| WordPiece (bert-base-uncased) | encode-to-ids | short | 289 ns | 899 ns | 3.11x | 0 B | 456 B | 8.0 KB | 4.0 KB |  |
| WordPiece (bert-base-uncased) | count | short | 287 ns | 821 ns | 2.86x | 0 B | 240 B | 4.0 KB | 0 B |  |
| WordPiece (bert-base-uncased) | decode | short | 71 ns | 167 ns | 2.35x | 144 B | 176 B | 4.0 KB | 4.0 KB |  |
| WordPiece (bert-base-uncased) | encode-to-ids | medium | 2.85 µs | 7.63 µs | 2.68x | 0 B | 2.3 KB | 36.0 KB | 4.0 KB |  |
| WordPiece (bert-base-uncased) | count | medium | 2.92 µs | 7.59 µs | 2.60x | 0 B | 1.1 KB | 0 B | 64.0 KB |  |
| WordPiece (bert-base-uncased) | decode | medium | - | - | - | - | - | - | - | decoded text differs at char 74 (lengths 525 vs 519) |
| WordPiece (bert-base-uncased) | encode-to-ids | long | - | - | - | - | - | - | - | ids differ: 357483 vs 356166 tokens, first difference at 159 (ours 1456, comparator 100) |
| WordPiece (bert-base-uncased) | count | long | - | - | - | - | - | - | - | ids differ: 357483 vs 356166 tokens, first difference at 159 (ours 1456, comparator 100) |
| WordPiece (bert-base-uncased) | decode | long | - | - | - | - | - | - | - | ids differ: 357483 vs 356166 tokens, first difference at 159 (ours 1456, comparator 100) |
| Unigram (xlm-roberta-base) | load | - | 260.55 ms | 766.93 ms | 2.94x | - | - | - | - | load time; ours from tokenizer.json |
| Unigram (xlm-roberta-base) | encode-to-ids | short | 206 ns | 1.40 µs | 6.80x | 0 B | 216 B | 0 B | 15.3 MB |  |
| Unigram (xlm-roberta-base) | count | short | 203 ns | 1.42 µs | 7.01x | 0 B | 0 B | 0 B | 0 B |  |
| Unigram (xlm-roberta-base) | decode | short | 73 ns | 140 ns | 1.92x | 144 B | 176 B | 0 B | 56.0 KB |  |
| Unigram (xlm-roberta-base) | encode-to-ids | medium | 1.66 µs | 11.47 µs | 6.93x | 0 B | 2.2 KB | 0 B | 32.0 KB |  |
| Unigram (xlm-roberta-base) | count | medium | 1.70 µs | 12.32 µs | 7.24x | 0 B | 0 B | 0 B | 440.0 KB |  |
| Unigram (xlm-roberta-base) | decode | medium | 658 ns | 1.50 µs | 2.28x | 1.0 KB | 1.1 KB | 4.0 MB | 0 B |  |
| Unigram (xlm-roberta-base) | encode-to-ids | long | - | - | - | - | - | - | - | ids differ: 250378 vs 251115 tokens, first difference at 30076 (ours 49729, comparator 1759) |
| Unigram (xlm-roberta-base) | count | long | - | - | - | - | - | - | - | ids differ: 250378 vs 251115 tokens, first difference at 30076 (ours 49729, comparator 1759) |
| Unigram (xlm-roberta-base) | decode | long | - | - | - | - | - | - | - | ids differ: 250378 vs 251115 tokens, first difference at 30076 (ours 49729, comparator 1759) |
| SentencePiece BPE, byte fallback (TinyLlama) | load | - | 60.91 ms | 11.69 ms | 0.19x | - | - | - | - | load time; ours from tokenizer.json |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | short | 92 ns | 4.08 µs | 44.59x | 0 B | 1.6 KB | 0 B | 0 B |  |
| SentencePiece BPE, byte fallback (TinyLlama) | count | short | 87 ns | 4.21 µs | 48.55x | 0 B | 1.4 KB | 0 B | 0 B |  |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | short | 79 ns | 148 ns | 1.88x | 144 B | 176 B | 0 B | 0 B |  |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | medium | 12.26 µs | 43.60 µs | 3.56x | 0 B | 12.3 KB | 0 B | 12.0 KB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | count | medium | 12.24 µs | 43.22 µs | 3.53x | 0 B | 11.1 KB | 12.0 KB | 0 B |  |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | medium | 707 ns | 1.56 µs | 2.21x | 1.0 KB | 1.1 KB | 0 B | 0 B |  |
| SentencePiece BPE, byte fallback (TinyLlama) | encode-to-ids | long | 89.74 ms | 262.47 ms | 2.92x | 1.8 MB | 21.5 MB | 0 B | 55.9 MB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | count | long | 87.95 ms | 252.61 ms | 2.87x | 1.8 MB | 17.5 MB | 0 B | 26.3 MB |  |
| SentencePiece BPE, byte fallback (TinyLlama) | decode | long | 4.50 ms | 6.54 ms | 1.45x | 1.6 MB | 1.6 MB | 87.6 MB | 56.6 MB |  |
| Byte-level BPE (gpt2) | load | - | 31.46 ms | 24.78 ms | 0.79x | - | - | - | - | load time; ours from tokenizer.json |
| Byte-level BPE (gpt2) | encode-to-ids | short | 454 ns | 722 ns | 1.59x | 0 B | 312 B | 0 B | 0 B |  |
| Byte-level BPE (gpt2) | count | short | 442 ns | 650 ns | 1.47x | 0 B | 96 B | 0 B | 0 B |  |
| Byte-level BPE (gpt2) | decode | short | 62 ns | 120 ns | 1.93x | 144 B | 176 B | 0 B | 0 B |  |
| Byte-level BPE (gpt2) | encode-to-ids | medium | 3.96 µs | 6.53 µs | 1.65x | 0 B | 1.2 KB | 0 B | 0 B |  |
| Byte-level BPE (gpt2) | count | medium | 4.11 µs | 5.97 µs | 1.45x | 0 B | 96 B | 0 B | 0 B |  |
| Byte-level BPE (gpt2) | decode | medium | 444 ns | 990 ns | 2.23x | 1.0 KB | 1.1 KB | 4.0 KB | 0 B |  |
| Byte-level BPE (gpt2) | encode-to-ids | long | 28.81 ms | 63.16 ms | 2.19x | 0 B | 14.0 MB | 12.0 KB | 65.8 MB |  |
| Byte-level BPE (gpt2) | count | long | 28.69 ms | 63.88 ms | 2.23x | 0 B | 10.0 MB | 0 B | 0 B |  |
| Byte-level BPE (gpt2) | decode | long | 4.71 ms | 7.23 ms | 1.54x | 1.6 MB | 1.6 MB | 124.0 MB | 60.5 MB |  |
| Tiktoken cl100k_base | load | - | 30.39 ms | 23.48 ms | 0.77x | - | - | - | - | load time; ours from cl100k_base.tiktoken |
| Tiktoken cl100k_base | encode-to-ids | short | 510 ns | 711 ns | 1.39x | 408 B | 312 B | 0 B | 0 B |  |
| Tiktoken cl100k_base | count | short | 510 ns | 664 ns | 1.30x | 408 B | 96 B | 0 B | 0 B |  |
| Tiktoken cl100k_base | decode | short | 71 ns | 108 ns | 1.51x | 144 B | 176 B | 0 B | 0 B |  |
| Tiktoken cl100k_base | encode-to-ids | medium | 5.16 µs | 6.68 µs | 1.30x | 1.3 KB | 1.2 KB | 0 B | 0 B |  |
| Tiktoken cl100k_base | count | medium | 5.21 µs | 5.90 µs | 1.13x | 1.3 KB | 96 B | 0 B | 96.0 KB |  |
| Tiktoken cl100k_base | decode | medium | 522 ns | 1.03 µs | 1.97x | 1.0 KB | 1.1 KB | 0 B | 0 B |  |
| Tiktoken cl100k_base | encode-to-ids | long | 47.19 ms | 72.39 ms | 1.53x | 8.8 MB | 12.5 MB | 53.2 MB | 20.4 MB |  |
| Tiktoken cl100k_base | count | long | 47.82 ms | 63.87 ms | 1.34x | 8.8 MB | 8.5 MB | 36.5 MB | 3.8 MB |  |
| Tiktoken cl100k_base | decode | long | 4.41 ms | 6.22 ms | 1.41x | 1.6 MB | 1.6 MB | 38.6 MB | 72.3 MB |  |
| Tiktoken o200k_base | load | - | 33.55 ms | 50.85 ms | 1.52x | - | - | - | - | load time; ours from o200k_base.tiktoken |
| Tiktoken o200k_base | encode-to-ids | short | 576 ns | 772 ns | 1.34x | 408 B | 312 B | 0 B | 0 B |  |
| Tiktoken o200k_base | count | short | 592 ns | 741 ns | 1.25x | 408 B | 96 B | 0 B | 0 B |  |
| Tiktoken o200k_base | decode | short | 70 ns | 110 ns | 1.58x | 144 B | 176 B | 0 B | 0 B |  |
| Tiktoken o200k_base | encode-to-ids | medium | 5.46 µs | 6.84 µs | 1.25x | 1.3 KB | 1.2 KB | 0 B | 0 B |  |
| Tiktoken o200k_base | count | medium | 5.24 µs | 6.59 µs | 1.26x | 1.3 KB | 96 B | 8.0 KB | 0 B |  |
| Tiktoken o200k_base | decode | medium | 553 ns | 948 ns | 1.71x | 1.0 KB | 1.1 KB | 0 B | 0 B |  |
| Tiktoken o200k_base | encode-to-ids | long | 46.13 ms | 71.88 ms | 1.56x | 8.8 MB | 10.2 MB | 130.9 MB | 7.8 MB |  |
| Tiktoken o200k_base | count | long | 43.73 ms | 64.85 ms | 1.48x | 8.8 MB | 6.2 MB | 62.3 MB | 0 B |  |
| Tiktoken o200k_base | decode | long | 4.24 ms | 5.85 ms | 1.38x | 1.6 MB | 1.6 MB | 66.4 MB | 65.1 MB |  |
