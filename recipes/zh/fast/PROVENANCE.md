# Provenance: Chinese fast tier (`recipes/zh/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Chinese/weights.manifest.props`.

- **Data:** UD Chinese-GSDSimp r2.18 (CC BY-SA 4.0; train and dev) for words, tags, morphology, lemmas, parser and sentence starts; UNER Chinese-GSDSIMP (commit 1f030759, CC BY-SA 4.0; train and dev) for entities. Hashes: `tools/fetch/zh/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`. The Traditional-script UD Chinese-GSD is not used: its sentences are those of GSDSimp, so it would put test sentences in training.
- **Share-alike lineage:** yes, every part. Label generator: none. Pretrained components: none.
- **Configuration (final):**
  - *Segmenter:* `FastEncoder` width 192, two window layers, ReLU, 64-wide embeddings (one hash) of the unit's lower-cased text (16,384 rows), one-character prefix and suffix (4,096 rows each), shape (1,024), previous and next unit (8,192 rows per table), units two away and the shapes of the neighbours (8,192), unit pairs (32,768 per table), two bidirectional LSTM layers of 96 units per direction; tagger over `B`/`I`. AdamW 1e-3, 200 warm-up steps, half-cosine over 12 epochs, dropout 0.5, 1,000-unit batches, eight sentences per document, seed 1.
  - *Tagging:* width 256, one ReLU window layer, one 64-unit LSTM layer, no layer normalization after the mix, 64-wide embeddings (one hash, 32,768 rows) of the lower-cased word and its one-character prefix and suffix and shape; AdamW 3e-3, half-cosine over 20 epochs, dropout 0.1, 1,000-word batches, seed 1. Tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer.
  - *Parser:* width 256, two window layers with layer normalization, 64-wide embeddings (one hash, 32,768 rows) of the lower-cased word, one-character prefix and suffix and shape, arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, ten sentences per document, at most 40 epochs, patience 6, seed 7.
  - *NER:* width 256, two window layers, 96-wide embeddings with two hashes (32,768 rows) of the lower-cased word, one-character prefix and suffix and shape; BILUO tagging, hidden 256, dropout 0.5, lr 3e-3, patience 8, seed 7.
- **Produced:** 2026-11-06, AMD Ryzen 9 7950X3D (shared), 4 training threads per part, in chunks of at most 28 minutes that resume from checkpoints; .NET SDK 10.0.403, OneModel 1.0.0. Segmenter: 12 epochs, 449 s (best dev word F1 over units 89.18 at the last epoch). Tagging: 20 epochs, 260 s (best at step 1,615, dev mean of UPOS, XPOS, UFeats, Lemmas 94.97). Parser: 40 epochs in two chunks (best at step 2,457, dev LAS 68.22). NER: 13 epochs (best at step 504, dev F1 63.81).
- **Weights (sha256):** `segmenter.onenlp` 6520915c3d6cfa6d238012e52cdae03641154b8d9fc81e31ee8e1ffcd934b3cb (43.5 MB); `tagging.onenlp` 3f773289d49b3b905e35993c6928266919ac74effa95c9f0c8ef061b5857bc59 (35.5 MB); `parser.onenlp` 773dca5b6aa51f4ff2615ea6028e337e5d9176ea0d83266f00ed41966f50187e (46.2 MB); `ner.onenlp` 97ac3eb877d88cef2bea9628310259acf8c655663e47de3b509b1708c6287c5d (52.6 MB).
- **Choices on the development set only** (every number is dev):
  - *Segmenter* (word F1 over units): 128 wide, one LSTM layer of 64, dropout 0.1: 87.04; the same with dropout 0.4: 87.06; fewer, narrower tables: 86.18; 192 wide, two LSTM layers of 96, dropout 0.5: 88.39; 256 wide, two layers of 128: 88.75; 192 wide, three layers: 88.52; 192 wide, two layers, lr 1e-3, 1,000-unit batches (kept): 89.18. The training loss reaches zero by epoch 8 in every run (the 4,000 training sentences are memorised); the lever that helped was capacity in the recurrent layers, not regularisation.
  - *Tagging* (mean of UPOS, XPOS, UFeats, Lemmas): three-character affixes 93.47; one-character prefix and suffix (kept) 94.97; two hashes with 32,768 norm rows and 96-wide embeddings 94.95 and 94.93; prefix 2, suffix 1: 94.52; previous and next word tables and word pairs 93.65; 256 wide with two layers: 93.42. Chinese words are short, so the three-character affixes of the English encoder were the whole word and one character carries the information a morphological affix does elsewhere.
  - *Parser* (dev LAS): defaults 64.89 (30 epochs); one-character affixes, 40 epochs (kept) 68.22; with two hashes and 96-wide embeddings 66.85; a larger LSTM (256 units, three layers) and a context layer with larger arc and label sizes were stopped at their chunk limit after 18 and 28 epochs and not resumed; at the same epochs the kept configuration had 66.67 (against 66.48) and 67.54 (against 67.01) on dev.
  - *NER* (dev F1): defaults 53.90; one-character affixes, dropout 0.5: 59.01; the same with span decoding 59.35; plus neighbour tables, an LSTM layer and a narrower encoder 57.09; two hashes with 96-wide embeddings (kept) 63.81; plus neighbour tables 61.44; plus an LSTM layer 63.43.
- **Test reads:** the final configuration once for UD (both modes) and once for entities (gold words, and raw text with the segmenter). Nothing was chosen on test.
- **Separation check:** none of the 500 test sentences occurs in the training or development files, in either family (`tools/fetch/zh/MANIFEST.md`).
- **Cost** (indicative, one thread on the shared worker at load average 8-13, raw test text, `speed`): segmenter alone 22-23k words/s (3.7 kB allocated per word); segmenter and tagging 16-17k words/s; whole pipeline 4.5-4.6k words/s.
- **Training time against PyTorch:** the heads and encoder are the English ones (C#-to-PyTorch CPU ratios 0.5 to 1.7); the ratio was not re-timed.

The dependency-parser and entity numbers on dev are lower than on test (the dev and test files differ in difficulty): the test rows are in `docs/results.md`.

## Parser, faster configuration

The parser part was retrained with one LSTM layer and product label features (`docs/parser.md`, "Speed configuration"): flags `--prefix 1 --suffix 1 --epochs 40 --lstm 192 --lstm-layers 1 --arc 192 --label 96 --label-bilinear 0 --label-product 1`; everything else as above (seed 7, dropout 0.25, 1,500-word batches, ten sentences per document, patience 6 on dev LAS). Data: UD Chinese-GSDSimp r2.18 train and dev (CC BY-SA 4.0 or CC BY 4.0 as listed above; share-alike lineage unchanged). Best dev LAS 66.814 at step 2,313; about 13 minutes on 4 threads of the shared worker; trainer weights hash `f721ccf0d2a578b7bfa9d08e82b003197afa36b9403f1f2dccb1741ac222792c`. File `parser.onenlp`: 39,260,234 bytes, sha256 `eb5a8e1dc60ac5536dc1e22bb9b43dc704ea0cda22c5b12f5ba3b157aef88457`. Test rows, speed and the earlier parser's rows: `docs/results.md`, "Parser speed". The earlier parser is reproduced by the same command without the label and LSTM flags above (defaults of the recipe).

## Entity confidence threshold

The entity model's output had precision below recall (test 62.44 against spaCy's 67.08, paired -4.64 [-8.52, -0.50]). `EntityRecognizer.MinConfidence` drops decoded entities whose confidence (the mean tag probability of their tokens) is below a value; the weights are unchanged, so no retraining was done. The value was chosen on the development file only (500 sentences, 753 entities), by `ner-eval --min-confidence`:

| Threshold | F1 | Precision | Recall |
|---|---|---|---|
| 0 (before) | 63.81 | 63.35 | 64.28 |
| 0.5 | 62.74 | 64.78 | 60.82 |
| 0.6 | 63.32 | 68.91 | 58.57 |
| 0.7 | 62.91 | 72.37 | 55.64 |
| 0.8 | 61.82 | 76.37 | 51.93 |
| 0.9 | 59.48 | 81.34 | 46.88 |

0.6 is the lowest value with precision up by more than the 4.6-point gap and F1 within 1 point of the best (the rule was written before the test read). `ner-threshold MODEL OUT 0.6` writes the shipped `ner.onenlp` from the earlier file (same weights, `min_confidence` added to the head's configuration): 52,586,107 bytes, sha256 `840bd9287c6c7ffe9f8b8f724480558c8272acd309961b266f2e825c323993a7` (before: `97ac3eb877d88cef2bea9628310259acf8c655663e47de3b509b1708c6287c5d`). SA lineage unchanged.

UNER Chinese-GSDSIMP test, gold tokens, paired against spaCy 3.8.18 `gold_preproc` (1,000 resamples, seed 1; the test file was read once for this setting; the same file was already read for the earlier model): micro F1 63.79 [59.39, 68.27] vs 61.86 (+1.93 [-1.48, 5.33]); precision 69.28 vs 67.08 (+2.20 [-2.02, 6.59]); recall 59.11 vs 57.40 (+1.70 [-1.61, 5.12]); LOC 70.38 vs 68.15, ORG 51.28 vs 45.00, PER 57.95 vs 59.08. Every row is within noise. Before: 62.80, 62.44, 63.17, LOC 69.86, ORG 47.91, PER 57.69. Speed: the filter runs on the few decoded entities, so tokens per second do not change.
