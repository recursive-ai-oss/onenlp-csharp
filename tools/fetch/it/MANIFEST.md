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

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-it-parlamint | `it_parlamint-ud-train.conllu` | 326 | 10,026 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Italian-ParlaMint/r2.18/it_parlamint-ud-train.conllu | `2b83d41e5eacfab1511245e9032a12a412d273cbb153bcdef2cc5ac793d91aea` |
| eval | ud-it-parlamint | `it_parlamint-ud-test.conllu` | 375 | 10,434 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Italian-ParlaMint/r2.18/it_parlamint-ud-test.conllu | `aa0e0fc94683fef4d4ee73a387e747c31b1639c1123570b4a536174af2327229` |
| train | ud-it-markit | `it_markit-ud-train.conllu` | 611 | 19,893 | CC BY 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Italian-MarkIT/r2.18/it_markit-ud-train.conllu | `4f6fc3423acb00d49e1e167bd21b11ebbc23e22275fd6f5eeb4d75cfbbccc5c6` |
| dev | ud-it-markit | `it_markit-ud-dev.conllu` | 341 | 10,399 | CC BY 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Italian-MarkIT/r2.18/it_markit-ud-dev.conllu | `633b6186f47ecfebfcff4ff9c7e19a0cbffd1f655f14fc1e9265b12bda597f97` |
| eval | ud-it-markit | `it_markit-ud-test.conllu` | 340 | 10,196 | CC BY 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Italian-MarkIT/r2.18/it_markit-ud-test.conllu | `9048ed0064e3989e2c37377755a28c25c7e462b1ad916e5808dc0a7ce95fc3fc` |
| eval | ud-it-pud | `it_pud-ud-test.conllu` | 1,000 | 23,732 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Italian-PUD/r2.18/it_pud-ud-test.conllu | `ad5d302bdfd05194155d9ad43dc54c3cb915076d66b3eb6ef33eca9a11f1372a` |
| train | wikiner-it-wp3 | `wikiner-it-wp3-train.iob2` | 115,965 | 3,172,998 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446344 (converted by tools/fetch/fetch_lang.py) | `d04c5d4e2a9689c0a7a145ae262bd85674fbd94bc174b3082327a909a032ab66` |
| dev | wikiner-it-wp3 | `wikiner-it-wp3-dev.iob2` | 1,914 | 52,042 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446344 (converted by tools/fetch/fetch_lang.py) | `e805005685c85f422263716b2015b3fbfcd3c39a56c94d39f0e34184a9eaaa66` |
| eval | wikiner-it-wp3 | `wikiner-it-wp3-test.iob2` | 6,234 | 170,441 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446344 (converted by tools/fetch/fetch_lang.py) | `6dcab55fd030dc93fd7be266e28504bcc4c41be5f362cc175cdc6931a9bd7429` |
| train | ud-it-gating | `it_gating-ud-train.conllu` | 897 | 28,881 | CC BY-SA 4.0 and CC BY 4.0 | derived from it_parlamint and it_markit (tools/fetch/fetch_lang.py) | `909763cc8f5d2b15eb42f8f874482f455cf6c975827414c3e3266b8406685938` |
| dev | ud-it-gating | `it_gating-ud-dev.conllu` | 381 | 11,437 | CC BY-SA 4.0 and CC BY 4.0 | derived from it_parlamint and it_markit (tools/fetch/fetch_lang.py) | `53e038adcbc5f93978a1523abcd16ee91a12dee496683a4d00c972ea2efffc1c` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| wikiner-it-wp3 | `wikiner-it-wp3.bz2` | `d702670de515508b7b3341e3e088c9efd882c3647be6930b39f26a920dc4df69` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/it/it_parlamint-ud-test.conllu` | ud-it-markit, ud-it-parlamint | 375 | 13 | 13 |
| `eval/ud/it/it_parlamint-ud-test.conllu` | wikiner-it-wp3 (other family) | 375 | 0 | 0 |
| `eval/ud/it/it_markit-ud-test.conllu` | ud-it-markit, ud-it-parlamint | 340 | 0 | 0 |
| `eval/ud/it/it_markit-ud-test.conllu` | wikiner-it-wp3 (other family) | 340 | 0 | 0 |
| `eval/ud/it/it_pud-ud-test.conllu` | ud-it-markit, ud-it-parlamint | 1,000 | 0 | 0 |
| `eval/ud/it/it_pud-ud-test.conllu` | wikiner-it-wp3 (other family) | 1,000 | 0 | 0 |
| `eval/ner/it/wikiner-it-wp3/wikiner-it-wp3-test.iob2` | wikiner-it-wp3 | 6,234 | 87 | 88 |
| `eval/ner/it/wikiner-it-wp3/wikiner-it-wp3-test.iob2` | ud-it-markit, ud-it-parlamint (other family) | 6,234 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/it/ud-it-parlamint/LICENSE.txt` | `899b1804a12ebc090b96339614eede1b64b686721b650a71430b55b5235f7f79` |
| `notices/it/ud-it-parlamint/README.md` | `dd5d92a90c9d82b8ee4425f7c3d4b6c033a3a4851216c320eecfe6a16e639edf` |
| `notices/it/ud-it-markit/LICENSE.txt` | `5cbfe2d4d12938f9961bf5a0028390f4b6b60749b075e228a5740848afcb4a9b` |
| `notices/it/ud-it-markit/README.md` | `2b4d5d533bd3222cf71379af7483920907dc03c80c776f13f1e950648674325a` |
| `notices/it/ud-it-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/it/ud-it-pud/README.md` | `ae6f83587c6478aae20249b89957f6e09d0215e82964a1cf626c8dcd51579e3f` |
