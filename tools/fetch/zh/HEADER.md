# Chinese data manifest

Fetched by `tools/fetch/zh/fetch.sh <data-root>` (layout in `tools/fetch/fetch_lang.py`). UD release r2.18.

## Choice

- **Gating treebank: UD Chinese-GSDSimp** (CC BY-SA 4.0; 3,997 / 500 / 500 sentences). It is the Simplified-script conversion of the Traditional-script UD Chinese-GSD (same sentences, same annotation, OpenCC conversion with manual fixes). **Train on one of the two only**: the test sentences of one are the training sentences of the other. GSD is not fetched.
- **Second test set: UD Chinese-PUD** (CC BY-SA 3.0, 1,000 sentences, eval-only, no training split). **PUD is in Traditional script**, so a model trained on the Simplified GSDSimp sees mostly unseen characters: the PUD rows measure script transfer, not the gating quality. Traditional GSD (not fetched) is the matching training set for a Traditional-script model.
- **NER: Universal NER Chinese-GSDSIMP** (CC BY-SA 4.0; PER, ORG, LOC) for training and evaluation, **Chinese-PUD** (test only) as a second evaluation. UNER's GSDSIMP has the same sentence texts as the UD files; tokens differ in 2 train, 1 dev and 2 test sentences.
- Not used: Chinese-CFL and Chinese-HK (test-only, eval-only), Beginner and PatentChar (no-go).

## Tokenization of the Chinese treebanks

- Text has no spaces. Words are multi-character units (1.6 characters per word on the GSDSimp test set, 1.66 on PUD); `SpaceAfter=No` is absent on all but 29 tokens, so offsets cannot be recovered from spaces.
- Word segmentation is the treebank's own, so "Tokens" and "Words" in raw-text scoring are segmentation F1. spaCy's default Chinese configuration segments by single characters (`segmenter = "char"`), so its raw-text rows are limited by segmentation (Tokens F1 far below 100); gold-token rows isolate the trained components.
- Lemmas equal the form for 99.3% of test tokens (the treebank lemmatizes almost nothing); UFeats are present on 12% of tokens; there are no multiword-token lines.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.
