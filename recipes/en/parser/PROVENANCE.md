# Provenance: en-parser (English dependency parser, fast encoder)

- **Model:** `en-parser`, 0.1; the `parser` head (biaffine arcs and labels, 2-layer bidirectional LSTM of 192 per direction, sentence-start classifier) over its own `fast` encoder (width 96, depth 4); English, fast tier.
- **Recipe:** `recipes/en/parser`; command line in `README.md` with `--context 0 --lstm 192 --lstm-layers 2 --dropout 0.25 --epochs 40` (the defaults of the recipe); no config file.
- **Seed(s):** training seed 7 (shuffle, dropout, head weights); encoder seed 1; no train/dev split (the official dev file is the dev set).
- **Produced:** 2026-11-04; Linux x64, AMD Ryzen 9 7950X3D (32 threads, shared with other jobs), 4 training threads; wall time 0:30 (two chunks of 25 and 5.5 minutes, resumed from `last.ckpt`); .NET 10.0.14, OneModel 1.0.0, onenlp-csharp branch `wip/u15`.
- **Weights:** `model.onenlp`, 22,108,789 bytes, sha256 `612101ebeb4c62b6239a94c11727a2536537bfdb60245f337075eae1e9ddfccc`; trainer weights hash of the best checkpoint (step 2,914, dev LAS 79.656) `0195a244ef854d024c1cf88e82880246b75a33a6c8a635031a0a4996687d7f7b`. Kept on the build worker, not in git.
- **SA lineage:** yes; Universal Dependencies English-EWT (CC BY-SA 4.0).
- **Licence of the weights:** MIT OR Apache-2.0 for our weights, with the credit below; the training data is CC BY-SA 4.0 (owner ruling of 2026-10-30 allows it, with credit).

## Training data

| Dataset | Release | Role | Files | Licence | URL | sha256 |
|---|---|---|---|---|---|---|
| UD English-EWT | r2.18 | train | `en_ewt-ud-train.conllu` | CC BY-SA 4.0 ("The annotations and database rights of the Universal Dependencies English Web Treebank are licensed under a Creative Commons Attribution-ShareAlike 4.0 International License.") | https://raw.githubusercontent.com/UniversalDependencies/UD_English-EWT/r2.18/en_ewt-ud-train.conllu | `d68e06122a702464c613076523d56740f047e5bbe89dd90ec32737e04d952143` |
| UD English-EWT | r2.18 | dev | `en_ewt-ud-dev.conllu` | same | same path | `39239e0a60db3ae68f4b7036189f11b6692741d10ff8240dd91f74f2760d90f8` |

Credit: Universal Dependencies English Web Treebank (Silveira et al. 2014 and later releases); texts from the English Web Treebank (LDC2012T13 source texts, redistributed with the annotations under the treebank's licence).

## Label generator

None: labels are the treebank's gold annotations (HEAD, DEPREL and the sentence boundaries).

## Pretrained components

None; trained from scratch (no vectors).

## Evaluation data

| Dataset | Release | Role | Files | sha256 | Used for |
|---|---|---|---|---|---|
| UD English-EWT | r2.18 | eval | `en_ewt-ud-test.conllu` | `fa024f43dc5da3c5ac02563bc9bd0e974f46cbb1560823976a8f342a37dc494a` | UAS, LAS, CLAS, sentences |

## Train/test separation

- Manifest: `manifest-en.json` written by `tools/fetch/en/fetch.sh`, hashes in `tools/fetch/en/MANIFEST.md`.
- Text-level overlap: 114 of 2,077 test sentences have identical text in train or dev (127 after folding case and whitespace), mostly one- and two-word fragments; left in, as for the spaCy reference.
- The recipe reads the train and dev files only; the test file is read only by `predict`. Model selection (the best checkpoint, the configuration: window layers against LSTM, widths, dropout) used dev LAS. The test file was also scored on earlier, rejected models (hashed-token encoder, window layers only) as information; none of those scores chose a setting.

## Results

EWT r2.18 test, paired bootstrap 95% CI (1,000 resamples over sentences, default seed), reference spaCy 3.8.18 `parser` pipeline on the same data and mode (predictions of the baseline run, see `tools/baselines/spacy/PROVENANCE.md`):

| Mode | Metric | Ours | spaCy | Within noise |
|---|---|---|---|---|
| gold tokens and sentences | UAS | 85.77 [84.93, 86.61] | 85.40 [84.48, 86.28] | yes |
| | LAS | 81.68 [80.80, 82.56] | 81.96 [81.00, 82.91] | yes |
| | CLAS | 76.76 [75.73, 77.82] | 77.28 [76.14, 78.40] | yes |
| raw text, parser sentences | UAS | 81.80 [80.86, 82.71] | 81.34 [80.25, 82.34] | yes |
| | LAS | 78.13 [77.22, 79.04] | 78.53 [77.49, 79.54] | yes |
| | CLAS | 73.04 [71.97, 74.12] | 74.34 [73.19, 75.49] | yes |
| | Sentences | 76.15 [74.47, 77.96] | 72.66 [70.46, 74.70] | yes |
| raw text, rule sentencizer (information) | LAS | 75.19 [74.22, 76.18] | 78.53 | no |

Regenerate: `predict` (three modes) then `tools/OneNlp.Eval ud --gold G --pred P --ref-pred R --metrics UAS,LAS,CLAS,Sentences`.

## Notes

- The parser has its own encoder here. A pipeline that shares one encoder across the tagging heads and the parser trains them together with `Train`.
- Indicative throughput, raw text through tokenizer and parser, 4 threads: about 10,000 tokens per second (9,350 to 11,210 over three runs), 1.4 KB managed allocation per token.
