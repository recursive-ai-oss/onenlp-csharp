# The base-size accurate tier

`RecursiveAI.OneNlp.Models.Multilingual.Large`: one fine-tuned mmBERT-base encoder shared by per-language heads for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese. Recipe: `recipes/accurate/multilingual`. Scoring: `tools/accurate/score_ml.sh`, comparison with the published rows: `tools/accurate/compare_refs.py`. Data and licences: the model's `PROVENANCE.md`.

## Recipe

- Encoder: mmBERT-base through `TrainableTransformerEncoder` (all layers trained, token table frozen, subword vectors averaged per token).
- Heads: tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, biaffine parser, entity recognizer (BIO tagging). One set for each language; a `ScopedHead` runs a head only on documents of its language, so the encoder sees all languages and each head sees one.
- 600 steps of about 3,000 words, learning rate 1e-4, seed 1, language-balanced sampling (a language with fewer words repeats at most twice), best development score kept. One shared encoder was chosen over one encoder per language on development sets: at 1,000 steps, Turkish and German were within noise or ahead of a single-language encoder at 500 steps (the runs are not matched in words seen), and one encoder keeps the package at 1.3 GB instead of nine times that.
- **Input semantics.** The encoder reads the words of multiword tokens. A treebank splits French `du` into the words `de` and `le`; the tokenizer cuts it into two pieces; with `ExpandWordForms` the encoder reads the text `de le`, so each word has a vector of its own. `MultiwordFormsComponent` (`mwt-forms`, the first component of every package pipeline) sets `Token.Norm` of the pieces from the language's segmentation data, and the lemmatizer applies its edit trees to that word form. Training reads the same word-form text. Without this, pt and fr UPOS stopped at about 90 and 94.7.

## Package layout

A thin package (heads for nine languages, encoder settings, tokenizer, registration) depends on six parts that carry the encoder: `Encoder1` and `Encoder2` (the checkpoint of the 22 layers, cut in two halves) and `TokenTable1` to `TokenTable4` (the frozen table by row ranges, byte-identical to the export). About 1.3 GB in all, each package under 250 MB. Users reference one id. The package registers its pipelines as the accurate tier (`ModelPackages.Register(language, Tier.Accurate, ..., priority: 1)`). A pipeline file holds only heads and names the encoder `multilingual-large` (`SharedEncoder`); the encoder is built once from the parts on first use and shared by the nine languages. Pack with `ONENLP_MLLARGE_WEIGHTS` pointing at the directory the manifests list; the pack fails on a missing or different file.

## Rows

Test splits, UD r2.18, gold tokens and sentences ("gold") or raw text with our tokenizer, the parser setting sentences ("raw"). Intervals are 95% bootstrap (1,000 resamples, seed 20261106); the full tables are written by `score_ml.sh`.

| Treebank | Gold UPOS | UFeats | Lemmas | UAS | LAS | Raw UPOS | Raw LAS | NER F1 (corpus) |
|---|---|---|---|---|---|---|---|---|
| en EWT | 96.3 | 95.7 | 95.0 | 88.2 | 84.7 | 95.7 | 84.3 | 78.9 (UNER) |
| de GSD | 95.8 | 85.6 | 92.0 | 86.2 | 82.5 | 93.2 | 80.7 | 81.2 (GermEval) |
| fr GSD | 97.0 | 95.4 | 94.6 | 88.8 | 85.7 | 96.7 | 87.7 | 79.0 (WikiNER) |
| es AnCora | 97.9 | 95.9 | 94.7 | 87.3 | 84.2 | 97.9 | 86.7 | 84.1 (WikiNER) |
| pt Bosque | 96.4 | 94.2 | 94.7 | 86.8 | 82.9 | 96.2 | 85.3 | 87.3 (UNER) |
| it ParlaMint | 96.4 | 87.4 | 89.4 | 84.6 | 80.2 | 96.2 | 80.4 | 85.3 (WikiNER) |
| it MarkIT | 94.3 | 89.4 | 81.1 | 78.6 | 73.7 | 94.2 | 76.7 | |
| nl Alpino | 96.5 | 96.1 | 86.7 | 89.0 | 85.5 | 96.2 | 87.6 | 84.2 (WikiNER) |
| tr BOUN | 90.8 | 79.2 | 76.9 | 72.7 | 65.3 | 87.9 | 61.1 | 81.8 (Turkish-WikiNER) |
| zh GSDSimp | 87.3 | 95.5 | 99.5 | 65.6 | 59.3 | not meaningful | | 83.2 (UNER) |

## Gaps and conventions

- **Hindi** has no allowed training data (docs/ud-licences.md); not in this package.
- **Chinese raw text**: the UAX tokenizer has no word segmentation (that is the Chinese fast-tier segmenter's job), so raw rows are not meaningful.
- **XPOS**: French-GSD and Portuguese-Bosque have none; BOUN's XPOS is `_` on half its tokens, so XPOS and AllTags are not scored for Turkish.
- **References**: `docs/references.md` rows are Stanza, UDPipe 2 and Trankit, mostly UD 2.5 to 2.17 and end-to-end raw text; "gold" rows compare only with UDPipe 2's gold-token rows. UDPipe 2's models are non-commercial and only their published numbers are used. The comparison is not within noise on most rows: see the report of the unit and `compare_refs.py` output.
- **Entity corpora differ by language** and the published rows are on other splits (docs/references.md), so entity rows are information.
- Italian trains on 897 sentences; this limits its lemma, feature and parse scores.
