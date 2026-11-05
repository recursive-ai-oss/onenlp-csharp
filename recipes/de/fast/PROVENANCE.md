# Provenance: German fast tier (`recipes/de/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.German/weights.manifest.props`.

- **Data:** UD German-GSD r2.18 (CC BY-SA 4.0; train and dev) for tags, morphology, lemmas, parser and sentence starts; GermEval 2014 (CC BY 4.0; official train and dev; outer level, OTH is MISC, derived and partial types are O) for entities. Hashes: `tools/fetch/de/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`.
- **Share-alike lineage:** yes (UD German-GSD). Label generator: none. Pretrained components: none.
- **Configuration (all defaults of the recipe):** `FastEncoder.English()` configuration (64-wide hash embeddings, one hash function and 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape, mixed to 256, two window layers). Tagging: AdamW 2e-3, 200 warm-up steps, dropout 0.1, 1,000-word batches, seed 1. Parser: arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document. NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7. Patience 6 on the development score everywhere (tagging: mean of UPOS, XPOS, UFeats, Lemmas; parser: LAS; NER: entity F1).
- **Produced:** 2026-11-05, AMD Ryzen 9 7950X3D (shared), 4 training threads, .NET SDK 10.0.403, OneModel 1.0.0. Wall time: tagging 16 min (25 epochs, best at step 5,078), parser 27 min in two chunks (15 epochs, best at step 1,697, dev LAS 80.02), NER 14 min (10 epochs, best at step 1,832, dev F1 66.89).
- **Weights (sha256):** `tagging.onenlp` ddce9e38d52563a2fd1a353332bf1f8f52374c52d17a966197ae6bf57ef2e79e (37.4 MB); `parser.onenlp` bb21d812d517f1f6ec8f90df375b0bc5996b9cfaf890e8ad8ddb8f8a24ff21b4 (46.2 MB); `ner.onenlp` f0afd41dd4545c02e58811b5c76a4a4ee3b2475bb7b1596114dcda73b524ee28 (35.7 MB).
- **Choices on the development set only** (the test file was read once, for the final models): three tagging encoders (4-character suffix with a cased table; the same at depth 3, width 320; the same with 96-wide embeddings) scored 92.23, 92.28 and 92.43 against 92.26 for the default, and three NER encoders (cased table; 4-character suffix with dropout 0.5; with a window layer) 66.79, 66.21, 65.65 against 66.89: no gain beyond noise, so the default stayed. Tokenization rules were chosen on the training and development files (`docs/segmentation.md`).
- **Separation check:** 27 of the 977 GSD test sentences also occur as exact text in the training file (short fragments); the official split is kept.

## Results (UD German-GSD r2.18 test and GermEval 2014 test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold tokens and sentences (paired with spaCy's predictions):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 94.56 [94.18, 94.94] | 94.59 [94.22, 94.95] | yes |
| XPOS | 95.35 [94.98, 95.71] | 95.21 [94.83, 95.57] | yes |
| UFeats | 86.25 [85.60, 86.96] | 84.71 [84.03, 85.39] | no (ahead) |
| AllTags | 82.78 [82.07, 83.52] | 81.29 [80.53, 82.02] | no (ahead) |
| Lemmas | 95.95 [95.67, 96.25] | 95.65 [95.34, 95.98] | yes |
| UAS | 83.27 [82.25, 84.32] | 81.88 [80.84, 82.93] | yes |
| LAS | 78.25 [77.23, 79.26] | 77.31 [76.29, 78.36] | yes |

Raw text (spaCy's tokenizer keeps `zum` as one token, so its Words and tagging rows lose on contractions):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.91 [99.84, 99.97] | 98.03 [97.69, 98.36] | no (ahead) |
| Sentences | 83.06 [80.56, 85.36] | 78.73 [75.98, 81.23] | yes |
| Words | 99.91 [99.84, 99.97] | 95.51 [95.06, 95.95] | no (ahead) |
| UPOS | 94.22 [93.83, 94.61] | 90.13 [89.57, 90.67] | no (ahead) |
| XPOS | 94.93 [94.57, 95.29] | 90.63 [90.05, 91.18] | no (ahead) |
| UFeats | 85.32 [84.67, 86.04] | 80.11 [79.28, 80.86] | no (ahead) |
| AllTags | 81.75 [81.02, 82.50] | 76.75 [75.92, 77.54] | no (ahead) |
| Lemmas | 95.52 [95.21, 95.84] | 91.28 [90.77, 91.81] | no (ahead) |
| UAS | 81.38 [80.38, 82.41] | 75.30 [74.16, 76.48] | no (ahead) |
| LAS | 76.75 [75.74, 77.76] | 70.77 [69.59, 71.95] | no (ahead) |

Entities, GermEval 2014 test, gold tokens (paired): micro F1 66.28 [64.94, 67.61] vs 65.77 [64.49, 67.19] (difference 0.51 [-0.72, 1.67], within noise); precision 70.67 vs 70.67; recall 62.40 vs 61.50. Per type: LOC 71.45 vs 70.20, PER 72.70 vs 70.80, ORG 57.28 vs 59.38 (difference -2.10 [-4.66, 0.55]), MISC 48.58 vs 49.68; all within noise.

**Misses:** none. The tagging heads now sit on a context encoder (see below); UFeats and AllTags in gold mode are ahead of spaCy outside the intervals, UPOS, XPOS and Lemmas are within noise, and every raw-text row is within noise or ahead. Parser rows are unchanged (the parser keeps its own encoder).

## Tagging encoder with context (replaces the first tagging model)

The first tagging model (`FastEncoder.English()` configuration, dev UPOS 93.59, XPOS 94.95, UFeats 84.62, AllTags 80.70, Lemmas 95.87) missed spaCy in the tagging rows; the errors were mostly decisions that need words far from the token (NOUN against PROPN, VERB against AUX, case and gender agreement). Its replacement adds one bidirectional LSTM layer (Hochreiter and Schmidhuber 1997; 64 units per direction, mapped back to the 256-wide window output and added to it) after one window layer, over each document, with ReLU everywhere and no layer normalization after the mix (`FastEncoder` settings `plain`, `relu`, `depth` 1, `recurrentLayers` 1, `recurrentSize` 64). Training uses AdamW at 3e-3 with 200 warm-up steps and a half-cosine fall over a fixed 20 epochs (Loshchilov and Hutter 2017) instead of a constant rate cut off by early stopping; the weights of the best development epoch are kept. The parser and entity models are unchanged.

```
train-tagging --train ... --dev ... --out DIR --plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --lr 0.003 --cosine-epochs 20 --patience 100 --threads 4
```

- **Data, seed:** UD German-GSD r2.18 train and dev only (CC BY-SA 4.0, share-alike lineage), seed 1, as above; the test file was read once for this configuration, for the rows above.
- **Development scores, gold tokens** (before, then after): UPOS 93.59, XPOS 94.95, UFeats 84.62, AllTags 80.70, Lemmas 95.87; UPOS 94.68, XPOS 95.74, UFeats 87.65, AllTags 84.11, Lemmas 96.30. Mean over the tagging metrics 92.26, then 93.59.
- **Weights:** `tagging.onenlp` sha256 `9551d090788d24898486fda8532a93bd3fbe821bfd71785fb3423ef869df6192`, 37,387,816 bytes, produced 2026-11-05 with 4 training threads, about 20 minutes. Fixed in `src/OneNlp.Models.German/weights.manifest.props`.
- **Screened on the development set before choosing:** a 2-layer LSTM of 128 per direction on the GELU encoder (dev mean slightly lower, at roughly twice the encoder cost); 1 layer of 16, 32 and 64 units; no window layer (cheaper, worse: the window layer matters); the cosine schedule against a constant rate; a rate of 3e-3 against 2e-3. A separate UPOS tagger head next to the morphologizer scored UPOS 93.45 against 93.59 (no gain), and a feature-only tagger head cannot predict "no features", so the joint head stays.
- **Cost** (indicative, one thread, `speed` command on development text, raw text through tokenizer, sentencizer and the tagging heads): 19.3k tokens/s (before: 17.4-22.1k). Encoder alone (1 thread, 5,632 tokens): one ReLU window layer 6.2 us/token; plus the 64-unit LSTM 11.9 us/token; 32 units 9.8; 128 units 18.7; no window layer with the 64-unit LSTM 6.1.
