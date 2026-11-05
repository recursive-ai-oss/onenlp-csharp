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

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-es-ancora | `es_ancora-ud-train.conllu` | 14,287 | 453,039 | CC BY 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Spanish-AnCora/r2.18/es_ancora-ud-train.conllu | `47b6fc49d6ed48a016e7fa5ad9f5c7ecf3ec21e9db005df3ea14d10f07c6deb6` |
| dev | ud-es-ancora | `es_ancora-ud-dev.conllu` | 1,654 | 53,476 | CC BY 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Spanish-AnCora/r2.18/es_ancora-ud-dev.conllu | `1a132068062edf2654fb45b41cd4c59ba7e5c18e7c5f642ed5a9cc351fd9f0fe` |
| eval | ud-es-ancora | `es_ancora-ud-test.conllu` | 1,721 | 53,622 | CC BY 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Spanish-AnCora/r2.18/es_ancora-ud-test.conllu | `679f1c05ca654aa26801c80319df45f8f97606718fb534b91755b48f3e67fa6f` |
| eval | ud-es-pud | `es_pud-ud-test.conllu` | 1,000 | 23,283 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Spanish-PUD/r2.18/es_pud-ud-test.conllu | `48a7b5c7f409100b24eba90c7397e01f6fecaaf3207315a9a1bda029f77e98d3` |
| train | wikiner-es-wp3 | `wikiner-es-wp3-train.iob2` | 113,959 | 3,145,704 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446332 (converted by tools/fetch/fetch_lang.py) | `40720c77b9856888db2c191efb42abd49781bd8027e330e89c32e397aa1599bf` |
| dev | wikiner-es-wp3 | `wikiner-es-wp3-dev.iob2` | 1,653 | 45,070 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446332 (converted by tools/fetch/fetch_lang.py) | `eeeed400ef4f9722d16529277eeb1af848775af449404bd747579a285be64d77` |
| eval | wikiner-es-wp3 | `wikiner-es-wp3-test.iob2` | 6,468 | 172,501 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446332 (converted by tools/fetch/fetch_lang.py) | `1d25596ad6f64e64dbfb86aa485b52a9df8a36b73500143e910f17506257c4c6` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| wikiner-es-wp3 | `wikiner-es-wp3.bz2` | `872248bf61439990f216079052c4d6377cadecbb563fdc49c2ba018b42e169e1` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/es/es_ancora-ud-test.conllu` | ud-es-ancora | 1,721 | 5 | 5 |
| `eval/ud/es/es_ancora-ud-test.conllu` | wikiner-es-wp3 (other family) | 1,721 | 0 | 0 |
| `eval/ud/es/es_pud-ud-test.conllu` | ud-es-ancora | 1,000 | 0 | 0 |
| `eval/ud/es/es_pud-ud-test.conllu` | wikiner-es-wp3 (other family) | 1,000 | 0 | 0 |
| `eval/ner/es/wikiner-es-wp3/wikiner-es-wp3-test.iob2` | wikiner-es-wp3 | 6,468 | 69 | 70 |
| `eval/ner/es/wikiner-es-wp3/wikiner-es-wp3-test.iob2` | ud-es-ancora (other family) | 6,468 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/es/ud-es-ancora/LICENSE.txt` | `653702c7fdee69f7d7a439c9ba4c178c26e745479f6e6d2943bed1a9b9c7bf67` |
| `notices/es/ud-es-ancora/README.md` | `695eeb9e89e7da50f08a9831c5e95063d124f873d8e47ded77c461790af467e2` |
| `notices/es/ud-es-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/es/ud-es-pud/README.md` | `e0f187331cb5c0a4b8afc4b209dd6b273eb9f98fe2944adaea45967987b7c668` |
