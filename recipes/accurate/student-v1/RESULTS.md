# student-v1: frozen and fine-tuned results

The distilled encoder (`PROVENANCE.md`) with the same heads and data as the teacher rows. Everything here ran through the public `Train` API (`recipes/accurate/probe`, `tools/distill/finetune.sh`, `ft-round.sh`), on CPU. Selection used the development splits only; each test split was read once per reported configuration.

Data: UD English-EWT r2.18 (train, dev, test), UD French-GSD r2.18 (train, dev), UNER English-EWT commit `8ed072de` (train, dev, test). All CC BY-SA 4.0: the models have share-alike lineage.

## Development rows

| Encoder, training | EWT UPOS | French-GSD UPOS | UNER EWT NER F1 |
|---|---:|---:|---:|
| mmBERT-base, frozen, head learning rate 0.003 | 96.39 | 95.01 | 79.15 |
| mmBERT-base, fine-tuned (5e-5, 800 steps) | 96.894 | | |
| student, frozen, head 0.003 | 93.01 | 93.06 | 72.18 |
| student, frozen, head 0.001 | 92.81 | 92.89 | **72.47** |
| student, frozen, head 0.01 | 92.87 | 92.90 | 69.26 |
| student, fine-tuned, 7e-5 | 95.37 | | 72.23 |
| student, fine-tuned, 1.5e-4 | **95.50** | | 70.08 |
| student, fine-tuned, 3e-4 | 95.29 | 94.93 | 67.51 |
| student, fine-tuned, 6e-4 (stopped at step 600 to 700 of 1000) | 94.86 | 94.45 | 52.51 |

Fine-tuning schedule: AdamW, one learning rate for the encoder and the head (`Train` has no parameter groups yet), 50 warm-up steps, cosine decay to 10% over 1,000 steps, batches of about 1,000 words (about 4 epochs of EWT, 2 of French-GSD), dropout 0.1 (NER 0.2), clip 1.0, token table frozen, mean pooling, seed 1; the best development checkpoint is kept (EWT: step 900 of 1,000 at 1.5e-4). The frozen heads train for up to 20 epochs with patience 4. The French run used 3e-4 and 6e-4 only.

## Test rows (gold tokens), paired against the English fast tier

The fast tier is `fast-tagging-v2` and `ner-tagging-english` (sha256 `86009eed…` and `fe3e0974…`). Paired bootstrap, 1,000 resamples over sentences, seed 1 (`OneNlp.Eval`).

| Row | Student | Fast tier | Difference |
|---|---:|---:|---:|
| EWT r2.18 test UPOS, student fine-tuned at 1.5e-4 (chosen on dev) | 95.88 [95.55, 96.19] | 93.89 [93.50, 94.29] | +1.99 [+1.63, +2.36] |
| UNER EWT test micro F1, student frozen with head 0.001 (chosen on dev) | 75.48 [72.66, 78.03] | 61.52 [58.46, 64.54] | +13.95 [+10.91, +17.03] |
| UNER EWT test micro F1, student fine-tuned at 7e-5 (second read, information) | 73.51 [70.85, 76.19] | 61.52 | +11.99 [+9.08, +14.84] |

spaCy 3.8.18 on the same data: UPOS 94.29, NER 62.32. For NER the frozen head is the development-best configuration (72.47 against 72.23 for fine-tuning, inside noise); fine-tuning the whole encoder on the small NER training set does not beat a frozen encoder with a tuned head.

## Cost

- Fine-tuning step: 1,000-word batches, 2 to 4 threads, shared host at load 12 to 17: about 2.7 to 4.3 s per step. Peak resident memory 5.6 GB (4 threads, a 30-step run with one development pass).
- Chunks (each at most 30 minutes, resumable): 3 for the 3e-4 round, 2 for the 6e-4 round, 4 for 1.5e-4 and 3 for 7e-5 (two tasks at a time share one job), plus the frozen head sweep.
- Models: `r3-upos-en` 43.6 MB (sha256 `1ca90e4f…`), `fz-lr0.001-ner-en` 2.0 MB (`ba82601f…`), `r4-ner-en` 45.5 MB (`5653cf0a…`), French `r1-upos-fr` 43.6 MB (`fbfb23f7…`). The encoder weights are in the fine-tuned pipelines; the 142 MB ONNX and the tokenizer are referenced by name.

## Speed (indicative)

`recipes/accurate/probe speed`: tokens per second of the trained components over the gold tokens of the EWT test split (25,094 tokens), batches of 32 documents, median of 2 repetitions after a warm-up, on a shared host (load 12 to 17), AVX-512. Not a gated row.

| Pipeline | Components | 1 thread | 8 threads |
|---|---|---:|---:|
| English fast tier (`fast-tagging-v2`) | tagger, morphologizer, lemmatizer | 27,300 | 46,100 |
| student, frozen encoder (graph runtime) | tagger | 2,190 | 10,050 |
| student, fine-tuned (trainable modules) | tagger | 714 | 1,368 |
| mmBERT-base, frozen (graph runtime) | tagger | 225 | 732 |

The fine-tuned student runs through the training modules, which are about three times slower than the graph runtime for the same weights; writing fine-tuned weights back to an ONNX file would give the graph-runtime row.
