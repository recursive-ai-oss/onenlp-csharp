## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | uner-id-gsd | `id_gsd-ud-train.iob2` | 4,482 | 97,602 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Indonesian-GSD/a8a8f52b43c4bf0e20bb67537290a141e618e847/id_gsd-ud-train.iob2 | `89fe691e6a38e0aaacd5ae64cb3dba5274614bac4549352690c1484ca5a9e72e` |
| dev | uner-id-gsd | `id_gsd-ud-dev.iob2` | 559 | 12,661 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Indonesian-GSD/a8a8f52b43c4bf0e20bb67537290a141e618e847/id_gsd-ud-dev.iob2 | `bc4fe9e932a417979d0393d799b5428bd12156ede4693270b68b58af5cddc968` |
| eval | uner-id-gsd | `id_gsd-ud-test.iob2` | 557 | 11,756 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Indonesian-GSD/a8a8f52b43c4bf0e20bb67537290a141e618e847/id_gsd-ud-test.iob2 | `4c17f30609c0f56c02c8ec0140505eb9f18a4bd78c0dcc4e853e82fa6e578d20` |
| train | uner-id-csui | `id_csui-ud-train.iob2` | 656 | 18,222 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Indonesian-CSUI/93847a9fa384c28605ebdae5df6af32a6043badf/id_csui-ud-train.iob2 | `06bae5bb7377f764af56af84c6920f0575e11a87e8ff1465a22db4d9acd1cb0f` |
| eval | uner-id-csui | `id_csui-ud-test.iob2` | 374 | 10,041 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Indonesian-CSUI/93847a9fa384c28605ebdae5df6af32a6043badf/id_csui-ud-test.iob2 | `ea9a13c4905fec7b6d836d64abc467d9af3660bd7461384b51a0856095bee193` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/uner/id/id_gsd-ud-test.iob2` | uner-id-csui, uner-id-gsd | 557 | 4 | 4 |
| `eval/uner/id/id_csui-ud-test.iob2` | uner-id-csui, uner-id-gsd | 374 | 1 | 1 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/id/uner-id-csui/README.md` | `1dde09bae106dd54ec476aa4b700b2ce1f1e83cdac31d0b71915a8100da7dd28` |
