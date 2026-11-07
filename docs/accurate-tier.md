# The base-size accurate tier

`RecursiveAI.OneNlp.Models.Multilingual.Large`: one fine-tuned mmBERT-base encoder shared by per-language heads for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese. Recipe: `recipes/accurate/multilingual`. Scoring: `tools/accurate/score_ml.sh`, comparison with the published rows: `tools/accurate/compare_refs.py`. Data and licences: the model's `PROVENANCE.md`.

## Recipe

- Encoder: mmBERT-base through `TrainableTransformerEncoder` (all layers trained, token table frozen, subword vectors averaged per token).
- Heads: tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, biaffine parser, entity recognizer (BIO tagging). One set for each language; a `ScopedHead` runs a head only on documents of its language, so the encoder sees all languages and each head sees one.
- **First run:** 600 steps of about 3,000 words at learning rate 1e-4, language-balanced sampling (a language with fewer words repeats at most twice), best development score kept. One shared encoder was chosen over one encoder per language on development sets: at 1,000 steps, Turkish and German were within noise or ahead of a single-language encoder at 500 steps (the runs are not matched in words seen), and one encoder keeps the package at 1.3 GB instead of nine times that.
- **Continued run (the shipped weights):** 300 more steps of 3,000 words from the first run's weights and optimizer state (`--init`; the learning-rate schedule carries on from step 600: 10 steps of warm-up to 6e-5, half cosine down to 6e-6). The languages are not sampled alike any more: the multiplicity of each language's treebank in the training list is set from the first run's development scores (`--repeat`: Chinese 1.8, Turkish 1.8, Italian 3.5, Dutch 0.65, German 0.45, English 0.5, Portuguese 0.5, French 0.2, Spanish 0.15), and 700 entity documents per language are used instead of 2,500. The development score (mean over the nine languages) went 86.79 (first run) to 86.89, 87.68 and 87.91 at steps 100, 200 and 300 of the continued run. About 24 CPU-hours (8 threads, resumable chunks of 26 minutes, about 33 seconds per step). The label sets and class order of the heads come from the first run's training list, so that its checkpoint fits whatever the sampling is.
- **Lemma tables.** Each lemmatizer has a table of the training treebank's (lower-cased form, UPOS) pairs with their lemma, kept where the most frequent lemma has at least 0.6 to 1.0 of the occurrences (chosen per language on development sentences with `tools/accurate/lexicon_dev.py`: en 0.9, de 0.6, fr 0.9, es 0.6, pt 1.0, it 0.6, nl 0.6, tr 0.6, zh 0.9). A hit gives the table's lemma, a miss falls back to the edit trees. On development sentences (gold tokens, 400 per language) lemma accuracy went en 94.6 to 97.7, de 93.0 to 97.3, fr 95.5 to 98.3, es 95.6 to 98.9, pt 95.5 to 97.9, it 92.2 to 95.2, nl 90.1 to 94.9, tr 80.6 to 86.7, zh 99.6 to 99.8. It costs a few MB per language and no training.
- **Chinese raw text.** The Chinese pipeline starts with the trained word segmenter of the Chinese fast tier (`RecursiveAI.OneNlp.Models.Chinese`), so raw-text rows score on real words (raw UPOS 80.96 against 37.85 with the Unicode word rules, which cut Chinese into single characters).
- **Input semantics.** The encoder reads the words of multiword tokens. A treebank splits French `du` into the words `de` and `le`; the tokenizer cuts it into two pieces; with `ExpandWordForms` the encoder reads the text `de le`, so each word has a vector of its own. `MultiwordFormsComponent` (`mwt-forms`, the first component of every package pipeline) sets `Token.Norm` of the pieces from the language's segmentation data, and the lemmatizer applies its edit trees (and looks its table up) on that word form. Training reads the same word-form text. Without this, pt and fr UPOS stopped at about 90 and 94.7.
- **Serving.** After training, `TrainableTransformerEncoder.ExportOnnx` writes the fine-tuned weights into the original encoder graph, and the package runs that graph on OneModel's inference runtime (`TransformerEncoder.FromStreams`) instead of the trainable modules. On 80,000 development words in eight languages the predictions equal the trainable path's in every column but the lemma (which the tables change on purpose): 0 differing words in de, en, es, fr, it, nl, pt, tr and 7 of 10,180 in zh (floating-point rounding differences in the graph kernels).

## Package layout

A thin package (heads for eight languages with their lemma tables, the tokenizer, registration) depends on seven parts that carry the rest: `Encoder1`, `Encoder2` and `TokenTable1` to `TokenTable4` (the encoder's ONNX graph, 1.23 GB, cut into six consecutive pieces of 205 MB; the part names are historical), and `Chinese` (the Chinese heads and word segmenter, 65 MB). About 1.3 GB in all, each package under 250 MB (largest 205 MB). Users reference one id. The package registers its pipelines as the accurate tier (`ModelPackages.Register(language, Tier.Accurate, ..., priority: 1)`). A pipeline file holds only heads and names the encoder `multilingual-large` (`SharedEncoder`); the graph is read once on first use as one seekable stream (no second copy of its bytes), and a language's heads are read when that language is first used. Pack with `ONENLP_MLLARGE_WEIGHTS` pointing at the directory the manifests list; the pack fails on a missing or different file. `tools/accurate/split_graph.py` cuts the graph.

### Speed, memory and load (indicative; 1 thread, 60 test sentences, shared 32-core x64 host, load average 8 to 20)

| | First model (parts loaded into trainable modules) | This model (ONNX graph) |
|---|---|---|
| Wall time for process start, load and 60 German sentences | 37 s | 4.0 s |
| Load (encoder and one language's heads) | not measured separately (under 37 s) | 2.0 s |
| Words per second, 1 thread (de, tr) | 31 to 75 (first unit's measurement) | 526, 354 |
| Peak resident set, one language | 4.76 GB (de), 4.81 GB (tr) | 2.85 GB (de), 2.94 GB (tr) |
| Peak resident set, all nine languages, 8 threads, full test splits | not measured | 6.3 GB (80,000-word dev run: 4.8 GB) |

The remaining 2.8 GB is the graph's bytes (1.23 GB, read once from the embedded resource into one array) plus the runtime's own copy of the weights; a loader that used the embedded bytes in place would roughly halve it (a request for OneModel, not done here).

## Rows

Test splits, UD r2.18, gold tokens and sentences ("gold") or raw text with our tokenizer (Chinese: the trained segmenter), the parser setting sentences ("raw"). Score after the continued run, first run in brackets. Intervals are 95% bootstrap (1,000 resamples, seed 20261106). The full tables, with the published score of every system next to each row, are in `accurate-tier-rows.md`.

| Treebank | Gold UPOS | Gold UFeats | Gold Lemmas | Gold LAS | Raw UPOS | Raw Lemmas | Raw LAS | NER F1 |
|---|---|---|---|---|---|---|---|---|
| en EWT | 96.58 (96.33) | 96.44 (95.74) | 97.09 (95.00) | 85.43 (84.66) | 95.95 (95.72) | 96.14 (94.30) | 85.02 (84.28) | 79.07 (78.95) |
| de GSD | 96.16 (95.85) | 86.81 (85.57) | 96.25 (91.96) | 83.32 (82.46) | 93.27 (93.16) | 93.36 (89.27) | 81.17 (80.75) | 82.68 (81.19) |
| fr GSD | 97.21 (97.00) | 96.24 (95.41) | 97.56 (94.65) | 86.55 (85.67) | 97.06 (96.70) | 97.42 (94.44) | 88.07 (87.70) | 81.56 (78.98) |
| es AnCora | 98.10 (97.88) | 96.50 (95.92) | 97.72 (94.75) | 85.37 (84.25) | 98.09 (97.91) | 97.75 (94.83) | 87.42 (86.73) | 85.58 (84.08) |
| pt Bosque | 96.56 (96.40) | 94.80 (94.20) | 97.49 (94.70) | 84.01 (82.92) | 96.59 (96.21) | 96.96 (94.12) | 86.47 (85.35) | 88.28 (87.25) |
| it ParlaMint | 97.72 (96.44) | 90.34 (87.36) | 96.14 (89.41) | 85.53 (80.23) | 98.04 (96.21) | 96.18 (89.64) | 86.18 (80.39) | 86.30 (85.32) |
| it MarkIT | 95.69 (94.30) | 91.91 (89.42) | 86.37 (81.05) | 77.27 (73.72) | 95.98 (94.23) | 86.46 (81.04) | 81.12 (76.69) |  |
| nl Alpino | 96.89 (96.47) | 96.73 (96.11) | 91.88 (86.68) | 87.63 (85.47) | 96.66 (96.21) | 91.54 (86.35) | 89.06 (87.59) | 85.18 (84.23) |
| tr BOUN | 91.50 (90.77) | 80.57 (79.17) | 86.40 (76.87) | 67.92 (65.34) | 89.08 (87.93) | 84.51 (75.57) | 63.03 (61.12) | 83.55 (81.79) |
| zh GSDSimp | 88.77 (87.34) | 96.03 (95.50) | 99.80 (99.49) | 63.29 (59.30) | 80.96 (37.85) | 89.45 (43.40) | 58.84 (10.54) | 84.84 (83.25) |

Entity F1 is on each language's own entity corpus (UNER for en, pt, zh; GermEval 2014 for de; WikiNER for es, fr, it, nl; Turkish-WikiNER for tr).

**Against the published rows (Stanza 1.5.1, UDPipe 2, Trankit, `references.md`): 320 of 399 comparisons still miss (338 before).** No model row (tags, features, lemmas, parse) is within the published systems' reach in any language; the eight rows with no system above our interval are tokenization and sentence rows. The gaps that remain are largest in the dependency parse (LAS 3 to 9 points under the best published system in every language; Chinese 20) and in features and tags for German, Turkish and Chinese. Lemmas moved most (Turkish 76.9 to 86.4, Italian 89.4 to 96.1, Dutch 86.7 to 91.9, German 92.0 to 96.3) and are now within 1 to 3 points of the best published system in most languages. Against spaCy's black-box baselines (`accurate-tier-rows.md`) the model is ahead in 72 of 90 rows, overlapping in 12 and behind in 6.

## Gaps and conventions

- **Hindi** has no allowed training data (docs/ud-licences.md); not in this package.
- **XPOS**: French-GSD and Portuguese-Bosque have none; BOUN's XPOS is `_` on half its tokens, so XPOS and AllTags are not scored for Turkish.
- **References**: `docs/references.md` rows are Stanza, UDPipe 2 and Trankit, mostly UD 2.5 to 2.17 and end-to-end raw text; "gold" rows compare only with UDPipe 2's gold-token rows. UDPipe 2's models are non-commercial and only their published numbers are used. The comparison is not within noise on most rows: see `accurate-tier-rows.md`.
- **Entity corpora differ by language** and the published rows are on other splits (docs/references.md), so entity rows are information.
- Italian trains on 897 sentences; this limits its lemma, feature and parse scores.
