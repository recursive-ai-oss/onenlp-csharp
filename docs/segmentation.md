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

`tools/eval/segmentation.sh` scores both against a UD split with `tools/OneNlp.Eval` (`segment` writes the CoNLL-U, `ud` scores it with the CoNLL 2018 metric definitions; the shared-task script can be run beside it as a cross-check), and against spaCy's rule `sentencizer` on `spacy.blank("en")`. The raw text is the `# text` lines joined by single spaces. The `mwt` variant writes the pieces of a split contraction as a multi-word token line, as English UD treebanks annotate them. The English data was tuned on the English-EWT development split only, the French data on the French-GSD development split only, and the Dutch data on the Dutch-Alpino development split only; each test split was scored once.

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
- **Verbs with enclitic pronouns** are multiword tokens in AnCora (`conseguirlo` = `conseguir` + `lo`, `situándose` = `situando` + `se`). A token is cut when it ends in one to three of `me te se lo la le nos les las los`, what is left is an infinitive (`-ar -er -ir`) or a gerund (`-ando -iendo -yendo`) once the accent written for the stress is removed, and it is not on a short list of words the training file writes whole although they look like a verb and a pronoun (`parte`, `fuerte`, `muerte`, `Carlos`; the 47 words that occur more often whole than split). The pieces are the words when they spell the surface form (`conseguir`|`lo`), otherwise nearly equal parts (`situá`|`ndose`), as the reader cuts them. `os` is not an enclitic here: `-ros` and `-tros` words are far more common than second-person-plural forms. Imperative and subjunctive forms (`dámelo`, `véase`: about 45 of 1,540 clitic tokens in the training file) are not recognized. `SegmentationData.TryMatchMultiword` gives the words (`encontrárnoslas` is `encontrar` `nos` `las`; `reírse` keeps its accent).
- **Abbreviations** keep their full stop: single letters (`W.`, `A.`), `fig.`, `EE.UU.`, `Co.`, `kg.` and the usual titles (`Sr.`, `Dra.`, `Prof.`, `Avda.`, `núm.`). Newswire AnCora has few of them. Sentences do not break after the titles.
- **Inverted `¿` and `¡`** are tokens of their own (UAX #29 already separates them). Numbers with `.` or `,` between digits (`2.000`, `3,5`) stay whole; hyphenated names (`Ruiz-Mateos`) stay whole, as in the neutral defaults; apostrophe names (`d'Hebron`) stay whole by UAX #29.

Development split of UD Spanish-AnCora, raw text (`recipes/es/fast segment`): tokens 99.92 [99.88, 99.95], words 99.91 [99.87, 99.94], sentences 94.89 [93.87, 95.90] (rules alone; the parser sets sentences in the pipeline). What is left: team names joined by a slash (`Reynard/Honda`, 11 tokens), quotes glued to a word, and `km.24`.

## Portuguese data (`SegmentationData.Portuguese`, name `portuguese`)

Written from the UD Portuguese guidelines and the Portuguese-Bosque treebank; the lists below were counted on its training and development files only.

- **Contractions** of a preposition with an article, pronoun or demonstrative (`do`, `da`, `no`, `na`, `ao`, `pelo`, `nesse`, `dele`, `daquilo`, `num`, any case; 78 forms) are cut into two pieces. A form is listed when the treebank writes it as a multiword token far more often than as a single word. The pieces are cut as the CoNLL-U reader cuts the surface form of a multiword token (`docs/data-formats.md`): the words when they spell it (`ao` is `a`|`o`), else nearly equal lengths, longer first (`do` is `d`|`o`, `pelo` is `pe`|`lo`, `nesse` is `nes`|`se`). A consumer that needs UD words writes them back (`de` `o`); the recipe's `predict` does, from the table `Contractions`. `nos` is listed although the pronoun `nos` also occurs (29 of 308 in the training file).
- **`à`** (`a` + `a`) is one character for two words, so it stays one token; the recipe trains on it as one word and writes the two words back.
- **Hyphenated words** stay whole (`quinta-feira`, `norte-americano`), except that a pronoun after a verb's hyphen (`se`, `me`, `te`, `lhe`, `lhes`, `nos`, `vos`, `o`, `a`, `os`, `as`, `lo`, `la`, `los`, `las`) is split off with its hyphen: `trata-se` is `trata` `-se`, `dá-lo` is `dá` `-lo`. Every single-hyphen form ending in one of these is a two-word multiword token in the treebank (counted on the training and development files). Forms with two hyphens (`dar-lhe-á`) stay whole. The recipe writes `-lo` back as the word `lo` or `o`, as the training file spells it.
- **Currency**: `US$`, `R$` and `CR$` are one token (`DollarWords`); the amount is the next token.
- **Abbreviations** keep their full stop: the listed words (`Dr.`, `Sra.`, `art.`, `pág.`, `etc.`, `Av.`, `Cia.`), dotted initials (`J.F.K.`) and single letters except `a`, `e` and `o`. Sentences do not break after the titles and abbreviations that precede a name or a noun.
- **Marks**: a run of one repeated mark is one token (`...`, `--`); `!` and `?` are single tokens. Decimal commas and thousands points stay inside the number (`5,50`, `2.200,00`).

Development split of UD Portuguese-Bosque, raw text (`recipes/pt/fast segment`, rule sentencizer): tokens 99.89 [99.83, 99.94], words 99.81 [99.74, 99.87], sentences 80.55 [78.27, 82.61].

## Dutch data (`SegmentationData.Dutch`, name `dutch`)

Written from the Dutch-Alpino treebank; the choices below were made on its training and development files only (`tools/fetch/nl/token_stats.py` prints the counts, `tools/fetch/nl/seg_diff.py` the differences against a gold file).

- **Apostrophes**: a word with an apostrophe inside stays whole by UAX #29 (`zo'n`, `z'n`, `auto's`, `Jan's`). The short forms that start with an apostrophe (`'t`, `'s`, `'n`, `'m`, `'ns`) and years without their century (`'69`) are one token when the apostrophe follows white space, punctuation or the start of the text and the word after it is exactly one of these; any other apostrophe is a quotation mark of its own (`'ja'` is `'` `ja` `'`). A hyphen after such a form continues the token (`'s-avonds`).
- **Hyphens** between letters or digits stay (`Zuid-Afrika`, `21-jarige`, `2-0`); a hyphen with white space around it is a token.
- **Abbreviations** keep their full stop: the listed words (`mr.`, `drs.`, `prof.`, `pct.`, `mln.`, `enz.`, `bijv.`), dotted single letters (`o.a.`, `b.v.`, `t.o.v.`, `N.V.`) and single letters (initials: `J. Bakker`). Sentences do not break after the titles, dotted abbreviations and initials that name a person or place (`mr. Van Dam`, `J. Bakker`).
- **Numbers**: a thousands point or decimal comma between digits stays inside the number by UAX #29 (`130.000`, `2,5`); `/` between digits does too (`1/2`); an amount in whole guilders with a trailing dash (`5,-`, `130.000,-`, `11.-`) is one token.
- **Marks**: a run of one repeated mark is one token (`...`, `,,` and `''` as quotation marks), but `!` and `?` are always single tokens.
- **Contractions**: none are cut. UD Dutch-Alpino has one multiword token (`nogeens`) in its training and development files, and the tokenizer leaves it whole.

Development split of UD Dutch-Alpino, raw text (`recipes/nl/fast segment`): tokens 99.78 [99.65, 99.89], words 99.78, sentences 81.60 (the rule sentencizer alone; the parser sets sentence starts in the full pipeline). The remaining differences are a few company names with `&` (`AZ&PC`), hyphens left open before a coordination (`mond- en keelholte`) and `,,`.
## Turkish data (`SegmentationData.Turkish`, name `turkish`)

Written from the tokenization of UD Turkish-BOUN; every choice below was made on its training and development files only (`tools/fetch/tr/`).

- **Apostrophes** stay inside the token: a suffix written after a name or a number is part of it (`Ankara'da`, `Ali'nin`, `1882'den`). UAX #29 already keeps a letter, apostrophe, letter run together; between a digit and a letter it cuts, so the data joins that case.
- **Hyphens** are always tokens of their own, between letters (`Türk-Arap`) and between digits (`1-2`), as BOUN writes them.
- **A full stop that closes a quotation** is one token with the closing quote (`gel."`), the more common form in BOUN (91 against 42 separate).
- **Ordinals**: one to four digits and a full stop are one token before a lower-case word or a month name (`3. Ocak`); otherwise the stop is its own token (`2001.`).
- **Multiword tokens.** BOUN writes a copula or a suffix such as `-ki`, `-ken`, `-ydi` that is spelled onto its word as a word of its own: `vardır` is `var` + `dır`, `yaparken` is `yapar` + `ken`. Whether it is a word of its own depends on the stem and the treebank is not consistent about it, so no word list decides. The tokenizer reads a table of word endings (`src/OneNlp/Segmentation/Data/tr.mwt.tsv`, 119 rows, written by `tools/fetch/tr/mwt_table.py` from the training split): the last two to nine letters of a lower-cased token (Turkish case rules: `I` is `ı`, `İ` is `i`) map to the length of the final piece, the longest ending that matches decides, and a token that is not all letters is never cut. A row is kept when the ending occurs at least three times in the training split and at least 90% of those tokens are cut the same way (`tools/fetch/tr/mwt_sweep.py` chose the two thresholds on the development split by the Words F1 they give). On the development split the table makes 119 of the treebank's 266 cuts and 87 cuts the treebank does not make. A cut word is written back as one multiword token with its two words (`SegmentationData.TryMatchMultiword` finds the pieces).
- No abbreviation list: only six tokens of the training split keep a full stop inside or after letters (`Dr.` twice), so every full stop after a word is a token of its own.

## Chinese: trained word segmentation (`NeuralWordSegmenter`)

Chinese is written without spaces and UAX #29 gives one word per ideograph, so the words of the Chinese pipeline come from a trained model, not from rules, and not from `SegmentationData`. The segmentation is the one of the training treebank (UD Chinese-GSDSimp), which writes personal names as a surname token and a given-name token and cuts compounds finely.

- **Model.** Character tagging (Xue 2003; Peng, Feng and McCallum 2004): each unit is labelled `B` (a word starts here) or `I` (it continues the word before it). Two labels instead of the usual four (B, I, E, S) mean that every label sequence is a segmentation, so the most likely label of each unit is the answer and there is nothing to decode. The tagger is the ordinary `Tagger` over a `FastEncoder` whose tokens are the units; the encoder's neighbour, word-pair and wider-context hash tables give each unit its surrounding characters and its bidirectional LSTM gives it the sentence.
- **Units.** Each character that is not white space is a unit (a surrogate pair is one character), except that a run of Latin, Greek or Cyrillic letters, and a run of digits (a full stop or comma between two digits stays inside), is one unit, because the treebank keeps such runs whole. White space is never inside a word, and the first unit after it always begins a word. Gold word boundaries inside a unit cannot be represented; there are none in the training and development files.
- **Long texts** are cut into pieces of at most 1,024 units, after a sentence-ending mark (`。`, `！`, `？`, `；`, `!`, `?`, `;`, a line feed) in the last quarter of the piece when there is one, so the cost stays linear.
- **Where it plugs in.** The segmenter is the `IWordTokenizer` of the pipeline; a saved pipeline records it as the tokenizer kind `segmenter` (`docs/model-files.md`). Sentence boundaries come from the rule sentencizer's UAX #29 rules, which break after `。！？` and their closing quotes, or from the parser.
- **Training.** Reference documents have the units as tokens and `B`/`I` in the tag column (`NeuralWordSegmenter.Units`, `NeuralWordSegmenter.Tags`); eight consecutive treebank sentences, joined by a space, make one document; the tagger trains with the pipeline's `Train` extension; the recipe is `recipes/zh/fast` (`train-segmenter`).
- **Result.** UD Chinese-GSDSimp r2.18 test, raw text: tokens and words F1 89.62 [88.87, 90.42] (spaCy 3.8.18's default Chinese tokenizer, which cuts single characters: 39.43); sentences 99.40 [98.70, 99.90] with the parser. Published, for information: Stanza 92.14, UDPipe 2 90.13 (words), Trankit base 96.94 (older annotation of the same treebank) (`docs/references.md`).
