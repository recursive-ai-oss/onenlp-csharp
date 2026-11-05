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
   - **Elision** (French): when the text before the first apostrophe is a listed word (`c d j l m n s t qu jusqu lorsqu puisqu quoiqu`), the elided word, apostrophe included, is a token and the rest is segmented again: `l'homme` is `l'` `homme`, `qu'est-ce` is `qu'` `est` `-ce`. Words whose prefix is not listed, and listed whole forms, stay whole (`aujourd'hui`, `quelqu'un`, `l'on`, `c'est-à-dire`).
   - **Hyphenated pronouns** (French): a token that ends in a hyphen and a listed pronoun splits at the hyphen, the hyphen going with the pronoun (`dis-moi` is `dis` `-moi`, `a-t-il` is `a` `-t-il`, `donne-le-lui` is `donne` `-le` `-lui`). `-ci` and `-là` split off except after `celui celle celles ceux jusque par`. Other hyphenated words, and listed whole forms such as `rendez-vous`, stay whole (`peut-être`, `lui-même`, `week-end`).
   - **Contractions that are multiword tokens** (French): `du au aux duquel auquel desquels desquelles auxquels auxquelles` are cut at fixed lengths into the pieces of the multiword token they are (`du` is `d` `u`); the pieces are what a model tags. `des` is cut the same way (`de` `s`) after a word that is not on a list of verbs, prepositions and conjunctions (`tools/fetch/fr/des_context.py` draws it from the training split) and not after punctuation, and is one token (the plural article) otherwise. `SegmentationData.TryMatchMultiword` gives the words a group of pieces stands for (`de` `le`), so that output can be written as a CoNLL-U multiword token.
   - **Numbers** (French): digits in groups of three separated by a space (`25 785`, `1 234 567`) are one token, which holds the space; a minus sign after white space and before a digit belongs to the number; `20h30` is `20` `h` `30`; a capital letter and its full stop before another capitalized word (`V. Prebeg`) is one token; `n°` is one token.
5. Offsets are UTF-16 offsets into the original string. Nothing is normalized. A lone surrogate is a one-character token.
6. Work is linear in the text. The look-ahead for a URL, e-mail local part or domain is capped as above, and there is no regular expression.

## Sentences

The `sentencizer` component writes sentence starts from the sentence boundaries of UAX #29 section 5, snapped to tokens: a sentence starts at the first token that begins at or after a Unicode boundary. Token 0 always starts one. Data may list tokens after which no sentence starts (English `Mr.`, `Dr.`). It is rule-based: UAX #29 does not break before a lower-case word after a full stop, nor split web text that has no end punctuation, so it scores below a trained segmenter.

## Measurement

`tools/eval/segmentation.sh` scores both against a UD split with `tools/OneNlp.Eval` (`segment` writes the CoNLL-U, `ud` scores it with the CoNLL 2018 metric definitions; the shared-task script can be run beside it as a cross-check), and against spaCy's rule `sentencizer` on `spacy.blank("en")`. The raw text is the `# text` lines joined by single spaces. The `mwt` variant writes the pieces of a split contraction as a multi-word token line, as English UD treebanks annotate them. The English data was tuned on the English-EWT development split only, and the French data on the French-GSD development split only; each test split was scored once.

## German data (`SegmentationData.German`, name `german`)

Written from the UD German guidelines and the German-GSD treebank; the choices below were made on its training and development files only.

- **Preposition-article contractions** (`zum`, `zur`, `im`, `am`, `vom`, `beim`, `ins`, `ans`, `ums`, `aufs`, `übers`, `durchs`, `fürs`, `hinterm`, `überm`, `unterm`, `vorm`, any case) are cut into two pieces of nearly equal length, longer first (`zu`|`m`, `be`|`im`), which is how the CoNLL-U reader cuts the surface form of a multiword token whose words do not spell it (`docs/data-formats.md`). A consumer that needs UD words writes the words back (`zu` `dem`); the recipe's `predict` does, from a table of the treebank's words.
- **Abbreviations** keep their full stop: the listed words, dotted single letters (`z.B.`, `u.a.`, `d.h.`) and single letters (`Johann S. Bach`, `z. B.`). The list holds the words GSD keeps joined to their stop more often than not (`Dr.`, `St.`, `ca.`, `etc.`, `Prof.`); `usw.` and `bzw.` are split there and so are not listed.
- **Ordinals**: one to four digits and a full stop are one token (`3.`) when the next word is a month name, one of a few listed nouns (`Quartal`, `Jahrhundert`, `Weltkrieg`), or starts in lower case; otherwise the stop ends a sentence and is its own token (`im Jahr 2010.`). No sentence ends after an ordinal token.
- **Hyphens** between letters split (`US-Soldaten` is `US` `-` `Soldaten`); between digits they stay (`1-2`).
- **Marks**: a run of one repeated mark is one token (`...`, `--`, ` `` `, `''`), but `!` and `?` are always single tokens, as GSD writes them; mixed runs do not form (`.''` is a stop and a quote). `'s` after a stem splits off (`geht's`).
- Sentences do not break after the listed titles and abbreviations (`Dr.`, `Prof.`, `St.`, `Nr.`, `z.B.`).
- Dates and numbers with `.` or `,` between digits (`24.12.2020`, `1.234,56`) stay whole by UAX #29.

Development split of UD German-GSD, raw text (`recipes/de/fast segment`): tokens 99.81 [99.69, 99.91], words 99.81, sentences 94.64.

## Turkish data (`SegmentationData.Turkish`, name `turkish`)

Written from the tokenization of UD Turkish-BOUN; every choice below was made on its training and development files only (`tools/fetch/tr/`).

- **Apostrophes** stay inside the token: a suffix written after a name or a number is part of it (`Ankara'da`, `Ali'nin`, `1882'den`). UAX #29 already keeps a letter, apostrophe, letter run together; between a digit and a letter it cuts, so the data joins that case.
- **Hyphens** are always tokens of their own, between letters (`Türk-Arap`) and between digits (`1-2`), as BOUN writes them.
- **A full stop that closes a quotation** is one token with the closing quote (`gel."`), the more common form in BOUN (91 against 42 separate).
- **Ordinals**: one to four digits and a full stop are one token before a lower-case word or a month name (`3. Ocak`); otherwise the stop is its own token (`2001.`).
- **Multiword tokens.** BOUN writes a copula or a suffix such as `-ki`, `-ken`, `-ydi` that is spelled onto its word as a word of its own: `vardır` is `var` + `dır`, `yaparken` is `yapar` + `ken`. Whether it is a word of its own depends on the stem and the treebank is not consistent about it, so no word list decides. The tokenizer reads a table of word endings (`src/OneNlp/Segmentation/Data/tr.mwt.tsv`, 119 rows, written by `tools/fetch/tr/mwt_table.py` from the training split): the last two to nine letters of a lower-cased token (Turkish case rules: `I` is `ı`, `İ` is `i`) map to the length of the final piece, the longest ending that matches decides, and a token that is not all letters is never cut. A row is kept when the ending occurs at least three times in the training split and at least 90% of those tokens are cut the same way (`tools/fetch/tr/mwt_sweep.py` chose the two thresholds on the development split by the Words F1 they give). On the development split the table makes 119 of the treebank's 266 cuts and 87 cuts the treebank does not make. A cut word is written back as one multiword token with its two words (`SegmentationData.TryMatchMultiword` finds the pieces).
- No abbreviation list: only six tokens of the training split keep a full stop inside or after letters (`Dr.` twice), so every full stop after a word is a token of its own.
