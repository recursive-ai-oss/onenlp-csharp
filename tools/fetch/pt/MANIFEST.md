# Portuguese data manifest

Fetched by `tools/fetch/pt/fetch.sh <data-root>`. UD release r2.18.

## Choice

- **Gating treebank: UD Portuguese-Bosque** (CC BY-SA 4.0; 7,018 / 1,172 / 1,167 sentences). Universal NER annotates exactly the same tokens, so one treebank carries both the UD columns and entity labels.
- **Second test set: UD Portuguese-PUD** (CC BY-SA 3.0, 1,000 sentences, eval-only). Portuguese-GSD (larger, CC BY-SA 4.0), PetroGold and Porttinari are allowed and not used.
- **NER: Universal NER Portuguese-Bosque** (CC BY-SA 4.0; PER, ORG, LOC), **Portuguese-PUD** (test only) as a second evaluation. UNER's tokens are identical to r2.18 Bosque's in every sentence of every split.
- Not used: DANTEStocks (hold, tweets), CINTIL (no-go).

## Tokenization

Bosque has multiword tokens (`do`, `na`, clitics); the CoNLL 2018 "Words" column scores syntactic words.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-pt-bosque | `pt_bosque-ud-train.conllu` | 7,018 | 171,776 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Portuguese-Bosque/r2.18/pt_bosque-ud-train.conllu | `b56d7124af821e2968302c38298904ec67af342304a33ae8a9dc4fa57df89648` |
| dev | ud-pt-bosque | `pt_bosque-ud-dev.conllu` | 1,172 | 28,447 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Portuguese-Bosque/r2.18/pt_bosque-ud-dev.conllu | `f8a67abae12fbab85a3995204a6459d1958c11065062d4ea5349d6b914e78f81` |
| eval | ud-pt-bosque | `pt_bosque-ud-test.conllu` | 1,167 | 27,604 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Portuguese-Bosque/r2.18/pt_bosque-ud-test.conllu | `9a824650b7a02cf411f6e09b39e8fb423c85e1a5fb3a55b87e7f4c0c2ea5b3bb` |
| eval | ud-pt-pud | `pt_pud-ud-test.conllu` | 1,000 | 23,407 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Portuguese-PUD/r2.18/pt_pud-ud-test.conllu | `e2bd4012a3d0e17dc7ba5d699dd17e05f57986901bb24df07bd0a271cf23178c` |
| train | uner-pt-bosque | `pt_bosque-ud-train.iob2` | 7,018 | 171,776 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Portuguese-Bosque/f2c2ad67aed900385fbe5d655af41f439c9b3333/pt_bosque-ud-train.iob2 | `1d8762040667fc15f4ec83801e20d14d10ebea2b90c16c312f3ba03da2d1f860` |
| dev | uner-pt-bosque | `pt_bosque-ud-dev.iob2` | 1,172 | 28,447 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Portuguese-Bosque/f2c2ad67aed900385fbe5d655af41f439c9b3333/pt_bosque-ud-dev.iob2 | `0e6cb1db280e6677e2fd4380d695039791b5cb70bb5100376166aae5690da01a` |
| eval | uner-pt-bosque | `pt_bosque-ud-test.iob2` | 1,167 | 27,604 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Portuguese-Bosque/f2c2ad67aed900385fbe5d655af41f439c9b3333/pt_bosque-ud-test.iob2 | `45cf232ff7677cb6f4020dad96dbf8412d1f2cca38d81164ac96ee8c03b6b09f` |
| eval | uner-pt-pud | `pt_pud-ud-test.iob2` | 1,000 | 23,407 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Portuguese-PUD/cda2cf86b68d45e896aab97ceca5e91d43a86db7/pt_pud-ud-test.iob2 | `e91d2e88c4e0b8007f2db01fbea8f101e83e6756ba21256aaf420e8af65fda8a` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/pt/pt_bosque-ud-test.conllu` | ud-pt-bosque | 1,167 | 1 | 2 |
| `eval/ud/pt/pt_bosque-ud-test.conllu` | uner-pt-bosque (other family) | 1,167 | 1 | 2 |
| `eval/ud/pt/pt_pud-ud-test.conllu` | ud-pt-bosque | 1,000 | 0 | 0 |
| `eval/ud/pt/pt_pud-ud-test.conllu` | uner-pt-bosque (other family) | 1,000 | 0 | 0 |
| `eval/uner/pt/pt_bosque-ud-test.iob2` | uner-pt-bosque | 1,167 | 1 | 2 |
| `eval/uner/pt/pt_bosque-ud-test.iob2` | ud-pt-bosque (other family) | 1,167 | 1 | 2 |
| `eval/uner/pt/pt_pud-ud-test.iob2` | uner-pt-bosque | 1,000 | 0 | 0 |
| `eval/uner/pt/pt_pud-ud-test.iob2` | ud-pt-bosque (other family) | 1,000 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/pt/ud-pt-bosque/LICENSE.txt` | `ab3a36080055dd29ad2b59162e43d1368c7f15643666bea982d4e103f546e025` |
| `notices/pt/ud-pt-bosque/README.md` | `6b603f73859f6648bf9bf3d4e582cfb9507995972d2d948b11b61ab9f19614ab` |
| `notices/pt/ud-pt-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/pt/ud-pt-pud/README.md` | `4a36c32fea7af4eb77e81d7cb8eed56743434226797712f1e0bd48f1454ec2a1` |
| `notices/pt/uner-pt-bosque/LICENSE.txt` | `ab3a36080055dd29ad2b59162e43d1368c7f15643666bea982d4e103f546e025` |
| `notices/pt/uner-pt-bosque/README.md` | `4fb294d1c946cfa3ca294afd65e2025c42b9ee622fef5bbb32179a3290a65f88` |
| `notices/pt/uner-pt-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/pt/uner-pt-pud/README.md` | `6ee7184ccd34fd43e5c283ddc8f0716bb8c89ce3e41c2cf5964d463de96d4449` |
