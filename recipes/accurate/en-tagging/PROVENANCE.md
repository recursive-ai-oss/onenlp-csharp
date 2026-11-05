# Provenance: accurate/en-tagging (frozen mmBERT-base, UPOS)

- **Model:** a `Tagger` (UPOS) on a frozen `TransformerEncoder` over the ONNX export of `jhu-clsp/mmBERT-base`, mean subword pooling. A proof of the accurate tier's seam; not a shipped model.
- **Recipe:** `recipes/accurate/en-tagging/` (`Program.cs` holds the constants).
- **Seed(s):** training and data order 1; head weights drawn from the training seed; dropout stream derived from it. The dev set is the official dev file.
- **Produced:** 2026-11-04; AMD Ryzen 9 7950X3D (16 cores, 32 threads; AVX2, AVX-512, FMA), 8 training threads on a shared host (load average 12 to 25 during the run); one chunk, 426 s wall (first epoch with encoding about 227 s, then about 20 s per epoch), 11 epochs, early stop, best at step 1452; .NET SDK 10.0.403 (runtime 10.0.14), RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0.
- **Weights:** `model.onenlp`, 53,337 bytes, sha256 `58b18d28ff4cb88aea624ea090ced293eee0f55e0db8210f04a0e59502bff5d7` (training-state hash printed by `Train`: `2ab25817ff80810f167bafcce2d2f07c6991205da25a35d2080a1d8c13bcf45d`). Kept out of git.
- **SA lineage:** yes; Universal Dependencies English-EWT (CC BY-SA 4.0), <https://github.com/UniversalDependencies/UD_English-EWT>. Credit as in the other English recipes.
- **Licence of the weights:** MIT OR Apache-2.0 for the head; the encoder is the MIT-licensed mmBERT-base (see below).

## Training data

| Dataset | Release | Role | File | sha256 |
|---|---|---|---|---|
| UD English-EWT | r2.18 | train | `en_ewt-ud-train.conllu` | `d68e06122a702464c613076523d56740f047e5bbe89dd90ec32737e04d952143` |
| UD English-EWT | r2.18 | dev | `en_ewt-ud-dev.conllu` | `39239e0a60db3ae68f4b7036189f11b6692741d10ff8240dd91f74f2760d90f8` |

Licence: "licensed under a Creative Commons Attribution-ShareAlike 4.0 International License" (the treebank's own licence file).

## Label generator

None: labels are the treebank's gold UPOS.

## Pretrained components

| Component | Source | Revision | Licence | ONNX sha256 |
|---|---|---|---|---|
| mmBERT-base encoder (frozen) | `jhu-clsp/mmBERT-base` | `c5955035435e2bf121cde7f3c8863ef52ff35d82` | MIT (model card) | `6a9f485066d95de7eec7fb29c0e3839a9c4ddbefd6f87d46ece33b2cb52a550b` |
| its tokenizer | same repository, `tokenizer.json` | same | MIT | `197d4cc5406ee12cc50c8b5511f2393cc32d9db321545979ce041c1199178356` |

Exported with torch 2.15.0 (CPU), transformers 5.22.1, opset 17 (`docs/accurate-encoder.md`).

## Evaluation data

| Dataset | Release | Role | File | sha256 |
|---|---|---|---|---|
| UD English-EWT | r2.18 | eval | `en_ewt-ud-test.conllu` | `fa024f43dc5da3c5ac02563bc9bd0e974f46cbb1560823976a8f342a37dc494a` |

The test file was not used for early stopping or for any choice: the pooling (mean over first piece) was chosen on the development set (mean 96.39, first piece 95.62). The first-piece model was also scored on test, as information (95.39 [95.04, 95.71]).

## Results

UD English-EWT r2.18 test, gold tokens and sentences, UPOS; 95% CI by bootstrap over sentences (1,000 resamples, seed 1), `tools/OneNlp.Eval ud`; references are system runs on the same data and mode.

| System | UPOS test [95% CI] | Paired difference to ours |
|---|---|---|
| **mmBERT-base frozen, mean pooling, linear head** | **96.23 [95.93, 96.51]** | |
| fast tier `en-fast-tagging` v1 | 93.72 [93.29, 94.10] | ours +2.51 [2.16, 2.86] |
| spaCy 3.8.18 default config, same data | 94.29 [93.89, 94.66] | ours +1.94 [1.60, 2.30] |

Development UPOS: mmBERT mean 96.39 [96.09, 96.64]; mmBERT first piece 95.62 (best epoch); xlm-roberta-base first piece 94.68 (20 epochs, still improving when the epoch limit ended it).

Training time in C#: about 20 s per epoch with the encoder cached; the cache fill (encoding 14.5k sentences, 12.5k train and 2k dev) is most of the 227 s first epoch. Inference throughput of the encoder alone: not measured as a gated row.
