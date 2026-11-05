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
