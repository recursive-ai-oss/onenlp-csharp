# Binary document format

Normative and language-neutral. Two containers share one body encoding: a single document (`ONDC`) and a bin of many documents (`ONDB`) that share a string table. Version 1.

## Conventions

- Integers are little-endian. `u16` and `u32` are fixed width.
- A **varint** is LEB128: seven bits per byte, low bits first, the high bit set while more bytes follow. A value is at most 32 bits and at most 5 bytes; a longer or larger one is invalid.
- A **signed varint** is the zig-zag mapping `(v << 1) ^ (v >> 31)` written as a varint.
- A **float** is an IEEE 754 binary32 stored as its `u32` bit pattern, so confidences round-trip bit for bit.
- A **string** is a varint `h` followed by `h >> 1` bytes. If `h & 1` is 0 the bytes are UTF-8 and must be valid; if 1 they are UTF-16LE code units (an even byte count) and may hold unpaired surrogates. A writer uses UTF-8 whenever the string is well-formed.
- A **section** is `u8 id`, `u32 length`, then `length` bytes. Sections inside one body appear in increasing id order, each at most once. A reader skips an id it does not know.

## Header (12 bytes, both containers)

| Offset | Size | Field |
|---|---|---|
| 0 | 4 | magic: `ONDC` (one document) or `ONDB` (a bin) |
| 4 | 2 | format version, 1 |
| 6 | 2 | flags: bit 0 = a CRC-32 trailer follows the body; every other bit must be 0 |
| 8 | 4 | body length: the bytes after the header, not counting the trailer |

The optional trailer is a `u32` CRC-32 (IEEE 802.3, polynomial 0xEDB88320, reflected, as in zip and PNG) of every byte before it, header included. The body length makes a single document self-delimiting, so documents written back to back can be read in turn from a stream.

## String table (section 1)

`varint count`, then `count` strings. Table index 0 is the empty string and is not stored; entry *i* (1-based) is the *i*-th stored string. The table holds only strings the documents use: attribute values and entity labels, in order of first use.

## Document body (sections 2–4)

All string-valued fields are table indexes (varints); 0 means "not set".

**Text (section 2).** One encoding byte (0 = UTF-8, 1 = UTF-16LE) and the text. UTF-16 is used only when the text has an unpaired surrogate.

**Tokens (section 3).** In this order:

1. `varint n`, the token count.
2. For each token, two varints: the gap from the end of the previous token (for the first token, from the start of the text) to this token's start, and its length. Both count UTF-16 code units. Lengths are at least 1 and every token ends inside the text. Whether a space follows a token is derived from the text, not stored.
3. Six attribute columns, each `n` string indexes, in `TokenColumn` order: norm, lemma, pos, tag, dep, morph.
4. Heads: `n` signed varints, each the head's token index minus the token's own index (0 for a root).
5. Sentence boundaries: `ceil(n / 4)` bytes, two bits per token, low bits first: 0 unset, 1 start, 2 inside (3 is invalid).
6. Confidences: a mode byte. 0: every confidence is exactly 1.0 and nothing follows. 1: `n` floats follow.

The section must end exactly there.

**Entities (section 4).** Present only when entity annotation is set (a document with tokens whose `EntIob` is set; an empty list is a document with no entities). `varint count`, then per entity in text order: the gap in tokens from the previous entity's end (or from 0), its length in tokens (at least 1), the label's table index (not 0) and a float confidence. Entities do not overlap.

## Single document (`ONDC`)

Header, string table section, then sections 2–4, then the optional trailer.

## Bin (`ONDB`)

Header, string table section (shared by every document), then section 5 and the optional trailer. Section 5 holds `u32 count` and `count` records, each a `u32 length` followed by a document body (sections 2–4). The section's length must equal the body bytes that remain, and the records must fill it exactly. A reader may take the records one at a time (`DocBinReader`); it verifies the checksum after the last.

## Reader limits

A reader checks every length and count against the bytes that remain before it allocates, and rejects the input with `InvalidDataException` when:

- the magic, version or flags are wrong, or the input is shorter than the length it declares, or has bytes after the end;
- a section is out of order or repeated, or does not end where its contents do;
- the string count exceeds the bytes in the table, a string is invalid UTF-8, or an index is beyond the table;
- the token count exceeds 33,554,432 or needs more than 9 bytes per token of the section;
- a token is empty or outside the text, a head is outside the document, an entity is empty, unordered, overlapping or unlabelled, a confidence is outside 0 to 1, or a boundary value is 3;
- the checksum does not match.

From a stream, a reader takes at most a declared length of 1 GiB and reads it in 1 MiB steps, so a false length cannot make it allocate more than the stream delivers.
