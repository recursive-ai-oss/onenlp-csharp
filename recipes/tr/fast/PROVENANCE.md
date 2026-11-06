# Provenance: Turkish fast tier (`recipes/tr/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Turkish/weights.manifest.props`.

- **Data:** UD Turkish-BOUN r2.18 (CC BY-SA 4.0; train and dev) for tags, morphology, lemmas, parser and sentence starts; Turkish-WikiNER (CC BY-SA 4.0; official train and dev; PERSON, ORG, GPE and LOC kept, GPE and LOC both LOC, every other type O) for entities. Hashes: `tools/fetch/tr/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`. The table-based tokenizer's multiword table (`src/OneNlp/Segmentation/Data/tr.mwt.tsv`, still the data of `SegmentationData.Turkish`) was written from the BOUN training split; the shipped tokenizer is the learned splitter.
- **Share-alike lineage:** yes (BOUN, Turkish-WikiNER). Label generator: none. Pretrained components: none.
- **Configuration:** Tagging (`tagging.onenlp`): fast encoder with 384-wide output, 96-wide hash embeddings (one hash function, 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape), two window layers, Turkish lower-casing of the form (`I` is `ı`, `İ` is `i`); AdamW 3e-3, 200 warm-up steps, dropout 0.2, 500-word batches, seed 1. Parser: the German-recipe defaults (256-wide `FastEncoder.English()` configuration with Turkish lower-casing, arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document). NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7, same encoder as the parser. Patience 6 on the development score everywhere (tagging: mean of UPOS, XPOS, UFeats, Lemmas; parser: LAS; NER: entity F1). Splitter (`splitter.onenlp`): whole-word Turkish tokenizer, then a B/I tagger over the characters of each letters-only token in its sentence window (3 to 48 letters, at most 40 tokens per window): fast encoder of width 128 (48-wide embeddings, 4,096 rows for form, prefix and suffix, 1,024 for shape, one window layer pair of depth 2, Turkish lower-casing) with one bidirectional LSTM of 64 units per direction; AdamW 1e-3, dropout 0.3, 2,000-unit batches, seed 1, patience 6 on the F1 of the cut positions. Tagging in the shipped package is the dev-chosen cosine model: 384-wide plain ReLU encoder, depth 1, 96-wide embeddings, cased table 32,768 rows, one bidirectional LSTM of 64, lr 3e-3, 500-word batches, dropout 0.2, 20 cosine epochs (`--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --cosine-epochs 20 --patience 100`).
- **Produced:** 2026-11-05 on a shared AMD Ryzen 9 7950X3D, 4 training threads each, .NET SDK 10.0.403, OneModel 1.0.0. Wall time: tagging 18 min (21 epochs, best at step 3,086, dev mean 74.09), parser 12 min (16 epochs, best at step 706, dev LAS 58.01), NER under 10 min (10 epochs, best at step 1,228, dev F1 67.65). Splitter and tagging retrained 2026-11-06 on the same machine type, 4 threads, OneModel 1.0.0: splitter 586 s (17 epochs, best at step 3,385, dev cut F1 62.75), tagging 1,073 s (20 epochs, best at step 3,291, dev mean 74.31 against 74.09 for the previous model).
- **Weights (sha256):** `splitter.onenlp` 2a4d8d9a7076c4c5ef0b9135593edcaa3e405c1d4af2dddae36fca31f48e00a2 (3.5 MB); `tagging.onenlp` 460b4b8eca237c61432dd5b5ccce116ce6386710003199b06649b1deac71131b (76.7 MB); `parser.onenlp` 3545c480763a571980a92dab1eaa5bdf69e473e5105562423333d90c5a1c8d59 (46.3 MB); `ner.onenlp` 66f600a4cb4ad6a63fef6cb41c64487aceac008d6da9c7598c997150531cdbf2 (35.7 MB). The parser and entity weights are unchanged.
- **Choices on the development set only** (the test file was read for two configurations: the first tagging model, then the final one). Tagging dev mean (UPOS, XPOS, UFeats, Lemmas): default 73.10; invariant lower-casing 73.04; suffix 4 + prefix 4 + depth 3 + 96-wide 72.90; suffix 5 71.67; dropout 0.2 73.31; two hashes with bigger tables 73.18; width 384 + 96-wide 73.70; cased table 73.25; lr 3e-3 with 500-word batches 73.87; the last three together with dropout 0.2 74.09 (chosen). The Turkish lower-casing is within noise on the development set and kept because it is the right mapping. The parser and the entity recognizer use their first configuration. Without the multiword table, raw-text development scores fall (UPOS 83.74 against 84.34 with it, Words 96.33 against 96.76). For the splitter and the retrained tagging, every choice was made on the development file (splitter: dropout 0.3, lr 1e-3, a larger model, two LSTM layers, a smaller encoder and a cosine schedule were compared by dev cut F1 and by dev raw UPOS/LAS with the final tagger and parser; tagging: three variants by dev mean); the test file was then read once, for the final configuration, in both modes. The end-to-end dev raw UPOS: never cut 83.74, table 84.34, shipped splitter 84.52 (with the previous tagging) and 84.75 (with the new tagging).
- **Separation check:** 6 of the 979 BOUN test sentences also occur as exact text in the training file (short fragments); the official split is kept.

## Results (UD Turkish-BOUN r2.18 test and Turkish-WikiNER test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold words and sentences (paired with spaCy's predictions; the scored file is the final configuration, read once):

| Metric | Ours | spaCy | Verdict |
|---|---|---|---|
| UPOS | 88.97 [88.31, 89.58] | 88.04 [87.37, 88.64] | within noise |
| XPOS | 44.57 [43.78, 45.33] | 38.59 [37.80, 39.37] | ahead |
| UFeats | 78.15 [77.40, 78.90] | 78.30 [77.46, 79.05] | within noise |
| AllTags | 38.13 [37.40, 38.89] | 34.70 [33.96, 35.45] | ahead |
| Lemmas | 85.11 [84.38, 85.74] | 80.24 [79.47, 80.92] | ahead |
| UAS | 69.58 [68.26, 70.95] | 67.44 [66.05, 68.81] | within noise |
| LAS | 58.70 [57.32, 60.05] | 57.87 [56.52, 59.18] | within noise |
| CLAS | 54.16 [52.68, 55.54] | 55.03 [53.63, 56.41] | within noise |
| MLAS | 37.14 [35.88, 38.37] | 38.85 [37.60, 40.07] | within noise |
| BLEX | 44.51 [43.15, 45.84] | 42.60 [41.33, 43.88] | within noise |

Raw text (the BOUN `# text` lines, ten sentences per document):

| Metric | Ours | spaCy | Verdict |
|---|---|---|---|
| Tokens | 99.56 [99.41, 99.70] | 99.42 [99.25, 99.58] | within noise |
| Sentences | 87.28 [85.14, 89.25] | 82.09 [79.75, 84.65] | ahead |
| Words | 96.55 [96.15, 96.94] | 97.06 [96.70, 97.42] | within noise |
| UPOS | 86.02 [85.28, 86.68] | 86.82 [86.14, 87.48] | within noise |
| XPOS | 43.37 [42.59, 44.15] | 38.26 [37.45, 39.07] | ahead |
| UFeats | 76.42 [75.64, 77.18] | 77.20 [76.35, 77.92] | within noise |
| AllTags | 37.27 [36.52, 38.08] | 34.52 [33.76, 35.26] | ahead |
| Lemmas | 83.23 [82.48, 83.88] | 78.83 [78.02, 79.53] | ahead |
| UAS | 63.24 [61.92, 64.64] | 63.96 [62.53, 65.42] | within noise |
| LAS | 53.62 [52.28, 54.97] | 55.86 [54.53, 57.27] | within noise |
| CLAS | 49.84 [48.50, 51.22] | 53.43 [52.04, 54.84] | behind |
| MLAS | 34.87 [33.67, 36.05] | 38.02 [36.79, 39.23] | behind |
| BLEX | 41.85 [40.56, 43.13] | 41.28 [39.99, 42.64] | within noise |

Entities, Turkish-WikiNER test, gold tokens (paired): micro F1 67.40 [64.90, 69.81] vs 65.82 [63.16, 68.28] (difference 1.57 [-0.89, 4.03], within noise); precision 68.10 vs 68.49; recall 66.71 vs 63.36 (difference 3.36 [0.81, 5.95]). Per type: LOC 68.80 vs 68.30, ORG 50.79 vs 46.49, PER 71.63 vs 69.62; all within noise.

**Misses (raw text only):** CLAS (-3.59) and MLAS (-3.15). Every gold-token row is within noise or ahead. Before this model (table of word endings, previous tagging weights) the raw misses were UPOS (-1.36), CLAS (-3.41) and MLAS (-3.79) and the gold miss was MLAS (-2.49): the learned splitter and the retrained tagging heads close UPOS and the gold MLAS gap and leave CLAS and MLAS about where they were. What was tried, on the development file only: five splitter configurations of two sizes (dev F1 of the cuts 59 to 63, with epoch-to-epoch swings of 3 points because the file has 253 multiword tokens), a cosine schedule and a minimum cut confidence of 50 to 97 (precision stays 55 to 60%, raw LAS/CLAS/MLAS move by at most 0.3), three tagging variants (dev mean 73.9 to 74.3; gold MLAS unchanged because it is set by the parser and by the exact feature strings of auxiliaries, whose gold annotation is inconsistent: `tı` is `Aspect=Perf|...|Tense=Past` in some sentences and `Polarity=Pos|...` in others), and keeping the rule sentencizer (worse: raw LAS 52.7 against 53.9). The remaining raw gap is in the parser's raw-text behaviour: on development, raw LAS is 6.5 points under gold LAS with a tokenizer that never cuts (52.1 against 58.6) and 4.7 under it with the splitter (53.9), more than the multiword tokens explain (about 3.7% of the words), while spaCy loses 2 points on test. The parser and its sentence starts are out of this model's scope.
