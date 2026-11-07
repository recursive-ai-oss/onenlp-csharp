# Tokenizer parity

OneNLP's tokenizers must equal the reference libraries byte for byte: Hugging Face `tokenizers` for the WordPiece, Unigram,
SentencePiece-BPE and byte-level BPE families, and `tiktoken` for the Tiktoken encodings. One mismatch is a bug, never a tolerance.
This page defines what is compared, and how to run the harness. The reference libraries are only ever run as installed
packages through their documented Python APIs.

## What is compared

For every record of the parity corpus the harness compares our results with a **golden** record written by the reference library.

| Check | Our call | Compared with |
|---|---|---|
| `encode.special`, `encode.plain` | `Encode(char span, addSpecialTokens: true/false)` | `ids`, `tokens`, `offsets` (UTF-16), `special_mask`, `type_ids`, `word_ids` |
| `encode.utf8.special`, `encode.utf8.plain` | `Encode(UTF-8 span, …)` | the same, with `offsets` in UTF-8 bytes of the caller's input |
| `encode.trunc`, `encode.utf8.trunc` | `Encode` with the truncation and padding below | the same plus `attention_mask`, over the padded length |
| `ids.char.*`, `ids.utf8.*` | `EncodeToIds` into an exact-size buffer, then into a buffer of half the size | `ids`, `status` (`Done`, then `DestinationTooSmall`), `written`, and `consumed` = end offset of the last written token |
| `ids.*` (count) | `CountTokens` | number of ids |
| `decode.skip`, `decode.keep` | `Decode(ids, skipSpecialTokens: true/false)` on the reference's ids | the reference's `decode` |
| `pair.special`, `pair.trunc` | `Encode(first, second, …)` (BERT and RoBERTa families) | the same fields as a single text, offsets into each text |

`Tokenizer.Truncation` and `Padding` are `null` for every check except the `trunc` ones.

### Offsets

Offsets are into the caller's original input, after any normalisation the tokenizer applied internally.
Hugging Face reports offsets as Python code-point indices. The oracle driver converts every offset to the unit of our API:
**UTF-16 code units** for the char API and **UTF-8 bytes** for the UTF-8 API. A code point above U+FFFF counts 2 units and 1 to 4 bytes.
A padding position has offsets `(0, 0)`.

`tiktoken` reports no offsets, so they are derived from the exact bytes of each token: the token covers the code points its
bytes touch, and a token that begins or ends inside a multi-byte character covers the whole character. The same rule applies to
the byte-level BPE families of Hugging Face, which report the span of the whole character for a partial-character token.

### Invalid input

Python strings cannot hold invalid UTF-8 or lone surrogates, so the oracle sees a **defined** replacement of the record:

- **Invalid UTF-8** (UTF-8 API only): each *maximal subpart* of an ill-formed sequence becomes one U+FFFD, as in the Unicode
  Standard section 3.9, "U+FFFD Substitution of Maximal Subparts". This is also what .NET's `UTF8Encoding` does, and what
  `Tokenizer`'s UTF-8 methods do. The offsets of the oracle's tokens are mapped back to byte positions in the *original* bytes,
  so a U+FFFD that stands for three invalid bytes spans those three bytes.
- **Lone surrogates** (char API only): each lone UTF-16 surrogate becomes one U+FFFD. It is one UTF-16 unit before and after,
  so offsets are unchanged.

### Special tokens

- Hugging Face: `add_special_tokens` is the post-processor's flag. A special token that appears *in the text* is encoded as that
  token and gets `special_mask` 0 and its own word index (observed in the output of `tokenizers` 0.22.1; only the tokens the
  post-processor adds are marked special).
- Tiktoken: `addSpecialTokens: true` means special-token text in the input is encoded as the special token
  (`allowed_special="all"`); `false` reads it as ordinary text (`encode_ordinary`). `special_mask` is 1 for ids in the encoding's
  special-token set. `decode` with `skipSpecialTokens: true` removes those ids first (tiktoken has no such flag).
- Tiktoken has no tokens strings, word ids, pair encoding, truncation or padding, so those checks do not exist for it.

### Truncation and padding

One configuration per family, alternating so both sides are exercised: truncate to 16 tokens **including** the special tokens,
from the right and pad on the left to length 24 with id 0 and token `<pad>`, or the reverse; the family list order picks which.
For pairs the strategy is `longest_first`, and the golden header records the configuration. Observed in the output of
`tokenizers` 0.22.1, for a pair with budget *n* (max length minus the 3 special tokens): the shorter sequence keeps at most
⌊n/2⌋ tokens and the longer one the rest, and when the two are equal the second counts as the longer.

## Inputs

The corpus is built deterministically (fixed seed) from two inputs and synthetic classes.

| Class | Source |
|---|---|
| `udhr-full`, `udhr-para` | Universal Declaration of Human Rights in Unicode, every language: the whole text, and four sampled paragraphs. Unicode licence. |
| `tatoeba` | Up to 200 sentences for every Tatoeba language with at least 200 (reservoir sample). Mostly CC BY 2.0 FR; fetched, never committed. |
| `empty`, `single-char` | the empty string; ~200 single characters across scripts, controls and planes |
| `cjk-run`, `hangul`, `emoji`, `combining`, `rtl-bidi` | CJK without spaces; jamo and syllables; ZWJ, skin tone, flag and keycap sequences; mark stacks; bidi controls |
| `whitespace`, `control`, `numbers`, `url-code` | whitespace runs (tab, NBSP, U+3000, CRLF…); controls, noncharacters, tags, private use; digits in several scripts; URLs, paths, code |
| `special-lookalike` | `[CLS]`, `<s>`, `<|endoftext|>`, partial and doubled forms, alone and inside text |
| `lone-surrogate` | char API only |
| `invalid-utf8` | UTF-8 API only |
| `long-line` | single lines of 1,000,000 characters: words, CJK, mixed scripts, one repeated character, whitespace, emoji |
| `tatoeba-pair`, `synthetic-pair` | pairs for the pair checks |

Every record has a class tag. Records longer than 100,000 code points are compared by **digest**: the golden stores, per field,
the count and the SHA-256 of the values (int32 little-endian; strings as an int32 length plus UTF-8), and the diff tool computes
the same. A digest mismatch names the field but not the position.

## Running it

All scripts take directories as arguments. Python is standard-library only for fetching and building; the oracle driver runs in
an environment that has the reference package installed (`pip install tokenizers`, `pip install tiktoken`).

```sh
# 1. fetch: tokenizer files at pinned revisions (tools/fetch/tokenizer-models.lock.json), then the raw text
python tools/fetch/fetch_tokenizers.py   --out $MODELS
python tools/fetch/fetch_parity_inputs.py --out $RAW

# 2. corpus: deterministic; two builds give the same corpus_sha256 in manifest.json
python tools/parity/build_corpus.py --udhr $RAW/udhr --tatoeba $RAW/sentences.csv --out $CORPUS

# 3. goldens: one <family>.golden.jsonl.gz each; the header records the oracle's self-reported version
python tools/parity/oracle.py --families tools/parity/families.json --models $MODELS --corpus $CORPUS --out $GOLDEN \
    --family bert-base-uncased --family gpt2        # omit --family for all; run tiktoken families with the tiktoken environment

# 4. diff
dotnet run --project tools/OneNlp.Parity -c Release -- --families tools/parity/families.json --models $MODELS \
    --goldens $GOLDEN --corpus $CORPUS --out $RESULTS [--family ID]... [--load vocab|sentencepiece|…]
```

The diff tool loads each family with `Tokenizer.FromFile`. A family whose loader does not exist yet reports `not supported` and
does not fail the run (`--require-supported` makes it fail). Per family it writes to `$RESULTS`:

- `<family>.summary.json`: records and pairs compared, comparisons made, mismatches by `check.field`, by input class, and by both,
  and the first few mismatches with details;
- `<family>.mismatch-tests.cs`: xunit tests built from the *shortest* failing inputs (one per distinct check and field before
  repeating), each stating the reference's value. They load the tokenizer through `ParityTokenizers.Load(familyId)`, which reads
  `ONENLP_TOKENIZER_MODELS` and skips when it is unset. Inputs are the corpus's own, not further shrunk: the reference is not
  available to the diff tool to produce expected values for a reduced input;
- `summary.txt`: one line per family.

The exit code is 0 when every family matched or has no loader, 1 on any mismatch or failed load, 2 on a usage error.
A full run is mostly the tokenizers' own time; run it per family (`--family`) to shard, and under the heavy queue on shared hosts.

`--load` picks another file of a family (`vocab` for `vocab.txt`, `sentencepiece` for the `.model` file, …), listed in
`tools/parity/families.json`. Those files do not carry the post-processing of `tokenizer.json`, so they are compared only where
the family's loader defines the same behaviour.

## Self-check

`tests/OneNlp.Tests/Parity/` holds a toy character-level tokenizer (`toy/tokenizer.json`, and a C# class written from it), a
small synthetic corpus and the golden file the reference library wrote for it. The tests show that the harness passes when the
tokenizer matches (all classes, pairs, truncation, padding, digests) and reports each injected defect: wrong ids, offsets,
special mask, decode, `consumed`, truncation side and the pair tie-break. To regenerate:

```sh
python tools/parity/build_corpus.py --profile toy --out tests/OneNlp.Tests/Parity/toy/corpus
python tools/parity/oracle.py --adhoc toy=tests/OneNlp.Tests/Parity/toy/tokenizer.json \
    --corpus tests/OneNlp.Tests/Parity/toy/corpus --out tests/OneNlp.Tests/Parity/toy --digest-min-chars 10000
```

## Golden file format

`<family>.golden.jsonl.gz` is gzip (mtime 0, so equal input gives equal bytes) of JSON lines. Line 1 is `{"header": …}`:
`format`, `family`, `oracle`, `oracle_version` (the library's self-reported `__version__`), `oracle_dist_version` (package
metadata), `python`, `tokenizer_sha256`, `vocab_size`, `corpus_sha256`, `digest_min_chars`, `trunc_pad` (the configuration, or
null), `has_pairs`, `has_word_ids`, `has_tokens`. Then one line per corpus record, in corpus order:
`{"id", "cls", "special", "plain", "trunc", "dec", "oracle_text"?}`, where each encoding holds `ids`, `tok`, `o16`, `o8` (absent
for records with no UTF-8 form), `sm`, `ty`, `wi`, and `am` (in `trunc`); `dec` holds `skip` and `keep`; `oracle_text` is the text
the oracle saw when it differs from the corpus record. Pair lines follow: `{"pair": id, "special", "trunc"}`.

## Release run

Full sweep over `~/onenlp/data/parity/golden/` for the release gate: 12 families, 47,981 corpus records each, 0 mismatches. Oracles as they report themselves: `tokenizers` 0.22.1, `tiktoken` 0.14.0.

| Family | Comparisons |
|---|---:|
| bert-base-uncased | 3,451,629 |
| bert-base-multilingual-cased | 3,451,661 |
| xlm-roberta-base | 3,451,681 |
| t5-small | 3,449,575 |
| mmBERT-base | 3,449,935 |
| TinyLlama | 3,449,935 |
| gpt2 | 3,449,919 |
| roberta-base | 3,452,041 |
| Qwen2.5-0.5B | 3,449,919 |
| ModernBERT-base | 3,449,935 |
| cl100k_base | 2,395,778 |
| o200k_base | 2,395,778 |

UAX #29 (Unicode 17.0.0): `WordBreakTest.txt` 1944 lines and `SentenceBreakTest.txt` 512 lines, all pass.
