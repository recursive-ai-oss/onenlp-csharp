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

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-zh-gsdsimp | `zh_gsdsimp-ud-train.conllu` | 3,997 | 98,614 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Chinese-GSDSimp/r2.18/zh_gsdsimp-ud-train.conllu | `956636fe612a1166e8b19e7413fee2e73d68231aca2f0455be2c616b947d629d` |
| dev | ud-zh-gsdsimp | `zh_gsdsimp-ud-dev.conllu` | 500 | 12,665 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Chinese-GSDSimp/r2.18/zh_gsdsimp-ud-dev.conllu | `d03f1eeb93b16071bfbbe6c76b971554be87c9a2307b3f3a820dd7c07f73fb63` |
| eval | ud-zh-gsdsimp | `zh_gsdsimp-ud-test.conllu` | 500 | 12,010 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Chinese-GSDSimp/r2.18/zh_gsdsimp-ud-test.conllu | `3af8046a6f32477b4d5cf3dd06bbf38682a380fe77aade3f68de97e51ab94900` |
| eval | ud-zh-pud | `zh_pud-ud-test.conllu` | 1,000 | 21,415 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Chinese-PUD/r2.18/zh_pud-ud-test.conllu | `e12582af2e2bbc2e27155ca7dc12b6fc4a037dbc716e1f805d67e34ffb2596b3` |
| train | uner-zh-gsdsimp | `zh_gsdsimp-ud-train.iob2` | 3,997 | 98,616 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Chinese-GSDSIMP/1f0307599dece7b83f4cba90ce9b63ef37a7b162/zh_gsdsimp-ud-train.iob2 | `a03b5260c3d960aa711ff5f12b797de5c7d7f412563593c173cd0e7135001db5` |
| dev | uner-zh-gsdsimp | `zh_gsdsimp-ud-dev.iob2` | 500 | 12,663 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Chinese-GSDSIMP/1f0307599dece7b83f4cba90ce9b63ef37a7b162/zh_gsdsimp-ud-dev.iob2 | `8aeab3ac662468e258a65f887844c50cee8311cb080b60a421ff7aa8a05e8032` |
| eval | uner-zh-gsdsimp | `zh_gsdsimp-ud-test.iob2` | 500 | 12,012 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Chinese-GSDSIMP/1f0307599dece7b83f4cba90ce9b63ef37a7b162/zh_gsdsimp-ud-test.iob2 | `f1d8faa790d914fe01a24627874c8e7fad9c40b54742f5aefab5ba3f7c8edede` |
| eval | uner-zh-pud | `zh_pud-ud-test.iob2` | 1,000 | 21,415 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Chinese-PUD/647ba53747e1aa1818541965db99a0196440b7d2/zh_pud-ud-test.iob2 | `c7549cf2ece208ec0068a1b71bc40eec4e562579738a45b0ad7ea33482e787c4` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/zh/zh_gsdsimp-ud-test.conllu` | ud-zh-gsdsimp | 500 | 0 | 0 |
| `eval/ud/zh/zh_gsdsimp-ud-test.conllu` | uner-zh-gsdsimp (other family) | 500 | 0 | 0 |
| `eval/ud/zh/zh_pud-ud-test.conllu` | ud-zh-gsdsimp | 1,000 | 0 | 0 |
| `eval/ud/zh/zh_pud-ud-test.conllu` | uner-zh-gsdsimp (other family) | 1,000 | 0 | 0 |
| `eval/uner/zh/zh_gsdsimp-ud-test.iob2` | uner-zh-gsdsimp | 500 | 0 | 0 |
| `eval/uner/zh/zh_gsdsimp-ud-test.iob2` | ud-zh-gsdsimp (other family) | 500 | 0 | 0 |
| `eval/uner/zh/zh_pud-ud-test.iob2` | uner-zh-gsdsimp | 1,000 | 0 | 0 |
| `eval/uner/zh/zh_pud-ud-test.iob2` | ud-zh-gsdsimp (other family) | 1,000 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/zh/ud-zh-gsdsimp/LICENSE.txt` | `899b1804a12ebc090b96339614eede1b64b686721b650a71430b55b5235f7f79` |
| `notices/zh/ud-zh-gsdsimp/README.md` | `02287bdf80282151d8ca7ef3c3f7a3c3b98609f7266f145e3e3dd0a05693abd3` |
| `notices/zh/ud-zh-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/zh/ud-zh-pud/README.md` | `4abc2a54f3b38f8da61c9a751f26d8e3292c99461e9e7fe28b6ad545d3fbd4e7` |
| `notices/zh/uner-zh-gsdsimp/LICENSE.txt` | `76cbab13f5ddb068524dafb0600017c796311cd459def36d3d9d8cd0b8fb49be` |
| `notices/zh/uner-zh-gsdsimp/README.md` | `cf0ac7971c6a58b7ebb6361a66279faf5c800f427e512488ff0b0d5429e247ca` |
| `notices/zh/uner-zh-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/zh/uner-zh-pud/README.md` | `f3844b000b59792724b2b7b07c96cb1f11aeddfbfca5e11254f48b7b9c950e14` |
