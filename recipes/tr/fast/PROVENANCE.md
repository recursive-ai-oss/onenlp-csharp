# Provenance: Turkish fast tier (`recipes/tr/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Turkish/weights.manifest.props`.

- **Data:** UD Turkish-BOUN r2.18 (CC BY-SA 4.0; train and dev) for tags, morphology, lemmas, parser and sentence starts; Turkish-WikiNER (CC BY-SA 4.0; official train and dev; PERSON, ORG, GPE and LOC kept, GPE and LOC both LOC, every other type O) for entities. Hashes: `tools/fetch/tr/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`. The tokenizer's multiword table (`src/OneNlp/Segmentation/Data/tr.mwt.tsv`) was written from the BOUN training split, and its two thresholds were chosen on the development split.
- **Share-alike lineage:** yes (BOUN, Turkish-WikiNER). Label generator: none. Pretrained components: none.
- **Configuration:** Tagging (`tagging.onenlp`): fast encoder with 384-wide output, 96-wide hash embeddings (one hash function, 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape), two window layers, Turkish lower-casing of the form (`I` is `ı`, `İ` is `i`); AdamW 3e-3, 200 warm-up steps, dropout 0.2, 500-word batches, seed 1. Parser: the German-recipe defaults (256-wide `FastEncoder.English()` configuration with Turkish lower-casing, arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document). NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7, same encoder as the parser. Patience 6 on the development score everywhere (tagging: mean of UPOS, XPOS, UFeats, Lemmas; parser: LAS; NER: entity F1).
- **Produced:** 2026-11-05 on a shared AMD Ryzen 9 7950X3D, 4 training threads each, .NET SDK 10.0.403, OneModel 1.0.0. Wall time: tagging 18 min (21 epochs, best at step 3,086, dev mean 74.09), parser 12 min (16 epochs, best at step 706, dev LAS 58.01), NER under 10 min (10 epochs, best at step 1,228, dev F1 67.65).
- **Weights (sha256):** `tagging.onenlp` 700d5a0c09fa6cba7ff05524ae313f23cc74167537ccb8ca1a2b8e9e14304c95 (64.7 MB); `parser.onenlp` 3545c480763a571980a92dab1eaa5bdf69e473e5105562423333d90c5a1c8d59 (46.3 MB); `ner.onenlp` 66f600a4cb4ad6a63fef6cb41c64487aceac008d6da9c7598c997150531cdbf2 (35.7 MB).
- **Choices on the development set only** (the test file was read for two configurations: the first tagging model, then the final one). Tagging dev mean (UPOS, XPOS, UFeats, Lemmas): default 73.10; invariant lower-casing 73.04; suffix 4 + prefix 4 + depth 3 + 96-wide 72.90; suffix 5 71.67; dropout 0.2 73.31; two hashes with bigger tables 73.18; width 384 + 96-wide 73.70; cased table 73.25; lr 3e-3 with 500-word batches 73.87; the last three together with dropout 0.2 74.09 (chosen). The Turkish lower-casing is within noise on the development set and kept because it is the right mapping. The parser and the entity recognizer use their first configuration. Without the multiword table, raw-text development scores fall (UPOS 83.74 against 84.34 with it, Words 96.33 against 96.76).
- **Separation check:** 6 of the 979 BOUN test sentences also occur as exact text in the training file (short fragments); the official split is kept.

## Results (UD Turkish-BOUN r2.18 test and Turkish-WikiNER test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold words and sentences (paired with spaCy's predictions):

| Metric | Ours | spaCy | Verdict |
|---|---|---|---|
| UPOS | 88.42 [87.78, 89.01] | 88.04 [87.42, 88.65] | within noise |
| XPOS | 44.68 [43.89, 45.45] | 38.59 [37.78, 39.41] | ahead |
| UFeats | 77.48 [76.64, 78.28] | 78.30 [77.49, 79.06] | within noise |
| AllTags | 38.12 [37.35, 38.81] | 34.70 [33.92, 35.48] | ahead |
| Lemmas | 84.88 [84.23, 85.52] | 80.24 [79.45, 80.92] | ahead |
| UAS | 69.58 [68.32, 70.84] | 67.44 [66.19, 68.82] | within noise |
| LAS | 58.70 [57.51, 59.92] | 57.87 [56.66, 59.24] | within noise |
| CLAS | 54.16 [52.93, 55.52] | 55.03 [53.70, 56.50] | within noise |
| MLAS | 36.36 [35.14, 37.58] | 38.85 [37.59, 40.05] | behind |
| BLEX | 44.17 [42.99, 45.53] | 42.60 [41.33, 43.86] | within noise |

Raw text (the BOUN `# text` lines, ten sentences per document):

| Metric | Ours | spaCy | Verdict |
|---|---|---|---|
| Tokens | 99.56 [99.40, 99.71] | 99.42 [99.24, 99.59] | within noise |
| Sentences | 87.46 [85.18, 89.55] | 82.09 [79.43, 84.60] | ahead |
| Words | 96.71 [96.32, 97.09] | 97.06 [96.69, 97.42] | within noise |
| UPOS | 85.46 [84.70, 86.12] | 86.82 [86.13, 87.51] | behind |
| XPOS | 43.19 [42.40, 43.95] | 38.26 [37.48, 39.09] | ahead |
| UFeats | 75.67 [74.85, 76.44] | 77.20 [76.36, 77.99] | within noise |
| AllTags | 37.01 [36.27, 37.77] | 34.52 [33.74, 35.31] | ahead |
| Lemmas | 82.87 [82.22, 83.52] | 78.83 [78.04, 79.55] | ahead |
| UAS | 63.43 [62.10, 64.74] | 63.96 [62.56, 65.47] | within noise |
| LAS | 53.59 [52.37, 54.88] | 55.86 [54.55, 57.23] | within noise |
| CLAS | 50.02 [48.72, 51.31] | 53.43 [52.11, 54.88] | behind |
| MLAS | 34.21 [33.09, 35.32] | 38.02 [36.81, 39.26] | behind |
| BLEX | 41.42 [40.32, 42.65] | 41.28 [40.07, 42.58] | within noise |

Entities, Turkish-WikiNER test, gold tokens (paired): micro F1 67.40 [64.90, 69.81] vs 65.82 [63.16, 68.28] (difference 1.57 [-0.89, 4.03], within noise); precision 68.10 vs 68.49; recall 66.71 vs 63.36 (difference 3.36 [0.81, 5.95]). Per type: LOC 68.80 vs 68.30, ORG 50.79 vs 46.49, PER 71.63 vs 69.62; all within noise.

**Misses:** MLAS in both modes (-2.5 gold, -3.8 raw: it needs the UFeats string and the function-word attachments right), and in raw text UPOS (-1.36) and CLAS (-3.41). Gold-token UFeats (-0.82) is inside the interval but under spaCy. Raw text loses on Words (96.71 against 97.06) because BOUN's multiword tokens (`vardır` = `var` + `dır`) are not predictable from the spelling: the table of word endings makes about half the cuts the treebank does, and the other half are wrong cuts that cost words.
