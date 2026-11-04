# Data formats

Normative for the readers in `RecursiveAI.OneNlp.Data`. Both readers stream (one sentence or line at a time), bound every size before allocating, and reject malformed input with a `DataFormatException` that carries the 1-based line number (`LineNumber`; the message starts `line N:`).

## CoNLL-U

Per the CoNLL-U format specification (universaldependencies.org/format.html). `ConllUReader.Read` yields a `ConllUSentence` per sentence; `ConllUReader.ReadExamples` yields `Example`s.

**Input.** UTF-8 (a leading byte-order mark is skipped; invalid bytes are an error). Lines end at LF or CR LF. A blank line ends a sentence; the last sentence needs no blank line after it. Limits (`ConllUOptions`): line 1,048,576 UTF-16 units, 100,000 word and multiword-token lines per sentence, 10,000 comment lines per sentence.

**Lines.**
- `#` lines are comments. They must come before the first word of a sentence. They are kept as written (`Comments`); `GetComment("sent_id")` reads `# key = value`.
- A word line has exactly 10 tab-separated columns. IDs are `1, 2, 3, ...` in order. FORM must not be empty. HEAD is an integer from 0 to the number of words, or `_`; a word cannot be its own head. `_` in LEMMA, UPOS, XPOS, FEATS, DEPREL, DEPS and MISC means "not set".
- A multiword token line has an ID range `a-b` where `a` is the next word and `b > a`; its words follow immediately. Ranges do not nest or overlap.
- An empty node line has an ID `N.M`. It is kept (position and text) and has no token in the document.
- A sentence of only comments, or an empty node with no word, is an error.

**The document.** One token per syntactic word. `Doc.Text` is the surface text: the surface forms (a multiword token's range form, else the word's form) joined by one space, except after a token whose MISC has `SpaceAfter=No` (on the range line for a multiword token). Columns: LEMMA to `Lemma`, UPOS to `Pos`, XPOS to `Tag`, FEATS to `Morph` (as written), DEPREL to `Dep`; HEAD 0 and `_` both make the word its own head (a root); the first token is a sentence start and the rest are inside the sentence.

**Words of a multiword token** need characters of their own, since a `Doc` token is a non-empty slice of the text. If the word forms concatenate to the surface form (`do` + `n't` = `don't`), each word covers its own form. Otherwise (`de` + `el` for `del`) the surface form is cut into pieces of nearly equal length, longer pieces first. The word's own form stays in `ConllUSentence.Forms`.

**Kept beside the document** so that `ConllUWriter.Write` gives the sentence back exactly: comments, `Forms`, `Deps`, `Misc` (as written), `MultiwordTokens`, `EmptyNodes`. Reading and writing a UD file reproduces it byte for byte when it uses LF and a blank line after each sentence (checked on English-EWT train, dev and test). `ConllUWriter.Write(writer, doc)` writes any document, for example a prediction.

## JSON Lines

`JsonlReader.Read` yields one `Example` per non-blank line. A line is one JSON object (nesting depth at most 16; at most 16,777,216 UTF-16 units by default); unknown properties are ignored.

| Property | Type | Meaning |
|---|---|---|
| `text` | string, required | The text. |
| `tokens` | optional array of `[start, end]` | Token boundaries, UTF-16 offsets into `text`, `end` exclusive, in order, non-empty, not overlapping. Without it the reader's tokenizer (default `RuleWordTokenizer`) splits the text. |
| `entities` | optional array of `[start, end, "LABEL"]` | Gold entities, character offsets. Must not overlap. Their presence, even as `[]`, marks the document as entity-annotated (every other token is outside an entity); absence leaves the entity column unset. Stored in the reference `Doc`. |
| `spans` | optional object, name to array of `[start, end, "LABEL"]` | Span groups; spans may overlap. Stored as token ranges in `Example.Spans`. |
| `cats` | optional object, label to number (or boolean) in `[0, 1]` | Text categories (multi-class or multi-label). Stored in `Example.Cats`. |

A span or entity whose edges are not token boundaries is an error, or is dropped when `JsonlOptions.Misaligned` is `Skip`. Labels are non-empty. Offsets must lie inside `text` with `start < end`.

```json
{"text": "Ada met Bob in Paris.", "entities": [[0, 3, "PER"], [8, 11, "PER"], [15, 20, "LOC"]], "cats": {"travel": 1}}
{"text": "Hi there", "tokens": [[0, 2], [3, 8]]}
```

## Example alignment

`Example.Alignment` aligns the predicted and reference documents when they are tokenized differently. The texts must be equal after whitespace is removed and case is ignored. Each token covers a range of the remaining characters; two tokens are aligned when their ranges overlap. `Example.GetAligned(column)` gives the reference value to a predicted token only when it overlaps exactly one reference token; `GetAlignedEntities()` keeps a reference entity only when both its edges are predicted token boundaries.
