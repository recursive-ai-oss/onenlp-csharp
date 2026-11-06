UD English-EWT r2.18 test, raw text, assembled English model; reference as in test-rows-gold.md.

| Metric | Score | 95% CI | spaCy | Within noise |
|---|---:|---|---|---|
| UPOS | 92.90 | [92.42, 93.35] | 92.45 [91.84, 93.06] | yes |
| XPOS | 91.91 | [91.38, 92.38] | 91.72 [91.09, 92.34] | yes |
| UFeats | 93.87 | [93.48, 94.26] | 93.37 [92.81, 93.92] | yes |
| AllTags | 90.04 | [89.49, 90.57] | 89.99 [89.32, 90.62] | yes |
| Lemmas | 95.03 | [94.61, 95.43] | 93.69 [93.08, 94.29] | no (ahead) |
| UAS | 81.40 | [80.41, 82.30] | 81.34 [80.25, 82.37] | yes |
| LAS | 77.76 | [76.82, 78.70] | 78.53 [77.46, 79.55] | yes |
| CLAS | 72.63 | [71.53, 73.75] | 74.34 [73.14, 75.47] | yes |
| MLAS | 65.61 | [64.46, 66.72] | 69.05 [67.81, 70.21] | no (behind; not a gated row) |
| Sentences | 72.63 | [70.67, 74.61] | 72.66 [70.48, 74.75] | yes |
