## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | uner-sk-snk | `sk_snk-ud-train.iob2` | 8,483 | 80,628 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Slovak-SNK/886e7c6d7e066e23246af5070d115a036a465c6d/sk_snk-ud-train.iob2 | `2c1aaf803cb940212b56591b450edef4518038d336f52496ecbc0924f7d7d77f` |
| dev | uner-sk-snk | `sk_snk-ud-dev.iob2` | 1,060 | 12,733 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Slovak-SNK/886e7c6d7e066e23246af5070d115a036a465c6d/sk_snk-ud-dev.iob2 | `2391d6efb9bcaddc712470c7edc9d4665deffd4b8cc2669da34b91d15da01678` |
| eval | uner-sk-snk | `sk_snk-ud-test.iob2` | 1,061 | 12,736 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalNER/UNER_Slovak-SNK/886e7c6d7e066e23246af5070d115a036a465c6d/sk_snk-ud-test.iob2 | `b2f48b43e60bf5a18f8075805592273cb91a1cd3f37dd11d6f11e81d10de3c04` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/uner/sk/sk_snk-ud-test.iob2` | uner-sk-snk | 1,061 | 12 | 12 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/sk/uner-sk-snk/LICENSE.txt` | `76cbab13f5ddb068524dafb0600017c796311cd459def36d3d9d8cd0b8fb49be` |
| `notices/sk/uner-sk-snk/README.md` | `900d7e4e9b466c90bcc235d50d24977f0e5b371cde0d2d04d5c62687220d3b0b` |
