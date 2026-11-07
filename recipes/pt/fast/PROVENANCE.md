# Provenance: Portuguese fast tier (`recipes/pt/fast`)

Format: `docs/provenance.md`. The shipped weights are listed in `src/OneNlp.Models.Portuguese/weights.manifest.props`.

- **Data:** UD Portuguese-Bosque r2.18 (CC BY-SA 4.0; train and dev) for morphology, lemmas, parser and sentence starts; Universal NER Portuguese-Bosque (commit f2c2ad67; CC BY-SA 4.0; train and dev; PER, ORG, LOC) for entities. Hashes: `tools/fetch/pt/MANIFEST.md`. Test files are read only by `predict` and `ner-eval`.
- **Share-alike lineage:** yes. Label generator: none. Pretrained components: none.
- **Configuration (all defaults of the recipe):** `FastEncoder.English()` configuration (64-wide hash embeddings, one hash function and 32,768 rows for each of lower-cased form, 3-character prefix, 3-character suffix and shape, mixed to 256, two window layers). Morphologizer and lemmatizer share one encoder: AdamW 2e-3, 200 warm-up steps, dropout 0.1, 1,000-word batches, seed 1; there is no tagger because Bosque has no XPOS. Parser: arc 256, label 128, two-layer BiLSTM 192, dropout 0.25, 1,500-word batches, seed 7, ten sentences per document. NER: BILUO tagging, hidden 256, dropout 0.4, lr 3e-3, seed 7. Patience 6 on the development score everywhere (tagging: mean of UPOS, UFeats, Lemmas; parser: LAS; NER: entity F1).
- **Training files:** `prepare` rewrites the treebank into the tokens the tokenizer makes: `à` (one character, two words) is one word with the columns of its first word; the verb and the pronoun of `trata-se` are two words, `trata` and `-se`; contractions (`do` = `de` + `o`) stay multiword tokens that the reader cuts into two pieces. `ner-convert` carries the Universal NER tags (which are on the treebank's words) onto the same tokens. Development files are rewritten the same way; test files are never rewritten for scoring except in memory, to give the pipeline its tokens (`predict --mode gold`).
- **Produced:** 2026-11-05, AMD Ryzen 9 7950X3D (shared), 4 training threads, .NET SDK 10.0.403, OneModel 1.0.0. Wall time: morphologizer and lemmatizer 4.5 min (11 epochs, best at step 873, dev mean 95.14), parser 25 min in two chunks (21 epochs, best at step 1,888, dev LAS 82.34), NER 5.5 min (11 epochs, best at step 874, dev F1 70.68).
- **Weights (sha256):** `tagging.onenlp` a1c625a70b86bb487abfbde88b97b76012bb2c3d724a40568f0f0bbd5a8a5407 (36.7 MB); `parser.onenlp` 277a4526586eb4233f552373923f2bccadb3495afc70956924ba8ca4a08566d4 (46.0 MB); `ner.onenlp` 3d81efdbb1017159ba8076153188d33bb0c0953da46c2a93510a41f752a96ef1 (35.7 MB; with a bidirectional LSTM layer, see below; the earlier window-only one was 79829139344a306f8683542708916e20b74e7d122c8031af2ec990229848af9f).
- **Choices on the development set only** (the test file was read once for each final configuration): four tagging encoders (4-character suffix with a cased table 94.69; width 320, depth 3, 96-wide embeddings 94.96; two hash functions with larger tables 94.83) against 95.07 for the default; four NER variants (cased table and 4-character suffix 70.77; dropout 0.5 70.87; one neighbour on each side 71.43) against 70.99 for the default, within the seed spread of about 1.5 points, so the default stayed. Adding UD Portuguese-GSD r2.18 (CC BY-SA 4.0, 9,616 sentences) to the training text lowered the Bosque development mean to about 90 after seven epochs (the two treebanks annotate features and lemmas differently), so it is not used. Tokenization rules were chosen on the training and development files (`docs/segmentation.md`).
- **Separation check:** 1 of the 1,167 Bosque test sentences also occurs as exact text in the training file (2 folded); the official split is kept.

## Results (UD Portuguese-Bosque r2.18 test and Universal NER Portuguese-Bosque test, bootstrap 95% over sentences, 1,000 resamples, seed 1; reference: spaCy 3.8.18 default configuration trained on the same data, `tools/baselines/spacy/lang/PROVENANCE.md`)

Gold tokens and sentences (the treebank's tokens, with `à` and the pronouns after a verb's hyphen as the tokenizer makes them; paired with spaCy's predictions, which are on the treebank's words). Bosque has no XPOS, so XPOS and AllTags are not reported.

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Words | 99.97 [99.94, 99.99] | 100.00 | no |
| UPOS | 95.89 [95.62, 96.14] | 95.92 [95.66, 96.22] | yes |
| UFeats | 94.51 [94.17, 94.80] | 94.11 [93.82, 94.42] | yes |
| Lemmas | 97.30 [97.10, 97.51] | 96.90 [96.70, 97.11] | yes |
| UAS | 86.93 [86.32, 87.53] | 86.05 [85.45, 86.64] | yes |
| LAS | 82.74 [82.07, 83.35] | 81.96 [81.30, 82.61] | yes |

Raw text (spaCy's tokenizer keeps contractions whole, so its Words and tagging rows lose on them):

| Metric | Ours | spaCy | Within noise |
|---|---|---|---|
| Tokens | 99.86 [99.79, 99.92] | 99.87 [99.80, 99.93] | yes |
| Sentences | 92.72 [91.10, 94.29] | 84.48 [82.19, 86.74] | no (ahead) |
| Words | 99.74 [99.65, 99.81] | 88.51 [88.02, 88.99] | no (ahead) |
| UPOS | 95.35 [95.05, 95.64] | 84.15 [83.59, 84.73] | no (ahead) |
| UFeats | 93.96 [93.63, 94.31] | 82.16 [81.55, 82.73] | no (ahead) |
| Lemmas | 96.78 [96.56, 96.99] | 85.35 [84.83, 85.86] | no (ahead) |
| UAS | 86.28 [85.64, 86.89] | 70.23 [69.30, 71.07] | no (ahead) |
| LAS | 82.09 [81.38, 82.76] | 65.85 [64.88, 66.65] | no (ahead) |

Entities, Universal NER Portuguese-Bosque test, gold tokens (paired, scored on the treebank's words): micro F1 73.13 [70.58, 75.60] vs 75.27 [72.73, 77.60] (difference -2.14 [-4.26, 0.39], within noise); precision 72.68 vs 76.96 (difference -4.27 [-6.58, -1.59], outside); recall 73.58 vs 73.66. Per type: LOC 72.50 vs 74.43, ORG 67.34 vs 70.32, PER 79.05 vs 80.64; all within noise.

**Misses:** none. The tagging heads now sit on a context encoder (see below); UPOS, UFeats and Lemmas in gold mode are within noise, and every raw-text row is within noise or ahead. Words is 99.97 in gold mode (an enclitic's word is spelled from the training file's most frequent form, and a few hyphenated compounds such as `Poder-se-á` stay one token), 6 of 1,172 development sentences differ. Parser rows are unchanged.

## Tagging encoder with context (replaces the first tagging model)

The first tagging model (`FastEncoder.English()` configuration, dev UPOS 95.32, UFeats 93.29, AllTags 91.36, Lemmas 96.82) missed spaCy in the tagging rows; the errors were mostly decisions that need words far from the token (NOUN against PROPN, VERB against AUX, case and gender agreement). Its replacement adds one bidirectional LSTM layer (Hochreiter and Schmidhuber 1997; 64 units per direction, mapped back to the 256-wide window output and added to it) after one window layer, over each document, with ReLU everywhere and no layer normalization after the mix (`FastEncoder` settings `plain`, `relu`, `depth` 1, `recurrentLayers` 1, `recurrentSize` 64). Training uses AdamW at 3e-3 with 200 warm-up steps and a half-cosine fall over a fixed 20 epochs (Loshchilov and Hutter 2017) instead of a constant rate cut off by early stopping; the weights of the best development epoch are kept. The parser and entity models are unchanged.

```
train-tagging --train ... --dev ... --out DIR --plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --lr 0.003 --cosine-epochs 20 --patience 100 --threads 4
```

- **Data, seed:** UD Portuguese-Bosque r2.18 train and dev only (CC BY-SA 4.0, share-alike lineage), seed 1, as above; the test file was read once for this configuration, for the rows above.
- **Development scores, gold tokens** (before, then after): UPOS 95.32, UFeats 93.29, AllTags 91.36, Lemmas 96.82; UPOS 95.83, UFeats 94.29, AllTags 92.57, Lemmas 97.29. Mean over the tagging metrics 95.07, then 95.81.
- **Weights:** `tagging.onenlp` sha256 `d9bfe33eb4080660cff562c9b9289933a83e3c009fabbec7e1e57c6a980442f5`, 36,696,933 bytes, produced 2026-11-05 with 4 training threads, about 20 minutes. Fixed in `src/OneNlp.Models.Portuguese/weights.manifest.props`.
- **Screened on the development set before choosing:** a 2-layer LSTM of 128 per direction on the GELU encoder (dev mean slightly lower, at roughly twice the encoder cost); 1 layer of 16, 32 and 64 units; no window layer (cheaper, worse: the window layer matters); the cosine schedule against a constant rate; a rate of 3e-3 against 2e-3. In gold mode the recipe feeds the treebank tokens as the tokenizer makes them (`prepare`), which is the model's own input convention; the 0.03-point Words gap comes from 6 of 1,172 development sentences (all-capital contractions, `à`, and hyphenated compounds), not from tagging.
- **Cost** (indicative, one thread, `speed` command on development text, raw text through tokenizer, sentencizer and the tagging heads): 24.3k tokens/s (before: 22.2-24.5k). Encoder alone (1 thread, 5,632 tokens): one ReLU window layer 6.2 us/token; plus the 64-unit LSTM 11.9 us/token; 32 units 9.8; 128 units 18.7; no window layer with the 64-unit LSTM 6.1.

## Entity recognizer with sentence context

The shipped `ner.onenlp` adds one bidirectional LSTM layer of 64 units per direction after one ReLU window layer (`--depth 1 --plain 1 --relu 1 --recurrent 1 --recurrent-size 64`), so an entity decision sees the whole sentence. Seed 7, the other settings as above; 11 epochs, best at step 874, development F1 72.58 against 70.68 (the window-only model, scored the same way); about 7 minutes of training on 4 threads. The Spanish recipe (`recipes/es/fast/PROVENANCE.md`) has the comparison across languages.

UNER Portuguese-Bosque test, gold tokens, paired against spaCy 3.8.18 (1,000 resamples, seed 1; read once for this model): micro F1 73.48 [70.95, 75.86] vs 75.27 (difference -1.79 [-3.95, 0.29]); precision 75.46 vs 76.96 (-1.50 [-3.76, 0.83]); recall 71.60 vs 73.66 (-2.06 [-4.45, 0.27]); LOC 71.82 vs 74.43; ORG 68.60 vs 70.32; PER 79.32 vs 80.64. All within noise. The window-only model scored micro 73.13, precision 72.68 (-4.27, outside the interval), recall 73.58, LOC 72.50, ORG 67.34, PER 79.05, so precision now sits inside the noise rule and LOC is the one row lower than before (-0.7, inside both intervals). SA lineage as before.

## Parser, faster configuration

The parser part was retrained with one LSTM layer and product label features (`docs/parser.md`, "Speed configuration"): flags `--lstm 128 --lstm-layers 1 --arc 128 --label 64 --label-bilinear 0 --label-product 1`; everything else as above (seed 7, dropout 0.25, 1,500-word batches, ten sentences per document, patience 6 on dev LAS). Data: UD Portuguese-Bosque r2.18 train and dev (as rewritten by `prepare`) (CC BY-SA 4.0 or CC BY 4.0 as listed above; share-alike lineage unchanged). Best dev LAS 81.510 at step 1,132; about 8 minutes on 4 threads of the shared worker; trainer weights hash `6d41c695695a766b9147cb40db0e09d73b180f352e8cb32e69bee7a976d07292`. File `parser.onenlp`: 37,481,759 bytes, sha256 `2a3a0277c1e4ac99b31361434a80aa2c78540684195aa411d33c2e502aac1b5e`. Test rows, speed and the earlier parser's rows: `docs/results.md`, "Parser speed". The earlier parser is reproduced by the same command without the label and LSTM flags above (defaults of the recipe).
