# Italian data manifest

Fetched by `tools/fetch/it/fetch.sh <data-root>`. UD release r2.18.

## Choice: Italian is data-starved

The two standard Italian treebanks are non-commercial (ISDT: CC BY-NC-SA 3.0, VIT: CC BY-NC-SA 3.0), as are ParTUT, PoSTWITA and KIParlaForest. What is allowed and modern is small:

- **UD Italian-ParlaMint** (CC BY-SA 4.0): Senate transcripts, 326 train sentences (10,026 words) and 375 test sentences, **no dev split**.
- **UD Italian-MarkIT** (CC BY 4.0): 611 / 341 / 340 grammar-example sentences.
- TWITTIRO is on hold (tweets); Italian-Old is Dante's Comedy (Old Italian, not modern text); Valico is test-only learner essays.

So the training set is derived: **`it_gating` = ParlaMint train minus every 8th sentence + MarkIT train** (897 sentences, 28,881 words), **dev = every 8th ParlaMint train sentence + MarkIT dev** (381 sentences, 11,437 words). The derivation is `derive_italian` in `tools/fetch/fetch_lang.py`. **Gating test: ParlaMint test** (375 sentences); MarkIT test and Italian-PUD (1,000 sentences, CC BY-SA 3.0, eval-only) are second and third test sets. 13 of the 375 ParlaMint test sentences also occur in train, listed below.

The resulting model is trained on 29k words: the baseline is indicative and no published system trained on ISDT is comparable. A larger Italian training set needs a decision on one of the held or NC treebanks, or other data.

- **NER: WikiNER, Italian wp3** (CC BY 4.0 per its figshare record; silver labels; PER, LOC, ORG, MISC); same split rule as French.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.
