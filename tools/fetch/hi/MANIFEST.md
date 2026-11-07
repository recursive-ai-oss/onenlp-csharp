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

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| eval | ud-hi-pud | `hi_pud-ud-test.conllu` | 1,000 | 23,829 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Hindi-PUD/r2.18/hi_pud-ud-test.conllu | `3562b7f49420912acad70a45a483f683dcb44624ea92f82e21b29f2ee41a9dff` |
| train | naamapadam-hi | `naamapadam-hi-train.iob2` | 98,579 | 2,200,329 | CC0 1.0 (packaging; the text is not owned by the curators) | https://huggingface.co/datasets/ai4bharat/naamapadam/resolve/9d4f21ac57d11ed4f9ea64854fdc9f5618e61acc/data/hi_IndicNER_v1.0.zip#hi_train.json (every 10th line, converted) | `a1bd621a517f347720f24e2a198dff3b064950792f5b50a95656970fda286f8d` |
| dev | naamapadam-hi | `naamapadam-hi-dev.iob2` | 1,923 | 44,218 | CC0 1.0 (packaging; the text is not owned by the curators) | https://huggingface.co/datasets/ai4bharat/naamapadam/resolve/9d4f21ac57d11ed4f9ea64854fdc9f5618e61acc/data/hi_IndicNER_v1.0.zip#hi_val.json (every 7th line, converted) | `feb44c1c7cfeba5ce15f3d73380dd27cbe2a4902472ba53c3c00ce1f9cd87f33` |
| eval | naamapadam-hi | `naamapadam-hi-test.iob2` | 864 | 19,893 | CC0 1.0 (packaging; the text is not owned by the curators) | https://huggingface.co/datasets/ai4bharat/naamapadam/resolve/9d4f21ac57d11ed4f9ea64854fdc9f5618e61acc/data/hi_IndicNER_v1.0.zip#hi_test.json (every 1th line, converted) | `6bd0e6cefa23ce91c2f4c89bc522c74bf446c5b71c37b620d71c35dd432c4d68` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| naamapadam-hi | `hi_IndicNER_v1.0.zip` | `051cd8313d55204a84c2e5535147438a8314867c6a772cb45d9f64cb5768fe32` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/hi/hi_pud-ud-test.conllu` | naamapadam-hi (other family) | 1,000 | 0 | 0 |
| `eval/ner/hi/naamapadam-hi/naamapadam-hi-test.iob2` | naamapadam-hi | 864 | 53 | 53 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/hi/ud-hi-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/hi/ud-hi-pud/README.md` | `5ddd43a77e6d947d934bb442a8c9ba79549fb70d54ec8e428ceffc7d7fa864ed` |
| `notices/hi/naamapadam-hi/README.md` | `2f6a5559ac0c9035f5b8b9c06e67a43f66c4f7b57d048a6ff36aac09b90221f3` |

## Hindi fast tier (self-labelled web text)

The Hindi student does not train on the corpora above. Its text is FineWeb-2 `hin_Deva` (see `recipes/hi/fast/PROVENANCE.md`); `selfdev.py` (with `selfdev_own.txt`, 83 sentences we wrote), `nerspot.py` and `iob2_to_conllu.py` in this directory build the hand-annotated checks in `testdata/hi/`. The Marathi, Sindhi and Punjabi treebanks that train the related-language label heads (UD r2.18, CC BY-SA 4.0) are fetched by `tools/fetch/fetch_ud.py` (`tools/fetch/ud/MANIFEST.md`). Hindi-PUD (above, sha256 `3562b7f49420912acad70a45a483f683dcb44624ea92f82e21b29f2ee41a9dff`) is read once, for the final model. Naamapadam stays out of training and of every row.
