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
