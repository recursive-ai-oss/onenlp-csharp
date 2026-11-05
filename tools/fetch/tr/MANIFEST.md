# Turkish data manifest

Fetched by `tools/fetch/tr/fetch.sh <data-root>`. UD release r2.18. Turkish needs pyarrow at fetch time (`PYTHON` names an interpreter that has it).

## Choice

- **Gating treebank: UD Turkish-BOUN** (CC BY-SA 4.0; 7,803 / 979 / 979 sentences, 100,713 training words). IMST, the standard Turkish treebank, is CC BY-NC-SA 4.0 and out. Kenet, Tourism and FrameNet are allowed and not used (dictionary examples, reviews, grammar examples); Penn is on hold (a translation of Penn Treebank text).
- **Second test set: UD Turkish-PUD** (CC BY-SA 3.0, 1,000 sentences, eval-only).
- **NER: Turkish-WikiNER** (turkish-nlp-suite, CC BY-SA 4.0; gold labels; official split 17,967 / 1,000 / 1,000 sentences; 18 OntoNotes-style types). The baseline keeps PERSON, ORG, GPE and LOC (GPE and LOC both become LOC) and turns the other types into O, stated in `tools/baselines/spacy/run_lang.sh`.

## Tokenization

BOUN is agglutinative; words are surface forms, with multiword tokens only where a word splits into several syntactic words. The BOUN README describes the genre as non-fiction news.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-tr-boun | `tr_boun-ud-train.conllu` | 7,803 | 100,713 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Turkish-BOUN/r2.18/tr_boun-ud-train.conllu | `ae54aef82e69cb7ccc54703a07e2ebcde66c2e4453a5debfac1eb78dfed60e9f` |
| dev | ud-tr-boun | `tr_boun-ud-dev.conllu` | 979 | 12,289 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Turkish-BOUN/r2.18/tr_boun-ud-dev.conllu | `b3531bb26337b3ef258796e6948d2c3469e71704613b9373cd87f341409e2d8d` |
| eval | ud-tr-boun | `tr_boun-ud-test.conllu` | 979 | 12,210 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Turkish-BOUN/r2.18/tr_boun-ud-test.conllu | `c7f234f6a7c1b7025f8cab9025239286836e971b653fb1dd3f266a84ac74dbeb` |
| eval | ud-tr-pud | `tr_pud-ud-test.conllu` | 1,000 | 16,881 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Turkish-PUD/r2.18/tr_pud-ud-test.conllu | `7c1b6fe01662a7617758fbecdda6af6fc3914ace46b1809d6514af0a1c1df88c` |
| train | turkish-wikiner | `turkish-wikiner-train.iob2` | 17,967 | 303,443 | CC BY-SA 4.0 | https://huggingface.co/datasets/turkish-nlp-suite/turkish-wikiNER/resolve/99df1dd7f5a01d39cd5903e51e361d708cb2a5a6/data/train-00000-of-00001.parquet (converted) | `4c97b5d98f012cc4eb9a9b352c27f60fdd52ffd3ec35e738e71c52bdc2e5da89` |
| dev | turkish-wikiner | `turkish-wikiner-dev.iob2` | 1,000 | 16,658 | CC BY-SA 4.0 | https://huggingface.co/datasets/turkish-nlp-suite/turkish-wikiNER/resolve/99df1dd7f5a01d39cd5903e51e361d708cb2a5a6/data/validation-00000-of-00001.parquet (converted) | `dee9ec06448a5ccb6c193ef4ce5058400fbc17073dfe59cd92846466fe1c6017` |
| eval | turkish-wikiner | `turkish-wikiner-test.iob2` | 1,000 | 17,275 | CC BY-SA 4.0 | https://huggingface.co/datasets/turkish-nlp-suite/turkish-wikiNER/resolve/99df1dd7f5a01d39cd5903e51e361d708cb2a5a6/data/test-00000-of-00001.parquet (converted) | `ee907c75bcc5fdec3c3ac57977bf851d19abadefd71ebad7f8e910cd1a51e084` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| turkish-wikiner | `train` | `7862961d3ac283a0460bca75c64e827ee3f800e2febc7b36f398d8e7a6a58501` |
| turkish-wikiner | `validation` | `a79fade31c37fade234ec7af2cd90aad0df944b72193a8cec097f9087f0141b8` |
| turkish-wikiner | `test` | `60b4b8b5ba3d02542546019c8a1f85d02789f50099788a37d9ff9b44074cea7f` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/tr/tr_boun-ud-test.conllu` | ud-tr-boun | 979 | 6 | 6 |
| `eval/ud/tr/tr_boun-ud-test.conllu` | turkish-wikiner (other family) | 979 | 0 | 0 |
| `eval/ud/tr/tr_pud-ud-test.conllu` | ud-tr-boun | 1,000 | 0 | 0 |
| `eval/ud/tr/tr_pud-ud-test.conllu` | turkish-wikiner (other family) | 1,000 | 0 | 0 |
| `eval/ner/tr/turkish-wikiner/turkish-wikiner-test.iob2` | turkish-wikiner | 1,000 | 0 | 0 |
| `eval/ner/tr/turkish-wikiner/turkish-wikiner-test.iob2` | ud-tr-boun (other family) | 1,000 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/tr/ud-tr-boun/LICENSE.txt` | `899b1804a12ebc090b96339614eede1b64b686721b650a71430b55b5235f7f79` |
| `notices/tr/ud-tr-boun/README.md` | `b3dfa89603a5cc303eeef758f9384596b30d321a632f95a14fef843791a9292e` |
| `notices/tr/ud-tr-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/tr/ud-tr-pud/README.md` | `eb8cbfbf881fe9e073f4749406cb23ba83fab7adaea2230350311c2b09c8b6c5` |
| `notices/tr/turkish-wikiner/README.md` | `51182c199326d4b5d58878f89a36c67582ec8d423f94ae794b0b5c76b9ecc04d` |
