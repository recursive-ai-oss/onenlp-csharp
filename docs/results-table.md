# Results table

One table of what is measured, what it is measured against, and whether it holds. Every number is copied from a file in this repository, named in the "Source" column or the section heading; nothing here was measured for this page. Rows that lose, and gaps, are listed in their own sections below, not left out.

**How to read it.**

- **Fast-tier and text-classification rows** are scored on official test splits, read once for the final configuration (the development splits chose every setting). The reference is spaCy 3.8.18, trained as a black box with its default configuration on the same data, in the same input mode (`tools/baselines/spacy/`). Intervals are paired bootstrap 95% over sentences or documents (1,000 resamples; seeds are in each source file). **Within noise** means the two intervals overlap (`measurement.md`); **ahead** means they do not and ours is higher; **behind** means they do not and ours is lower.
- **Gold** rows give the treebank's own words and sentences as input; **raw** rows give only the text, so tokenization and sentence splitting are scored too. spaCy's tokenizers keep contractions such as `del` and `zum` whole, which is why several raw rows are far ahead.
- **Within noise is not a claim of equal.** With a few hundred sentences an overlap is a weak statement. Where the paired difference excludes zero while the intervals overlap, the source file says so, and the row is also named under "Misses and caveats".
- **Speed rows** are in [results.md](results.md). The tokenizer, language-ID and recognizer rows there were taken alone on the machine and are repeated at the release gate; the fast-tier speed rows are indicative, and the publishable run against Catalyst is made at the release gate.

## Tokenizers and segmentation

| Component | Metric | Ours | Reference | Verdict | Source |
|---|---|---|---|---|---|
| Tokenizers: WordPiece, Unigram, SentencePiece-BPE, byte-level BPE, Tiktoken (12 model families) | IDs, tokens, offsets, special-token mask, decoded text vs Hugging Face `tokenizers` 0.22.1 and `tiktoken` 0.14.0 on the multilingual parity corpus | byte-exact (a mismatch is a bug, never a tolerance) | the two libraries, run as installed packages | holds: 0 mismatches on all 12 families (bert-base-uncased, bert-base-multilingual-cased, xlm-roberta-base, t5-small, mmBERT-base, TinyLlama, gpt2, roberta-base, Qwen2.5, ModernBERT, cl100k_base, o200k_base), each over 47,981 corpus records (about 3.45 million comparisons per family); release gate run on 2026-11-07 (tokenizers 0.22.1, tiktoken 0.14.0): 0 mismatches on every family; comparisons per family: bert-base-uncased 3,451,629; bert-base-multilingual-cased 3,451,661; xlm-roberta-base 3,451,681; t5-small 3,449,575; mmBERT-base 3,449,935; TinyLlama 3,449,935; gpt2 3,449,919; roberta-base 3,452,041; Qwen2.5 3,449,919; ModernBERT 3,449,935; cl100k_base 2,395,778; o200k_base 2,395,778 | [parity.md](parity.md), `tools/parity/families.json` |
| UAX #29 word and sentence boundaries | every line of Unicode 17.0.0 `WordBreakTest.txt` and `SentenceBreakTest.txt` through our layer with no language data | conformance tests pass | the Unicode test files | holds: WordBreakTest 1944/1944, SentenceBreakTest 512/512 (release gate run, both conformance tests pass) | [segmentation.md](segmentation.md) |
| English tokenization, raw EWT r2.18 test | Tokens F1 | 99.00 [98.80, 99.19] | spaCy 95.70 [95.14, 96.21] | ahead | [results.md](results.md) |

Tokenizer speed against Microsoft.ML.Tokenizers 2.0.1, release gate, arm64 (Apple M2, 8 cores, .NET 10.0.403, load 2.0 to 3.3 before and during, idle after; `wq` bench class): 47 timed rows, none below 1.00x. 13 rows are not timed because the comparator's output differs from ours (WordPiece medium decode and long x3, Unigram long x3, others listed in the run's `rows.md`); they are not counted as passes. x64 rows: measured in the release run, part 2. Load time is reported, not gated, and is slower than the comparator on three rows: WordPiece from tokenizer.json 28.16 ms vs 19.17 ms (0.68x), SentencePiece-BPE TinyLlama 49.60 ms vs 11.66 ms (0.24x), cl100k_base 19.93 ms vs 19.00 ms (0.95x); ours is faster on Unigram (3.47x), gpt2 (1.02x) and o200k_base (1.31x).

## Language identification

Held-out Leipzig texts ("main") and a Tatoeba slice, macro accuracy over 75 languages (every language weighs the same), against `lingua-language-detector` 2.2.0 and SearchPioneer.Lingua 2.0.0 in high-accuracy mode, built for the same 75 languages. Source: [language-id.md](language-id.md), `recipes/langid/results/`.

| Set / bucket | Ours | Lingua (Python) high | Lingua (.NET) high | Ours minus Lingua Python | Verdict |
|---|---|---|---|---|---|
| main / words | 77.14 [76.86, 77.46] | 75.59 [75.30, 75.86] | 75.35 | +1.56 [+1.31, +1.86] | ahead |
| main / pairs | 89.53 | 85.66 | 85.49 | +3.87 [+3.65, +4.08] | ahead |
| main / sentences | 98.03 | 95.98 | 95.81 | +2.05 [+1.95, +2.15] | ahead |
| Tatoeba / words | 79.14 | 75.03 | 74.79 | +4.10 [+3.39, +4.77] | ahead |
| Tatoeba / pairs | 91.14 | 85.79 | 85.75 | +5.35 [+4.66, +6.18] | ahead |
| Tatoeba / sentences | 96.15 | 93.27 | 93.19 | +2.88 [+2.55, +3.20] | ahead |

The macro averages hide languages where we are below Lingua: on the main set 14 languages on words (ar da ga hr ko lg mk nl ru so ur xh yo zh), 8 on pairs (hr ja lg ms ru xh yo zh) and 2 on sentences (en hr). The closely related Latin-script pairs are the hard rows. The Leipzig texts come from the collection Lingua says its models were built from (this may favour Lingua), and our training includes other Tatoeba sentences (this may favour us); both are reported. Per-language rows: `recipes/langid/results/per-language.*.csv`.

Language-ID speed (alone on the machine, x64 and arm64): at least 7.6x Lingua on every row; 10.7 ms load against 326 ms (low accuracy) and 3,850 ms (high accuracy) on x64 (`results.md`).

Language-ID speed, release gate, arm64 (Apple M2, 8 cores; median microseconds per text, ours / SearchPioneer.Lingua 2.0.0 high accuracy / low accuracy): words 7.74 / 76.42 / 63.54 (9.9x and 8.2x faster), pairs 8.09 / 88.65 / 55.80 (11.0x and 6.9x), sentences 10.77 / 300.58 / 216.10 (27.9x and 20.1x). Allocation 1.4 KB per text against 75 to 126 KB; RSS 66 MB against 2,575 and 480 MB; load 9.3 ms against 4,329 and 328 ms. No row is slower. x64: measured in the release run, part 2 (earlier x64 figures above).

## Recognizers

Our hand-labelled gold sets, one per language, written from the type definitions and real text (`recognizer-gold.md`), test split. Reference: Microsoft.Recognizers.Text 1.8.13 in the same culture, run as a black box. Exact-span F1, all types pooled, with the paired bootstrap 95% interval (1,000 resamples over sentences, seed 20260101). Source: `testdata/recognizers/<lang>/comparators/ours-vs-recognizers.test.md`.

| Language | Test items | Ours, all types | Recognizers.Text, all types | Value accuracy on exact matches (ours) | Verdict |
|---|---:|---|---|---|---|
| English | 312 | 97.1 [95.3, 98.9] | 59.9 [56.2, 63.1] | 100.0 | ahead |
| French | 347 | 95.6 [93.7, 97.5] | 45.2 [41.6, 48.7] | 99.6 | ahead |
| German | 349 | 96.0 [93.8, 97.9] | 53.9 [50.9, 57.0] | 99.6 | ahead |
| Spanish | 348 | 97.3 [95.7, 98.6] | 46.7 [43.1, 50.4] | 99.6 | ahead |
| Portuguese | 349 | 94.4 [91.8, 96.8] | 45.3 [41.8, 48.8] | 99.6 | ahead |
| Italian | 368 | 96.5 [94.5, 98.2] | 51.0 [48.0, 54.2] | 99.6 | ahead |
| Dutch | 369 | 97.2 [95.0, 98.8] | 54.6 [51.7, 57.6] | 99.0 | ahead |
| Chinese | 338 | 99.3 [98.2, 100.0] | 49.8 [46.4, 53.0] | 100.0 | ahead |
| Turkish | 316 | 95.2 [92.9, 97.1] | 56.3 [53.1, 59.9] | 100.0 | ahead |
| Hindi | 324 | 96.9 [95.2, 98.6] | 49.3 [45.8, 52.5] | 100.0 | ahead |

No type in any language is behind Recognizers.Text (the `behind` verdict appears in none of the ten comparison files). "All types" pools types with ten to thirty items each, so per-type intervals are wide (the per-type rows are in the source files, and Duckling's, which are information, beside them). The comparator uses its own conventions for range ends and drops stated time zones; the gold uses ours, and the differences are listed in [recognizer-gold.md](recognizer-gold.md). The Chinese test file was read twice after a rule change; the published row is the first read (`recognizer-gold.md`).

Recognizer speed against Microsoft.Recognizers.Text 1.8.13, release gate, arm64 (English): numbers, units, currency and sequences 0.605 s against 31.874 s (52.7x faster; 402 MB allocated against 8,798 MB); with date/time 0.721 s against 27.085 s (37.6x; 616 MB against 4,879 MB); RSS 85 and 68 MB against 310 and 292 MB. x64: measured in the release run, part 2 (earlier x64 figures in `results.md`: 54.6x and 43.8x). The comparator is far slower in Turkish and Hindi (0.01 to 0.03 million characters per second); those speed rows are not part of the gate run.

## Fast tier: tagging, lemmas, parsing, entities

Source for each language: its section in [results.md](results.md) and the recipe's `PROVENANCE.md` (`recipes/<lang>/...`). Treebanks are UD r2.18 test (English-EWT, German-GSD, French-GSD, Spanish-AnCora, Portuguese-Bosque, Italian-ParlaMint, Dutch-Alpino, Turkish-BOUN, Chinese-GSDSimp). Entity data is named per row. Ours / spaCy, each with its interval. Only the headline columns are shown here; XPOS, AllTags, UAS, CLAS, MLAS, sentences and per-type entity rows are in the sources.

### English

| Input | Metric | Ours | spaCy 3.8.18 | Verdict |
|---|---|---|---|---|
| gold | UPOS | 94.16 [93.79, 94.54] | 94.29 [93.89, 94.66] | within noise |
| gold | UFeats | 95.15 [94.86, 95.47] | 95.18 [94.88, 95.45] | within noise |
| gold | Lemmas | 96.11 [95.77, 96.44] | 95.80 [95.42, 96.13] | within noise |
| gold | LAS | 81.53 [80.62, 82.43] | 81.96 [81.03, 82.83] | within noise |
| raw | UPOS | 92.90 [92.42, 93.35] | 92.45 [91.84, 93.06] | within noise |
| raw | Lemmas | 95.03 [94.61, 95.43] | 93.69 [93.08, 94.29] | ahead |
| raw | LAS | 77.26 [76.28, 78.25] | 78.53 [77.46, 79.55] | within noise |
| raw | Sentences (parser) | 71.08 [69.06, 72.98] | 72.66 [70.48, 74.75] | within noise |
| raw | MLAS | 65.30 [64.12, 66.48] | 69.05 [67.81, 70.21] | **behind** (information row, not gated) |
| gold, UNER EWT test | Entities micro F1 | 63.09 [60.01, 65.89] | 62.32 [59.42, 65.33] | within noise |

### Other languages

| Language | Input | Metric | Ours | spaCy 3.8.18 | Verdict |
|---|---|---|---|---|---|
| German | gold | UPOS | 94.56 [94.18, 94.94] | 94.59 [94.22, 94.95] | within noise |
| German | gold | UFeats | 86.25 [85.60, 86.96] | 84.71 [84.03, 85.39] | ahead |
| German | gold | Lemmas | 95.95 [95.67, 96.25] | 95.65 [95.34, 95.98] | within noise |
| German | gold | LAS | 76.66 [75.63, 77.72] | 77.31 [76.29, 78.36] | within noise |
| German | raw | UPOS | 94.22 [93.83, 94.61] | 90.13 [89.57, 90.67] | ahead |
| German | raw | LAS | 74.87 [73.79, 75.92] | 70.77 [69.59, 71.95] | ahead |
| German | gold, GermEval 2014 | Entities micro F1 | 66.28 [64.94, 67.61] | 65.77 [64.49, 67.19] | within noise |
| French | gold | UPOS | 96.77 [96.33, 97.19] | 97.07 [96.68, 97.47] | within noise |
| French | gold | UFeats | 96.34 [95.89, 96.81] | 96.41 [95.91, 96.88] | within noise |
| French | gold | Lemmas | 97.51 [97.16, 97.84] | 97.49 [97.14, 97.84] | within noise |
| French | gold | LAS | 86.96 [85.86, 87.96] | 85.98 [84.80, 87.12] | within noise |
| French | raw | UPOS | 96.06 [95.60, 96.53] | 91.80 [91.07, 92.55] | ahead |
| French | raw | LAS | 86.07 [84.92, 87.11] | 78.10 [76.77, 79.43] | ahead |
| French | gold, WikiNER test | Entities micro F1 | 82.48 [81.74, 83.28] | 75.50 [74.68, 76.46] | ahead |
| Spanish | gold | UPOS | 98.11 [97.98, 98.25] | 98.18 [98.04, 98.32] | within noise |
| Spanish | gold | UFeats | 97.71 [97.56, 97.86] | 97.74 [97.58, 97.89] | within noise |
| Spanish | gold | Lemmas | 98.84 [98.73, 98.95] | 98.72 [98.59, 98.83] | within noise |
| Spanish | gold | LAS | 86.41 [85.90, 86.89] | 85.95 [85.44, 86.44] | within noise |
| Spanish | raw | UPOS | 97.86 [97.71, 98.01] | 94.81 [94.56, 95.02] | ahead |
| Spanish | raw | LAS | 86.15 [85.61, 86.64] | 81.82 [81.25, 82.37] | ahead |
| Spanish | gold, WikiNER test | Entities micro F1 | 84.81 [84.08, 85.54] | 83.35 [82.63, 84.08] | ahead (paired +1.46 [0.84, 2.14]; PER 91.57 vs 90.45, ahead) |
| Portuguese | gold | UPOS | 95.89 [95.62, 96.14] | 95.92 [95.66, 96.22] | within noise |
| Portuguese | gold | UFeats | 94.51 [94.17, 94.80] | 94.11 [93.82, 94.42] | within noise |
| Portuguese | gold | Lemmas | 97.30 [97.10, 97.51] | 96.90 [96.70, 97.11] | within noise |
| Portuguese | gold | LAS | 81.80 [81.10, 82.49] | 81.96 [81.31, 82.61] | within noise |
| Portuguese | raw | UPOS | 95.35 [95.05, 95.64] | 84.15 [83.59, 84.73] | ahead |
| Portuguese | raw | LAS | 81.14 [80.44, 81.82] | 65.85 [65.02, 66.75] | ahead |
| Portuguese | gold, UNER Bosque test | Entities micro F1 | 73.48 [70.95, 75.86] | 75.27 [72.73, 77.60] | within noise (precision 75.46 vs 76.96, paired -1.50 [-3.76, 0.83]) |
| Italian | gold | UPOS | 94.44 [93.92, 94.96] | 94.10 [93.58, 94.60] | within noise |
| Italian | gold | UFeats | 89.52 [88.85, 90.16] | 87.42 [86.67, 88.14] | ahead |
| Italian | gold | Lemmas | 95.18 [94.73, 95.63] | 93.13 [92.58, 93.64] | ahead |
| Italian | gold | LAS | 71.55 [70.26, 72.97] | 71.39 [70.26, 72.58] | within noise |
| Italian | raw | UPOS | 94.01 [93.45, 94.51] | 83.53 [82.50, 84.48] | ahead |
| Italian | raw | LAS | 71.55 [70.27, 72.90] | 58.38 [56.92, 59.86] | ahead |
| Italian | gold, WikiNER test | Entities micro F1 | 86.35 [85.73, 87.01] | 83.54 [82.85, 84.26] | ahead |
| Dutch | gold | UPOS | 93.83 [93.16, 94.49] | 94.42 [93.77, 95.04] | within noise |
| Dutch | gold | UFeats | 92.94 [92.24, 93.53] | 93.13 [92.47, 93.72] | within noise |
| Dutch | gold | Lemmas | 91.61 [90.99, 92.14] | 91.83 [91.21, 92.39] | within noise |
| Dutch | gold | LAS | 79.12 [77.87, 80.39] | 79.85 [78.58, 81.16] | within noise |
| Dutch | raw | UPOS | 93.09 [92.40, 93.77] | 94.36 [93.67, 94.98] | within noise |
| Dutch | raw | LAS | 78.18 [76.87, 79.46] | 79.57 [78.26, 80.89] | within noise |
| Dutch | gold, WikiNER test | Entities micro F1 | 87.47 [86.91, 87.98] | 86.21 [85.70, 86.78] | ahead (paired +1.26 [0.76, 1.72]) |
| Turkish | gold | UPOS | 88.97 [88.31, 89.58] | 88.04 [87.37, 88.64] | within noise |
| Turkish | gold | UFeats | 78.15 [77.40, 78.90] | 78.30 [77.46, 79.05] | within noise |
| Turkish | gold | Lemmas | 85.11 [84.38, 85.74] | 80.24 [79.47, 80.92] | ahead |
| Turkish | gold | LAS | 62.80 [61.37, 64.19] | 57.87 [56.52, 59.18] | ahead |
| Turkish | raw | UPOS | 86.02 [85.28, 86.68] | 86.82 [86.14, 87.48] | within noise |
| Turkish | raw | LAS | 57.53 [56.08, 59.01] | 55.86 [54.53, 57.27] | within noise |
| Turkish | gold, Turkish-WikiNER test | Entities micro F1 | 67.40 [64.90, 69.81] | 65.82 [63.16, 68.28] | within noise |
| Chinese | gold (spaCy `gold_preproc`) | UPOS | 90.41 [89.70, 91.07] | 90.35 [89.70, 90.99] | within noise |
| Chinese | gold | UFeats | 98.38 [98.13, 98.59] | 98.43 [98.19, 98.66] | within noise |
| Chinese | gold | Lemmas | 99.87 [99.79, 99.94] | 99.91 [99.85, 99.96] | within noise |
| Chinese | gold | LAS | 67.49 [65.94, 69.14] | 68.24 [66.47, 69.94] | within noise |
| Chinese | gold | MLAS | 55.78 [53.92, 57.68] | 58.87 [56.85, 60.72] | within noise |
| Chinese | raw | Words | 89.62 [88.87, 90.42] | 39.43 [38.60, 40.23] | ahead (spaCy's default Chinese tokenizer cuts single characters) |
| Chinese | raw | UPOS | 81.90 [80.99, 82.86] | 34.92 [34.11, 35.68] | ahead |
| Chinese | raw | LAS | 56.93 [55.23, 58.73] | 8.59 [7.94, 9.28] | ahead |
| Chinese | gold, UNER GSDSIMP test | Entities micro F1 | 63.79 [59.39, 68.27] | 61.86 [57.66, 66.12] | within noise (precision 69.28 vs 67.08, recall 59.11 vs 57.40, all within noise) |
| Chinese | raw, UNER GSDSIMP test | Entities micro F1 (character spans) | 53.71 [49.28, 58.49] | no reference | no raw spaCy row: the stored spaCy raw predictions are tokenized as single characters and do not align with the gold character spans, so the scorer gives a meaningless 13.30. Re-measured at the release gate (was 52.88, taken before the 0.6 threshold) |

### Hindi (self-labelled; information, not a gated comparison)

There is no allowed Hindi treebank or entity corpus (`ud-licences.md`: Hindi-HDTB and the Samanantar-sourced NER sets are non-commercial), so the Hindi student learns from Hindi web text (FineWeb-2) labelled by the accurate tier: the heads of five large languages and of heads we trained on the same frozen encoder for Marathi, Sindhi and Punjabi (UD, CC BY-SA), combined by agreement, with explicit adult content filtered out of the text first (`recipes/hi/fast/README.md`). Hindi-PUD (1,000 sentences, CC BY-SA 3.0, sha256 `3562b7f4...`, test only) was read once for the final model, which is the second round of this recipe; the first round (labels from nine unrelated languages) is shown as "before". Intervals are 95% bootstrap (1,000 resamples, seed 20261107). The "labeller" is the unfiltered ensemble of the source heads on the same input, the ceiling of what the student was shown. spaCy 3.8.18 is the black-box baseline trained on the same labelled sentences with its default configuration and the pipeline `morphologizer,parser` (the labels have no XPOS or lemmas). The published Stanza, UDPipe 2 and Trankit numbers are on Hindi-HDTB with non-commercial training data and are not comparable (`references.md`, "Hindi"). **Hindi cannot be brought within noise of the published references with the training data that may be used.**

| Mode | Metric | Student | Labeller | spaCy, same labels | Verdict against spaCy | Student before |
|---|---|---|---|---|---|---|
| gold | UPOS | 83.19 [82.65, 83.75] | 85.55 [85.05, 86.08] | 83.78 [83.24, 84.31] | within noise | 70.93 |
| gold | UFeats | 34.28 [33.67, 34.90] | 34.63 [34.00, 35.27] | 34.29 [33.66, 34.90] | within noise | 34.01 |
| gold | Lemmas (form) | 72.68 [72.15, 73.24] | none | none | n/a | 72.68 |
| gold | UAS | 65.00 [64.01, 66.09] | 60.88 [59.84, 61.93] | 65.00 [64.01, 66.12] | within noise | 42.57 |
| gold | LAS | 52.79 [51.85, 53.77] | 49.04 [48.06, 50.05] | 53.44 [52.48, 54.47] | within noise | 31.90 |
| gold | CLAS | 45.60 [44.56, 46.69] | 44.52 [43.49, 45.56] | 46.46 [45.41, 47.57] | within noise | 29.62 |
| gold | MLAS | 7.72 [7.30, 8.17] | 8.16 [7.74, 8.62] | 8.36 [7.91, 8.81] | within noise | 5.14 |
| raw | Tokens | 99.38 [99.23, 99.53] | 99.38 | 99.82 [99.72, 99.89] | behind (spaCy's tokenizer) | 99.38 |
| raw | Sentences | 89.68 [87.47, 91.82] | 98.31 [97.22, 99.25] (rule sentencizer) | 91.30 [89.07, 93.19] | within noise | 90.75 |
| raw | UPOS | 82.41 [81.85, 82.95] | 84.99 [84.45, 85.54] | 83.65 [83.11, 84.18] | behind (1.2 points) | 70.49 |
| raw | UFeats | 34.03 [33.43, 34.65] | 34.23 [33.61, 34.85] | 34.22 [33.60, 34.82] | within noise | 33.72 |
| raw | UAS | 65.10 [64.09, 66.14] | 58.47 [57.47, 59.55] | 64.77 [63.77, 65.88] | within noise | 42.73 |
| raw | LAS | 52.87 [51.93, 53.84] | 46.76 [45.81, 47.78] | 53.32 [52.35, 54.36] | within noise | 32.00 |
| raw | CLAS | 45.54 [44.52, 46.67] | 42.60 [41.58, 43.68] | 46.40 [45.38, 47.57] | within noise | 29.80 |
| raw | MLAS | 7.56 [7.16, 8.00] | 7.95 [7.55, 8.39] | 8.32 [7.86, 8.77] | within noise | 5.23 |

The labeller's own gold-token scores rose from UPOS 72.77 and LAS 26.43 in the first round to 85.55 and 49.04. The student is 2.4 points below the labeller on UPOS and 3.8 above it on LAS (a parser trained on the medoid trees of many sources is more consistent than any one source). Against spaCy trained on the same labels it is within noise on every gold-token row and on raw UFeats, UAS, LAS, CLAS, MLAS and Sentences, and behind on raw UPOS (1.2 points; the tokenizer differs, 99.38 against 99.82). **Lemmas:** the model has no lemmatizer, and an edit-tree lemmatizer trained on Marathi was not clearly better than copying the form on 315 nouns, verbs and adjectives that we lemmatized by hand (62.5% against 61.0%), so the package fills `Token.Lemma` with the token text; the Lemmas row above is that, 72.7% on Hindi-PUD, and wrong for inflected words. Words are not scored separately from Tokens: Hindi has no multiword tokens.

**Entities (information only).** There is no Hindi entity gold we may use. A spot check on 91 held-out web sentences (61 entities) that we labelled by hand (`testdata/hi/nerspot.iob2`; PER, ORG, LOC): student 70.48 [57.14, 82.05] F1 (precision 84.09, recall 60.66), labeller 77.48 [65.35, 86.27], spaCy on the same labels 70.48 [56.47, 82.05]; the first round's student scored 67.92. On held-out labelled text the student reaches 79.36 F1 against the labeller's own entities. This is not a gated row.

**Hand-annotated development sentences.** 100 sentences (931 tokens, `testdata/hi/selfdev.conllu`: 17 web sentences and 83 that we wrote), gold tokens, UPOS / UAS / LAS: first-round student 80.56 [78.16, 82.80] / 70.46 / 55.85; second-round student 91.30 [89.43, 92.99] / 83.46 / 75.83; labeller (used set) 92.16 [90.64, 93.62] / 82.38 / 74.44; the nine large languages alone 83.03 / 71.64 / 59.51. These sentences ranked the source languages and the labelling model (`recipes/hi/fast/README.md`), so they are a development set, not a test.

Indicative speed (x64 Ryzen 9 7950X3D, 1 thread, load 7 to 9 from other jobs): full pipeline about 4.1k to 4.6k tokens/s over 20 repetitions on the development sentences (3.5k to 4.0k in the first round at higher load; the architecture and sizes are unchanged), 6.5 kB allocated per token.

## Text classification and span categorization (English)

Reference: spaCy 3.8.18 `textcat`, `textcat_multilabel` and `spancat`, default configurations, same data. Sources: `recipes/en/textcat/README.md`, `recipes/en/textcat-multilabel/README.md`, `recipes/en/spancat/README.md`.

| Task, data | Metric | Ours | spaCy | Verdict |
|---|---|---|---|---|
| Multi-class, BANKING77 test (3,080) | Accuracy | 89.87 [88.80, 90.94] | 86.69 [85.42, 87.92] | ahead (paired +3.18 [2.08, 4.35]) |
| | Macro-F1 | 89.87 [88.71, 90.81] | 86.17 [84.78, 87.22] | ahead |
| Multi-label, EUR-LEX test (5,000) | Micro-F1 | 59.60 [58.92, 60.30] | 44.86 [44.07, 45.69] | ahead |
| | Macro-F1 | 40.93 [39.78, 41.78] | 15.66 [15.27, 16.05] | ahead |
| | Macro ROC-AUC | 91.81 [91.47, 92.15] | 67.29 [66.65, 67.93] | ahead |
| Spans, UNER English-EWT test (threshold 0.8, chosen on dev) | Span F1 | 63.79 [60.78, 66.63] | 60.19 [57.02, 63.40] | ahead (paired +3.60 [1.14, 6.07]) |
| | Precision | 75.98 [72.83, 79.18] | 75.38 [72.01, 78.86] | within noise (paired +0.60 [-3.02, 4.17]) |
| | Recall | 54.96 [51.67, 58.24] | 50.09 [46.64, 53.48] | ahead (paired +4.87 [2.44, 7.10]) |

For information, the LexGLUE paper's EUR-LEX numbers on the same test split (TF-IDF with an SVM: micro-F1 71.3, macro-F1 51.4; BERT-base: 71.4 and 57.2) are above ours; ours is a from-scratch fast-tier model with a 512-word cut.

## Embeddings

Two shipped models, scored with `mteb` 2.27.1 as a black box. "Ours minus sentence-transformers" is 0.00 on every task, because the vectors agree to 4e-7 and 3e-7 (maximum absolute difference, 2,103 texts, zero token-ID mismatches). "Published" is the model card's number. The gate is runtime fidelity (same model, same harness); the card gaps below are shown, not hidden. Source: [embeddings.md](embeddings.md).

| Model | Task | Published | sentence-transformers 6.2.0 | Ours | Ours minus published |
|---|---|---|---|---|---|
| bge-small-en-v1.5 | STSBenchmark | 85.86 | 85.86 | 85.86 | 0.00 |
| | Banking77Classification | 85.74 | 81.75 | 81.75 | **-3.99** |
| | TwentyNewsgroupsClustering | 48.52 | 48.63 | 48.63 | +0.10 |
| | TwitterSemEval2015 | 73.33 | 73.24 | 73.24 | -0.09 |
| | AskUbuntuDupQuestions | 62.59 | 62.58 | 62.58 | 0.00 |
| | SciFact | 71.28 | 71.27 | 71.27 | 0.00 |
| multilingual-e5-small | STSBenchmark | 84.01 | 84.11 | 84.11 | +0.10 |
| | STS22 (multilingual) | 66.01 | 65.13 | 65.13 | -0.88 (66.10 under `mteb` 1.12.94: +0.09) |
| | Banking77Classification | 79.42 | 70.44 | 70.44 | **-8.97** |
| | MTOPDomainClassification (en, de, es, fr) | 88.01 | 85.59 | 85.59 | **-2.42** |
| | TwentyNewsgroupsClustering | 33.22 | 32.81 | 32.81 | -0.41 |
| | TwitterSemEval2015 | 70.95 | 70.75 | 70.75 | -0.20 |
| | AskUbuntuDupQuestions | 57.88 | 56.42 | 56.42 | **-1.46** |
| | SciFact | 67.70 | 67.70 | 67.70 | 0.00 |

The four bold cells are below the card for sentence-transformers itself, unchanged across `mteb` 1.1.1, 1.12.94 and 2.27.1; the card's numbers could not be reproduced from the published model with the released harness, whoever computes the vectors. The int8 variants are information only (0.3 to 3.8 points lower).

## Accurate tier (information; the base-size model is not yet packaged)

No accurate-tier model package is released yet, so these rows are information and no gate is claimed. Rows come from [accurate-encoder.md](accurate-encoder.md), `recipes/accurate/student-v1/RESULTS.md` and, for the references, [references.md](references.md) (published numbers on other UD releases: compare the direction, not the last digit).

| Model | Data (test) | Metric | Ours | Reference | Reading |
|---|---|---|---|---|---|
| Distilled student, fine-tuned (6 layers, 35.5 M weights) | UD English-EWT r2.18, gold tokens | UPOS | 95.88 [95.55, 96.19] | English fast tier 93.89; UDPipe 2 (gold, UD 2.17) 97.61 | above the fast tier (+1.99 [1.63, 2.36]); below the published UDPipe 2 |
| Distilled student, frozen, tuned head | UNER English-EWT | Entities micro F1 | 75.48 [72.66, 78.03] | English fast tier 61.52; published XLM-R large 85.8 | above the fast tier; below the published large model |
| mmBERT-base, fine-tuned 0.58 epoch (one chunk) | UD English-EWT r2.18, gold tokens | UPOS | 97.07 [96.79, 97.35] | spaCy 94.29; UDPipe 2 (gold, UD 2.17) 97.61 | above spaCy by 2.78 [2.44, 3.16]; below the published UDPipe 2 |

## Misses and caveats

- **English raw MLAS** 65.30 against spaCy's 69.05: outside the interval; information, not one of the gated rows.
- **Span categorizer:** at spaCy's threshold of 0.5 precision was 70.33 against 75.38 (paired -5.05 [-8.63, -1.42]); the shipped default threshold is 0.8, chosen on the development split (`recipes/en/spancat/README.md`).
- **Embeddings:** the five cells below the model cards, shared with sentence-transformers (above).
- **Language ID:** the languages named under the table are below Lingua on short text.
- **Entities per type:** per-type rows are in each source file; several have intervals that overlap with a paired difference that excludes zero (for example English LOC with the single-encoder option); the packaged English model has none of those.
- **Gaps:** Hindi has a fast tier but only on transferred labels (see "Hindi", not a gated comparison); no German, Spanish, Portuguese, Italian, Dutch, Turkish or Chinese rows against Catalyst (it has English models only); open-type NER and the accurate-tier packages are in progress and have no rows yet.
- **Speed against Catalyst 26.10.6514** (English, 1 thread, arm64 Apple M2, release gate; median tokens/s of the last 10 of 12 passes): tagging path (Catalyst tagging 1,007,344 tokens/s, 307 B/token; ours sentences, UPOS/UFeats and lemmas 670,783, 330 B/token) is **0.666x**, above the 0.5x bar. It counts under matched or better accuracy on raw EWT test: Tokens 99.00 vs 94.47, Sentences 71.08 vs 64.57, UPOS 92.90 vs 80.70, Lemmas 95.03 vs 83.84. NER path: Catalyst with WikiNER 478,220 vs ours sentences and entities 333,620 (0.70x), not matched work (ours excludes tagging; Catalyst's NER needs it), and so indicative only; entities on UNER character spans 63.09 vs 57.77. Ours parser path 25,535 and full pipeline 22,634 tokens/s (Catalyst has no parser). RSS 311 MB against 1,401 MB. English model load time not measured in this run. x64: measured in the release run, part 2.
- **Spanish gold LAS** is within noise at the release gate (86.41 [85.90, 86.89] vs 85.95); an earlier version of this table showed an older parser.
- **Rows behind at the gate:** English raw MLAS (information); Hindi raw Tokens 99.38 vs 99.82 and raw UPOS 82.41 vs 83.65 (Hindi is not a gated comparison); Language ID below Lingua on short text for the languages named under that table; embeddings cells below the model cards (shared with sentence-transformers); tokenizer load time (three rows, above); es WikiNER PER per type as listed in `results.md`. No recognizer, fast-tier gated or tokenizer exactness row is behind.
- **Not re-run at the gate:** open-type NER (it sits on the distilled encoder being retrained; part 2); Duckling and Lingua predictions were reused from pinned earlier runs; English entities have no paired spaCy file at the gate; embeddings MTEB rows stand because the package manifests are unchanged since those runs (parity re-run on the packaged files: bge max abs 4.03e-7, e5 3.20e-7, 0 id mismatches).
