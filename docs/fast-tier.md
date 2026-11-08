# The fast tier

The fast tier is a per-language pipeline built on a small hash-embedding encoder: each head reads vectors made from the hashed lower-cased form, prefix, suffix and shape of every token, mixed by a few window layers (and, for some languages, one bidirectional LSTM layer). It runs on one CPU thread with no GPU, loads in a fraction of a second and is the model behind `Nlp.Analyze` and `Nlp.Entities` when a language package is referenced. Every part is trained in C# with `Train` ([training.md](training.md)); the encoder and heads are specified in [contracts.md](contracts.md), the file layout in [model-files.md](model-files.md).

## What a language package contains

One saved pipeline (`PipelineFile`) of these parts:

- **Tokenizer.** UAX #29 words plus the language's exception data ([segmentation.md](segmentation.md)): elision and clitics, contractions cut into the pieces the treebank counts as words, abbreviations. Turkish uses a trained multiword splitter and Chinese a trained word segmenter.
- **Tagging heads.** `Tagger` (the treebank's own tag, `Token.Tag`), `Morphologizer` (universal POS and features, `Token.Pos` and `Token.Morph`) and the edit-tree `EditTreeLemmatizer` (`Token.Lemma`).
- **`Parser`.** A biaffine dependency parser (Dozat and Manning 2017) that also sets sentence starts (`Token.Dep`, `Token.Head`). It reads its own encoder.
- **`EntityRecognizer`.** Entities with the labels of its training data (`PER`, `ORG`, `LOC`, and `MISC` where the data has it).

Where a treebank has no XPOS column (French-GSD, Portuguese-Bosque) there is no tagger and `Token.Tag` stays empty.

## Languages

| Package | Treebank (UD r2.18) | Entity data | Language-specific tokenization |
|---|---|---|---|
| `.Models.English` | English-EWT | UNER English-EWT | English exceptions; the packaged tagging and entity encoders are also trained on web text labelled by the previous model |
| `.Models.French` | French-GSD | WikiNER French | elision, inversion clitics; `du`, `au`, `aux`, `des` as multiword tokens |
| `.Models.German` | German-GSD | GermEval 2014 | contractions (`zum`, `im`) as multiword tokens, ordinals, abbreviations |
| `.Models.Spanish` | Spanish-AnCora | WikiNER Spanish | `del`, `al`, enclitic pronouns, `¿` and `¡` |
| `.Models.Portuguese` | Portuguese-Bosque | UNER Portuguese-Bosque | contractions as pieces, enclitics after a verb's hyphen |
| `.Models.Italian` | Italian-ParlaMint and Italian-MarkIT (897 sentences in all: the larger Italian treebanks are non-commercial) | WikiNER Italian | `del`, `farlo` and similar |
| `.Models.Dutch` | Dutch-Alpino | WikiNER Dutch | Dutch abbreviations and compounds |
| `.Models.Turkish` | Turkish-BOUN | Turkish-WikiNER | apostrophe suffixes kept; a trained multiword splitter; Turkic case folding |
| `.Models.Chinese` | Chinese-GSDSimp | UNER Chinese-GSDSIMP | a trained character-tagging word segmenter |
| `.Models.Hindi` | none (no allowed treebank): FineWeb-2 Hindi text labelled by the accurate tier and by Marathi, Sindhi and Punjabi heads on its encoder, 32,276 sentences kept by agreement | the same text, 50,380 sentences | none; the danda is a token. No lemmas (the token text stands in), no XPOS |

Every training source is credited in the package's `THIRD-PARTY-NOTICES.md`, and `PROVENANCE.md` lists datasets, recipes, seeds and hashes. Most of the data is CC BY-SA, so the weights carry that lineage (credited), and they ship under `MIT OR Apache-2.0` ([ud-licences.md](ud-licences.md) lists every treebank considered and why some are not used). There is no Hindi package: Hindi has no allowed UD or named-entity training data.

## Measured

Per language, against spaCy 3.8.18 trained on the same data with its default configuration, in gold-token and raw-text modes: [results-table.md](results-table.md). The details and the recipes' own tables are in [results.md](results.md) and in each recipe's `PROVENANCE.md`. Speed is indicative until the release gate.

## Train your own

The same recipes run on your data; for a language we do not ship, start from the nearest one: `recipes/<lang>/...` has the commands, and `Pipeline.Blank(Language.FromCode("xx"))` takes any language code (the tokenizer falls back to plain UAX #29 words).
