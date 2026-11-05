# French data manifest

Fetched by `tools/fetch/fr/fetch.sh <data-root>`. UD release r2.18.

## Choice

- **Gating treebank: UD French-GSD** (CC BY-SA 4.0; 14,450 / 1,476 / 416 sentences). The test split is small (416 sentences, 10,017 words): intervals are wide. The README's Google notice says the underlying sentences are provided "AS IS" and Google claims no ownership; the annotations are CC BY-SA 4.0.
- **Second test set: UD French-PUD** (CC BY-SA 3.0, 1,000 sentences, eval-only).
- **NER: WikiNER, French wp3** (CC BY 4.0 per its figshare record; silver labels from Wikipedia links; PER, LOC, ORG, MISC). No official split: articles (blank-line-delimited blocks) go to test at index mod 20 = 0 and to dev at index mod 20 = 10; dev is thinned to 2,000 sentences. There is no allowed gold-annotated French NER corpus; WikiNEuRal and MultiNERD are NC.
- Not used: Rhapsodie and ParisStories (go, spoken French, small), Sequoia and FQB (hold: LGPL-LR), ParTUT (no-go, NC). FTB is not in release 2.18.

## Tokenization

GSD has multiword-token lines for contractions (`du` = `de le`, `au` = `à le`); the CoNLL 2018 "Words" column scores the syntactic words. spaCy's French tokenizer keeps surface tokens, which costs it in raw-text scoring.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.
