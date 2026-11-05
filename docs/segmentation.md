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

## Spanish data (`SegmentationData.Spanish`, name `spanish`)

Written from the UD Spanish guidelines and the AnCora treebank; the choices below were made on its training and development files only.

- **Contractions** `del` (`de` + `el`) and `al` (`a` + `el`), any case, are cut into two pieces, which is how the CoNLL-U reader cuts a surface form its words do not spell (`de`|`l`, `a`|`l`; `docs/data-formats.md`).
- **Verbs with enclitic pronouns** are multiword tokens in AnCora (`conseguirlo` = `conseguir` + `lo`, `situándose` = `situando` + `se`). A token is cut when it ends in one to three of `me te se lo la le nos les las los`, what is left is an infinitive (`-ar -er -ir`) or a gerund (`-ando -iendo -yendo`) once the accent written for the stress is removed, and it is not on a short list of words the training file writes whole although they look like a verb and a pronoun (`parte`, `fuerte`, `muerte`, `Carlos`; the 47 words that occur more often whole than split). The pieces are the words when they spell the surface form (`conseguir`|`lo`), otherwise nearly equal parts (`situá`|`ndose`), as the reader cuts them. `os` is not an enclitic here: `-ros` and `-tros` words are far more common than second-person-plural forms. Imperative and subjunctive forms (`dámelo`, `véase`: about 45 of 1,540 clitic tokens in the training file) are not recognized. `SegmentationData.SplitMultiwordToken` gives the words (`encontrárnoslas` is `encontrar` `nos` `las`; `reírse` keeps its accent).
- **Abbreviations** keep their full stop: single letters (`W.`, `A.`), `fig.`, `EE.UU.`, `Co.`, `kg.` and the usual titles (`Sr.`, `Dra.`, `Prof.`, `Avda.`, `núm.`). Newswire AnCora has few of them. Sentences do not break after the titles.
- **Inverted `¿` and `¡`** are tokens of their own (UAX #29 already separates them). Numbers with `.` or `,` between digits (`2.000`, `3,5`) stay whole; hyphenated names (`Ruiz-Mateos`) stay whole, as in the neutral defaults; apostrophe names (`d'Hebron`) stay whole by UAX #29.

Development split of UD Spanish-AnCora, raw text (`recipes/es/fast segment`): tokens 99.92 [99.88, 99.95], words 99.91 [99.87, 99.94], sentences 94.89 [93.87, 95.90] (rules alone; the parser sets sentences in the pipeline). What is left: team names joined by a slash (`Reynard/Honda`, 11 tokens), quotes glued to a word, and `km.24`.
