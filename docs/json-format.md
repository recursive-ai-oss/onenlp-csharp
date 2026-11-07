# JSON document format

Normative and language-neutral. A document is a JSON object in the shape spaCy documents for `Doc.to_json`, so documents can move between the two. Fields of ours are additions that other readers can ignore (spaCy 3.8.18's `Doc.from_json` does).

```json
{
  "text": "Ada Lovelace wrote notes in London.",
  "ents": [{"start": 0, "end": 12, "label": "PERSON"}],
  "sents": [{"start": 0, "end": 35}],
  "tokens": [{"id": 0, "start": 0, "end": 3, "tag": "NNP", "pos": "PROPN", "morph": "Number=Sing",
              "lemma": "Ada", "dep": "compound", "head": 1}]
}
```

## Fields

| Field | Meaning |
|---|---|
| `text` | the original text |
| `tokens[]` | one object per token, in order: `id` (its index), `start`, `end` (end exclusive), and `tag`, `pos`, `morph`, `lemma`, `dep`, `head` |
| `ents[]` | `start`, `end`, `label`; written only when entities have been set (an empty list means "set, none found") |
| `sents[]` | `start` (first token's start) and `end` (last token's end) of each sentence; written only when sentence boundaries have been set |

The writer emits `tag`, `pos`, `morph`, `lemma` and `dep` for every token when any token has the value, `head` when any token has a dependency or a head other than itself, and omits an annotation nobody set. A token that is its own head is a root. The reader treats a missing or empty annotation as not set and a missing `head` as the token itself.

**Categories and span groups.** `cats` is an object from label to score (0 to 1, in the order the component wrote them), written when a classifier has set categories. `spans` is an object from group name to a list of `{start, end, label}` with code point offsets as for `ents`; a span may overlap or nest in another of its group, and keeps `conf` when not 1. spaCy's extra span fields (`kb_id`, `token_start`, `token_end`) are skipped on reading. A document without them writes neither field.

**Our additions.** Token `norm` (the normalized form), token `conf` (the confidence of the token's tag; written when not 1, read as 1 when missing), token `sb` (the sentence boundary: 1 starts a sentence, 2 is inside one; a token with no `sb` has an unset boundary) and entity `conf`. When no token has `sb` and `sents` is present (a document from spaCy), the first token of each sentence is a start and every other token is inside.

## Offsets

`start` and `end` count Unicode code points, as spaCy's do (Python string indexes). A `Doc` counts UTF-16 code units; the two differ only after a character outside the Basic Multilingual Plane, and the writer and reader convert. A character offset in `ents` and `sents` must fall on a token boundary.

## Not carried

JSON text cannot hold an unpaired surrogate, so writing a document whose text or a label has one throws `InvalidOperationException`; use the binary form. A token boundary inside a surrogate pair has no code point offset and throws the same way. `user_data` and unknown fields are skipped on reading, because a `Doc` does not store them.

## Reading limits

The reader accepts UTF-8 with an optional byte-order mark. It rejects, with `InvalidDataException`: nesting deeper than 32, trailing data, wrong types, a token `id` that is not its index, a `head` outside the document, a `conf` outside 0 to 1, an `sb` other than 0, 1 or 2, and any offset beyond the text. From a stream it takes at most 512 MiB.
