# Rule matching

How `Matcher`, `PhraseMatcher` and `EntityRuler` behave. The token-pattern vocabulary (attributes, operators, predicates) follows the one spaCy documents for its rule-based matching; where that description is silent, behaviour was settled by running spaCy 3.8.18 on probe inputs, and the cases are kept as a differential test (see Verification).

## Token patterns

A pattern is a list of positions. A run of tokens matches when each position, in order, accepts the tokens assigned to it. A position is a set of attribute tests that must all hold (an empty set accepts any token) and an operator that says how many tokens it covers.

### Operators

| Operator | Tokens covered |
|---|---|
| none | exactly one |
| `!` | exactly one token that does **not** satisfy the tests |
| `?` | zero or one |
| `+` | one or more |
| `*` | zero or more |
| `{n}` `{n,m}` `{n,}` `{,m}` | exactly n; n to m; n or more; up to m |

- Bounds are at most 1000, `m >= n`, `m >= 1`. A pattern may expand to at most 10,000 token slots and has at most 1000 positions.
- `OP` must be left out for exactly one; an empty `OP` is an error.
- Matching is exhaustive: every start and every end that the pattern can reach is a match, so `a+` over `a a a` gives six matches. A match covers at least one token; a pattern that could match nothing yields nothing at that point.
- Patterns run as a non-deterministic automaton without backtracking over tokens, so time is linear in the document for a given pattern.

### Attributes

| Name (any letter case in files) | Value | Meaning |
|---|---|---|
| `ORTH`, `TEXT` | string | the text as written |
| `LOWER` | string | the text lower-cased with the invariant culture |
| `NORM` | string | the document's norm column; the lower-cased text where none is set |
| `LENGTH` | integer | number of Unicode code points |
| `LEMMA`, `POS`, `TAG`, `DEP`, `MORPH` | string | the document's columns; empty where no component set them |
| `ENT_TYPE` | string | label of the entity containing the token, empty outside entities |
| `ENT_IOB` | `B` `I` `O` or empty (or 3 1 2 0) | position in an entity; empty when the document has no entity annotation |
| `SHAPE` | string | word shape, below |
| `IS_ALPHA` `IS_ASCII` `IS_DIGIT` `IS_LOWER` `IS_UPPER` `IS_TITLE` `IS_PUNCT` `IS_SPACE` `IS_CURRENCY` `IS_LEFT_PUNCT` `IS_RIGHT_PUNCT` `IS_QUOTE` `IS_BRACKET` | boolean | orthographic flags (`TokenText`) |
| `LIKE_NUM` `LIKE_URL` `LIKE_EMAIL` | boolean | resemblance flags |
| `IS_STOP` | boolean | lower-cased text is in `Matcher.StopWords` (empty by default) |
| `IS_SENT_START`, `SENT_START` | boolean | the token starts a sentence; token 0 always does |
| `SPACY` | boolean | the token is followed by white space |

Not supported: extension attributes (`_`), `ENT_ID`, `ENT_KB_ID`, dependency patterns.

### Predicates

A value may be an object of predicates instead of a plain value:

- `REGEX` (string attributes): a .NET regular expression searched anywhere in the value. The linear-time engine runs it when the expression allows; look-around and back-references fall back to the backtracking engine with a one-second timeout, and a timed-out test counts as no match.
- `IN`, `NOT_IN`: a list of strings (string attributes except `ENT_IOB`), or of integers (`LENGTH`).
- `IS_SUBSET`, `IS_SUPERSET`, `INTERSECTS` (`MORPH` only): the token's features, split on `|`, compared as a set with the listed `Key=Value` features.
- `==`, `!=`, `>=`, `<=`, `>`, `<` (`LENGTH` only): integer comparisons.

A plain `MORPH` string must equal the whole feature string; the empty string never matches (use `NOT_IN` or `IS_SUBSET` to find tokens without features).

### Word shape

Each letter becomes `X` (upper case) or `x`, each digit `d`, every other character stays, and a run of the same output character is cut after four. `Hello` is `Xxxxx`, `12-3456` is `dd-dddd`, `123456` is `dddd`. Letters, case and digits are Unicode properties of code points; title-case letters count as `x`.

### Several patterns, keys, greedy filtering, callbacks

- Patterns are added under a key. A key with several patterns matches when any does; the same range found by two patterns of one key is reported once. Keys do not affect each other.
- `GreedyMode.First` and `GreedyMode.Longest` thin one key's overlapping matches to a non-overlapping set. First prefers the earliest start, then the longer match; Longest prefers the longer match, then the earlier start. Adjacent matches do not overlap.
- Results are ordered by end token, then start token, then the order keys were added. (spaCy's documentation does not define an order; the differential test compares sets.)
- A callback registered for a key runs once per match of that key, after matching, with the whole result list.

## PhraseMatcher

Compares one attribute (`Orth` by default; `Lower`, `Norm`, `Shape`, `Lemma`, `Pos`, `Tag`, `Dep`, `Morph`) between phrase and document tokens, with phrases stored in a trie. All occurrences are reported, overlapping and nested ones included, ordered like `Matcher` results. A phrase added under two keys is reported for each key. Phrases given as text are split by the matcher's word tokenizer, so use the one that tokenizes your documents. Attributes other than `Orth`, `Lower`, `Norm` and `Shape` need phrase documents carrying that annotation; an unannotated phrase token is an error.

## EntityRuler

A pipeline component, registered as `entity_ruler`, with settings `overwrite_ents`, `phrase_matcher_attr` (`ORTH`, `LOWER`, `NORM`, `SHAPE`) and `patterns_path`.

1. Every token pattern and phrase is matched.
2. Matches are taken longest first; ties go to the earlier start, then to the rule added first. A match overlapping one already taken is dropped.
3. A match that overlaps an entity already in the document is dropped, unless `OverwriteEnts` is set, in which case it is kept and each existing entity it overlaps is removed.
4. The document's entities become the surviving existing entities plus the new ones, in text order. Rule entities have confidence 1.

A rule's ID is not stored in the document; `FindEntities` returns it with each entity. When two rules with different labels or IDs match exactly the same tokens, the rule added first wins (spaCy's documentation does not say, and its choice is not stable enough to copy).

## Pattern files

Token pattern: a JSON array of objects, e.g. `[{"LOWER": "hello"}, {"IS_PUNCT": true, "OP": "?"}, {"LOWER": {"IN": ["world", "there"]}}]`.

Entity rules: a JSON array of objects or JSON Lines, each `{"label": "ORG", "pattern": "Acme Corp", "id": "acme"}` where `pattern` is a phrase string or a token pattern and `id` is optional. Other keys are ignored. `EntityRuler.SavePatterns` writes JSON Lines.

Readers reject, with `PatternFormatException` and before allocating for them: nesting deeper than 16, more than 1000 positions in a pattern, more than 64 tests on a position, regular expressions longer than 4096 characters or with a counted repeat above 999, repeat bounds above 1000, unknown attributes or predicates, wrong value types, invalid UTF-8, truncated input, and (for streams) input over a byte limit (default 256 MiB). JSON Lines errors name the line.

## Differences from spaCy

- **Regular expressions** use the .NET dialect and run over UTF-16 units: `.` and `{n}` count a character outside the Basic Multilingual Plane as two, and `\w` does not include numeric characters such as `½`.
- **Lower-casing** uses simple per-character invariant mapping, so Python's context rules (final sigma, `İ`) do not apply.
- **Lexical flags** follow the Unicode data of the .NET runtime, which can be newer than the Python build's. `LIKE_URL`, `LIKE_EMAIL` and the number-word list of `LIKE_NUM` are written from observed behaviour and agree on every probe string in the test set, but are not claimed identical for all inputs.
- **Unset annotation:** spaCy raises when `LEMMA`, `POS`, `TAG` or `DEP` is tested on an unannotated document; here the value is the empty string.
- **Stop words:** no list ships; set `Matcher.StopWords`.
- **Order of results** is defined above; spaCy's is not documented.

## Verification

`tests/OneNlp.Tests/Matching/` holds the generated documents, patterns, phrase lists and rule sets with spaCy 3.8.18's answers (`cases.json.gz`, `expected.json.gz`); `tools/oracles/spacy_matcher` regenerates them. Tokens come from the cases, so tokenization does not enter the comparison. The test compares match sets for every pattern, phrase list and rule set, and the lexical flags and shapes for every case string and, for the single-character properties, every code point below U+20000 that the Python build assigns.
