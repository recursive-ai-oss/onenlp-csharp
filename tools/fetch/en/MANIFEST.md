# English data manifest

Written by `fetch.sh` (same directory), which also produces the full `manifest-en.json` under the data root (role, URL, licence, size, SHA-256 and the overlap report for every file). This page keeps the hashes and licences in the repository. The data itself is never committed.

Checked 2026-11-04. Files are fetched from the tagged or pinned revisions below; a re-run verifies nothing has changed by comparing against these hashes.

## Files

| Dataset | Role | File | Sentences | Bytes | SHA-256 |
|---|---|---|---|---|---|
| UD English-EWT r2.18 | train | `en_ewt-ud-train.conllu` | 12,544 | 15,034,493 | `d68e06122a702464c613076523d56740f047e5bbe89dd90ec32737e04d952143` |
| UD English-EWT r2.18 | dev | `en_ewt-ud-dev.conllu` | 2,001 | 1,898,574 | `39239e0a60db3ae68f4b7036189f11b6692741d10ff8240dd91f74f2760d90f8` |
| UD English-EWT r2.18 | eval | `en_ewt-ud-test.conllu` | 2,077 | 1,900,101 | `fa024f43dc5da3c5ac02563bc9bd0e974f46cbb1560823976a8f342a37dc494a` |
| UNER English-EWT (commit `8ed072de`) | train | `en_ewt-ud-train.iob2` | 12,543 | 4,718,581 | `647b296624433d2b0f304f81974404fb1d5ed1520cc05649b4646f5777a5497d` |
| UNER English-EWT (commit `8ed072de`) | dev | `en_ewt-ud-dev.iob2` | 2,001 | 620,415 | `0cafd881cf31d39897b6d588420fab8fbbe4423d271725c59921ac9dd49c0713` |
| UNER English-EWT (commit `8ed072de`) | eval | `en_ewt-ud-test.iob2` | 2,077 | 623,751 | `821ad826d5f503ac03b4edc3fbc2174f21fbb005ec25863caf2051c434616d6e` |

Sources:

- UD: `https://raw.githubusercontent.com/UniversalDependencies/UD_English-EWT/r2.18/<file>` (release tag r2.18, README dated 2026-05-15).
- UNER: `https://raw.githubusercontent.com/UniversalNER/UNER_English-EWT/8ed072de2fd24022cc62458997fe96a8fe191ea4/<file>`. The repository README says the data are those of Universal NER v1, built on UD English-EWT at UD commit `c491d62a7a0447946ac85b11308083ea61328ece`.

Notices kept next to the data (hashes of the copies): UD `LICENSE.txt` `b3d1b0f4…ae7`, UD `README.md` `c2dc7eda…281`, UNER `LICENSE.txt` `b3d1b0f4…ae7` (the same Creative Commons text), UNER `README.md` `e5f3f454…c5f`.

## Licences

| Dataset | Licence | Where it is stated |
|---|---|---|
| UD English-EWT | CC BY-SA 4.0 | README, "License/Copyright": "The annotations and database rights of the Universal Dependencies English Web Treebank are licensed under a Creative Commons Attribution-ShareAlike 4.0 International License." Machine-readable metadata: `License: CC BY-SA 4.0`. "The underlying texts come from various sources collected for the LDC English Web Treebank. Some parts are in the public domain. Portions may be © 2012 Google Inc., © 2011 Yahoo! Inc., © 2012 Trustees of the University of Pennsylvania and/or © other original authors." |
| UNER English-EWT | CC BY-SA 4.0 | The repository's `LICENSE.txt` is the "Attribution-ShareAlike 4.0 International" legal text. Mayhew et al. (NAACL 2024), appendix C: UD datasets "are licensed under Creative Commons Attribution-ShareAlike (CC BY-SA)… Thus, we distribute all of our datasets under the same license." |

Both are share-alike sources: a model trained on them is marked `SA lineage: yes` in its provenance log (`docs/provenance.md`) and credits Universal Dependencies and Universal NER.

## Splits

The official train/dev/test splits of both releases are used as they come. The test side is never read by training, tuning or model selection. Hashes above were written before any model saw the training side.

## Overlap between test and training text

Text-level check on the `# text =` line, run by `fetch.sh`:

| Test file | Against | Test sentences | Identical text in train or dev | After case- and whitespace-folding |
|---|---|---|---|---|
| UD test | UD train + dev | 2,077 | 114 | 127 |
| UNER test | UNER train + dev | 2,077 | 114 | 127 |
| UNER test | UD train + dev | 2,077 | 114 | 127 |
| UD test | UNER train + dev | 2,077 | 114 | 127 |

These are almost all one- and two-word fragments that the treebank repeats across its official splits ("John,", "FYI.", "Hi.", "Thank you.", "...", and similar; all but one have six words or fewer); they were left in, because the official test set is the reference every published number uses. Any result can be recomputed without them from the manifest.

UNER versus UD r2.18: UNER was cut from an older UD commit, so its sentence texts are not identical to r2.18's. Per split, the number of UNER sentence texts not present in the matching UD r2.18 split is 2,158 of 12,543 (train), 303 of 2,001 (dev) and 305 of 2,077 (test), and sentence order differs. Treat the two as separate datasets: UD scores come from the UD files, NER scores from the UNER files, and no model is trained on one and scored on the other's text.

## Other UD English treebanks

Licence read from each treebank's README metadata (`License:` line), 2026-11-04. Only the first group may be used, and none of them is used for the English baselines.

| Treebank | Licence | Use |
|---|---|---|
| Atis, ESLSpok, Pronouns, CTeTex | CC BY-SA 4.0 | allowed for training and evaluation, with credit; not used so far |
| PUD | CC BY-SA 3.0 | test-only treebank: evaluation only |
| GUMReddit | CC BY 4.0, but the text is reconstructed from Reddit | held: not used (platform terms apply to the text) |
| GUM, GENTLE, ParTUT | CC BY-NC-SA 4.0 | out: non-commercial; never fetched |
| LinES | CC BY-NC-SA 4.0 (UD licence table; its README was not fetched) | out: non-commercial; never fetched |

Reasons not to add the allowed ones now: they are far smaller than EWT and differ in domain (travel queries, spoken learner English, pronoun sentences, technical text), so mixing them would change what the spaCy baseline is compared against; they can be added to a later recipe with the reason recorded.
