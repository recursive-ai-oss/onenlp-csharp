# Hindi data manifest

Fetched by `tools/fetch/hi/fetch.sh <data-root>`. UD release r2.18.

## Choice: there is no allowed Hindi training treebank

- Hindi-HDTB, the only sizeable Hindi treebank, is **CC BY-NC-SA 4.0** and out. Hindi-Convers is not in release 2.18.
- **UD Hindi-PUD** (CC BY-SA 3.0, 1,000 sentences) is test-only and **eval-only**. It is fetched as the evaluation set so that the test side is fixed; there is no training split, so no tagger, morphologizer, lemmatizer or parser baseline can be trained, and no published number is on the same treebank (Stanza, UDPipe 2 and Trankit report Hindi-HDTB).
- Hindi UD models would need data the rule does not allow, or new data of our own .

## NER

- **Naamapadam, Hindi** (AI4Bharat; the packaging is CC0, the text is not owned by the curators): 985,787 silver training sentences projected from English over parallel Samanantar text, a 13,460-sentence validation file and a gold-annotated test set (the file has 867 lines, 864 usable sentences; the dataset card's own count of 437 is stale). We train on every 10th training sentence (98,579) and dev on every 7th validation sentence (1,923), the whole test set (864). 53 of the 864 test sentences also occur in train (the test file repeats sentences). Types: PER, ORG, LOC.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.
