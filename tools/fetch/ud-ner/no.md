## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | uner-nob-norne | `nob_norne-ud-train.iob2` | 15,696 | 243,886 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Norwegian-NDT/f1da13196788be028fe8124cdc7b9b197399ad98/nob_norne-ud-train.iob2 | `88fbcfd346822c42a011f3b2a24d9c0448d241bf68a80997ae0737db9ac178bf` |
| dev | uner-nob-norne | `nob_norne-ud-dev.iob2` | 2,409 | 36,369 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Norwegian-NDT/f1da13196788be028fe8124cdc7b9b197399ad98/nob_norne-ud-dev.iob2 | `a93228929ba48e3e16c77000863f2afa3a7d4171d24686d4d8460f3658f512dd` |
| eval | uner-nob-norne | `nob_norne-ud-test.iob2` | 1,939 | 29,966 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Norwegian-NDT/f1da13196788be028fe8124cdc7b9b197399ad98/nob_norne-ud-test.iob2 | `71a9379911a37da56f42ee6b0639d881b509f20658be22db4dd95254e5f6d11f` |
| train | uner-nno-norne | `nno_norne-ud-train.iob2` | 14,174 | 245,330 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Norwegian-NDT/f1da13196788be028fe8124cdc7b9b197399ad98/nno_norne-ud-train.iob2 | `0da88bcfb489e02182d623d23fa6d527fef2534238fd8bd50923567fca935df2` |
| dev | uner-nno-norne | `nno_norne-ud-dev.iob2` | 1,890 | 31,250 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Norwegian-NDT/f1da13196788be028fe8124cdc7b9b197399ad98/nno_norne-ud-dev.iob2 | `eade67afc9960b07531bbbfe67adae5b5a5b17bdc972a2e44ca1cd61fb7bf6c6` |
| eval | uner-nno-norne | `nno_norne-ud-test.iob2` | 1,511 | 24,773 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Norwegian-NDT/f1da13196788be028fe8124cdc7b9b197399ad98/nno_norne-ud-test.iob2 | `de2aa0c72fe9e5a61c7bd03bd71e0010f04eeaaaf19cc39b546e2dd4ebda18ee` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/uner/no/nob_norne-ud-test.iob2` | uner-nno-norne, uner-nob-norne | 1,939 | 15 | 15 |
| `eval/uner/no/nno_norne-ud-test.iob2` | uner-nno-norne, uner-nob-norne | 1,511 | 21 | 21 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/no/uner-nob-norne/LICENSE.txt` | `df653272772ee86b0098bfc640b0ab583f6ecf59bb6254b82feaba3aed3ed93c` |
| `notices/no/uner-nob-norne/README.md` | `79fbcf17ca35f062dfc3b72583e6aa46b05518f8357a3bce957f5d789e33bca0` |
| `notices/no/uner-nno-norne/LICENSE.txt` | `df653272772ee86b0098bfc640b0ab583f6ecf59bb6254b82feaba3aed3ed93c` |
| `notices/no/uner-nno-norne/README.md` | `79fbcf17ca35f062dfc3b72583e6aa46b05518f8357a3bce957f5d789e33bca0` |
