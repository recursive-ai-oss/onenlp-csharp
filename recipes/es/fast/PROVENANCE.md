# Provenance: Spanish fast tier (`recipes/es/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Spanish/weights.manifest.props`.

- **Data:** UD Spanish-AnCora r2.18 (CC BY 4.0; train and dev) for tags, morphology, lemmas, parser and sentence starts; WikiNER Spanish wp3 (CC BY 4.0 labels, silver, over Wikipedia text; our split by article index, `tools/fetch/es/MANIFEST.md`; train and dev) for entities, with `del`, `al` and verbs with enclitics cut into the tokenizer's pieces. Hashes: `tools/fetch/es/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`.
- **Share-alike lineage:** yes for the entity recognizer (Wikipedia text); the tagging and parser parts use CC BY data only. Label generator: none. Pretrained components: none.
- **Configuration (all defaults of the recipe):** `FastEncoder.English()` configuration (64-wide hash embeddings, one hash function and 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape, mixed to 256, two window layers). Tagging: AdamW 2e-3, 200 warm-up steps, dropout 0.1, 1,000-word batches, seed 1. Parser: arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document, at most 40 epochs. NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7. Patience 6 on the development score everywhere (tagging: mean of UPOS, XPOS, UFeats, Lemmas; parser: LAS; NER: entity F1).
- **Produced:** 2026-11-05, AMD Ryzen 9 7950X3D (shared), 4 training threads per part, in chunks of at most 28 minutes that resume from checkpoints; .NET SDK 10.0.403, OneModel 1.0.0. Tagging: 17 epochs (best at step 5,091, dev mean 95.05). Parser: 40 epochs, still improving slowly at the cap (best at step 11,983, dev LAS 87.25). NER: 13 epochs (best at step 22,877, dev F1 85.59); an earlier NER run on whole tokens (`del` unsplit) was discarded for raw-text use, see the README.
- **Weights (sha256):** `tagging.onenlp` b38d7e6bd263385c3de75d61ee57f5b4b9bb23cb7c3d0567f98eb762f6c37ebf (37.1 MB); `parser.onenlp` 0e4a5757c048902f7bc150e85901dfd5c7e210b3e153641778256730945a6073 (46.0 MB); `ner.onenlp` 6997910ba28da1ad85d87c16003d832db02f9838d182bfd8d3e2e0022aad03cc (35.7 MB).
- **Choices on the development set only:** the default encoder was kept for all three parts (German's variants gave no gain beyond noise there; none were tried here). Tokenization rules were chosen on the training and development files (`docs/segmentation.md`). The whole-token NER run was scored on the test file once, to measure what the pieces cost, before the pieces run was trained; the pieces run was selected on dev.
- **Separation check:** 5 of the 1,721 AnCora test sentences, and 69 of the 6,468 WikiNER test sentences, also occur as exact text in the training file (short fragments); the official splits are kept.
- **Training time against PyTorch:** the architectures are the English ones (same heads and encoder), whose C#-to-PyTorch CPU ratios were 0.5 to 1.7; the ratio was not re-timed.

## Results (UD Spanish-AnCora r2.18 test and WikiNER Spanish wp3 test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold words and sentences (the treebank's, as the reference is run; paired with spaCy's predictions):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 97.80 [97.65, 97.94] | 98.18 [98.04, 98.32] | no (behind) |
| XPOS | 86.32 [85.90, 86.74] | 84.97 [84.51, 85.44] | no (ahead) |
| UFeats | 97.39 [97.24, 97.56] | 97.74 [97.58, 97.89] | no (behind) |
| AllTags | 85.65 [85.23, 86.07] | 84.47 [84.01, 84.93] | no (ahead) |
| Lemmas | 98.70 [98.58, 98.81] | 98.72 [98.59, 98.83] | yes |
| UAS | 90.05 [89.57, 90.49] | 89.04 [88.59, 89.52] | no (ahead) |
| LAS | 87.00 [86.50, 87.51] | 85.95 [85.44, 86.44] | no (ahead) |

Raw text (spaCy's tokenizer keeps `del` and `verlo` whole, so its Words and tagging rows lose on multiword tokens):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.84 [99.76, 99.90] | 99.90 [99.85, 99.94] | yes |
| Sentences | 98.14 [97.39, 98.87] | 97.70 [96.89, 98.49] | yes |
| Words | 99.83 [99.75, 99.89] | 96.60 [96.40, 96.80] | no (ahead) |
| UPOS | 97.57 [97.41, 97.73] | 94.81 [94.56, 95.02] | no (ahead) |
| XPOS | 86.09 [85.68, 86.52] | 85.55 [85.12, 86.01] | yes |
| UFeats | 97.17 [97.00, 97.35] | 94.57 [94.32, 94.81] | no (ahead) |
| AllTags | 85.40 [84.97, 85.83] | 85.05 [84.62, 85.52] | yes |
| Lemmas | 98.48 [98.34, 98.60] | 95.37 [95.15, 95.59] | no (ahead) |
| UAS | 89.72 [89.22, 90.19] | 84.87 [84.34, 85.40] | no (ahead) |
| LAS | 86.70 [86.15, 87.23] | 81.82 [81.25, 82.37] | no (ahead) |

Entities, WikiNER Spanish wp3 test, gold tokens (paired; our model reads the tokenizer's pieces and each token takes the tag of its first piece): micro F1 82.88 [82.12, 83.64] vs 83.35 [82.63, 84.08] (difference -0.46 [-1.14, 0.20], within noise); precision 82.86 vs 83.98 (-1.12 [-1.81, -0.46], inside the noise rule); recall 82.91 vs 82.72. Per type: LOC 84.38 vs 84.95 and MISC 69.01 vs 68.53 within noise; ORG 76.75 vs 71.79 (+4.96 [2.89, 7.24], ahead); PER 88.49 vs 90.45 (-1.96 [-2.83, -1.13], behind).

**Misses:** in gold-word mode UPOS (-0.38) and UFeats (-0.35) are behind spaCy outside the intervals, and PER entities (-1.96). Everything else is within noise or ahead.
