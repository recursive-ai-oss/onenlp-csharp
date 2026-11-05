# Provenance: Dutch fast tier (`recipes/nl/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Dutch/weights.manifest.props`.

- **Data:** UD Dutch-Alpino r2.18 (CC BY-SA 4.0; train and dev) for tags, morphology, lemmas, parser and sentence starts; WikiNER Dutch wp3 (labels CC BY 4.0, text from Wikipedia; train and dev, our article-index split) for entities. LassySmall (allowed) was not used. Hashes: `tools/fetch/nl/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`.
- **Share-alike lineage:** yes. Label generator: none. Pretrained components: none.
- **Configuration (all defaults of the recipe):** `FastEncoder.English()` configuration (64-wide hash embeddings, one hash function and 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape, mixed to 256, two window layers). Tagging: AdamW 2e-3, 200 warm-up steps, dropout 0.1, 1,000-word batches, seed 1. Parser: arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document. NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7. Patience 6 on the development score everywhere (tagging: mean of UPOS, XPOS, UFeats, Lemmas; parser: LAS; NER: entity F1).
- **Produced:** 2026-11-05, AMD Ryzen 9 7950X3D (shared), 4 training threads, .NET SDK 10.0.403, OneModel 1.0.0. Wall time: tagging 8.5 min (15 epochs, best at step 1,698, dev mean 93.50), parser about 28 min in two chunks (23 epochs, best at step 2,249, dev LAS 82.90), NER about 3.4 h in five chunks (15 epochs, best at step 28,965, dev F1 89.23).
- **Weights (sha256):** `tagging.onenlp` 5e900419cc46dd7024a7a0fdc7b8dbf71d028ad8ee14d27ffd9cddc0056c8051 (38.3 MB); `parser.onenlp` 1cd7f7667605c163d57f323b0c946f17bdc96fcc7361c2e3f233c518277a2f04 (45.8 MB); `ner.onenlp` 5e3d87f37da0162c70501ce6f7eed2a2fe4280fb21553169dea491b53c1f5287 (35.7 MB).
- **Choices on the development set only:** three tagging encoders (4-character suffix with a cased table; the same at depth 3, width 320; the same with 96-wide embeddings) scored 93.31, 93.34 and 93.29 against 93.50 for the default, so the default stayed. The tokenizer data was drawn from the training and development files (`docs/segmentation.md`, `tools/fetch/nl/token_stats.py`). The test file was read for the default configuration and was not used to choose among the variants.
- **Separation check:** no Alpino test sentence occurs in the training text; 35 of the 8,358 WikiNER test sentences also occur in its training text (short fragments); the official splits are kept.

## Data

| Dataset | Release | Role | Licence | sha256 |
|---|---|---|---|---|
| UD Dutch-Alpino, `nl_alpino-ud-train.conllu` | r2.18 | train | CC BY-SA 4.0 | `d6612a025ae77a8f58494458b987be6f39fdcaa40c94b5e2a5522e50c4166cfb` |
| UD Dutch-Alpino, `nl_alpino-ud-dev.conllu` | r2.18 | dev (early stopping, settings) | CC BY-SA 4.0 | `0444fc2590d7f522b5f201473940a40f0f1790770fcb568450581f12bcbcffd9` |
| UD Dutch-Alpino, `nl_alpino-ud-test.conllu` | r2.18 | eval only | CC BY-SA 4.0 | `20d0f718cdc73b51dbb2744386c9d4ecb1a9df6a076efa81b4c008ace3801fc0` |
| WikiNER Dutch wp3, `wikiner-nl-wp3-train.iob2` (converted, 163,725 sentences) | figshare 5462500 | train | CC BY 4.0 (text: Wikipedia, CC BY-SA) | `d8b5aec809b4ceddae16ad9facb93ec9892756f8bdaa3079c3815964c6328a7d` |
| WikiNER Dutch wp3, `wikiner-nl-wp3-dev.iob2` (converted, 1,645 sentences) | same | dev | same | `4328b8bb4a9d5a7edfdc5c6d3ed4d5536520dac12e3a944d4f3002b63a5750fe` |
| WikiNER Dutch wp3, `wikiner-nl-wp3-test.iob2` (converted, 8,358 sentences) | same | eval only | same | `6c2bc736a5789664662d3dec98822dc06370a9cd3c4b5b0484c7b10ed83801a2` |

## Results (UD Dutch-Alpino r2.18 test and WikiNER Dutch wp3 test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold tokens and sentences (the treebank's own words and sentences are the input; paired with spaCy's predictions):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 92.86 [92.20, 93.52] | 94.42 [93.77, 95.04] | no |
| XPOS | 89.84 [89.09, 90.58] | 91.73 [90.96, 92.40] | no |
| UFeats | 91.53 [90.85, 92.20] | 93.13 [92.47, 93.72] | no |
| AllTags | 88.12 [87.35, 88.86] | 90.51 [89.74, 91.19] | no |
| Lemmas | 91.06 [90.48, 91.56] | 91.83 [91.21, 92.39] | yes |
| UAS | 85.18 [84.06, 86.32] | 84.09 [82.92, 85.33] | yes |
| LAS | 80.28 [79.12, 81.51] | 79.85 [78.58, 81.16] | yes |

Raw text (our tokenizer; the parser sets sentence starts):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.66 [99.47, 99.81] | 99.82 [99.70, 99.94] | yes |
| Sentences | 91.31 [88.74, 93.73] | 89.60 [87.04, 92.17] | yes |
| Words | 99.66 [99.47, 99.81] | 99.82 [99.70, 99.94] | yes |
| UPOS | 92.28 [91.60, 93.00] | 94.36 [93.67, 94.98] | no |
| XPOS | 89.27 [88.49, 90.01] | 91.63 [90.88, 92.33] | no |
| UFeats | 90.98 [90.27, 91.69] | 92.97 [92.29, 93.56] | no |
| AllTags | 87.47 [86.67, 88.24] | 90.39 [89.62, 91.08] | no |
| Lemmas | 90.44 [89.80, 90.99] | 91.65 [91.05, 92.24] | no |
| UAS | 84.44 [83.24, 85.65] | 83.70 [82.56, 84.95] | yes |
| LAS | 79.48 [78.28, 80.78] | 79.57 [78.26, 80.89] | yes |

Entities, WikiNER Dutch wp3 test, gold tokens (paired): micro F1 87.47 [86.91, 87.98] vs 86.21 [85.70, 86.78] (difference +1.26 [0.76, 1.72]); precision 87.33 vs 85.96; recall 87.60 vs 86.46. Per type: LOC 90.22 vs 89.09, MISC 79.83 vs 78.19, ORG 80.94 vs 78.28, PER 89.72 vs 89.21; per-type intervals overlap in every case. The published WikiNER numbers (Stanza 94.8, Trankit 95.0) are on another split and are information only (`docs/references.md`).

**Misses:** in both modes UPOS, XPOS, UFeats and AllTags are behind spaCy outside the intervals (gold: UPOS -1.56, XPOS -1.89, UFeats -1.60, AllTags -2.39; raw: UPOS -2.08, XPOS -2.36, UFeats -1.99, AllTags -2.92; raw lemmas -1.21). Heads, sentences, tokens and gold-token lemmas are within noise, and entities are ahead.
