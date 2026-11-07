# open-ner results

Span F1 at threshold 0.5, frozen `student-v1`. Asked types are the file's own types. A: human labels only. B: human + Qwen labels (shipped).

| Set | Types | A | B |
|---|---|---|---|
| UNER English-EWT test (fixed PER/ORG/LOC) | 3 | 67.74 [64.62, 70.86] | 67.14 [64.34, 69.97] |
| Few-NERD test (seen types) | 66 | 50.51 [50.25, 50.75] (P 72.3, R 38.8) | 49.89 [49.63, 50.15] (P 72.2, R 38.1) |
| CrossNER politics (zero-shot) | | 43.63 [41.00, 46.52] | 41.27 [38.58, 44.09] |
| CrossNER science (zero-shot) | | 32.77 [29.15, 35.95] | 36.50 [33.09, 40.06] |
| CrossNER music (zero-shot) | | 16.72 [14.29, 19.18] | 26.01 [23.67, 28.35] |
| CrossNER literature (zero-shot) | | 19.92 [17.66, 22.33] | 30.38 [27.88, 33.05] |
| CrossNER AI (zero-shot) | | 16.74 [13.64, 20.05] | 21.10 [17.79, 24.52] |

References. UNER fixed types: spaCy 3.8.18 trained on the same data, 62.32 [59.45, 65.20]; our dedicated NER heads 61.52 (fast tier) and 75.48 (fine-tuned student). B is above spaCy and inside its interval; the dedicated heads are the better choice when the types are fixed. Few-NERD and CrossNER: the GLiNER paper's tables are the published comparison (it uses a much larger, separately trained encoder); our zero-shot numbers are lower and are information rows. The Qwen labels add 4 to 10 points on four of five CrossNER sets, and cost nothing outside the CI on the others; politics is 2.4 points lower, inside the interval.

A CrossNER mean of the five: A 25.9, B 31.1.
