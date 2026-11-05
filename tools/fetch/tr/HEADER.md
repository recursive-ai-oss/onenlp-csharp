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
