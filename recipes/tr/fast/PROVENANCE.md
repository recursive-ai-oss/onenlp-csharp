# Provenance: Turkish fast tier (`recipes/tr/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Turkish/weights.manifest.props`.

- **Data:** UD Turkish-BOUN r2.18 (CC BY-SA 4.0; train and dev) for tags, morphology, lemmas, parser and sentence starts; Turkish-WikiNER (CC BY-SA 4.0; official train and dev; PERSON, ORG, GPE and LOC kept, GPE and LOC both LOC, every other type O) for entities. Hashes: `tools/fetch/tr/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`. The table-based tokenizer's multiword table (`src/OneNlp/Segmentation/Data/tr.mwt.tsv`, still the data of `SegmentationData.Turkish`) was written from the BOUN training split; the shipped tokenizer is the learned splitter.
- **Share-alike lineage:** yes (BOUN, Turkish-WikiNER). Label generator: none. Pretrained components: none.
- **Configuration:** Tagging (`tagging.onenlp`): fast encoder with 384-wide output, 96-wide hash embeddings (one hash function, 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape), two window layers, Turkish lower-casing of the form (`I` is `ı`, `İ` is `i`); AdamW 3e-3, 200 warm-up steps, dropout 0.2, 500-word batches, seed 1. Parser (before the retraining described below): the German-recipe defaults (256-wide `FastEncoder.English()` configuration with Turkish lower-casing, arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document). NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7, same encoder as the parser. Patience 6 on the development score everywhere (tagging: mean of UPOS, XPOS, UFeats, Lemmas; parser: LAS; NER: entity F1). Splitter (`splitter.onenlp`): whole-word Turkish tokenizer, then a B/I tagger over the characters of each letters-only token in its sentence window (3 to 48 letters, at most 40 tokens per window): fast encoder of width 128 (48-wide embeddings, 4,096 rows for form, prefix and suffix, 1,024 for shape, one window layer pair of depth 2, Turkish lower-casing) with one bidirectional LSTM of 64 units per direction; AdamW 1e-3, dropout 0.3, 2,000-unit batches, seed 1, patience 6 on the F1 of the cut positions. Tagging in the shipped package is the dev-chosen cosine model: 384-wide plain ReLU encoder, depth 1, 96-wide embeddings, cased table 32,768 rows, one bidirectional LSTM of 64, lr 3e-3, 500-word batches, dropout 0.2, 20 cosine epochs (`--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --cosine-epochs 20 --patience 100`).
- **Produced:** 2026-11-05 on a shared AMD Ryzen 9 7950X3D, 4 training threads each, .NET SDK 10.0.403, OneModel 1.0.0. Wall time: tagging 18 min (21 epochs, best at step 3,086, dev mean 74.09), parser 12 min (16 epochs, best at step 706, dev LAS 58.01), NER under 10 min (10 epochs, best at step 1,228, dev F1 67.65). Splitter and tagging retrained 2026-11-06 on the same machine type, 4 threads, OneModel 1.0.0: splitter 586 s (17 epochs, best at step 3,385, dev cut F1 62.75), tagging 1,073 s (20 epochs, best at step 3,291, dev mean 74.31 against 74.09 for the previous model).
- **Weights (sha256):** `splitter.onenlp` 2a4d8d9a7076c4c5ef0b9135593edcaa3e405c1d4af2dddae36fca31f48e00a2 (3.5 MB); `tagging.onenlp` 460b4b8eca237c61432dd5b5ccce116ce6386710003199b06649b1deac71131b (76.7 MB); `parser.onenlp` e52173eb0ccc1224bee97f9100f5aa3d5bf1c221895dc411e7eb3e6887c7635a (46.3 MB, retrained 2026-11-06, below); `ner.onenlp` 66f600a4cb4ad6a63fef6cb41c64487aceac008d6da9c7598c997150531cdbf2 (35.7 MB). The entity weights are unchanged.
- **Choices on the development set only** (the test file was read for two configurations: the first tagging model, then the final one). Tagging dev mean (UPOS, XPOS, UFeats, Lemmas): default 73.10; invariant lower-casing 73.04; suffix 4 + prefix 4 + depth 3 + 96-wide 72.90; suffix 5 71.67; dropout 0.2 73.31; two hashes with bigger tables 73.18; width 384 + 96-wide 73.70; cased table 73.25; lr 3e-3 with 500-word batches 73.87; the last three together with dropout 0.2 74.09 (chosen). The Turkish lower-casing is within noise on the development set and kept because it is the right mapping. The parser and the entity recognizer use their first configuration. Without the multiword table, raw-text development scores fall (UPOS 83.74 against 84.34 with it, Words 96.33 against 96.76). For the splitter and the retrained tagging, every choice was made on the development file (splitter: dropout 0.3, lr 1e-3, a larger model, two LSTM layers, a smaller encoder and a cosine schedule were compared by dev cut F1 and by dev raw UPOS/LAS with the final tagger and parser; tagging: three variants by dev mean); the test file was then read once, for the final configuration, in both modes. The end-to-end dev raw UPOS: never cut 83.74, table 84.34, shipped splitter 84.52 (with the previous tagging) and 84.75 (with the new tagging).
- **Parser data (shipped weights):** the treebank (train; dev for stopping) with the annotation projected onto the splitter's words, plus 60,239 sentences of FineWeb-2 (ODC-By 1.0) and Wikipedia (CC BY-SA) Turkish text parsed by earlier parsers of this recipe (labels: our own models; SA lineage; extra file sha256 952187e9b2f54a3a09f7304f6d36dc06db90db1d9e28f79a7dbf18cfb317c67f; no sentence equals or shares an 8-word run with a BOUN dev, BOUN test or PUD test sentence, `tools/tr_overlap.py`).
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
| UAS | 72.31 [70.90, 73.65] | 67.44 [66.05, 68.81] | ahead |
| LAS | 62.80 [61.37, 64.19] | 57.87 [56.52, 59.18] | ahead |
| CLAS | 59.08 [57.51, 60.55] | 55.03 [53.63, 56.41] | ahead |
| MLAS | 40.47 [39.19, 41.88] | 38.85 [37.60, 40.07] | within noise |
| BLEX | 48.59 [47.12, 49.99] | 42.60 [41.33, 43.88] | ahead |

Raw text (the BOUN `# text` lines, ten sentences per document):

| Metric | Ours | spaCy | Verdict |
|---|---|---|---|
| Tokens | 99.56 [99.41, 99.70] | 99.42 [99.25, 99.58] | within noise |
| Sentences | 86.84 [84.70, 89.04] | 82.09 [79.75, 84.65] | ahead |
| Words | 96.55 [96.15, 96.94] | 97.06 [96.70, 97.42] | within noise |
| UPOS | 86.02 [85.28, 86.68] | 86.82 [86.14, 87.48] | within noise |
| XPOS | 43.37 [42.59, 44.15] | 38.26 [37.45, 39.07] | ahead |
| UFeats | 76.42 [75.64, 77.18] | 77.20 [76.35, 77.92] | within noise |
| AllTags | 37.27 [36.52, 38.08] | 34.52 [33.76, 35.26] | ahead |
| Lemmas | 83.23 [82.48, 83.88] | 78.83 [78.02, 79.53] | ahead |
| UAS | 65.73 [64.24, 67.18] | 63.96 [62.53, 65.42] | within noise |
| LAS | 57.53 [56.08, 59.01] | 55.86 [54.53, 57.27] | within noise |
| CLAS | 54.83 [53.32, 56.34] | 53.43 [52.04, 54.84] | within noise |
| MLAS | 38.10 [36.77, 39.39] | 38.02 [36.79, 39.23] | within noise |
| BLEX | 45.80 [44.41, 47.25] | 41.28 [39.99, 42.64] | ahead |

Entities, Turkish-WikiNER test, gold tokens (paired): micro F1 67.40 [64.90, 69.81] vs 65.82 [63.16, 68.28] (difference 1.57 [-0.89, 4.03], within noise); precision 68.10 vs 68.49; recall 66.71 vs 63.36 (difference 3.36 [0.81, 5.95]). Per type: LOC 68.80 vs 68.30, ORG 50.79 vs 46.49, PER 71.63 vs 69.62; all within noise.

**Raw-text parsing (the previous misses: CLAS -3.59 and MLAS -3.15).** On development, raw LAS was 5 to 6.5 points under gold LAS with any tokenizer, and the share of that gap that stayed when the sentences were cut by hand was small (gold sentences one per document: LAS 54.4 against 53.9), so the loss was in the parser itself, mostly in sentences where a multiword token is cut differently from the treebank. Levers, all on the development file (raw CLAS / MLAS; the parser of the previous model: 49.95 / 32.79): a smaller batch (400 words, lr 0.004) 51.88 / 34.44; dropout 0.35 and 0.45 at 700-word batches 50.60 and 52.63 (MLAS 33.54, 35.23); a wider encoder, three LSTM layers, a window layer, two hashes, longer affixes: 49.8 to 52.1, within seed noise (seed 7 against 11: 0.5); training on the splitter's words with the treebank annotation projected onto them (`--predicted-words`): +1.0 CLAS; the dropout 0.45 setting with the projection: 52.63 / 35.23. What moved it was more parsed text: 120,000 sentences of Turkish web text (FineWeb-2 and Wikipedia, `distill/text/tr.jsonl`) parsed by the p45 parser and by a second parser (two hashes), keeping the 32,761 sentences where the two agree on 80% of the words or more (`tools/tr_agree.py`), added to the treebank for a new parser: 54.88 / 36.41 (every sentence of 40,000 without the agreement filter: 53.49 / 35.49; agreement of 100% leaves 7% of the sentences and is not enough text). A second round, labelled by that parser and the unfiltered one on 70,000 other sentences (27,478 agreeing at 80%, 60,239 sentences in all with the first round), gave 55.02 / 36.76 at dropout 0.45 and 54.87 / 36.35 at 0.30: the same within noise, and the dropout 0.45 model is the one shipped. The test file was read once for it, in both modes.

The shipped parser: seed 7, dropout 0.45, 700-word batches, lr 0.003, ten sentences per document, `--predicted-words splitter.onenlp --extra pseudo-r12.conllu`; 11 epochs (best at step 6,134, dev LAS 61.90 on the splitter's words), 3 chunks of at most 29 minutes on 4 threads. No row is behind spaCy; the tagging heads, the splitter and the entity recognizer are unchanged.
