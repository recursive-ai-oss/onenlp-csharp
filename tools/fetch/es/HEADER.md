# Spanish data manifest

Fetched by `tools/fetch/es/fetch.sh <data-root>`. UD release r2.18.

## Choice

- **Gating treebank: UD Spanish-AnCora** (CC BY 4.0; 14,287 / 1,654 / 1,721 sentences; the largest allowed Spanish treebank, with complete lemmas and features). `LICENSE.txt` and the metadata say CC BY 4.0, and the changelog records the change from the GNU licence in release 2.8; the README still carries the older sentence "The GNU license is inherited from the original dataset". Recorded as a stale-text risk (`docs/ud-licences.md`).
- **Second test set: UD Spanish-PUD** (CC BY-SA 3.0, 1,000 sentences, eval-only). Spanish-GSD (CC BY-SA 4.0) is allowed and not used.
- **NER: WikiNER, Spanish wp3** (CC BY 4.0 per its figshare record; silver labels; PER, LOC, ORG, MISC); same split rule as French. There is no allowed gold-annotated Spanish NER corpus.

## Tokenization

AnCora has multiword tokens (`del` = `de el`, `al`, clitic verbs such as `dárselo`). The CoNLL 2018 "Words" column scores syntactic words; `spacy convert` trains on the words.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.
