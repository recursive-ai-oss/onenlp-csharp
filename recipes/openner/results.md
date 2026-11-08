# open-ner results

Span F1 at threshold 0.5, frozen `student-v1`. Asked types are the file's own types. A: human labels only. B: human + Qwen labels (shipped).

| Set | Types | A | B |
|---|---|---|---|
| UNER English-EWT test (fixed PER/ORG/LOC) | 3 | 67.74 [64.62, 70.86] | 67.14 [64.34, 69.97] |
| Few-NERD test (seen types) | 67 | 50.51 [50.25, 50.75] (P 72.3, R 38.8) | 49.89 [49.63, 50.15] (P 72.2, R 38.1) |
| CrossNER politics (zero-shot) | | 43.63 [41.00, 46.52] | 41.27 [38.58, 44.09] |
| CrossNER science (zero-shot) | | 32.77 [29.15, 35.95] | 36.50 [33.09, 40.06] |
| CrossNER music (zero-shot) | | 16.72 [14.29, 19.18] | 26.01 [23.67, 28.35] |
| CrossNER literature (zero-shot) | | 19.92 [17.66, 22.33] | 30.38 [27.88, 33.05] |
| CrossNER AI (zero-shot) | | 16.74 [13.64, 20.05] | 21.10 [17.79, 24.52] |

References. UNER fixed types: spaCy 3.8.18 trained on the same data, 62.32 [59.45, 65.20]; our dedicated NER heads 61.52 (fast tier) and 75.48 (fine-tuned student). B is above the spaCy point estimate (the intervals overlap; no paired comparison here); the dedicated heads are the better choice when the types are fixed. Few-NERD and CrossNER: see the [published-small-model comparison](../../docs/open-ner.md#published-comparison). GLiNER-S is 50M parameters versus our approximately 36.1M including the head; all five CrossNER scores are numerically behind it, not only behind the larger models. Protocol equivalence is unverified, so these are not passing quality comparisons. The Qwen labels add 4 to 10 points on four of five CrossNER sets, and cost nothing outside the CI on the others; politics is 2.4 points lower, inside the interval.

A CrossNER mean of the five: A 26.0, B 31.1.

Packed-model verification: all seven B scores and CIs reproduced to the precision
shown above using `MultilingualModel.LoadOpenEntities()`, model packages
0.3.0-preview.1 and core/Neural 0.4.0-preview.1. Embedded head, frozen student-v1
and tokenizer hashes match the current manifests. This does not verify a newly
packed multilingual 0.4.0-preview.1. The scorer, filtering and bootstrap are linked
unchanged by `tools/OneNlp.OpenNerGate`; see its README for commands. The Few-NERD
row includes overlapping coarse and fine gold labels, not a fine-only benchmark.

The packed run checked the type inventories: UNER 3; Few-NERD **67 derived
labels**, correcting the earlier 66-type heading (66 is the source fine-label
count); CrossNER politics 9, science 17, music 13, literature 12, AI 14. No UNER
or CrossNER sentences were filtered out; Few-NERD retained 37,637 of 37,648.
This heading correction changes neither predictions nor the metric.

Environment and package fingerprints: [measurement notes](../../docs/open-ner.md#measurement-environment-and-package-fingerprints).
