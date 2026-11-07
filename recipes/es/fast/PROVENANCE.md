# Provenance: Spanish fast tier (`recipes/es/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Spanish/weights.manifest.props`.

- **Data:** UD Spanish-AnCora r2.18 (CC BY 4.0; train and dev) for tags, morphology, lemmas, parser and sentence starts; WikiNER Spanish wp3 (CC BY 4.0 labels, silver, over Wikipedia text; our split by article index, `tools/fetch/es/MANIFEST.md`; train and dev) for entities, with `del`, `al` and verbs with enclitics cut into the tokenizer's pieces. Hashes: `tools/fetch/es/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`.
- **Share-alike lineage:** yes for the entity recognizer (Wikipedia text); the tagging and parser parts use CC BY data only. Label generator: none. Pretrained components: none.
- **Configuration:** hash embeddings as in `FastEncoder.English()` (64-wide, one hash function and 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape, mixed to 256). Tagging: one ReLU window layer and one bidirectional LSTM layer of 64 units per direction over each document, no layer normalization after the mix (`--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64`), AdamW 3e-3, 200 warm-up steps, a half-cosine fall over a fixed 20 epochs (`--lr 0.003 --cosine-epochs 20 --patience 100`), dropout 0.1, 1,000-word batches, seed 1. Parser and NER keep the `FastEncoder.English()` encoder (two window layers). Parser: arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document, at most 40 epochs. NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7. Patience 6 on the development score everywhere (tagging: mean of UPOS, XPOS, UFeats, Lemmas; parser: LAS; NER: entity F1).
- **Produced:** 2026-11-05, AMD Ryzen 9 7950X3D (shared), 4 training threads per part, in chunks of at most 28 minutes that resume from checkpoints; .NET SDK 10.0.403, OneModel 1.0.0. Tagging: 20 epochs, about 19 minutes of training (best at step 6,017, dev mean 95.37; the first tagging model, with the `FastEncoder.English()` encoder and a constant rate, scored 95.05 on dev and missed spaCy on UPOS and UFeats in gold mode, so it was replaced). Parser: 40 epochs, still improving slowly at the cap (best at step 11,983, dev LAS 87.25). NER: 13 epochs (best at step 22,877, dev F1 85.59); an earlier NER run on whole tokens (`del` unsplit) was discarded for raw-text use, see the README.
- **Weights (sha256):** `tagging.onenlp` 99c3343df18f156d661bf3632fc160de6ddf0b0000d00952858a3353db345e60 (37.1 MB); `parser.onenlp` 0e4a5757c048902f7bc150e85901dfd5c7e210b3e153641778256730945a6073 (46.0 MB); `ner.onenlp` 0e8eda9a203296556c88b135a10800ae41ee248eea25984a6b0c1c4a00becd7e (35.7 MB; the entity recognizer with a bidirectional LSTM layer, see below; the earlier window-only one was 6997910ba28da1ad85d87c16003d832db02f9838d182bfd8d3e2e0022aad03cc).
- **Choices on the development set only:** the default encoder was kept for the parser and entity parts (no variants were tried). For tagging, 64 and 128 LSTM units per direction scored 95.37 and 95.47 on dev against 95.05 for the old encoder; 64 was kept because it costs about half as much per token for a gain inside the noise. Tokenization rules were chosen on the training and development files (`docs/segmentation.md`). The whole-token NER run was scored on the test file once, to measure what the pieces cost, before the pieces run was trained; the pieces run was selected on dev.
- **Test reads:** the tagging and parser rows of the first tagging model were read once, which showed the UPOS and UFeats gap; the replacement tagging encoder was chosen on dev (three candidates) and read once on test; the whole-token NER run was read once (see above); the final NER model once.
- **Separation check:** 5 of the 1,721 AnCora test sentences, and 69 of the 6,468 WikiNER test sentences, also occur as exact text in the training file (short fragments); the official splits are kept.
- **Cost** (indicative, one thread on the shared worker at load average about 25, raw development text, `speed`): tagging heads alone 22.3-23.0k tokens/s (about 44 us/token, 231 managed bytes/token); the whole pipeline (tokenizer, tagging, parser, entities) 5.2-5.4k tokens/s (about 190 us/token).
- **Training time against PyTorch:** the architectures are the English ones (same heads and encoder), whose C#-to-PyTorch CPU ratios were 0.5 to 1.7; the ratio was not re-timed.

## Results (UD Spanish-AnCora r2.18 test and WikiNER Spanish wp3 test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold words and sentences (the treebank's, as the reference is run; paired with spaCy's predictions):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| UPOS | 98.11 [97.98, 98.25] | 98.18 [98.04, 98.32] | yes |
| XPOS | 86.73 [86.32, 87.15] | 84.97 [84.51, 85.44] | no (ahead) |
| UFeats | 97.71 [97.56, 97.86] | 97.74 [97.58, 97.89] | yes |
| AllTags | 86.09 [85.68, 86.52] | 84.47 [84.01, 84.93] | no (ahead) |
| Lemmas | 98.84 [98.73, 98.95] | 98.72 [98.59, 98.83] | yes |
| UAS | 90.05 [89.57, 90.49] | 89.04 [88.59, 89.52] | no (ahead) |
| LAS | 87.00 [86.50, 87.51] | 85.95 [85.44, 86.44] | no (ahead) |

Raw text (spaCy's tokenizer keeps `del` and `verlo` whole, so its Words and tagging rows lose on multiword tokens):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.84 [99.76, 99.90] | 99.90 [99.85, 99.94] | yes |
| Sentences | 98.14 [97.39, 98.87] | 97.70 [96.89, 98.49] | yes |
| Words | 99.83 [99.75, 99.89] | 96.60 [96.40, 96.80] | no (ahead) |
| UPOS | 97.86 [97.71, 98.01] | 94.81 [94.56, 95.02] | no (ahead) |
| XPOS | 86.38 [85.96, 86.81] | 85.55 [85.12, 86.01] | yes |
| UFeats | 97.47 [97.31, 97.64] | 94.57 [94.32, 94.81] | no (ahead) |
| AllTags | 85.66 [85.22, 86.10] | 85.05 [84.62, 85.52] | yes |
| Lemmas | 98.65 [98.51, 98.76] | 95.37 [95.15, 95.59] | no (ahead) |
| UAS | 89.72 [89.22, 90.19] | 84.87 [84.34, 85.40] | no (ahead) |
| LAS | 86.70 [86.15, 87.23] | 81.82 [81.25, 82.37] | no (ahead) |

Entities, WikiNER Spanish wp3 test, gold tokens (paired; our model reads the tokenizer's pieces and each token takes the tag of its first piece): micro F1 82.88 [82.12, 83.64] vs 83.35 [82.63, 84.08] (difference -0.46 [-1.14, 0.20], within noise); precision 82.86 vs 83.98 (-1.12 [-1.81, -0.46], inside the noise rule); recall 82.91 vs 82.72. Per type: LOC 84.38 vs 84.95 and MISC 69.01 vs 68.53 within noise; ORG 76.75 vs 71.79 (+4.96 [2.89, 7.24], ahead); PER 88.49 vs 90.45 (-1.96 [-2.83, -1.13], behind).

**Misses:** PER entities (-1.96, outside the interval) only. Gold-word UPOS (98.11) and UFeats (97.71) are within noise since the tagging model got a context encoder; the first tagging model was 0.38 and 0.35 behind. Everything else is within noise or ahead.

## Entity recognizer with sentence context

The first entity recognizer used the default encoder (two window layers), so a token saw about four neighbours. It missed spaCy on PER (-1.96). The shipped `ner.onenlp` adds one bidirectional LSTM layer of 64 units per direction after one ReLU window layer, the same encoder shape as the tagging model:

```
$R ner-train ner-train.jsonl ner-dev.jsonl ner-dir --depth 1 --plain 1 --relu 1 --recurrent 1 --recurrent-size 64 --threads 4 --checkpoint-every 100
```

Seed 7, AdamW 3e-3, dropout 0.4, patience 6; 13 epochs, best at step 22,877 (dev F1 86.87 against 85.59 for the window-only model, scored the same way on the same dev file); about 63 minutes of training on 4 threads of the shared worker in three chunks of at most 28 minutes. A 128-unit variant was at 85.2 after three epochs, no better than 64, and was stopped. Same size on disk (35.7 MB). SA lineage as before.

WikiNER Spanish wp3 test, gold tokens (paired against spaCy 3.8.18, 1,000 resamples, seed 1; the test file was read once for this model): micro F1 84.81 [84.08, 85.54] vs 83.35 (difference +1.46 [0.84, 2.14]); precision 85.04 vs 83.98; recall 84.58 vs 82.72. Per type: LOC 86.23 vs 84.95, MISC 70.63 vs 68.53, ORG 75.67 vs 71.79, PER 91.57 vs 90.45 (+1.12 [0.33, 1.95]). Every row is within noise or ahead; PER no longer misses. Before: micro 82.88, LOC 84.38, MISC 69.01, ORG 76.75, PER 88.49. ORG is 1.08 lower than before, inside both intervals.

Cost (indicative, NER part alone on development text, one core of a shared machine at load average about 11): 124-139k tokens/s before, 65-69k after, with 298 and 1,832 managed bytes per token. The whole Spanish pipeline is bounded by the parser (about 5k tokens/s), so it changes little.

### The same change on the other languages (development set only)

Entity F1 on each language's own development file, window-only model (shipped) against the same recipe with `--depth 1 --plain 1 --relu 1 --recurrent 1 --recurrent-size 64`. The lever is adopted where it gains about a point or more on development (the development files are small, 95% intervals about +-1.3 to +-3); the others stay as they are.

| Language (data) | Window-only | With LSTM | Decision |
|---|---|---|---|
| Spanish (WikiNER) | 85.59 | 86.87 | adopted |
| Portuguese (UNER Bosque) | 70.68 | 72.58 | adopted |
| Chinese (UNER GSDSIMP, gold words) | 63.81 | 64.46 | kept (+0.65, inside noise) |
| Italian (WikiNER), stopped at epoch 8 | 87.31 | 87.79 | kept (+0.5) |
| Dutch (WikiNER), stopped at epoch 11 | 89.23 | 89.45 | kept (+0.2) |
| French (WikiNER), stopped at epoch 7 | 83.49 | 83.17 | kept (-0.3) |
| German (GermEval) | 66.89 | 66.36 | kept (-0.5) |

Turkish was not tried (its package is being retrained for another reason). English needs no change: the shipped English recognizer already scores LOC 71.56 against spaCy 71.43 (paired +0.13 [-3.83, 3.97]).
