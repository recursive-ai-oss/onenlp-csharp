# Segmentation

Normative behaviour of the word tokenizer and the sentence segmenter. Language-neutral except where a language's data is named.

## Word tokens

1. The text is cut into word segments by the default word boundary rules of Unicode UAX #29 section 4 (the Unicode version OneFont pins). No tailoring.
2. A segment made only of white space (`char.IsWhiteSpace`) is dropped. Every other character belongs to exactly one token; white space belongs to none. `Token.HasSpaceAfter` recovers it.
3. With `SegmentationData.None` the tokens are exactly those segments. The conformance tests run every line of `WordBreakTest.txt` and `SentenceBreakTest.txt` through the layer in this mode.
4. With language data, each segment may be adjusted. All adjustments are data in `SegmentationData`, looked up by exact (case-insensitive) text or by one-character class, never by a pattern language:
   - **URLs and e-mail addresses** stay whole: `http://`, `https://`, `ftp://` or `www.` up to white space (at most 2048 characters), without trailing sentence punctuation or an unbalanced closing bracket; `local@domain.tld` with an ASCII local part (at most 256 characters) and a domain of at most 255 characters whose last label is at least two letters.
   - **Abbreviations** keep their full stop: a listed word (`Mr.`, `Inc.`) or dotted single letters (`U.S.`, `e.g.`).
   - **Hyphens**: a hyphen between two letters or digits joins them when the word before it is a listed prefix (`e-mail`, `anti-war`) and, for any language that lists none, always. Digits joined by a hyphen (`303-832-8160`) always join. Other hyphenated words split at the hyphen, as UD English does.
   - **Digit joiners** (`:` `/`) between two digits do not split (`3:30`, `1/2`).
   - **Mark runs**: a run of the marks `. - ! ? * = _ ~ < > # $ + / \` is one token (`...`, `--`, `?!`, `$$$`); a full stop that closes a run of other marks is its own token.
   - **Emoticons** from a short list stay whole.
   - **Contractions** (English): `n't` and the suffixes `'s 're 've 'll 'd 'm` after a letters-or-digits stem split off (`do` `n't`, `it` `'s`); the apostrophe may be `'` or U+2019. Fused forms split at fixed lengths (`can` `not`, `gon` `na`). `o'clock` and `rock'n'roll` stay whole.
5. Offsets are UTF-16 offsets into the original string. Nothing is normalized. A lone surrogate is a one-character token.
6. Work is linear in the text. The look-ahead for a URL, e-mail local part or domain is capped as above, and there is no regular expression.

## Sentences

The `sentencizer` component writes sentence starts from the sentence boundaries of UAX #29 section 5, snapped to tokens: a sentence starts at the first token that begins at or after a Unicode boundary. Token 0 always starts one. Data may list tokens after which no sentence starts (English `Mr.`, `Dr.`). It is rule-based: UAX #29 does not break before a lower-case word after a full stop, nor split web text that has no end punctuation, so it scores below a trained segmenter.

## Measurement

`tools/eval/segmentation.sh` scores both against a UD split with `tools/OneNlp.Eval` (`segment` writes the CoNLL-U, `ud` scores it with the CoNLL 2018 metric definitions; the shared-task script can be run beside it as a cross-check), and against spaCy's rule `sentencizer` on `spacy.blank("en")`. The raw text is the `# text` lines joined by single spaces. The `mwt` variant writes the pieces of a split contraction as a multi-word token line, as English UD treebanks annotate them. The English data was tuned on the English-EWT development split only; the test split was scored once.
