# German data manifest

Fetched by `tools/fetch/de/fetch.sh <data-root>`. UD release r2.18.

## Choice

- **Gating treebank: UD German-GSD** (CC BY-SA 4.0; 13,813 / 799 / 977 sentences). German-HDT, the larger and cleaner treebank, is on hold: its README says "Heise gave permission to distribute the text for academic use" (`docs/ud-licences.md`).
- **Second test set: UD German-PUD** (CC BY-SA 3.0, 1,000 sentences, eval-only).
- **NER: GermEval 2014 (NoSta-D)** (CC BY 4.0; gold labels; official split 24,000 / 2,200 / 5,100 sentences). Four main types with `-deriv` and `-part` variants and a second, embedded level. The files keep the outer level and the original tags; the baseline maps PER, LOC and ORG to themselves, OTH to MISC and drops the derived and part variants to O, which is stated in `tools/baselines/spacy/run_lang.sh`. Universal NER German-PUD (test only, eval-only) is fetched as a second evaluation.

## Tokenization

GSD has few multiword tokens (`zum`, `im`). German GSD lemmas are automatic ("Lemmas: automatic" in the metadata).

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-de-gsd | `de_gsd-ud-train.conllu` | 13,813 | 263,777 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_German-GSD/r2.18/de_gsd-ud-train.conllu | `6cb1b600029292fb7bf60531a1d6d7824544791d732c0f7abae8102a2ac337bf` |
| dev | ud-de-gsd | `de_gsd-ud-dev.conllu` | 799 | 12,480 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_German-GSD/r2.18/de_gsd-ud-dev.conllu | `01e8e674973592747ffe9a8c77fcf9d2f5936a8484e731ad4f76254318a8952c` |
| eval | ud-de-gsd | `de_gsd-ud-test.conllu` | 977 | 16,499 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_German-GSD/r2.18/de_gsd-ud-test.conllu | `595070aa50b706a91dc66f17c296f7a9a25cbc75269f177c27680fb1c21528ab` |
| eval | ud-de-pud | `de_pud-ud-test.conllu` | 1,000 | 21,332 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_German-PUD/r2.18/de_pud-ud-test.conllu | `a530bdb50349bbd7c13706b6a759a9d73e8f514fff41fbf27149e914b0c3e723` |
| train | germeval-2014 | `germeval-2014-train.iob2` | 24,000 | 452,853 | CC BY 4.0 (Benikova, Biemann, Reznicek 2014: "released under the permissive CC-BY license") | https://www.inf.uni-hamburg.de/en/inst/ab/lt/resources/data/germaner/germeval2014-complete-data.zip#NER-de-train.tsv (converted) | `958b1743dc888696062df4eeafdeacdeabb2e6560a64d8691ff82f610866a45f` |
| dev | germeval-2014 | `germeval-2014-dev.iob2` | 2,200 | 41,653 | CC BY 4.0 (Benikova, Biemann, Reznicek 2014: "released under the permissive CC-BY license") | https://www.inf.uni-hamburg.de/en/inst/ab/lt/resources/data/germaner/germeval2014-complete-data.zip#NER-de-dev.tsv (converted) | `d0c6bb759a5cfa6beb419067e16e925c52ad820f372094770343eab37e3ddca9` |
| eval | germeval-2014 | `germeval-2014-test.iob2` | 5,100 | 96,499 | CC BY 4.0 (Benikova, Biemann, Reznicek 2014: "released under the permissive CC-BY license") | https://www.inf.uni-hamburg.de/en/inst/ab/lt/resources/data/germaner/germeval2014-complete-data.zip#NER-de-test.tsv (converted) | `3aac959b721df30ec8e49113cf25f061c98a04a1730be433b9706d8a2c292df3` |
| eval | uner-de-pud | `de_pud-ud-test.iob2` | 1,000 | 21,331 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalNER/UNER_German-PUD/47eb039b6d80079b280ba70efe4136413fb7af9c/de_pud-ud-test.iob2 | `352c09251cf1c376d2786261813041c976c1aa7ce9bcf78388a4342a30bd00d4` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| germeval-2014 | `germeval2014-complete-data.zip` | `882473e99680fa976de4c23b443d85c3779bedcf6aae93e9d21d6b541968e27d` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/de/de_gsd-ud-test.conllu` | ud-de-gsd | 977 | 27 | 28 |
| `eval/ud/de/de_gsd-ud-test.conllu` | germeval-2014 (other family) | 977 | 0 | 0 |
| `eval/ud/de/de_pud-ud-test.conllu` | ud-de-gsd | 1,000 | 0 | 0 |
| `eval/ud/de/de_pud-ud-test.conllu` | germeval-2014 (other family) | 1,000 | 0 | 0 |
| `eval/ner/de/germeval-2014/germeval-2014-test.iob2` | germeval-2014 | 5,100 | 0 | 0 |
| `eval/ner/de/germeval-2014/germeval-2014-test.iob2` | ud-de-gsd (other family) | 5,100 | 0 | 0 |
| `eval/uner/de/de_pud-ud-test.iob2` | germeval-2014 | 1,000 | 0 | 0 |
| `eval/uner/de/de_pud-ud-test.iob2` | ud-de-gsd (other family) | 1,000 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/de/ud-de-gsd/LICENSE.txt` | `899b1804a12ebc090b96339614eede1b64b686721b650a71430b55b5235f7f79` |
| `notices/de/ud-de-gsd/README.md` | `5d1af1a1fb974e83ccb9ad9446d19a13d4608faf1f57ee2bf85d4e8351cf5fa2` |
| `notices/de/ud-de-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/de/ud-de-pud/README.md` | `34f2708e3efb935e5cf37c74c82c0dda9c3a417ca3ead0ba612673a7de2a36d5` |
| `notices/de/uner-de-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/de/uner-de-pud/README.md` | `5193bae44224c4334cee6338d49a5bb4fdd0e047d664af24f45afc9922eba3ab` |
