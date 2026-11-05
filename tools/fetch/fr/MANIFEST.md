# French data manifest

Fetched by `tools/fetch/fr/fetch.sh <data-root>`. UD release r2.18.

## Choice

- **Gating treebank: UD French-GSD** (CC BY-SA 4.0; 14,450 / 1,476 / 416 sentences). The test split is small (416 sentences, 10,017 words): intervals are wide. The README's Google notice says the underlying sentences are provided "AS IS" and Google claims no ownership; the annotations are CC BY-SA 4.0.
- **Second test set: UD French-PUD** (CC BY-SA 3.0, 1,000 sentences, eval-only).
- **NER: WikiNER, French wp3** (CC BY 4.0 per its figshare record; silver labels from Wikipedia links; PER, LOC, ORG, MISC). No official split: articles (blank-line-delimited blocks) go to test at index mod 20 = 0 and to dev at index mod 20 = 10; dev is thinned to 2,000 sentences. There is no allowed gold-annotated French NER corpus; WikiNEuRal and MultiNERD are NC.
- Not used: Rhapsodie and ParisStories (go, spoken French, small), Sequoia and FQB (go, LGPL-LR with credit; unused), ParTUT (no-go, NC). FTB is not in release 2.18.

## Tokenization

GSD has multiword-token lines for contractions (`du` = `de le`, `au` = `à le`); the CoNLL 2018 "Words" column scores the syntactic words. spaCy's French tokenizer keeps surface tokens, which costs it in raw-text scoring.

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-fr-gsd | `fr_gsd-ud-train.conllu` | 14,450 | 354,647 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_French-GSD/r2.18/fr_gsd-ud-train.conllu | `4b9a87b1b7e6e7003219df1d3ccca87ffaca0f00a6055b12b1857df5373589d2` |
| dev | ud-fr-gsd | `fr_gsd-ud-dev.conllu` | 1,476 | 35,721 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_French-GSD/r2.18/fr_gsd-ud-dev.conllu | `9221e5084cc6a1b1540671cfbd2fc0456efc223b2fe8dd2146c2bad921be02d4` |
| eval | ud-fr-gsd | `fr_gsd-ud-test.conllu` | 416 | 10,017 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_French-GSD/r2.18/fr_gsd-ud-test.conllu | `eee5a599b429658b6ee8582fae9993eb07161247fc64bad57d16b2050ed4eb1a` |
| eval | ud-fr-pud | `fr_pud-ud-test.conllu` | 1,000 | 24,726 | CC BY-SA 3.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_French-PUD/r2.18/fr_pud-ud-test.conllu | `4dfed37b83d76e77fd2e9963d0be00d723e9a010e7e2a746f8b7640c48063c10` |
| train | wikiner-fr-wp3 | `wikiner-fr-wp3-train.iob2` | 119,460 | 3,163,656 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446338 (converted by tools/fetch/fetch_lang.py) | `6c3bbfae6dc8d4ea22298140c776791b9dacea8802a9e069a72deaf5d55d8ab7` |
| dev | wikiner-fr-wp3 | `wikiner-fr-wp3-dev.iob2` | 1,769 | 46,119 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446338 (converted by tools/fetch/fetch_lang.py) | `112116cdf110fe89fd3caa7868ce913b61c12c97f8df2b633fc76286624674a9` |
| eval | wikiner-fr-wp3 | `wikiner-fr-wp3-test.iob2` | 5,723 | 153,803 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446338 (converted by tools/fetch/fetch_lang.py) | `bcabfe5c4120855d389b8013663faa8b24c08a687cfa5e946b69138812ce870f` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| wikiner-fr-wp3 | `wikiner-fr-wp3.bz2` | `b6b2232ef7e33c83bed1b1e20986543885bf5ac1ea646a5ae038436bc9f58b90` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/fr/fr_gsd-ud-test.conllu` | ud-fr-gsd | 416 | 1 | 1 |
| `eval/ud/fr/fr_gsd-ud-test.conllu` | wikiner-fr-wp3 (other family) | 416 | 0 | 0 |
| `eval/ud/fr/fr_pud-ud-test.conllu` | ud-fr-gsd | 1,000 | 0 | 0 |
| `eval/ud/fr/fr_pud-ud-test.conllu` | wikiner-fr-wp3 (other family) | 1,000 | 0 | 0 |
| `eval/ner/fr/wikiner-fr-wp3/wikiner-fr-wp3-test.iob2` | wikiner-fr-wp3 | 5,723 | 32 | 33 |
| `eval/ner/fr/wikiner-fr-wp3/wikiner-fr-wp3-test.iob2` | ud-fr-gsd (other family) | 5,723 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/fr/ud-fr-gsd/LICENSE.txt` | `899b1804a12ebc090b96339614eede1b64b686721b650a71430b55b5235f7f79` |
| `notices/fr/ud-fr-gsd/README.md` | `d1a541bb6790fc3d02ed70a281bd4320593b98b8b06ed71371004c98bd74e37d` |
| `notices/fr/ud-fr-pud/LICENSE.txt` | `b278eb53fe50b8bb7fa0d90fb8536c35fdcaa80f9d63812cb51db539555d2a89` |
| `notices/fr/ud-fr-pud/README.md` | `d76e6467a48f14e291d5006c5fb709243045d901a2f349dfcc15bcc89dc70abd` |
