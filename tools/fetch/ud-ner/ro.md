## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ronec-2.0 | `ronec-2.0-train.iob2` | 9,000 | 405,649 | MIT (dataset card) | https://huggingface.co/datasets/community-datasets/ronec/resolve/2a0856f6979067c4ef7ea6a462ff9daabd055826/ronec/train-00000-of-00001.parquet (converted) | `5464996de1332d3e6c71da1ae96d88c272a8bdf4e8edc6390c7de9493e671d94` |
| dev | ronec-2.0 | `ronec-2.0-dev.iob2` | 1,330 | 59,045 | MIT (dataset card) | https://huggingface.co/datasets/community-datasets/ronec/resolve/2a0856f6979067c4ef7ea6a462ff9daabd055826/ronec/validation-00000-of-00001.parquet (converted) | `3459e9c6570dfd85373748b34b49165aeb8f9dee4da982344e4c586646c51415` |
| eval | ronec-2.0 | `ronec-2.0-test.iob2` | 2,000 | 88,469 | MIT (dataset card) | https://huggingface.co/datasets/community-datasets/ronec/resolve/2a0856f6979067c4ef7ea6a462ff9daabd055826/ronec/test-00000-of-00001.parquet (converted) | `a51c997cce653bcffc0f1595ce661ae42d8a989eb4749f6a6e3306646faf6a00` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| ronec-2.0 | `train` | `ab7c6430dd3432da23b40fdbc421d2847c4e9d4e6111fa0d3ed6c2a085c3764e` |
| ronec-2.0 | `validation` | `76bcf692b9396a48d57153a594bafbdb7cea4756680926d9a37cbd680777a0d9` |
| ronec-2.0 | `test` | `e4bcf1108efc2fbdadabd995cb767c4a23fc6cbff9d77c65a3d08762ffd9d332` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ner/ro/ronec-2.0/ronec-2.0-test.iob2` | ronec-2.0 | 2,000 | 1 | 1 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/ro/ronec-2.0/README.md` | `1f189c334fb2ae46514fbc9ee0acb4c6d01f31cc73053b00c738548f3a7e36ca` |
