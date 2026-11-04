# BPE, byte-level BPE and Tiktoken

Behaviour of the byte-pair families. Language-neutral and normative. Pinned references: Hugging Face `tokenizers` 0.22.1 and `tiktoken` 0.14.0.

## BPE model (`tokenizer.json` `"type": "BPE"`)

- **Algorithm** (Sennrich, Haddow and Birch 2016, §3.2). A piece starts as one symbol per character (a surrogate pair is one character). The adjacent pair with the lowest merge rank is replaced by its merged symbol, leftmost first on equal ranks, until no adjacent pair has a rank. The result is the symbols' vocabulary IDs, with offsets into the piece.
- **Merges** come as `"left right"` strings (exactly one space) or `["left","right"]` pairs. The rank is the index. A pair listed twice keeps its last rank. The merged token is `left + right`, where the right half first loses `continuing_subword_prefix` if it starts with it. A merge whose halves or joined token are not in the vocabulary makes the file invalid.
- **Options:** `unk_token` (must be in the vocabulary), `continuing_subword_prefix` (added to every symbol after the first when looking a character up), `end_of_word_suffix` (added to the last symbol), `fuse_unk` (a run of unknown characters is one token), `byte_fallback` (an unknown character is spelled by its UTF-8 bytes as `<0xNN>` tokens when all of them exist, before the unknown token is tried), `ignore_merges` (a piece that is a whole vocabulary entry is one token), `dropout` (each merge is skipped with that probability; 0 or absent is the deterministic algorithm). With no `unk_token`, unknown characters are dropped.
- **Offsets.** A symbol covers the characters it was built from. Byte-fallback tokens and fused unknowns cover the whole characters they stand for, so the bytes of one character share its range.
- **Byte-level BPE** (GPT-2 and relatives) is this model over text that the ByteLevel pre-tokenizer has mapped to printable characters, one per byte. It needs nothing extra.
- **Vocabulary limits.** IDs must be non-negative and no sparser than 4× the entry count plus 65,536.
- **Cache.** Results for pieces up to 128 characters are cached (20,000 entries, then no more are added). Dropout disables the cache and is not reproducible between runs.
- **SentencePiece BPE** (`.model` files of the BPE type): the merge for a pair is the piece the two join into, ranked by that piece's score (higher score, lower rank; equal scores share a rank, so the leftmost wins). `BpeModel.FromSentencePieces` builds the model from pieces, scores and a per-piece "ordinary piece" flag.

## Tiktoken

- **Ranks file (`.tiktoken`).** One line per token: the token's bytes in standard padded base64, one space, the rank in decimal, then `\n` (a preceding `\r` is tolerated, the last newline may be missing). The rank is the token ID and the merge priority. All 256 single bytes must be present; token bytes and ranks are unique. Ranks may skip numbers (`p50k_base` has a gap at the position of its special token) but not by more than 4× the count plus 65,536. Limits: 64 MiB, 8 KiB per line, 4 million lines, rank at most 100 million. Violations throw `InvalidDataException`.
- **Encoding.** Special tokens are cut out first when allowed; the rest is split by the encoding's pattern into pieces; each piece's UTF-8 bytes are looked up whole, else byte-pair merged by rank (lowest rank first, leftmost on ties). `Encode(text, addSpecialTokens: true)` is `encode(allowed_special="all")` and `false` is `encode_ordinary`. Text that no match of the pattern covers is dropped.
- **Patterns and special tokens** are not in the file. They are those of the Python library's `Encoding` objects (`_pat_str`, `_special_tokens`, read from the running object) with possessive quantifiers written as atomic groups and a final `$` as `\z`, because the .NET engine has no possessive quantifiers and the engine tiktoken runs on reads `$` as the end of the text only.
  - `r50k_base`, `p50k_base`: `<|endoftext|>` 50256.
  - `cl100k_base`: `<|endoftext|>` 100257, `<|fim_prefix|>` 100258, `<|fim_middle|>` 100259, `<|fim_suffix|>` 100260, `<|endofprompt|>` 100276.
  - `o200k_base`: `<|endoftext|>` 199999, `<|endofprompt|>` 200018.
- **Characters outside the BMP.** The .NET engine matches UTF-16 units, so the named patterns would split such a character in two and not see it as a letter. For the named encodings each such character is swapped for one BMP character of the same general category while matching, and the matches are mapped back. A pattern you supply yourself is matched against raw UTF-16 units.
- **Offsets** are UTF-16 units (or bytes for UTF-8 input). A token that starts or ends inside a multi-byte character reports the whole character. The Python library reports start offsets in code points; the two agree below U+10000.
- **Token strings.** The token's UTF-8 text when its bytes are valid UTF-8, otherwise each byte as `<0xNN>`. `DecodeBytes` gives exact bytes; `Decode` replaces invalid UTF-8 with U+FFFD.
- **File names.** `Tokenizer.FromFile("x.tiktoken")` takes the encoding from the stem (`r50k_base`, `p50k_base`, `cl100k_base`, `o200k_base`), else from the rank count (50,256, 50,281, 100,256, 199,998); otherwise it throws and asks for `TiktokenTokenizer.Create` with a pattern.
- **Long runs.** Pieces over 48 bytes merge with a heap, so a long run of one letter costs O(n log n).
