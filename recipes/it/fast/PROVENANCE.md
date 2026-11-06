# Provenance: Italian fast tier (`recipes/it/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Italian/weights.manifest.props`.

- **Data:** the derived `it_gating` treebank for tags, morphology, lemmas, parser and sentence starts: UD Italian-ParlaMint r2.18 (CC BY-SA 4.0) train minus every eighth sentence, plus UD Italian-MarkIT r2.18 (CC BY 4.0) train, 897 sentences and 28,881 words; development is every eighth ParlaMint train sentence plus MarkIT dev, 381 sentences (`derive_italian` in `tools/fetch/fetch_lang.py`; hashes in `tools/fetch/it/MANIFEST.md`). WikiNER Italian wp3 (CC BY 4.0 labels, silver, over Wikipedia text; our split by article index) train and dev for entities, with articulated prepositions and verbs with enclitics cut into the tokenizer's pieces. The larger Italian treebanks (ISDT, VIT, ParTUT, PoSTWITA, KIParlaForest) are non-commercial and were not fetched. Test files are read only by `predict` and `ner-eval`.
- **Share-alike lineage:** yes (ParlaMint is CC BY-SA 4.0; WikiNER's text is Wikipedia). Label generator: none. Pretrained components: none.
- **Configuration:** hash embeddings as in `FastEncoder.English()` (64-wide, one hash function and 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape, mixed to 256).
  - Tagging (seed 1): one ReLU window layer and one bidirectional LSTM layer of 64 units per direction over each document, no layer normalization after the mix (`--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64`), AdamW 3e-3, 200 warm-up steps, a half-cosine fall over a fixed 60 epochs (`--lr 0.003 --cosine-epochs 60 --patience 100`), dropout 0.5, 1,000-word batches.
  - Parser (seed 7): biaffine parser, one bidirectional LSTM layer of 128 units per direction, dropout 0.4, AdamW 2e-3, at most 80 epochs with patience 20, documents of 10 sentences (`--lstm 128 --lstm-layers 1 --dropout 0.4 --epochs 80 --patience 20`).
  - Entities (seed 7): BILUO tagging with constrained decoding, the default encoder, dropout 0.4, at most 8 epochs with patience 3 (`--epochs 8 --patience 3`).
- **Produced:** 2026-11-06, AMD Ryzen 9 7950X3D (shared), 2 to 4 training threads per part, in chunks of at most 25 minutes that resume from checkpoints; .NET SDK 10.0.403, OneModel 1.0.0. Tagging: 60 epochs, 1,800 steps, about 8.5 minutes (best on dev at step 1,290, dev mean of UPOS, XPOS, UFeats and lemmas 93.27). Parser: 80 epochs, about 10 minutes (best at step 1,722, dev LAS 74.81). Entities: 8 epochs, 27,405 steps, about 1.5 hours over four chunks (best at step 20,553, dev F1 87.31).
- **Weights (sha256):** `tagging.onenlp` efa8565220bb46c7232fe85287df3a3abbbaaf0663b2c82c61d370866bc5a00c (36.0 MB); `parser.onenlp` 443df6be06ab59bebcdbaf507878e7109ff3278d389b5afd52783b0fe0853e7d (41.0 MB); `ner.onenlp` 2f74f2e7dc93f3029cbee8889a0ced83281ef26d4eb65852d63b3c76d8b7b40e (35.7 MB).
- **Choices on the development set only:** tagging was tried with 64 and 128 LSTM units, 20 and 60 epochs and dropout 0.1, 0.3 and 0.5; the dev means were 92.91 (20 epochs), 93.12 (60 epochs), 93.17 (128 units), 93.21 (dropout 0.3) and 93.27 (dropout 0.5), all within noise of each other, and the smallest configuration with the highest score was kept. Parser dev LAS: 74.20 (two layers of 192 units, dropout 0.25), 74.78 (the same, dropout 0.4, 80 epochs), 74.81 (one layer of 128, dropout 0.4, 80 epochs), 75.02 (two layers of 192, dropout 0.5, learning rate 3e-3, 101 epochs); the one-layer 128-unit model was kept because it costs less per token for a difference inside the noise. Tokenization rules were chosen on the training and development files (`docs/segmentation.md`).
- **Test reads:** one read of the ParlaMint test file for the final tagging and parser models in each mode, and one of the WikiNER test file for the final entity model. Nothing else was scored on test.
- **Cost** (indicative, one thread on the shared worker at load average about 13, raw test text, `speed`): the whole pipeline (tokenizer, tagging, parser, entities) 7.2k tokens/s, about 4.7 KB managed allocation per token.
- **Data limits:** 897 training sentences (28,881 words) are a small fraction of what the other fast-tier languages train on. The development set mixes ParlaMint transcripts with MarkIT grammar examples, so dev scores are optimistic about unseen transcripts. 13 of the 375 ParlaMint test sentences and 87 of the 6,234 WikiNER test sentences also occur as exact text in the training files (short fragments); the official splits are kept.

## Results (UD Italian-ParlaMint r2.18 test and WikiNER Italian wp3 test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold words and sentences (the treebank's, as the reference is run; paired with spaCy's predictions):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 94.44 [93.92, 94.96] | 94.10 [93.58, 94.60] | yes |
| XPOS | 93.74 [93.16, 94.27] | 93.59 [93.04, 94.13] | yes |
| UFeats | 89.52 [88.85, 90.16] | 87.42 [86.67, 88.14] | no (ahead) |
| AllTags | 86.31 [85.55, 87.06] | 84.08 [83.30, 84.89] | no (ahead) |
| Lemmas | 95.18 [94.73, 95.63] | 93.13 [92.58, 93.64] | no (ahead) |
| UAS | 78.52 [77.27, 79.92] | 77.08 [76.00, 78.21] | yes |
| LAS | 72.57 [71.24, 74.06] | 71.39 [70.26, 72.58] | yes |

Raw text (spaCy's tokenizer keeps `del` and `farlo` whole, so its Words and tagging rows lose on multiword tokens):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.86 [99.76, 99.96] | 99.75 [99.62, 99.87] | yes |
| Sentences | 96.00 [93.87, 97.86] | 87.55 [83.59, 91.22] | no (ahead) |
| Words | 99.67 [99.52, 99.80] | 89.73 [88.86, 90.58] | no (ahead) |
| UPOS | 94.01 [93.45, 94.51] | 83.53 [82.50, 84.48] | no (ahead) |
| XPOS | 93.32 [92.71, 93.88] | 83.20 [82.15, 84.24] | no (ahead) |
| UFeats | 89.03 [88.36, 89.72] | 76.92 [75.88, 77.94] | no (ahead) |
| AllTags | 85.83 [85.05, 86.61] | 73.36 [72.21, 74.42] | no (ahead) |
| Lemmas | 94.78 [94.29, 95.27] | 83.03 [82.06, 83.97] | no (ahead) |
| UAS | 78.29 [77.10, 79.62] | 64.08 [62.65, 65.57] | no (ahead) |
| LAS | 72.77 [71.46, 74.16] | 58.38 [56.92, 59.86] | no (ahead) |

Entities, WikiNER Italian wp3 test, gold tokens (our model reads the tokenizer's pieces and each token takes the tag of its first piece): micro F1 86.35 [85.73, 87.01] vs 83.54 [82.85, 84.26] (difference +2.80 [2.18, 3.42], ahead); precision 86.60 vs 83.89; recall 86.10 vs 83.20. Per type: LOC 87.89 vs 86.77 (+1.12 [0.39, 1.86], within noise); MISC 77.64 vs 72.06 (+5.57), ORG 82.84 vs 74.79 (+8.06) and PER 89.97 vs 87.34 (+2.64) ahead.

Tokenizer alone on the development split, raw text: tokens 99.75 [99.43, 99.93], words 99.61 [99.23, 99.79] (rules alone; the parser sets sentences in the pipeline).

**Misses:** none. Every row is within noise of spaCy or ahead. The parser rows (UAS 78.5, LAS 72.6) are low in absolute terms because 897 sentences are little data; the reference is trained on the same sentences, so the comparison is fair, but the model is not comparable with systems trained on ISDT (published Stanza, UDPipe 2 and Trankit rows in `docs/references.md` use it).
