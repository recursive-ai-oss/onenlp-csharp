## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | uner-sl-ssj | `sl_ssj-ud-train.iob2` | 10,903 | 215,155 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Slovenian_SSJ/f6c6f40e297749ecb7ce7143e9783083e545aadf/sl_ssj-ud-train.iob2 | `329e92b57bf3c78aa4eda4e932fea767ecf35124c28cc3ef257df8e582e8f1fc` |
| dev | uner-sl-ssj | `sl_ssj-ud-dev.iob2` | 1,250 | 26,500 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Slovenian_SSJ/f6c6f40e297749ecb7ce7143e9783083e545aadf/sl_ssj-ud-dev.iob2 | `23277de6fda46e9b4b29552a2f63c2cb3223680def284659162d3ce4c9ff179f` |
| eval | uner-sl-ssj | `sl_ssj-ud-test.iob2` | 1,282 | 25,442 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Slovenian_SSJ/f6c6f40e297749ecb7ce7143e9783083e545aadf/sl_ssj-ud-test.iob2 | `df3190f78b1f148776329f66e49cac78e97a66c0c3c9abc8ea0f799749047527` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/uner/sl/sl_ssj-ud-test.iob2` | uner-sl-ssj | 1,282 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/sl/uner-sl-ssj/LICENSE.txt` | `fcb427c5b741be52981022ce8d48a6498eae0d8c428aa82ef55ccc3e0f4c9c44` |
| `notices/sl/uner-sl-ssj/README.md` | `041e1358fa784e69743b20a311643b10f560df60a0885378a910193785be1274` |
