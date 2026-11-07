# WordPiece, WordLevel and Unigram models, and the `vocab.txt` and `.model` readers

Normative behaviour of the three model families behind the `tokenizer.json` pipeline and of the two file readers that build a tokenizer without a `tokenizer.json`. Offsets are UTF-16 units of the piece the model is given. A piece is never empty.

Where the published papers and docs leave a detail open, it was settled by observing the output of Hugging Face `tokenizers` 0.22.1 (its model objects, called through the documented Python API on crafted vocabularies); those details are marked *observed*.

## WordPiece

Schuster and Nakajima 2012; Devlin et al. 2019 (appendix A.3). Settings: `unk_token` (default `[UNK]`), `continuing_subword_prefix` (default `##`), `max_input_chars_per_word` (default 100).

- A word of more than `max_input_chars_per_word` *Unicode scalar values* (not UTF-16 units) is one unknown token covering the whole word. *Observed.*
- Otherwise the word is cut greedily from the left: at each start the longest vocabulary entry that matches is taken, with the prefix added in front of every entry that does not start the word. Shortening goes one scalar value at a time, so a surrogate pair is never split.
- If any start has no match, the whole word is one unknown token (the pieces found so far are dropped), with offsets `(0, word length)`.
- The token value is the vocabulary entry, prefix included. When the unknown token is not in the vocabulary and a word needs it, tokenizing throws `InvalidDataException`.
- Vocabulary size is the number of distinct entries. IDs need not be contiguous; an ID or entry that repeats keeps its last use.

## WordLevel

The piece is looked up whole. Not found: the unknown token (default `[UNK]`) with offsets `(0, piece length)`, or `InvalidDataException` when the vocabulary has none.

## Unigram

Kudo 2018 (subword regularization, section 2) and Kudo and Richardson 2018 (section 2.2): Viterbi search over the lattice of vocabulary pieces for the highest total score. Settings: `unk_id` (may be absent), `byte_fallback` (default false). The vocabulary is a list of `[piece, score]`; the ID is the list position; scores are 64-bit.

- A position with no one-character piece gets a synthesized one-character (one scalar value) unknown node scored ten below the lowest score in the vocabulary. *Observed.*
- Ties between segmentations go to the one found first: the earliest start, then the shortest piece. *Observed.*
- Consecutive unknown nodes fuse into one token whose value is the text it covers and whose ID is `unk_id`. A vocabulary piece that has the unknown ID counts as unknown and fuses too. *Observed.*
- With `byte_fallback`, a fused unknown run becomes one `<0xXX>` token per UTF-8 byte of the run, each reporting the whole run's range, when every byte has a piece; otherwise it stays one unknown token. A run that is a single vocabulary piece with the unknown ID is left as it is. *Observed.*
- Without `unk_id`, a character that has no one-character piece is an error (`InvalidDataException`) unless some matched piece ends right after that character, even when the best path never needs the missing piece. *Observed* (checked against 40,000 random cases).
- A repeated piece keeps its last ID for lookups and matching; vocabulary size is the list length.

## Untyped model objects

Older `tokenizer.json` files leave out the model's `type`. It is inferred: `merges` present means BPE; otherwise a `vocab` list means Unigram and a `vocab` object means WordPiece. *Observed* in the files of bert-base-uncased, t5-small and xlm-roberta-base.

## `vocab.txt` reader

One token per line; the ID is the line number. Read as the reference reads it (*observed*): lines end at `\n` only (not at `\r`, U+0085 or U+2028); trailing Unicode White_Space of a line is dropped (leading white space is kept); blank lines are empty tokens that keep their IDs; there is no byte-order-mark handling; a repeated token keeps its last ID; invalid UTF-8 is an error. The file must contain `[UNK]`. Files are limited to 64 MiB.

The tokenizer is composed as the BERT model cards describe it: `BertNormalizer` (clean text, Chinese characters spaced, lowercasing and accent stripping when the vocabulary is uncased), `BertPreTokenizer`, WordPiece (`##`, 100), the `[CLS] A [SEP]` / `[CLS] A [SEP] B [SEP]` template when `[CLS]` and `[SEP]` exist, and the WordPiece decoder with cleanup. `[PAD]`, `[UNK]`, `[CLS]`, `[SEP]` and `[MASK]` that are in the vocabulary are special added tokens. A `vocab.txt` does not say whether the model lowercases. `Tokenizer.FromFile` guesses: a vocabulary with any token that has an uppercase letter *which has a lowercase form* (outside bracketed special tokens) is cased. Letters without a case pair do not count (`bert-base-uncased` contains `ℝ`, which lowercasing leaves unchanged). `Tokenizer.FromWordPieceVocabulary(path, new WordPieceVocabularyOptions { LowerCase = …, StripAccents = … })` states the casing instead (`StripAccents` null follows `LowerCase`); prefer it, or the model's `tokenizer.json`, whenever the model's configuration is known.

## SentencePiece `.model` reader

The file is a protocol-buffer message; the reader follows the published schema (pieces with score and type; the trainer's model type, unknown ID and byte fallback; the normalizer's name, compiled table and whitespace flags) and skips other fields. Every length is checked against the data before anything is allocated. Limits: 128 MiB, 4 million pieces, 4096 bytes per piece.

- Unigram models only. A BPE-type model throws `NotSupportedException` until the BPE model is available; word and character models are refused. Models that keep runs of spaces or do not escape spaces are refused.
- The pipeline is the one the converted `tokenizer.json` files of such models have: the model's compiled table as the `Precompiled` normalizer, `WhitespaceSplit` then `Metaspace` (U+2581, prefix space per the model), the Unigram model, and the `Metaspace` decoder.
- The lattice holds ordinary and user-defined pieces; control, unknown, unused and byte pieces are never matched in text (the byte pieces serve byte fallback). User-defined pieces are also added tokens (not special, not normalized), so they are never split.
- No end-of-sequence token is added and control symbols written in the text are ordinary text, as in the SentencePiece library; a converted `tokenizer.json` differs there.
