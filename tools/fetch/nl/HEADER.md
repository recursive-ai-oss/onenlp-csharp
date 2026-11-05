# Dutch data manifest

Fetched by `tools/fetch/nl/fetch.sh <data-root>`. UD release r2.18.

## Choice

- **Gating treebank: UD Dutch-Alpino** (CC BY-SA 4.0; 12,289 / 718 / 596 sentences, news). LassySmall (CC BY-SA 4.0, Wikipedia, 13,817 / 1,542 / 1,761 sentences) is allowed and not used; it is the second Dutch treebank and the natural second test set when a model is trained on both.
- **NER: WikiNER, Dutch wp3** (CC BY 4.0 per its figshare record; silver labels; PER, LOC, ORG, MISC). The file is ISO-8859-1; the fetch script decodes it as such. Same split rule as French. There is no allowed gold-annotated Dutch NER corpus (CoNLL-2002 has no open terms).

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.
