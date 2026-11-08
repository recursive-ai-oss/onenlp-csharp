# Base-size accurate tier: per-row results

**The gate** is the [paired comparison with spaCy 3.8.18 trained on the same data](#paired-spacy-3818-comparison-gate): 144 rows, 117 ahead, 14 within the paired interval, **13 behind** (listed there and in the README's known gaps).

**Information:** published research systems (Stanza, UDPipe 2, Trankit) train a separate, larger model per treebank and are not a gate for this package. Three views of the same rows (no averaging over languages or metrics):
- **Best published system per row: 1314 comparisons, 1205 misses**, 109 within the interval or ahead.
- **Stanza 1.5.1 pretrained/charlm, no transformer: 650 comparisons, 534 misses**, 116 within the interval or ahead.
- **All published system comparisons: 2678 comparisons, 2346 misses**, 332 within the interval or ahead.

Intervals: 95% bootstrap, 1,000 resamples, seed 20261106. The published reference passes when it is no higher than our upper bound; these are not paired intervals against the reference. Gold and raw input are never crossed. A missing reference is not a pass. The reference UD version and test-split change flag accompany every comparison; changed splits are information, not a claim of equal evaluation data. Entirely absent gold columns are not scored. Before is the previous nine-language model, where available.

| Language / treebank | Input | Metric | Before [95% CI] | Current [95% CI] | Best published view | Stanza-only view |
|---|---|---|---|---|---|---|
| af: UD_Afrikaans-AfriBooms | gold | AllTags | not measured | 84.60 [83.80, 85.35] | 95.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | gold | CLAS | not measured | 71.68 [70.24, 73.17] | not published | not published |
| af: UD_Afrikaans-AfriBooms | gold | LAS | not measured | 77.91 [76.68, 79.12] | 87.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | gold | Lemmas | not measured | 96.86 [96.44, 97.24] | 98.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | gold | MLAS | not measured | 61.10 [59.33, 62.75] | 79.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | gold | UAS | not measured | 83.28 [82.11, 84.33] | 90.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | gold | UFeats | not measured | 94.08 [93.53, 94.58] | 98.32 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | gold | UPOS | not measured | 95.81 [95.37, 96.24] | 98.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | gold | XPOS | not measured | 86.44 [85.68, 87.14] | 95.94 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| af: UD_Afrikaans-AfriBooms | raw | AllTags | not measured | 82.91 [82.17, 83.62] | 96.54 (Trankit large, UD 2.5; test changed) — **MISS** | 95.66 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | CLAS | not measured | 75.53 [73.96, 77.02] | 85.30 (Trankit large, UD 2.5; test changed) — **MISS** | 80.10 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | LAS | not measured | 79.24 [77.98, 80.45] | 89.35 (Trankit large, UD 2.5; test changed) — **MISS** | 86.14 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | Lemmas | not measured | 93.98 [93.46, 94.48] | 98.29 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 98.29 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | MLAS | not measured | 65.29 [63.55, 66.93] | 82.64 (Trankit large, UD 2.5; test changed) — **MISS** | 76.71 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | Sentences | not measured | 95.74 [93.63, 97.51] | 100.00 (Trankit large, UD 2.5; test changed) — **MISS** | 99.65 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | Tokens | not measured | 96.77 [96.31, 97.18] | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | UAS | not measured | 83.39 [82.21, 84.57] | 91.61 (Trankit large, UD 2.5; test changed) — **MISS** | 89.54 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | UFeats | not measured | 92.19 [91.62, 92.77] | 98.68 (Trankit large, UD 2.5; test changed) — **MISS** | 98.41 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | UPOS | not measured | 93.68 [93.13, 94.18] | 98.87 (Trankit large, UD 2.5; test changed) — **MISS** | 98.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | Words | not measured | 96.77 [96.31, 97.18] | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| af: UD_Afrikaans-AfriBooms | raw | XPOS | not measured | 84.46 [83.73, 85.13] | 96.63 (Trankit large, UD 2.5; test changed) — **MISS** | 95.77 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| as: UD_Assamese-AiW | gold | CLAS | not measured | 54.29 [43.90, 64.79] | not published | not published |
| as: UD_Assamese-AiW | gold | LAS | not measured | 58.82 [47.62, 69.88] | not published | not published |
| as: UD_Assamese-AiW | gold | Lemmas | not measured | 67.06 [54.55, 79.52] | not published | not published |
| as: UD_Assamese-AiW | gold | MLAS | not measured | 24.29 [17.65, 32.12] | not published | not published |
| as: UD_Assamese-AiW | gold | UAS | not measured | 76.47 [61.25, 88.10] | not published | not published |
| as: UD_Assamese-AiW | gold | UFeats | not measured | 47.06 [37.65, 58.54] | not published | not published |
| as: UD_Assamese-AiW | gold | UPOS | not measured | 75.29 [66.27, 82.14] | not published | not published |
| as: UD_Assamese-AiW | raw | CLAS | not measured | 56.74 [42.96, 71.76] | not published | not published |
| as: UD_Assamese-AiW | raw | LAS | not measured | 60.00 [47.73, 73.49] | not published | not published |
| as: UD_Assamese-AiW | raw | Lemmas | not measured | 65.88 [54.55, 76.67] | not published | not published |
| as: UD_Assamese-AiW | raw | MLAS | not measured | 21.28 [10.61, 33.59] | not published | not published |
| as: UD_Assamese-AiW | raw | Sentences | not measured | 100.00 [100.00, 100.00] | not published | not published |
| as: UD_Assamese-AiW | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| as: UD_Assamese-AiW | raw | UAS | not measured | 78.82 [68.60, 87.80] | not published | not published |
| as: UD_Assamese-AiW | raw | UFeats | not measured | 45.88 [39.77, 54.02] | not published | not published |
| as: UD_Assamese-AiW | raw | UPOS | not measured | 64.71 [56.32, 74.03] | not published | not published |
| as: UD_Assamese-AiW | raw | Words | not measured | 100.00 [100.00, 100.00] | not published | not published |
| bor: UD_Bororo-BDT | gold | AllTags | not measured | 51.11 [50.40, 51.90] | 84.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | gold | CLAS | not measured | 42.27 [41.09, 43.51] | not published | not published |
| bor: UD_Bororo-BDT | gold | LAS | not measured | 46.49 [45.35, 47.72] | 72.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | gold | Lemmas | not measured | 83.28 [82.59, 83.87] | 91.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | gold | MLAS | not measured | 19.95 [19.11, 20.80] | 59.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | gold | UAS | not measured | 56.91 [55.72, 58.22] | 79.11 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | gold | UFeats | not measured | 66.97 [66.22, 67.69] | 89.82 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | gold | UPOS | not measured | 71.89 [71.13, 72.67] | 91.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | gold | XPOS | not measured | 71.09 [70.35, 71.87] | 90.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | AllTags | not measured | 51.81 [51.09, 52.61] | 84.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | CLAS | not measured | 47.37 [46.01, 48.70] | not published | not published |
| bor: UD_Bororo-BDT | raw | LAS | not measured | 51.41 [50.20, 52.63] | 72.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | Lemmas | not measured | 84.21 [83.57, 84.80] | 91.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | MLAS | not measured | 23.75 [22.80, 24.70] | 59.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | Sentences | not measured | 79.78 [78.05, 81.40] | 97.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | Tokens | not measured | 99.79 [99.69, 99.88] | not published | not published |
| bor: UD_Bororo-BDT | raw | UAS | not measured | 61.94 [60.73, 63.13] | 78.80 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | UFeats | not measured | 66.81 [66.07, 67.53] | 89.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | UPOS | not measured | 73.84 [73.10, 74.60] | 91.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | Words | not measured | 99.79 [99.69, 99.88] | 99.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bor: UD_Bororo-BDT | raw | XPOS | not measured | 72.25 [71.52, 73.02] | 90.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| bxr: UD_Buryat-BDT | gold | AllTags | not measured | 8.16 [7.52, 8.78] | not published | not published |
| bxr: UD_Buryat-BDT | gold | CLAS | not measured | 20.67 [19.71, 21.64] | not published | not published |
| bxr: UD_Buryat-BDT | gold | LAS | not measured | 25.01 [23.98, 25.97] | not published | not published |
| bxr: UD_Buryat-BDT | gold | Lemmas | not measured | 57.00 [56.07, 58.05] | not published | not published |
| bxr: UD_Buryat-BDT | gold | MLAS | not measured | 1.15 [0.92, 1.39] | not published | not published |
| bxr: UD_Buryat-BDT | gold | UAS | not measured | 47.48 [46.02, 48.87] | not published | not published |
| bxr: UD_Buryat-BDT | gold | UFeats | not measured | 39.30 [38.32, 40.29] | not published | not published |
| bxr: UD_Buryat-BDT | gold | UPOS | not measured | 20.69 [19.98, 21.43] | not published | not published |
| bxr: UD_Buryat-BDT | gold | XPOS | not measured | 99.99 [99.97, 100.00] | not published | not published |
| bxr: UD_Buryat-BDT | raw | AllTags | not measured | 8.25 [7.61, 8.89] | 67.97 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 67.97 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | CLAS | not measured | 22.31 [21.34, 23.32] | 31.58 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 31.58 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | LAS | not measured | 26.11 [25.14, 27.17] | 40.52 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 40.52 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | Lemmas | not measured | 55.90 [54.95, 56.93] | 77.78 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 77.78 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | MLAS | not measured | 1.39 [1.11, 1.67] | 24.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 24.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | Sentences | not measured | 18.61 [16.41, 20.73] | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | Tokens | not measured | 99.54 [99.31, 99.74] | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | UAS | not measured | 48.91 [47.69, 50.08] | 56.21 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 56.21 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | UFeats | not measured | 37.35 [36.44, 38.25] | 74.51 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 74.51 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | UPOS | not measured | 23.55 [22.73, 24.34] | 75.16 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 75.16 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | Words | not measured | 99.54 [99.31, 99.74] | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| bxr: UD_Buryat-BDT | raw | XPOS | not measured | 99.53 [99.28, 99.73] | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | gold | AllTags | not measured | 75.95 [75.36, 76.56] | 96.98 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | gold | CLAS | not measured | 78.17 [77.49, 78.84] | not published | not published |
| ca: UD_Catalan-AnCora | gold | LAS | not measured | 83.64 [83.10, 84.16] | 93.46 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | gold | Lemmas | not measured | 98.06 [97.92, 98.19] | 99.49 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | gold | MLAS | not measured | 64.60 [63.86, 65.35] | 88.08 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | gold | UAS | not measured | 87.28 [86.79, 87.77] | 94.98 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | gold | UFeats | not measured | 93.07 [92.81, 93.32] | 98.75 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | gold | UPOS | not measured | 95.95 [95.74, 96.14] | 99.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | gold | XPOS | not measured | 77.80 [77.23, 78.38] | 97.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ca: UD_Catalan-AnCora | raw | AllTags | not measured | 69.68 [69.10, 70.32] | 98.24 (Trankit large, UD 2.5; test changed) — **MISS** | 95.67 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | CLAS | not measured | 71.60 [70.83, 72.41] | 91.41 (Trankit large, UD 2.5; test changed) — **MISS** | 87.24 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | LAS | not measured | 73.13 [72.51, 73.80] | 93.83 (Trankit large, UD 2.5; test changed) — **MISS** | 91.66 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | Lemmas | not measured | 87.71 [87.37, 88.04] | 99.43 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.06 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | MLAS | not measured | 56.53 [55.72, 57.32] | 89.31 (Trankit large, UD 2.5; test changed) — **MISS** | 84.72 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | Sentences | not measured | 89.47 [88.25, 90.72] | 100.00 (Trankit base, UD 2.5; test changed) — **MISS** | 99.57 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | Tokens | not measured | 93.67 [93.42, 93.91] | 99.97 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.97 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | UAS | not measured | 75.62 [75.01, 76.27] | 95.15 (Trankit large, UD 2.5; test changed) — **MISS** | 93.55 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | UFeats | not measured | 83.14 [82.75, 83.53] | 98.67 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 98.27 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | UPOS | not measured | 86.15 [85.78, 86.50] | 99.11 (Trankit large, UD 2.5; test changed) — **MISS** | 98.70 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | Words | not measured | 89.32 [88.99, 89.64] | 99.95 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.95 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ca: UD_Catalan-AnCora | raw | XPOS | not measured | 71.10 [70.53, 71.72] | 99.07 (Trankit large, UD 2.5; test changed) — **MISS** | 95.93 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ckb: UD_Central_Kurdish-Mukri | gold | CLAS | not measured | 23.08 [14.91, 31.43] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | gold | LAS | not measured | 34.43 [26.12, 42.96] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | gold | Lemmas | not measured | 64.75 [57.98, 70.87] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | gold | MLAS | not measured | 8.97 [2.72, 15.69] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | gold | UAS | not measured | 47.54 [35.07, 59.84] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | gold | UFeats | not measured | 54.92 [46.40, 62.77] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | gold | UPOS | not measured | 54.10 [47.46, 60.17] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | CLAS | not measured | 19.05 [9.30, 29.46] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | LAS | not measured | 27.83 [18.10, 37.44] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | Lemmas | not measured | 55.65 [49.54, 61.80] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | MLAS | not measured | 5.44 [0.00, 11.76] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | Sentences | not measured | 93.33 [82.61, 100.00] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | UAS | not measured | 33.91 [21.70, 46.28] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | UFeats | not measured | 49.57 [42.62, 56.56] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | UPOS | not measured | 51.30 [43.88, 58.72] | not published | not published |
| ckb: UD_Central_Kurdish-Mukri | raw | Words | not measured | 81.74 [75.00, 88.11] | not published | not published |
| cop: UD_Coptic-Scriptorium | gold | AllTags | not measured | 62.73 [61.51, 63.94] | 96.10 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | gold | CLAS | not measured | 38.27 [36.78, 40.01] | not published | not published |
| cop: UD_Coptic-Scriptorium | gold | LAS | not measured | 51.38 [50.04, 52.70] | 88.27 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | gold | Lemmas | not measured | 88.04 [87.22, 88.88] | 97.67 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | gold | MLAS | not measured | 20.89 [19.66, 22.19] | 78.68 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | gold | UAS | not measured | 62.23 [60.95, 63.61] | 91.07 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | gold | UFeats | not measured | 72.24 [71.18, 73.33] | 97.94 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | gold | UPOS | not measured | 76.21 [75.18, 77.17] | 97.42 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | gold | XPOS | not measured | 73.58 [72.49, 74.68] | 97.16 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| cop: UD_Coptic-Scriptorium | raw | AllTags | not measured | 15.13 [14.46, 15.83] | 84.77 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 84.77 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | CLAS | not measured | 0.61 [0.37, 0.89] | 56.39 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 56.39 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | LAS | not measured | 0.80 [0.55, 1.10] | 64.43 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 64.43 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | Lemmas | not measured | 22.46 [21.49, 23.51] | 85.95 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 85.95 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | MLAS | not measured | 0.24 [0.11, 0.40] | 53.08 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 53.08 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | Sentences | not measured | 5.15 [2.78, 8.19] | 34.09 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 34.09 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | Tokens | not measured | 99.49 [98.85, 99.94] | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | UAS | not measured | 0.97 [0.68, 1.32] | 66.37 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 66.37 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | UFeats | not measured | 16.99 [16.30, 17.69] | 86.37 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 86.37 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | UPOS | not measured | 16.78 [16.00, 17.54] | 85.77 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 85.77 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | Words | not measured | 23.13 [22.10, 24.19] | 88.63 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 88.63 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cop: UD_Coptic-Scriptorium | raw | XPOS | not measured | 16.82 [16.08, 17.59] | 85.69 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 85.69 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| cs: UD_Czech-CAC | gold | AllTags | not measured | 68.03 [67.08, 68.96] | 97.42 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | gold | CLAS | not measured | 80.85 [79.61, 82.10] | not published | not published |
| cs: UD_Czech-CAC | gold | LAS | not measured | 83.93 [82.92, 84.99] | 94.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | gold | Lemmas | not measured | 94.33 [93.83, 94.84] | 99.28 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | gold | MLAS | not measured | 48.87 [47.38, 50.51] | 90.46 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | gold | UAS | not measured | 87.40 [86.41, 88.27] | 96.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | gold | UFeats | not measured | 73.92 [73.03, 74.80] | 97.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | gold | UPOS | not measured | 95.08 [94.62, 95.50] | 99.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | gold | XPOS | not measured | 76.79 [75.91, 77.66] | 98.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cs: UD_Czech-CAC | raw | AllTags | not measured | 70.84 [70.01, 71.68] | 97.65 (Trankit large, UD 2.5; test same size) — **MISS** | 92.92 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cs: UD_Czech-CAC | raw | CLAS | not measured | 86.79 [85.63, 87.85] | 93.06 (Trankit large, UD 2.5; test same size) — **MISS** | 86.78 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| cs: UD_Czech-CAC | raw | LAS | not measured | 88.56 [87.59, 89.47] | 94.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 88.71 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| cs: UD_Czech-CAC | raw | Lemmas | not measured | 95.06 [94.57, 95.50] | 99.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.46 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cs: UD_Czech-CAC | raw | MLAS | not measured | 55.33 [53.88, 56.80] | 90.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.27 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cs: UD_Czech-CAC | raw | Sentences | not measured | 97.60 [96.39, 98.72] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cs: UD_Czech-CAC | raw | Tokens | not measured | 99.92 [99.82, 99.98] | 99.99 (Trankit large, UD 2.5; test same size) — **MISS** | 99.82 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| cs: UD_Czech-CAC | raw | UAS | not measured | 91.21 [90.28, 92.08] | 96.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 91.09 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| cs: UD_Czech-CAC | raw | UFeats | not measured | 75.72 [74.89, 76.56] | 98.01 (Trankit large, UD 2.5; test same size) — **MISS** | 93.64 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cs: UD_Czech-CAC | raw | UPOS | not measured | 95.77 [95.32, 96.18] | 99.64 (Trankit large, UD 2.5; test same size) — **MISS** | 98.52 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cs: UD_Czech-CAC | raw | Words | not measured | 99.67 [99.53, 99.78] | 99.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.57 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| cs: UD_Czech-CAC | raw | XPOS | not measured | 79.17 [78.37, 79.96] | 98.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.59 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | gold | AllTags | not measured | 74.20 [73.55, 74.85] | 88.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | gold | CLAS | not measured | 70.34 [69.27, 71.39] | not published | not published |
| cy: UD_Welsh-CCG | gold | LAS | not measured | 75.92 [75.04, 76.75] | 82.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | gold | Lemmas | not measured | 91.15 [90.67, 91.63] | 95.32 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | gold | MLAS | not measured | 44.09 [42.90, 45.16] | 65.14 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | gold | UAS | not measured | 82.71 [81.92, 83.51] | 88.45 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | gold | UFeats | not measured | 79.27 [78.69, 79.90] | 90.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | gold | UPOS | not measured | 91.91 [91.45, 92.36] | 95.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | gold | XPOS | not measured | 91.03 [90.54, 91.45] | 94.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| cy: UD_Welsh-CCG | raw | AllTags | not measured | 67.37 [66.61, 68.13] | 87.83 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 87.83 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | CLAS | not measured | 62.93 [61.67, 64.17] | 74.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 74.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | LAS | not measured | 64.89 [63.74, 65.95] | 81.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.25 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | Lemmas | not measured | 81.89 [81.22, 82.52] | 94.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | MLAS | not measured | 37.17 [36.08, 38.25] | 64.32 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 62.81 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | Sentences | not measured | 81.61 [79.19, 83.90] | 97.79 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 97.79 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | Tokens | not measured | 92.22 [91.66, 92.72] | 99.90 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.90 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | UAS | not measured | 70.58 [69.41, 71.73] | 87.52 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 86.32 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | UFeats | not measured | 70.70 [69.97, 71.41] | 90.11 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 90.11 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | UPOS | not measured | 84.08 [83.38, 84.71] | 95.86 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 95.86 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | Words | not measured | 89.93 [89.38, 90.50] | 99.68 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.68 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| cy: UD_Welsh-CCG | raw | XPOS | not measured | 83.40 [82.70, 84.08] | 94.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 94.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | gold | AllTags | not measured | 1.00 [0.83, 1.19] | 96.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | gold | CLAS | not measured | 70.27 [68.75, 71.83] | not published | not published |
| da: UD_Danish-DDT | gold | LAS | not measured | 74.84 [73.49, 76.18] | 87.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | gold | Lemmas | not measured | 94.41 [93.87, 94.92] | 97.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | gold | MLAS | not measured | 55.74 [54.13, 57.42] | 80.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | gold | UAS | not measured | 78.94 [77.63, 80.25] | 89.84 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | gold | UFeats | not measured | 90.63 [89.97, 91.21] | 97.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | gold | UPOS | not measured | 94.66 [94.09, 95.18] | 98.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | gold | XPOS | not measured | 1.00 [0.83, 1.19] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| da: UD_Danish-DDT | raw | AllTags | not measured | 0.99 [0.82, 1.18] | 97.87 (Trankit large, UD 2.5; test same size) — **MISS** | 97.14 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | CLAS | not measured | 77.30 [75.79, 78.87] | 88.81 (Trankit large, UD 2.5; test same size) — **MISS** | 83.80 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | LAS | not measured | 80.60 [79.35, 81.96] | 90.33 (Trankit large, UD 2.5; test same size) — **MISS** | 86.61 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | Lemmas | not measured | 93.97 [93.43, 94.52] | 97.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 97.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | MLAS | not measured | 65.32 [63.63, 67.11] | 85.66 (Trankit large, UD 2.5; test same size) — **MISS** | 80.06 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | Sentences | not measured | 93.90 [91.83, 95.75] | 95.46 (Trankit large, UD 2.5; test same size) — within or ahead | 91.81 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| da: UD_Danish-DDT | raw | Tokens | not measured | 98.69 [98.37, 99.02] | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | UAS | not measured | 83.84 [82.62, 85.19] | 91.75 (Trankit large, UD 2.5; test same size) — **MISS** | 88.61 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | UFeats | not measured | 91.29 [90.67, 91.88] | 98.33 (Trankit large, UD 2.5; test same size) — **MISS** | 97.87 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | UPOS | not measured | 94.32 [93.75, 94.85] | 98.71 (Trankit large, UD 2.5; test same size) — **MISS** | 98.30 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | Words | not measured | 98.69 [98.37, 99.02] | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| da: UD_Danish-DDT | raw | XPOS | not measured | 0.99 [0.82, 1.18] | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| de: UD_German-GSD | gold | AllTags | 83.34 [82.57, 84.04] | 83.09 [82.34, 83.75] | 89.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | gold | CLAS | not measured | 78.67 [77.54, 79.74] | not published | not published |
| de: UD_German-GSD | gold | LAS | 83.32 [82.43, 84.17] | 83.49 [82.59, 84.31] | 86.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | gold | Lemmas | 96.25 [95.95, 96.53] | 96.64 [96.37, 96.92] | 97.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | gold | MLAS | not measured | 59.58 [58.38, 60.86] | 69.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | gold | UAS | 87.01 [86.18, 87.75] | 87.09 [86.26, 87.84] | 89.24 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | gold | UFeats | 86.81 [86.11, 87.44] | 86.58 [85.91, 87.19] | 91.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | gold | UPOS | 96.16 [95.83, 96.49] | 95.78 [95.43, 96.12] | 97.11 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | gold | XPOS | 96.07 [95.71, 96.43] | 95.84 [95.48, 96.19] | 97.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| de: UD_German-GSD | raw | AllTags | 81.04 [80.35, 81.70] | 81.69 [80.99, 82.33] | 88.82 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.19 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| de: UD_German-GSD | raw | CLAS | not measured | 81.12 [80.13, 82.07] | 81.49 (Trankit large, UD 2.5; test changed) — within or ahead | 77.08 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| de: UD_German-GSD | raw | LAS | 81.17 [80.27, 82.00] | 82.38 [81.55, 83.18] | 85.20 (Trankit large, UD 2.5; test changed) — **MISS** | 81.80 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| de: UD_German-GSD | raw | Lemmas | 93.36 [92.90, 93.78] | 94.07 [93.64, 94.48] | 97.23 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 97.23 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| de: UD_German-GSD | raw | MLAS | not measured | 60.36 [59.24, 61.44] | 67.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 62.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| de: UD_German-GSD | raw | Sentences | 88.70 [86.70, 90.74] | 88.65 [86.73, 90.58] | 92.72 (Trankit base, UD 2.5; test changed) — **MISS** | 83.84 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| de: UD_German-GSD | raw | Tokens | 97.40 [97.09, 97.69] | 97.40 [97.09, 97.69] | 99.75 (Trankit base, UD 2.5; test changed) — **MISS** | 99.62 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| de: UD_German-GSD | raw | UAS | 84.71 [83.86, 85.52] | 85.50 [84.64, 86.25] | 89.01 (Trankit large, UD 2.5; test changed) — **MISS** | 85.80 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| de: UD_German-GSD | raw | UFeats | 84.39 [83.77, 85.03] | 85.16 [84.51, 85.74] | 91.68 (Trankit large, UD 2.5; test changed) — **MISS** | 89.78 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| de: UD_German-GSD | raw | UPOS | 93.27 [92.79, 93.69] | 93.33 [92.87, 93.75] | 96.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.61 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| de: UD_German-GSD | raw | Words | 97.06 [96.70, 97.39] | 97.06 [96.70, 97.39] | 99.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.60 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| de: UD_German-GSD | raw | XPOS | 93.38 [92.91, 93.83] | 93.35 [92.91, 93.79] | 97.71 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.36 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| egy: UD_Egyptian-PC | gold | AllTags | not measured | 0.00 [0.00, 0.00] | not published | not published |
| egy: UD_Egyptian-PC | gold | CLAS | not measured | 37.45 [36.31, 38.59] | not published | not published |
| egy: UD_Egyptian-PC | gold | LAS | not measured | 42.41 [41.30, 43.51] | not published | not published |
| egy: UD_Egyptian-PC | gold | Lemmas | not measured | 84.54 [83.73, 85.28] | not published | not published |
| egy: UD_Egyptian-PC | gold | MLAS | not measured | 19.05 [18.22, 19.98] | not published | not published |
| egy: UD_Egyptian-PC | gold | UAS | not measured | 57.76 [56.65, 58.80] | not published | not published |
| egy: UD_Egyptian-PC | gold | UFeats | not measured | 52.85 [51.87, 53.87] | not published | not published |
| egy: UD_Egyptian-PC | gold | UPOS | not measured | 73.69 [72.75, 74.56] | not published | not published |
| egy: UD_Egyptian-PC | gold | XPOS | not measured | 0.00 [0.00, 0.00] | not published | not published |
| egy: UD_Egyptian-PC | raw | AllTags | not measured | 0.00 [0.00, 0.00] | not published | not published |
| egy: UD_Egyptian-PC | raw | CLAS | not measured | 9.42 [8.82, 10.07] | not published | not published |
| egy: UD_Egyptian-PC | raw | LAS | not measured | 12.52 [11.81, 13.22] | not published | not published |
| egy: UD_Egyptian-PC | raw | Lemmas | not measured | 38.59 [37.53, 39.58] | not published | not published |
| egy: UD_Egyptian-PC | raw | MLAS | not measured | 5.15 [4.72, 5.61] | not published | not published |
| egy: UD_Egyptian-PC | raw | Sentences | not measured | 3.26 [2.26, 4.34] | not published | not published |
| egy: UD_Egyptian-PC | raw | Tokens | not measured | 46.31 [45.19, 47.45] | not published | not published |
| egy: UD_Egyptian-PC | raw | UAS | not measured | 16.51 [15.72, 17.36] | not published | not published |
| egy: UD_Egyptian-PC | raw | UFeats | not measured | 25.94 [25.02, 26.83] | not published | not published |
| egy: UD_Egyptian-PC | raw | UPOS | not measured | 32.00 [31.01, 32.89] | not published | not published |
| egy: UD_Egyptian-PC | raw | Words | not measured | 45.07 [43.98, 46.14] | not published | not published |
| egy: UD_Egyptian-PC | raw | XPOS | not measured | 0.00 [0.00, 0.00] | not published | not published |
| el: UD_Greek-GUD | gold | CLAS | not measured | 81.01 [79.90, 82.11] | not published | not published |
| el: UD_Greek-GUD | gold | LAS | not measured | 82.87 [81.91, 83.75] | 90.79 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| el: UD_Greek-GUD | gold | Lemmas | not measured | 90.86 [90.34, 91.41] | 95.97 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| el: UD_Greek-GUD | gold | MLAS | not measured | 49.27 [47.87, 50.55] | 77.89 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| el: UD_Greek-GUD | gold | UAS | not measured | 89.65 [88.81, 90.43] | 94.05 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| el: UD_Greek-GUD | gold | UFeats | not measured | 78.37 [77.65, 79.14] | 94.41 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| el: UD_Greek-GUD | gold | UPOS | not measured | 92.10 [91.62, 92.62] | 98.13 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| el: UD_Greek-GUD | raw | CLAS | not measured | 83.78 [82.69, 84.84] | 80.28 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 80.28 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| el: UD_Greek-GUD | raw | LAS | not measured | 83.08 [82.11, 84.02] | 90.37 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 85.33 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | Lemmas | not measured | 87.84 [87.21, 88.52] | 95.93 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 92.63 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | MLAS | not measured | 52.10 [50.79, 53.43] | 77.62 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 68.93 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | Sentences | not measured | 92.76 [90.99, 94.36] | 98.16 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 98.16 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | Tokens | not measured | 99.51 [99.34, 99.69] | 99.89 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.89 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | UAS | not measured | 86.89 [85.95, 87.74] | 93.61 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 89.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | UFeats | not measured | 77.20 [76.41, 78.02] | 94.36 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 92.50 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | UPOS | not measured | 90.62 [90.00, 91.21] | 98.08 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 96.52 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| el: UD_Greek-GUD | raw | Words | not measured | 95.55 [95.09, 96.04] | 99.94 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 99.89 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| en: UD_English-EWT | gold | AllTags | 93.66 [93.25, 94.02] | 93.47 [93.07, 93.83] | 96.34 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | gold | CLAS | not measured | 81.48 [80.57, 82.45] | not published | not published |
| en: UD_English-EWT | gold | LAS | 85.43 [84.61, 86.19] | 85.12 [84.40, 85.87] | 92.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | gold | Lemmas | 97.09 [96.81, 97.36] | 97.26 [97.01, 97.52] | 97.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | gold | MLAS | not measured | 75.10 [74.10, 76.11] | 86.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | gold | UAS | 88.56 [87.81, 89.26] | 88.34 [87.65, 89.01] | 94.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | gold | UFeats | 96.44 [96.17, 96.69] | 96.37 [96.09, 96.62] | 98.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | gold | UPOS | 96.58 [96.26, 96.86] | 96.49 [96.19, 96.76] | 97.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | gold | XPOS | 96.02 [95.69, 96.32] | 95.73 [95.40, 96.04] | 97.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| en: UD_English-EWT | raw | AllTags | 93.14 [92.69, 93.57] | 93.39 [92.93, 93.80] | 95.51 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.64 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| en: UD_English-EWT | raw | CLAS | not measured | 83.38 [82.51, 84.30] | 87.41 (Trankit large, UD 2.5; test changed) — **MISS** | 84.11 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| en: UD_English-EWT | raw | LAS | 85.02 [84.25, 85.77] | 86.55 [85.82, 87.26] | 89.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 86.77 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| en: UD_English-EWT | raw | Lemmas | 96.14 [95.81, 96.48] | 96.39 [96.04, 96.74] | 97.25 (Trankit large, UD 2.5; test changed) — **MISS** | 96.63 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| en: UD_English-EWT | raw | MLAS | not measured | 78.46 [77.50, 79.41] | 83.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.27 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| en: UD_English-EWT | raw | Sentences | 75.28 [73.54, 77.14] | 75.85 [74.20, 77.60] | 90.49 (Trankit large, UD 2.5; test changed) — **MISS** | 89.16 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| en: UD_English-EWT | raw | Tokens | 97.13 [96.83, 97.43] | 97.13 [96.83, 97.43] | 99.17 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.17 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| en: UD_English-EWT | raw | UAS | 87.32 [86.58, 88.01] | 88.78 [88.07, 89.47] | 91.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 88.90 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| en: UD_English-EWT | raw | UFeats | 95.76 [95.42, 96.07] | 95.94 [95.61, 96.27] | 97.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.39 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| en: UD_English-EWT | raw | UPOS | 95.95 [95.59, 96.29] | 95.95 [95.59, 96.27] | 96.75 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.20 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| en: UD_English-EWT | raw | Words | 98.68 [98.45, 98.90] | 98.68 [98.45, 98.90] | 99.07 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 98.92 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| en: UD_English-EWT | raw | XPOS | 95.22 [94.83, 95.58] | 95.18 [94.78, 95.54] | 96.47 (Trankit large, UD 2.5; test changed) — **MISS** | 95.75 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | gold | AllTags | 83.67 [83.20, 84.13] | 83.21 [82.74, 83.67] | 95.64 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | gold | CLAS | not measured | 82.53 [81.95, 83.14] | not published | not published |
| es: UD_Spanish-AnCora | gold | LAS | 85.37 [84.89, 85.88] | 86.97 [86.51, 87.42] | 92.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | gold | Lemmas | 97.72 [97.58, 97.85] | 98.53 [98.42, 98.64] | 99.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | gold | MLAS | not measured | 75.87 [75.23, 76.53] | 87.03 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | gold | UAS | 88.14 [87.68, 88.63] | 89.50 [89.08, 89.94] | 94.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | gold | UFeats | 96.50 [96.33, 96.66] | 96.74 [96.58, 96.91] | 98.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | gold | UPOS | 98.10 [97.98, 98.24] | 98.00 [97.87, 98.14] | 99.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | gold | XPOS | 84.88 [84.41, 85.31] | 84.37 [83.90, 84.82] | 96.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| es: UD_Spanish-AnCora | raw | AllTags | 84.50 [84.05, 84.94] | 84.84 [84.40, 85.29] | 98.36 (Trankit large, UD 2.5; test changed) — **MISS** | 95.65 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | CLAS | not measured | 86.36 [85.78, 86.89] | 91.10 (Trankit large, UD 2.5; test changed) — **MISS** | 87.80 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | LAS | 87.42 [86.90, 87.92] | 89.70 [89.24, 90.11] | 93.29 (Trankit large, UD 2.5; test changed) — **MISS** | 91.30 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | Lemmas | 97.75 [97.61, 97.89] | 98.58 [98.46, 98.70] | 99.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.24 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | MLAS | not measured | 80.63 [79.97, 81.23] | 89.23 (Trankit large, UD 2.5; test changed) — **MISS** | 85.49 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | Sentences | 96.66 [95.79, 97.49] | 97.05 [96.24, 97.78] | 99.39 (Trankit large, UD 2.5; test changed) — **MISS** | 98.49 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | Tokens | 99.84 [99.76, 99.90] | 99.84 [99.76, 99.90] | 99.97 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.97 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | UAS | 90.08 [89.60, 90.51] | 91.93 [91.49, 92.31] | 94.77 (Trankit large, UD 2.5; test changed) — **MISS** | 93.09 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | UFeats | 96.57 [96.40, 96.73] | 96.83 [96.66, 96.99] | 98.85 (Trankit large, UD 2.5; test changed) — **MISS** | 98.54 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | UPOS | 98.09 [97.96, 98.24] | 98.20 [98.07, 98.34] | 99.06 (Trankit large, UD 2.5; test changed) — **MISS** | 98.96 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | Words | 99.83 [99.75, 99.89] | 99.83 [99.75, 99.89] | 99.96 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.96 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| es: UD_Spanish-AnCora | raw | XPOS | 85.63 [85.17, 86.09] | 85.87 [85.43, 86.33] | 99.00 (Trankit large, UD 2.5; test changed) — **MISS** | 96.10 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| fa: UD_Persian-Seraji | gold | AllTags | not measured | 1.58 [1.38, 1.79] | 97.82 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | gold | CLAS | not measured | 61.56 [60.30, 62.91] | not published | not published |
| fa: UD_Persian-Seraji | gold | LAS | not measured | 66.70 [65.63, 67.81] | 89.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | gold | Lemmas | not measured | 96.14 [95.87, 96.43] | 98.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | gold | MLAS | not measured | 29.39 [28.32, 30.42] | 85.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | gold | UAS | not measured | 73.22 [72.20, 74.32] | 92.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | gold | UFeats | not measured | 72.36 [71.68, 73.09] | 98.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | gold | UPOS | not measured | 85.15 [84.41, 85.81] | 98.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | gold | XPOS | not measured | 3.41 [3.09, 3.75] | 98.28 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fa: UD_Persian-Seraji | raw | AllTags | not measured | 4.75 [4.35, 5.14] | 97.49 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.31 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | CLAS | not measured | 65.75 [64.46, 66.96] | 86.86 (Trankit large, UD 2.5; test same size) — **MISS** | 85.43 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | LAS | not measured | 70.39 [69.29, 71.48] | 89.58 (Trankit large, UD 2.5; test same size) — **MISS** | 87.96 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | Lemmas | not measured | 95.47 [95.12, 95.82] | 98.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 98.18 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | MLAS | not measured | 32.32 [31.28, 33.28] | 84.97 (Trankit large, UD 2.5; test same size) — **MISS** | 83.76 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | Sentences | not measured | 90.42 [88.10, 92.57] | 99.25 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.25 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | Tokens | not measured | 100.00 [100.00, 100.00] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| fa: UD_Persian-Seraji | raw | UAS | not measured | 76.52 [75.54, 77.59] | 92.24 (Trankit large, UD 2.5; test same size) — **MISS** | 90.98 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | UFeats | not measured | 72.04 [71.38, 72.80] | 97.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.70 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | UPOS | not measured | 86.26 [85.61, 86.83] | 97.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.69 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | Words | not measured | 99.08 [98.88, 99.28] | 99.72 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.72 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fa: UD_Persian-Seraji | raw | XPOS | not measured | 7.17 [6.71, 7.66] | 97.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | gold | AllTags | not measured | 59.08 [58.24, 59.90] | 94.23 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | gold | CLAS | not measured | 72.18 [71.04, 73.24] | not published | not published |
| fi: UD_Finnish-FTB | gold | LAS | not measured | 72.93 [71.91, 73.94] | 89.42 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | gold | Lemmas | not measured | 81.17 [80.50, 81.86] | 95.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | gold | MLAS | not measured | 48.49 [47.40, 49.63] | 82.72 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | gold | UAS | not measured | 79.34 [78.40, 80.27] | 92.38 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | gold | UFeats | not measured | 78.17 [77.47, 78.83] | 96.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | gold | UPOS | not measured | 87.31 [86.69, 87.87] | 97.05 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | gold | XPOS | not measured | 62.13 [61.30, 62.92] | 95.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fi: UD_Finnish-FTB | raw | AllTags | not measured | 49.32 [48.47, 50.14] | 96.87 (Trankit large, UD 2.5; test same size) — **MISS** | 95.29 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | CLAS | not measured | 75.32 [74.16, 76.42] | 93.54 (Trankit large, UD 2.5; test same size) — **MISS** | 86.24 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | LAS | not measured | 77.41 [76.42, 78.35] | 94.53 (Trankit large, UD 2.5; test same size) — **MISS** | 88.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | Lemmas | not measured | 82.30 [81.70, 83.00] | 97.10 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 97.10 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | MLAS | not measured | 52.18 [51.07, 53.35] | 90.68 (Trankit large, UD 2.5; test same size) — **MISS** | 82.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | Sentences | not measured | 87.32 [85.87, 88.72] | 97.36 (Trankit large, UD 2.5; test same size) — **MISS** | 90.13 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | Tokens | not measured | 99.59 [99.44, 99.72] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | UAS | not measured | 82.60 [81.70, 83.50] | 95.84 (Trankit large, UD 2.5; test same size) — **MISS** | 90.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | UFeats | not measured | 79.28 [78.59, 79.94] | 98.09 (Trankit large, UD 2.5; test same size) — **MISS** | 97.12 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | UPOS | not measured | 87.62 [87.05, 88.17] | 98.32 (Trankit large, UD 2.5; test same size) — **MISS** | 96.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | Words | not measured | 99.36 [99.20, 99.51] | 99.98 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.98 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fi: UD_Finnish-FTB | raw | XPOS | not measured | 51.48 [50.63, 52.30] | 97.29 (Trankit large, UD 2.5; test same size) — **MISS** | 96.09 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | gold | AllTags | not measured | 77.78 [76.95, 78.63] | 92.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fo: UD_Faroese-FarPaHC | gold | CLAS | not measured | 66.42 [64.64, 68.31] | not published | not published |
| fo: UD_Faroese-FarPaHC | gold | LAS | not measured | 71.92 [70.54, 73.47] | 83.58 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fo: UD_Faroese-FarPaHC | gold | Lemmas | not measured | 100.00 [100.00, 100.00] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| fo: UD_Faroese-FarPaHC | gold | MLAS | not measured | 47.80 [46.06, 49.66] | 69.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fo: UD_Faroese-FarPaHC | gold | UAS | not measured | 77.08 [75.67, 78.58] | 87.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fo: UD_Faroese-FarPaHC | gold | UFeats | not measured | 86.06 [85.32, 86.77] | 94.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fo: UD_Faroese-FarPaHC | gold | UPOS | not measured | 94.59 [94.09, 95.06] | 97.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fo: UD_Faroese-FarPaHC | gold | XPOS | not measured | 81.33 [80.53, 82.21] | 93.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fo: UD_Faroese-FarPaHC | raw | AllTags | not measured | 77.85 [76.98, 78.74] | 92.46 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 90.86 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | CLAS | not measured | 72.03 [70.17, 73.86] | 72.28 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 72.28 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| fo: UD_Faroese-FarPaHC | raw | LAS | not measured | 76.88 [75.50, 78.29] | 82.71 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.07 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | Lemmas | not measured | 99.39 [99.20, 99.58] | 99.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.64 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | MLAS | not measured | 53.79 [52.01, 55.62] | 68.94 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 64.48 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | Sentences | not measured | 91.84 [88.85, 94.59] | 93.94 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 93.94 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| fo: UD_Faroese-FarPaHC | raw | Tokens | not measured | 99.41 [99.21, 99.59] | 99.65 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.65 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | UAS | not measured | 82.00 [80.73, 83.34] | 86.48 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 84.39 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | UFeats | not measured | 86.72 [85.97, 87.46] | 94.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 92.92 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | UPOS | not measured | 94.17 [93.65, 94.68] | 97.48 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.51 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | Words | not measured | 99.39 [99.20, 99.58] | 99.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.64 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fo: UD_Faroese-FarPaHC | raw | XPOS | not measured | 81.38 [80.53, 82.24] | 92.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 91.56 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| fr: UD_French-GSD | gold | CLAS | not measured | 82.47 [81.26, 83.85] | not published | not published |
| fr: UD_French-GSD | gold | LAS | 86.55 [85.57, 87.49] | 86.89 [85.93, 87.88] | 93.11 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fr: UD_French-GSD | gold | Lemmas | 97.56 [97.23, 97.88] | 98.03 [97.72, 98.31] | 98.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fr: UD_French-GSD | gold | MLAS | not measured | 75.00 [73.45, 76.63] | 86.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fr: UD_French-GSD | gold | UAS | 89.36 [88.49, 90.23] | 89.52 [88.61, 90.43] | 94.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fr: UD_French-GSD | gold | UFeats | 96.24 [95.75, 96.67] | 96.06 [95.58, 96.49] | 98.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fr: UD_French-GSD | gold | UPOS | 97.21 [96.82, 97.58] | 96.99 [96.60, 97.39] | 98.55 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| fr: UD_French-GSD | raw | CLAS | not measured | 86.65 [85.41, 87.83] | 90.17 (Trankit large, UD 2.5; test changed) — **MISS** | 87.44 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| fr: UD_French-GSD | raw | LAS | 88.07 [87.07, 89.05] | 89.78 [88.84, 90.63] | 93.37 (Trankit large, UD 2.5; test changed) — **MISS** | 91.28 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| fr: UD_French-GSD | raw | Lemmas | 97.42 [97.09, 97.77] | 97.96 [97.64, 98.25] | 98.20 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 98.20 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| fr: UD_French-GSD | raw | MLAS | not measured | 80.57 [79.06, 81.91] | 86.35 (Trankit large, UD 2.5; test changed) — **MISS** | 84.45 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| fr: UD_French-GSD | raw | Sentences | 92.40 [89.74, 94.66] | 92.65 [90.07, 94.98] | 98.80 (Trankit large, UD 2.5; test changed) — **MISS** | 95.03 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| fr: UD_French-GSD | raw | Tokens | 99.88 [99.79, 99.96] | 99.88 [99.79, 99.96] | 99.77 (Trankit large, UD 2.5; test changed) — within or ahead | 99.71 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| fr: UD_French-GSD | raw | UAS | 90.67 [89.74, 91.58] | 91.97 [91.12, 92.76] | 94.97 (Trankit large, UD 2.5; test changed) — **MISS** | 93.27 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| fr: UD_French-GSD | raw | UFeats | 96.07 [95.59, 96.52] | 96.26 [95.82, 96.68] | 97.74 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 97.74 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| fr: UD_French-GSD | raw | UPOS | 97.06 [96.71, 97.42] | 97.10 [96.71, 97.46] | 97.89 (Trankit large, UD 2.5; test changed) — **MISS** | 97.63 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| fr: UD_French-GSD | raw | Words | 99.48 [99.29, 99.65] | 99.48 [99.29, 99.65] | 99.72 (Trankit large, UD 2.5; test changed) — **MISS** | 99.50 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ga: UD_Irish-IDT | gold | AllTags | not measured | 60.83 [59.34, 62.05] | 88.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | gold | CLAS | not measured | 64.78 [62.68, 66.81] | not published | not published |
| ga: UD_Irish-IDT | gold | LAS | not measured | 71.66 [69.97, 73.24] | 82.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | gold | Lemmas | not measured | 91.11 [90.18, 91.90] | 95.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | gold | MLAS | not measured | 32.12 [30.49, 33.73] | 65.80 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | gold | UAS | not measured | 78.67 [77.14, 80.11] | 87.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | gold | UFeats | not measured | 68.36 [67.18, 69.47] | 91.43 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | gold | UPOS | not measured | 90.83 [89.85, 91.65] | 96.46 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | gold | XPOS | not measured | 85.82 [84.83, 86.77] | 95.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ga: UD_Irish-IDT | raw | AllTags | not measured | 61.92 [60.46, 63.32] | 88.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 86.52 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | CLAS | not measured | 68.12 [65.79, 70.27] | 76.34 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 76.34 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | LAS | not measured | 73.90 [71.88, 75.71] | 81.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 81.51 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | Lemmas | not measured | 89.93 [88.98, 90.82] | 95.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.32 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | MLAS | not measured | 34.25 [32.40, 35.98] | 65.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 63.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | Sentences | not measured | 86.06 [82.70, 89.19] | 98.24 (Trankit large, UD 2.5; test changed) — **MISS** | 97.24 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | Tokens | not measured | 98.09 [97.47, 98.59] | 99.75 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.75 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | UAS | not measured | 80.66 [78.83, 82.33] | 87.31 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 86.81 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | UFeats | not measured | 68.07 [66.72, 69.32] | 91.32 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.41 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | UPOS | not measured | 90.27 [89.27, 91.12] | 96.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.01 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | Words | not measured | 98.09 [97.47, 98.59] | 99.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.75 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ga: UD_Irish-IDT | raw | XPOS | not measured | 87.25 [86.27, 88.17] | 95.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.55 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | AllTags | not measured | 65.11 [63.95, 66.15] | 90.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | CLAS | not measured | 64.97 [63.30, 66.43] | not published | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | LAS | not measured | 71.21 [69.81, 72.39] | 83.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | Lemmas | not measured | 93.80 [93.19, 94.32] | 97.70 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | MLAS | not measured | 42.12 [40.61, 43.54] | 72.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | UAS | not measured | 78.53 [77.26, 79.68] | 87.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | UFeats | not measured | 77.00 [76.03, 77.94] | 93.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | UPOS | not measured | 90.89 [90.16, 91.57] | 96.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | gold | XPOS | not measured | 70.33 [69.21, 71.31] | 92.03 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | AllTags | not measured | 54.95 [53.83, 55.93] | 89.42 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 89.42 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | CLAS | not measured | 58.12 [56.23, 60.02] | 72.86 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 72.86 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | LAS | not measured | 59.34 [57.76, 60.86] | 77.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 77.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | Lemmas | not measured | 80.82 [79.94, 81.74] | 95.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 95.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | MLAS | not measured | 34.30 [32.91, 35.65] | 66.53 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 66.53 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | Sentences | not measured | 63.02 [59.09, 67.21] | 64.54 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 64.54 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | Tokens | not measured | 90.18 [89.42, 90.90] | 99.65 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.65 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | UAS | not measured | 64.70 [63.06, 66.21] | 82.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 82.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | UFeats | not measured | 65.18 [64.18, 66.18] | 91.91 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 91.91 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | UPOS | not measured | 78.37 [77.49, 79.18] | 94.70 (Trankit large, UD 2.5; test changed) — **MISS** | 94.62 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | Words | not measured | 85.57 [84.70, 86.41] | 99.42 (Trankit large, UD 2.5; test changed) — **MISS** | 98.86 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gd: UD_Scottish_Gaelic-ARCOSG | raw | XPOS | not measured | 59.64 [58.48, 60.65] | 90.24 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 90.24 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| gl: UD_Galician-TreeGal | gold | AllTags | not measured | 81.94 [80.86, 82.94] | 94.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | gold | CLAS | not measured | 72.40 [70.85, 74.02] | not published | not published |
| gl: UD_Galician-TreeGal | gold | LAS | not measured | 76.69 [75.42, 77.87] | 82.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | gold | Lemmas | not measured | 95.39 [94.95, 95.82] | 98.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | gold | MLAS | not measured | 54.87 [53.08, 56.73] | 71.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | gold | UAS | not measured | 82.48 [81.42, 83.50] | 86.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | gold | UFeats | not measured | 87.74 [86.84, 88.54] | 96.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | gold | UPOS | not measured | 91.85 [91.17, 92.52] | 97.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | gold | XPOS | not measured | 85.46 [84.56, 86.30] | 95.42 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| gl: UD_Galician-TreeGal | raw | AllTags | not measured | 72.91 [71.91, 73.96] | 94.97 (Trankit large, UD 2.5; test same size) — **MISS** | 90.64 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | CLAS | not measured | 71.90 [70.19, 73.59] | 80.31 (Trankit large, UD 2.5; test same size) — **MISS** | 64.38 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| gl: UD_Galician-TreeGal | raw | LAS | not measured | 66.34 [65.07, 67.80] | 83.90 (Trankit large, UD 2.5; test same size) — **MISS** | 71.86 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | Lemmas | not measured | 83.92 [83.08, 84.76] | 97.31 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.52 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | MLAS | not measured | 49.73 [48.04, 51.36] | 74.67 (Trankit large, UD 2.5; test same size) — **MISS** | 58.14 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | Sentences | not measured | 79.95 [76.05, 83.67] | 95.52 (Trankit large, UD 2.5; test same size) — **MISS** | 88.02 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | Tokens | not measured | 99.28 [98.99, 99.53] | 99.68 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.68 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | UAS | not measured | 70.24 [68.98, 71.51] | 87.17 (Trankit large, UD 2.5; test same size) — **MISS** | 77.27 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | UFeats | not measured | 78.13 [77.16, 79.07] | 96.50 (Trankit large, UD 2.5; test same size) — **MISS** | 93.11 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | UPOS | not measured | 81.47 [80.65, 82.36] | 97.62 (Trankit large, UD 2.5; test same size) — **MISS** | 93.89 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | Words | not measured | 87.31 [86.48, 88.11] | 99.22 (Trankit large, UD 2.5; test same size) — **MISS** | 98.53 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| gl: UD_Galician-TreeGal | raw | XPOS | not measured | 75.29 [74.29, 76.34] | 95.68 (Trankit large, UD 2.5; test same size) — **MISS** | 91.51 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| grc: UD_Ancient_Greek-PTNK | gold | CLAS | not measured | 67.13 [65.47, 68.59] | not published | not published |
| grc: UD_Ancient_Greek-PTNK | gold | LAS | not measured | 73.13 [71.87, 74.27] | 87.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | gold | Lemmas | not measured | 85.61 [84.97, 86.33] | 94.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | gold | MLAS | not measured | 36.20 [34.57, 37.58] | 70.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | gold | UAS | not measured | 77.95 [76.78, 79.06] | 92.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | gold | UFeats | not measured | 71.83 [70.76, 72.88] | 91.80 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | gold | UPOS | not measured | 93.42 [92.84, 93.97] | 98.55 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | CLAS | not measured | 70.73 [69.01, 72.43] | not published | not published |
| grc: UD_Ancient_Greek-PTNK | raw | LAS | not measured | 75.45 [74.11, 76.71] | 84.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | Lemmas | not measured | 84.76 [84.05, 85.46] | 94.84 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | MLAS | not measured | 38.16 [36.46, 39.77] | 66.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | Sentences | not measured | 46.79 [41.96, 52.07] | 55.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | Tokens | not measured | 99.03 [98.78, 99.26] | not published | not published |
| grc: UD_Ancient_Greek-PTNK | raw | UAS | not measured | 80.23 [78.90, 81.53] | 88.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | UFeats | not measured | 71.42 [70.28, 72.44] | 91.75 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | UPOS | not measured | 92.10 [91.47, 92.68] | 98.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| grc: UD_Ancient_Greek-PTNK | raw | Words | not measured | 98.96 [98.72, 99.19] | 99.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | AllTags | not measured | 73.33 [72.42, 74.24] | 91.52 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | CLAS | not measured | 73.52 [72.25, 74.92] | not published | not published |
| he: UD_Hebrew-IAHLTwiki | gold | LAS | not measured | 76.97 [75.90, 78.16] | 91.23 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | Lemmas | not measured | 94.16 [93.65, 94.62] | 98.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | MLAS | not measured | 43.05 [41.54, 44.44] | 75.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | UAS | not measured | 81.92 [80.92, 82.90] | 93.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | UFeats | not measured | 77.07 [76.20, 77.92] | 92.55 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | UPOS | not measured | 88.46 [87.69, 89.21] | 97.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | gold | XPOS | not measured | 90.72 [90.05, 91.35] | 97.11 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| he: UD_Hebrew-IAHLTwiki | raw | AllTags | not measured | 41.59 [40.42, 42.81] | 86.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 86.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | CLAS | not measured | 27.76 [26.30, 29.28] | 75.58 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 75.58 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | LAS | not measured | 29.34 [27.85, 30.73] | 81.19 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 81.19 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | Lemmas | not measured | 51.58 [50.39, 52.82] | 92.51 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 92.51 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | MLAS | not measured | 16.88 [15.80, 17.93] | 64.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 64.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | Sentences | not measured | 42.17 [37.22, 47.57] | 97.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.78 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | Tokens | not measured | 97.18 [96.65, 97.72] | 99.64 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.64 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | UAS | not measured | 30.34 [28.78, 31.81] | 83.23 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 83.23 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | UFeats | not measured | 42.73 [41.55, 43.94] | 86.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 86.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | UPOS | not measured | 50.85 [49.71, 52.06] | 91.69 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 91.69 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | Words | not measured | 54.72 [53.59, 55.98] | 94.61 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 94.61 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| he: UD_Hebrew-IAHLTwiki | raw | XPOS | not measured | 51.98 [50.83, 53.18] | 91.69 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 91.69 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hr: UD_Croatian-SET | gold | AllTags | not measured | 76.51 [75.83, 77.15] | 95.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | gold | CLAS | not measured | 75.54 [74.70, 76.39] | not published | not published |
| hr: UD_Croatian-SET | gold | LAS | not measured | 79.15 [78.36, 79.95] | 89.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | gold | Lemmas | not measured | 92.20 [91.78, 92.59] | 97.71 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | gold | MLAS | not measured | 54.16 [53.21, 55.13] | 82.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | gold | UAS | not measured | 84.29 [83.63, 85.02] | 92.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | gold | UFeats | not measured | 81.27 [80.65, 81.85] | 96.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | gold | UPOS | not measured | 93.74 [93.41, 94.10] | 98.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | gold | XPOS | not measured | 80.52 [79.83, 81.12] | 95.90 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hr: UD_Croatian-SET | raw | AllTags | not measured | 80.47 [79.88, 81.06] | 95.99 (Trankit large, UD 2.5; test same size) — **MISS** | 94.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | CLAS | not measured | 83.78 [83.02, 84.46] | 87.77 (Trankit large, UD 2.5; test same size) — **MISS** | 84.14 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| hr: UD_Croatian-SET | raw | LAS | not measured | 86.11 [85.44, 86.69] | 89.74 (Trankit large, UD 2.5; test same size) — **MISS** | 86.95 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | Lemmas | not measured | 92.60 [92.21, 92.96] | 97.64 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | MLAS | not measured | 62.72 [61.75, 63.63] | 82.19 (Trankit large, UD 2.5; test same size) — **MISS** | 78.19 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | Sentences | not measured | 94.43 [93.04, 95.73] | 99.16 (Trankit base, UD 2.5; test same size) — **MISS** | 97.08 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | Tokens | not measured | 98.83 [98.63, 99.02] | 99.96 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.96 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | UAS | not measured | 89.62 [89.01, 90.20] | 93.86 (Trankit large, UD 2.5; test same size) — **MISS** | 90.29 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | UFeats | not measured | 83.30 [82.78, 83.82] | 96.85 (Trankit large, UD 2.5; test same size) — **MISS** | 95.27 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | UPOS | not measured | 94.15 [93.80, 94.48] | 98.58 (Trankit large, UD 2.5; test same size) — **MISS** | 98.21 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | Words | not measured | 98.83 [98.63, 99.02] | 99.96 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.96 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| hr: UD_Croatian-SET | raw | XPOS | not measured | 83.44 [82.88, 83.98] | 96.55 (Trankit large, UD 2.5; test same size) — **MISS** | 94.89 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ht: UD_Haitian_Creole-Adolphe | gold | CLAS | not measured | 51.35 [49.48, 53.37] | not published | not published |
| ht: UD_Haitian_Creole-Adolphe | gold | LAS | not measured | 54.20 [52.50, 56.13] | 60.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | gold | Lemmas | not measured | 98.70 [98.42, 98.96] | 99.05 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | gold | MLAS | not measured | 40.26 [38.56, 42.04] | 50.10 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | gold | UAS | not measured | 59.00 [57.26, 60.95] | 64.83 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | gold | UFeats | not measured | 97.12 [96.65, 97.52] | 98.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | gold | UPOS | not measured | 93.12 [92.47, 93.71] | 95.55 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | CLAS | not measured | 55.04 [53.35, 56.98] | not published | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | LAS | not measured | 57.76 [56.19, 59.57] | 60.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | Lemmas | not measured | 98.91 [98.66, 99.15] | 99.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | MLAS | not measured | 45.00 [43.41, 46.76] | 50.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | Sentences | not measured | 97.28 [95.80, 98.64] | 98.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | UAS | not measured | 62.04 [60.34, 63.85] | 64.82 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | UFeats | not measured | 97.75 [97.40, 98.07] | 97.98 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | UPOS | not measured | 94.15 [93.65, 94.59] | 95.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ht: UD_Haitian_Creole-Adolphe | raw | Words | not measured | 100.00 [100.00, 100.00] | 99.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| hy: UD_Armenian-ArmTDP | gold | CLAS | not measured | 72.45 [70.80, 74.17] | not published | not published |
| hy: UD_Armenian-ArmTDP | gold | LAS | not measured | 76.44 [75.04, 77.94] | 87.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hy: UD_Armenian-ArmTDP | gold | Lemmas | not measured | 92.16 [91.51, 92.79] | 97.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hy: UD_Armenian-ArmTDP | gold | MLAS | not measured | 43.37 [41.73, 45.16] | 76.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hy: UD_Armenian-ArmTDP | gold | UAS | not measured | 82.51 [81.17, 83.84] | 90.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hy: UD_Armenian-ArmTDP | gold | UFeats | not measured | 71.29 [70.21, 72.45] | 94.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hy: UD_Armenian-ArmTDP | gold | UPOS | not measured | 93.30 [92.71, 93.86] | 98.06 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hy: UD_Armenian-ArmTDP | raw | CLAS | not measured | 69.55 [67.66, 71.50] | 80.91 (Trankit large, UD 2.5; test changed) — **MISS** | 52.52 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| hy: UD_Armenian-ArmTDP | raw | LAS | not measured | 72.59 [70.91, 74.32] | 86.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 61.80 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| hy: UD_Armenian-ArmTDP | raw | Lemmas | not measured | 89.43 [88.45, 90.37] | 96.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 91.35 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hy: UD_Armenian-ArmTDP | raw | MLAS | not measured | 41.47 [39.92, 43.13] | 76.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 29.58 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| hy: UD_Armenian-ArmTDP | raw | Sentences | not measured | 75.25 [72.09, 78.44] | 99.46 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.46 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hy: UD_Armenian-ArmTDP | raw | Tokens | not measured | 97.68 [96.98, 98.30] | 99.92 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.92 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hy: UD_Armenian-ArmTDP | raw | UAS | not measured | 77.81 [76.28, 79.54] | 89.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 74.04 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| hy: UD_Armenian-ArmTDP | raw | UFeats | not measured | 68.74 [67.65, 69.79] | 94.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 66.22 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| hy: UD_Armenian-ArmTDP | raw | UPOS | not measured | 90.12 [89.22, 90.98] | 97.67 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 83.05 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| hy: UD_Armenian-ArmTDP | raw | Words | not measured | 96.34 [95.57, 97.03] | 99.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.48 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | gold | CLAS | not measured | 68.29 [66.88, 69.56] | not published | not published |
| hyw: UD_Western_Armenian-ArmTDP | gold | LAS | not measured | 75.20 [74.07, 76.31] | 85.14 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hyw: UD_Western_Armenian-ArmTDP | gold | Lemmas | not measured | 91.15 [90.60, 91.67] | 97.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hyw: UD_Western_Armenian-ArmTDP | gold | MLAS | not measured | 33.62 [32.35, 34.93] | 70.46 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hyw: UD_Western_Armenian-ArmTDP | gold | UAS | not measured | 82.46 [81.40, 83.53] | 89.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hyw: UD_Western_Armenian-ArmTDP | gold | UFeats | not measured | 68.53 [67.63, 69.40] | 92.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hyw: UD_Western_Armenian-ArmTDP | gold | UPOS | not measured | 90.67 [90.07, 91.19] | 96.90 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| hyw: UD_Western_Armenian-ArmTDP | raw | CLAS | not measured | 64.78 [63.25, 66.27] | 77.33 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 77.33 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | LAS | not measured | 71.14 [69.87, 72.46] | 84.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 82.83 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | Lemmas | not measured | 88.68 [88.02, 89.34] | 96.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.70 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | MLAS | not measured | 32.06 [30.64, 33.36] | 70.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 68.89 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | Sentences | not measured | 89.14 [86.80, 91.23] | 99.56 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.56 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | Tokens | not measured | 97.81 [97.49, 98.16] | 99.95 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.95 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | UAS | not measured | 77.90 [76.61, 79.29] | 89.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | UFeats | not measured | 66.33 [65.43, 67.22] | 92.70 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 92.70 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | UPOS | not measured | 87.57 [86.89, 88.26] | 96.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.42 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| hyw: UD_Western_Armenian-ArmTDP | raw | Words | not measured | 96.53 [96.10, 96.98] | 99.91 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.91 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| id: UD_Indonesian-GSD | gold | AllTags | not measured | 74.40 [73.51, 75.36] | 89.24 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | gold | CLAS | not measured | 63.61 [62.21, 65.22] | not published | not published |
| id: UD_Indonesian-GSD | gold | LAS | not measured | 67.40 [66.10, 68.75] | 82.72 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | gold | Lemmas | not measured | 95.84 [95.43, 96.23] | 98.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | gold | MLAS | not measured | 50.56 [49.26, 52.04] | 73.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | gold | UAS | not measured | 75.84 [74.67, 77.07] | 88.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | gold | UFeats | not measured | 89.32 [88.69, 89.93] | 95.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | gold | UPOS | not measured | 91.42 [90.75, 92.11] | 94.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | gold | XPOS | not measured | 80.13 [79.27, 80.99] | 94.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| id: UD_Indonesian-GSD | raw | AllTags | not measured | 75.52 [74.56, 76.51] | 89.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 89.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | CLAS | not measured | 69.72 [68.23, 71.40] | 78.64 (Trankit base, UD 2.5; test changed) — **MISS** | 76.98 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | LAS | not measured | 73.87 [72.45, 75.36] | 81.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.83 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | Lemmas | not measured | 94.33 [93.77, 94.84] | 99.53 (Trankit large, UD 2.5; test changed) — **MISS** | 98.28 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | MLAS | not measured | 59.56 [57.98, 61.27] | 72.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 71.78 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | Sentences | not measured | 78.47 [74.98, 82.32] | 95.54 (Trankit large, UD 2.5; test changed) — **MISS** | 93.04 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | Tokens | not measured | 99.73 [99.59, 99.85] | 99.89 (Trankit large, UD 2.5; test changed) — **MISS** | 99.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | UAS | not measured | 80.64 [79.38, 82.02] | 87.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.05 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | UFeats | not measured | 88.20 [87.53, 88.85] | 96.11 (Trankit large, UD 2.5; test changed) — **MISS** | 95.58 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | UPOS | not measured | 90.19 [89.45, 90.92] | 94.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.16 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | Words | not measured | 96.96 [96.51, 97.36] | 99.89 (Trankit large, UD 2.5; test changed) — **MISS** | 99.64 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| id: UD_Indonesian-GSD | raw | XPOS | not measured | 80.07 [79.20, 80.92] | 95.06 (Trankit large, UD 2.5; test changed) — **MISS** | 94.69 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| is: UD_Icelandic-IcePaHC | gold | AllTags | not measured | 66.58 [66.30, 66.85] | 87.48 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | gold | CLAS | not measured | 60.44 [59.91, 60.99] | not published | not published |
| is: UD_Icelandic-IcePaHC | gold | LAS | not measured | 65.74 [65.29, 66.19] | 83.82 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | gold | Lemmas | not measured | 92.47 [92.31, 92.64] | 96.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | gold | MLAS | not measured | 34.68 [34.22, 35.16] | 67.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | gold | UAS | not measured | 71.65 [71.19, 72.11] | 87.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | gold | UFeats | not measured | 74.61 [74.33, 74.88] | 92.24 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | gold | UPOS | not measured | 92.14 [91.99, 92.32] | 97.08 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | gold | XPOS | not measured | 82.13 [81.89, 82.37] | 93.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| is: UD_Icelandic-IcePaHC | raw | AllTags | not measured | 66.85 [66.56, 67.12] | 87.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 85.48 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | CLAS | not measured | 65.66 [65.14, 66.17] | 77.27 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 77.27 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | LAS | not measured | 71.34 [70.88, 71.81] | 83.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 82.85 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | Lemmas | not measured | 92.34 [92.17, 92.52] | 96.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.78 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | MLAS | not measured | 38.66 [38.17, 39.16] | 66.86 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 64.78 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | Sentences | not measured | 89.93 [89.02, 90.80] | 93.07 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 93.07 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | Tokens | not measured | 99.76 [99.69, 99.81] | 99.90 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.90 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | UAS | not measured | 77.49 [77.06, 77.95] | 87.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 86.79 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | UFeats | not measured | 74.75 [74.44, 75.02] | 92.10 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 90.42 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | UPOS | not measured | 91.85 [91.68, 92.02] | 96.92 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.36 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | Words | not measured | 99.63 [99.56, 99.69] | 99.84 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.84 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| is: UD_Icelandic-IcePaHC | raw | XPOS | not measured | 82.26 [82.02, 82.51] | 93.34 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 92.27 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| it: UD_Italian-MarkIT | gold | AllTags | 89.51 [88.83, 90.21] | 89.79 [89.12, 90.43] | 92.60 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | gold | CLAS | not measured | 72.13 [70.70, 73.64] | not published | not published |
| it: UD_Italian-MarkIT | gold | LAS | 77.27 [76.19, 78.38] | 77.63 [76.52, 78.78] | 85.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | gold | Lemmas | 86.37 [85.60, 87.06] | 86.38 [85.63, 87.08] | 88.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | gold | MLAS | not measured | 57.58 [56.03, 59.25] | 70.84 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | gold | UAS | 81.82 [80.77, 82.86] | 82.05 [81.01, 83.15] | 89.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | gold | UFeats | 91.91 [91.27, 92.50] | 92.12 [91.51, 92.73] | 94.28 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | gold | UPOS | 95.69 [95.26, 96.12] | 95.81 [95.39, 96.26] | 97.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | gold | XPOS | 96.31 [95.90, 96.68] | 96.26 [95.87, 96.66] | 97.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| it: UD_Italian-MarkIT | raw | AllTags | 90.13 [89.50, 90.80] | 90.50 [89.88, 91.10] | 92.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 92.34 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| it: UD_Italian-MarkIT | raw | CLAS | not measured | 77.84 [76.30, 79.29] | 74.48 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 74.48 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| it: UD_Italian-MarkIT | raw | LAS | 81.12 [79.93, 82.18] | 82.22 [81.04, 83.28] | 84.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.63 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| it: UD_Italian-MarkIT | raw | Lemmas | 86.46 [85.69, 87.17] | 86.65 [85.93, 87.35] | 88.31 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.08 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| it: UD_Italian-MarkIT | raw | MLAS | not measured | 64.34 [62.76, 65.86] | 70.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 65.35 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| it: UD_Italian-MarkIT | raw | Sentences | 90.61 [87.84, 93.57] | 90.11 [87.27, 92.93] | 99.12 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.12 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| it: UD_Italian-MarkIT | raw | Tokens | 99.93 [99.86, 99.98] | 99.93 [99.86, 99.98] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| it: UD_Italian-MarkIT | raw | UAS | 85.40 [84.26, 86.44] | 86.39 [85.36, 87.29] | 88.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 85.58 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| it: UD_Italian-MarkIT | raw | UFeats | 92.40 [91.81, 93.01] | 92.84 [92.29, 93.42] | 94.28 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 94.28 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| it: UD_Italian-MarkIT | raw | UPOS | 95.98 [95.54, 96.36] | 96.12 [95.75, 96.52] | 97.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.29 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| it: UD_Italian-MarkIT | raw | Words | 99.73 [99.58, 99.86] | 99.73 [99.58, 99.86] | 99.67 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.67 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| it: UD_Italian-MarkIT | raw | XPOS | 96.56 [96.18, 96.92] | 96.45 [96.05, 96.80] | 97.03 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.34 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| ja: UD_Japanese-GSD | gold | AllTags | not measured | 55.45 [54.50, 56.44] | 97.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | gold | CLAS | not measured | 48.32 [46.78, 49.96] | not published | not published |
| ja: UD_Japanese-GSD | gold | LAS | not measured | 52.94 [51.62, 54.32] | 94.03 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | gold | Lemmas | not measured | 88.79 [88.18, 89.43] | 98.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | gold | MLAS | not measured | 31.47 [30.14, 32.98] | 88.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | gold | UAS | not measured | 60.33 [59.00, 61.70] | 94.94 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | gold | UFeats | not measured | 99.24 [99.08, 99.39] | 99.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | gold | UPOS | not measured | 77.54 [76.68, 78.44] | 98.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | gold | XPOS | not measured | 60.54 [59.69, 61.44] | 97.69 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ja: UD_Japanese-GSD | raw | AllTags | not measured | 52.40 [51.39, 53.38] | 95.45 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 95.45 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | CLAS | not measured | 52.15 [50.37, 53.93] | 84.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 84.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | LAS | not measured | 56.76 [55.25, 58.32] | 89.29 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 89.29 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | Lemmas | not measured | 87.06 [86.44, 87.73] | 96.02 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 96.02 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | MLAS | not measured | 37.05 [35.26, 38.80] | 83.86 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 83.86 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | Sentences | not measured | 75.90 [72.12, 79.31] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.72 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | Tokens | not measured | 96.34 [95.91, 96.79] | 97.37 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 97.37 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | UAS | not measured | 62.62 [61.04, 64.19] | 90.01 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 90.01 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | UFeats | not measured | 95.62 [95.16, 96.08] | 97.36 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 97.36 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | UPOS | not measured | 75.11 [74.17, 76.05] | 96.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 96.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | Words | not measured | 96.34 [95.91, 96.79] | 97.37 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 97.37 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ja: UD_Japanese-GSD | raw | XPOS | not measured | 58.15 [57.31, 59.00] | 95.72 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 95.72 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ka: UD_Georgian-GLC | gold | AllTags | not measured | 71.95 [70.53, 73.31] | 92.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | gold | CLAS | not measured | 74.32 [72.92, 75.66] | not published | not published |
| ka: UD_Georgian-GLC | gold | LAS | not measured | 75.62 [74.36, 76.88] | 81.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | gold | Lemmas | not measured | 86.00 [85.23, 86.81] | 94.86 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | gold | MLAS | not measured | 47.86 [46.15, 49.53] | 71.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | gold | UAS | not measured | 80.80 [79.59, 82.00] | 85.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | gold | UFeats | not measured | 75.66 [74.50, 76.73] | 92.52 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | gold | UPOS | not measured | 92.83 [92.27, 93.36] | 97.19 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | gold | XPOS | not measured | 91.99 [91.29, 92.73] | 97.08 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | AllTags | not measured | 62.53 [60.91, 64.07] | 91.43 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | CLAS | not measured | 58.39 [56.54, 60.36] | not published | not published |
| ka: UD_Georgian-GLC | raw | LAS | not measured | 57.09 [55.21, 59.04] | 79.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | Lemmas | not measured | 75.84 [74.68, 76.94] | 94.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | MLAS | not measured | 38.45 [36.66, 40.33] | 70.43 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | Sentences | not measured | 87.88 [85.15, 90.48] | 95.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | Tokens | not measured | 99.11 [98.75, 99.40] | not published | not published |
| ka: UD_Georgian-GLC | raw | UAS | not measured | 61.15 [59.26, 63.07] | 83.58 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | UFeats | not measured | 65.88 [64.51, 67.20] | 91.80 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | UPOS | not measured | 81.28 [80.19, 82.37] | 96.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | Words | not measured | 87.47 [86.58, 88.31] | 99.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ka: UD_Georgian-GLC | raw | XPOS | not measured | 80.93 [79.77, 82.01] | 96.34 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| kk: UD_Kazakh-KTB | gold | AllTags | not measured | 24.52 [23.64, 25.46] | not published | not published |
| kk: UD_Kazakh-KTB | gold | CLAS | not measured | 45.66 [44.25, 47.10] | not published | not published |
| kk: UD_Kazakh-KTB | gold | LAS | not measured | 49.32 [47.99, 50.61] | not published | not published |
| kk: UD_Kazakh-KTB | gold | Lemmas | not measured | 58.91 [57.97, 59.84] | not published | not published |
| kk: UD_Kazakh-KTB | gold | MLAS | not measured | 10.68 [9.94, 11.46] | not published | not published |
| kk: UD_Kazakh-KTB | gold | UAS | not measured | 67.55 [66.13, 68.93] | not published | not published |
| kk: UD_Kazakh-KTB | gold | UFeats | not measured | 46.86 [45.91, 47.77] | not published | not published |
| kk: UD_Kazakh-KTB | gold | UPOS | not measured | 50.89 [49.90, 51.85] | not published | not published |
| kk: UD_Kazakh-KTB | gold | XPOS | not measured | 58.02 [56.89, 59.15] | not published | not published |
| kk: UD_Kazakh-KTB | raw | AllTags | not measured | 32.17 [31.28, 33.00] | 74.50 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 74.50 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | CLAS | not measured | 32.15 [30.90, 33.51] | 49.45 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 49.45 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | LAS | not measured | 33.79 [32.50, 35.13] | 51.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 51.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | Lemmas | not measured | 56.21 [55.20, 57.18] | 89.21 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 89.21 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | MLAS | not measured | 8.37 [7.67, 9.03] | 38.40 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 38.40 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | Sentences | not measured | 18.15 [15.63, 20.76] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | Tokens | not measured | 97.45 [96.91, 97.93] | 99.42 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.42 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | UAS | not measured | 45.81 [44.34, 47.37] | 60.55 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 60.55 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | UFeats | not measured | 45.88 [44.91, 46.76] | 78.70 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 78.70 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | UPOS | not measured | 53.41 [52.43, 54.35] | 85.58 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 85.58 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kk: UD_Kazakh-KTB | raw | Words | not measured | 96.99 [96.44, 97.50] | 96.85 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 96.85 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| kk: UD_Kazakh-KTB | raw | XPOS | not measured | 63.99 [62.97, 65.01] | 86.15 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 86.15 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | AllTags | not measured | 13.72 [13.02, 14.37] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | CLAS | not measured | 44.92 [43.24, 46.64] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | LAS | not measured | 49.91 [48.41, 51.34] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | Lemmas | not measured | 62.32 [61.26, 63.37] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | MLAS | not measured | 4.98 [4.42, 5.54] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | UAS | not measured | 63.32 [61.80, 64.85] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | UFeats | not measured | 42.46 [41.64, 43.30] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | UPOS | not measured | 49.90 [48.87, 50.85] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | gold | XPOS | not measured | 45.00 [44.06, 45.88] | not published | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | AllTags | not measured | 21.91 [21.19, 22.61] | 52.27 (Trankit large, UD 2.5; test same size) — **MISS** | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | CLAS | not measured | 41.35 [39.52, 43.23] | 25.57 (Trankit large, UD 2.5; test same size) — within or ahead | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | LAS | not measured | 46.20 [44.40, 47.97] | 29.89 (Trankit large, UD 2.5; test same size) — within or ahead | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | Lemmas | not measured | 63.13 [62.13, 64.19] | 57.63 (Trankit large, UD 2.5; test same size) — within or ahead | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | MLAS | not measured | 5.53 [4.93, 6.12] | 9.04 (Trankit large, UD 2.5; test same size) — **MISS** | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | Sentences | not measured | 47.71 [43.89, 51.46] | 91.50 (Trankit large, UD 2.5; test same size) — **MISS** | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | Tokens | not measured | 96.59 [96.08, 97.08] | 94.95 (Trankit large, UD 2.5; test same size) — within or ahead | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | UAS | not measured | 55.20 [53.33, 57.16] | 37.12 (Trankit large, UD 2.5; test same size) — within or ahead | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | UFeats | not measured | 43.71 [42.81, 44.58] | 57.15 (Trankit large, UD 2.5; test same size) — **MISS** | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | UPOS | not measured | 53.90 [52.84, 54.98] | 75.07 (Trankit large, UD 2.5; test same size) — **MISS** | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | Words | not measured | 96.09 [95.57, 96.57] | 94.63 (Trankit large, UD 2.5; test same size) — within or ahead | not published |
| kmr: UD_Northern_Kurdish-Kurmanji | raw | XPOS | not measured | 53.43 [52.40, 54.38] | 74.16 (Trankit large, UD 2.5; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | gold | AllTags | not measured | 50.87 [50.24, 51.48] | 87.60 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | gold | CLAS | not measured | 64.84 [64.16, 65.57] | not published | not published |
| ko: UD_Korean-Kaist | gold | LAS | not measured | 68.13 [67.42, 68.83] | 86.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | gold | Lemmas | not measured | 82.01 [81.55, 82.51] | 94.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | gold | MLAS | not measured | 54.48 [53.71, 55.22] | 82.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | gold | UAS | not measured | 76.81 [76.13, 77.49] | 88.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | gold | UPOS | not measured | 83.77 [83.31, 84.20] | 96.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | gold | XPOS | not measured | 53.61 [53.00, 54.24] | 87.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ko: UD_Korean-Kaist | raw | AllTags | not measured | 55.15 [54.56, 55.79] | 88.07 (Trankit large, UD 2.5; test same size) — **MISS** | 85.81 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | CLAS | not measured | 72.10 [71.40, 72.85] | 86.37 (Trankit large, UD 2.5; test same size) — **MISS** | 83.65 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | LAS | not measured | 74.17 [73.47, 74.90] | 88.22 (Trankit large, UD 2.5; test same size) — **MISS** | 85.50 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | Lemmas | not measured | 82.88 [82.41, 83.38] | 94.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.10 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | MLAS | not measured | 62.12 [61.35, 62.90] | 83.56 (Trankit large, UD 2.5; test same size) — **MISS** | 80.93 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | Sentences | not measured | 92.51 [91.52, 93.49] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.93 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | Tokens | not measured | 99.90 [99.84, 99.95] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | UAS | not measured | 80.66 [79.98, 81.38] | 90.00 (Trankit large, UD 2.5; test same size) — **MISS** | 87.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | UPOS | not measured | 85.53 [85.09, 85.97] | 96.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.74 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | Words | not measured | 99.90 [99.84, 99.95] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ko: UD_Korean-Kaist | raw | XPOS | not measured | 57.88 [57.27, 58.51] | 88.07 (Trankit large, UD 2.5; test same size) — **MISS** | 85.81 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| koi: UD_Komi_Permyak-UH | gold | AllTags | not measured | 17.39 [15.54, 19.26] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | CLAS | not measured | 23.79 [19.78, 28.52] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | LAS | not measured | 30.79 [26.88, 35.53] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | Lemmas | not measured | 60.58 [56.73, 63.70] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | MLAS | not measured | 4.98 [3.51, 6.81] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | UAS | not measured | 50.61 [45.71, 56.22] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | UFeats | not measured | 44.96 [41.69, 47.92] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | UPOS | not measured | 54.71 [51.54, 57.78] | not published | not published |
| koi: UD_Komi_Permyak-UH | gold | XPOS | not measured | 17.39 [15.54, 19.26] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | AllTags | not measured | 17.39 [15.54, 19.26] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | CLAS | not measured | 27.03 [22.60, 32.05] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | LAS | not measured | 33.78 [29.41, 38.81] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | Lemmas | not measured | 61.35 [57.52, 64.60] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | MLAS | not measured | 4.10 [2.64, 5.81] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | Sentences | not measured | 75.38 [66.34, 82.90] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | UAS | not measured | 54.60 [49.09, 60.45] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | UFeats | not measured | 45.63 [42.91, 48.31] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | UPOS | not measured | 50.50 [47.91, 52.99] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | Words | not measured | 100.00 [100.00, 100.00] | not published | not published |
| koi: UD_Komi_Permyak-UH | raw | XPOS | not measured | 17.39 [15.54, 19.26] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | AllTags | not measured | 6.93 [6.30, 7.55] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | CLAS | not measured | 20.80 [19.60, 22.09] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | LAS | not measured | 25.75 [24.50, 27.15] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | Lemmas | not measured | 60.89 [59.87, 61.84] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | MLAS | not measured | 2.24 [1.85, 2.67] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | UAS | not measured | 45.10 [43.31, 46.85] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | UFeats | not measured | 12.17 [11.45, 12.95] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | UPOS | not measured | 26.84 [25.76, 27.89] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | gold | XPOS | not measured | 32.46 [31.47, 33.42] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | AllTags | not measured | 12.96 [12.23, 13.72] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | CLAS | not measured | 9.99 [9.02, 11.00] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | LAS | not measured | 12.68 [11.68, 13.80] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | Lemmas | not measured | 60.23 [59.20, 61.18] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | MLAS | not measured | 0.89 [0.64, 1.16] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | Sentences | not measured | 1.45 [0.47, 2.66] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | Tokens | not measured | 99.91 [99.82, 99.98] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | UAS | not measured | 22.17 [20.60, 23.81] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | UFeats | not measured | 20.18 [19.30, 21.14] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | UPOS | not measured | 36.32 [35.14, 37.42] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | Words | not measured | 99.24 [98.96, 99.51] | not published | not published |
| kpv: UD_Komi_Zyrian-Lattice | raw | XPOS | not measured | 37.42 [36.41, 38.43] | not published | not published |
| ky: UD_Kyrgyz-KTMU | gold | AllTags | not measured | 61.92 [60.81, 63.04] | 73.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | gold | CLAS | not measured | 61.76 [60.54, 62.98] | not published | not published |
| ky: UD_Kyrgyz-KTMU | gold | LAS | not measured | 65.35 [64.24, 66.42] | 74.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | gold | Lemmas | not measured | 80.51 [79.69, 81.38] | 89.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | gold | MLAS | not measured | 38.51 [37.17, 39.79] | 54.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | gold | UAS | not measured | 78.82 [77.89, 79.75] | 84.78 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | gold | UFeats | not measured | 66.43 [65.37, 67.55] | 77.98 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | gold | UPOS | not measured | 88.87 [88.18, 89.53] | 91.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | gold | XPOS | not measured | 88.72 [88.07, 89.39] | 91.06 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ky: UD_Kyrgyz-KTMU | raw | AllTags | not measured | 61.82 [60.78, 62.86] | 72.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 57.91 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ky: UD_Kyrgyz-KTMU | raw | CLAS | not measured | 64.75 [63.38, 66.06] | 52.60 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 52.60 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ky: UD_Kyrgyz-KTMU | raw | LAS | not measured | 67.79 [66.50, 68.97] | 73.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 58.77 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ky: UD_Kyrgyz-KTMU | raw | Lemmas | not measured | 79.55 [78.75, 80.43] | 88.71 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 88.09 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ky: UD_Kyrgyz-KTMU | raw | MLAS | not measured | 40.79 [39.41, 42.11] | 54.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 28.47 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ky: UD_Kyrgyz-KTMU | raw | Sentences | not measured | 92.70 [91.28, 94.09] | 98.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 98.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ky: UD_Kyrgyz-KTMU | raw | Tokens | not measured | 97.59 [97.23, 97.93] | 99.06 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.06 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ky: UD_Kyrgyz-KTMU | raw | UAS | not measured | 79.63 [78.60, 80.52] | 84.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 74.95 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ky: UD_Kyrgyz-KTMU | raw | UFeats | not measured | 66.26 [65.21, 67.25] | 77.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 60.22 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ky: UD_Kyrgyz-KTMU | raw | UPOS | not measured | 87.18 [86.43, 87.88] | 90.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.65 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| ky: UD_Kyrgyz-KTMU | raw | Words | not measured | 97.59 [97.23, 97.93] | 99.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.06 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ky: UD_Kyrgyz-KTMU | raw | XPOS | not measured | 87.55 [86.88, 88.18] | 90.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.94 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| la: UD_Latin-LLCT | gold | AllTags | not measured | 70.96 [70.03, 71.97] | 96.84 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | gold | CLAS | not measured | 77.30 [75.92, 78.81] | not published | not published |
| la: UD_Latin-LLCT | gold | LAS | not measured | 80.93 [79.78, 82.10] | 94.07 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | gold | Lemmas | not measured | 95.42 [94.99, 95.85] | 97.80 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | gold | MLAS | not measured | 51.35 [49.65, 53.15] | 88.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | gold | UAS | not measured | 83.95 [82.93, 84.98] | 95.05 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | gold | UFeats | not measured | 75.63 [74.78, 76.57] | 97.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | gold | UPOS | not measured | 96.38 [96.09, 96.67] | 99.78 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | gold | XPOS | not measured | 76.59 [75.69, 77.53] | 97.05 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| la: UD_Latin-LLCT | raw | AllTags | not measured | 72.20 [71.18, 73.34] | 96.83 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.47 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | CLAS | not measured | 79.91 [78.42, 81.41] | 93.53 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 93.53 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | LAS | not measured | 83.30 [82.10, 84.52] | 94.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 94.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | Lemmas | not measured | 95.64 [95.22, 96.06] | 98.09 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 98.09 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | MLAS | not measured | 53.58 [51.84, 55.42] | 89.49 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 89.49 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | Sentences | not measured | 88.69 [86.82, 90.62] | 99.49 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.32 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | Tokens | not measured | 99.73 [99.62, 99.82] | 99.99 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | UAS | not measured | 86.01 [84.90, 87.13] | 96.13 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 96.13 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | UFeats | not measured | 75.55 [74.62, 76.55] | 97.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.79 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | UPOS | not measured | 96.72 [96.43, 97.01] | 99.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.57 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | Words | not measured | 99.71 [99.60, 99.81] | 99.99 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| la: UD_Latin-LLCT | raw | XPOS | not measured | 77.82 [76.97, 78.83] | 97.05 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.65 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lij: UD_Ligurian-GLT | gold | CLAS | not measured | 25.63 [23.69, 27.63] | not published | not published |
| lij: UD_Ligurian-GLT | gold | LAS | not measured | 36.41 [34.59, 38.31] | not published | not published |
| lij: UD_Ligurian-GLT | gold | Lemmas | not measured | 72.29 [70.97, 73.49] | not published | not published |
| lij: UD_Ligurian-GLT | gold | MLAS | not measured | 4.68 [3.83, 5.43] | not published | not published |
| lij: UD_Ligurian-GLT | gold | UAS | not measured | 54.80 [52.79, 56.88] | not published | not published |
| lij: UD_Ligurian-GLT | gold | UFeats | not measured | 45.14 [43.85, 46.38] | not published | not published |
| lij: UD_Ligurian-GLT | gold | UPOS | not measured | 56.57 [55.42, 57.72] | not published | not published |
| lij: UD_Ligurian-GLT | raw | CLAS | not measured | 19.82 [18.05, 21.74] | 60.33 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 60.33 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | LAS | not measured | 25.76 [24.05, 27.47] | 69.18 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 69.18 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | Lemmas | not measured | 61.84 [60.48, 63.20] | 92.68 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 92.68 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | MLAS | not measured | 3.31 [2.61, 3.97] | 51.24 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 51.24 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | Sentences | not measured | 34.74 [28.95, 40.53] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | Tokens | not measured | 89.31 [88.32, 90.26] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | UAS | not measured | 37.84 [35.73, 39.98] | 77.61 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 77.61 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | UFeats | not measured | 38.11 [36.98, 39.19] | 88.25 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 88.25 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | UPOS | not measured | 47.86 [46.61, 49.07] | 89.14 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 89.14 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lij: UD_Ligurian-GLT | raw | Words | not measured | 81.74 [80.53, 83.00] | 98.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 98.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | gold | AllTags | not measured | 67.88 [66.97, 68.88] | 89.46 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | CLAS | not measured | 69.77 [68.26, 71.06] | not published | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | LAS | not measured | 72.74 [71.48, 73.89] | 79.92 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | Lemmas | not measured | 80.46 [79.60, 81.34] | 93.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | MLAS | not measured | 41.85 [40.64, 43.11] | 68.90 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | UAS | not measured | 78.38 [77.13, 79.56] | 83.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | UFeats | not measured | 72.11 [71.26, 73.06] | 91.19 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | UPOS | not measured | 88.95 [88.27, 89.65] | 96.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | gold | XPOS | not measured | 72.57 [71.68, 73.49] | 90.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lt: UD_Lithuanian-ALKSNIS | raw | AllTags | not measured | 69.48 [68.60, 70.43] | 93.30 (Trankit large, UD 2.5; test same size) — **MISS** | 85.54 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | CLAS | not measured | 74.53 [73.23, 75.77] | 86.66 (Trankit large, UD 2.5; test same size) — **MISS** | 70.23 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lt: UD_Lithuanian-ALKSNIS | raw | LAS | not measured | 76.39 [75.20, 77.55] | 87.67 (Trankit large, UD 2.5; test same size) — **MISS** | 72.75 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lt: UD_Lithuanian-ALKSNIS | raw | Lemmas | not measured | 81.55 [80.75, 82.31] | 94.30 (Trankit large, UD 2.5; test same size) — **MISS** | 92.95 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | MLAS | not measured | 46.90 [45.62, 48.09] | 79.86 (Trankit large, UD 2.5; test same size) — **MISS** | 60.53 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | Sentences | not measured | 81.03 [77.95, 83.57] | 95.72 (Trankit large, UD 2.5; test same size) — **MISS** | 89.93 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | Tokens | not measured | 99.71 [99.57, 99.82] | 99.94 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.94 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | UAS | not measured | 81.67 [80.47, 82.76] | 90.48 (Trankit large, UD 2.5; test same size) — **MISS** | 77.82 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lt: UD_Lithuanian-ALKSNIS | raw | UFeats | not measured | 73.62 [72.76, 74.51] | 94.46 (Trankit large, UD 2.5; test same size) — **MISS** | 88.16 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | UPOS | not measured | 89.59 [88.93, 90.24] | 97.45 (Trankit large, UD 2.5; test same size) — **MISS** | 93.71 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | Words | not measured | 99.71 [99.57, 99.82] | 99.94 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.94 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lt: UD_Lithuanian-ALKSNIS | raw | XPOS | not measured | 74.08 [73.23, 74.96] | 93.98 (Trankit large, UD 2.5; test same size) — **MISS** | 86.48 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lv: UD_Latvian-LVTB | gold | AllTags | not measured | 62.76 [62.26, 63.23] | 92.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | gold | CLAS | not measured | 74.70 [73.92, 75.41] | not published | not published |
| lv: UD_Latvian-LVTB | gold | LAS | not measured | 78.42 [77.72, 79.07] | 87.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | gold | Lemmas | not measured | 90.81 [90.48, 91.14] | 97.48 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | gold | MLAS | not measured | 50.21 [49.42, 51.02] | 80.23 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | gold | UAS | not measured | 83.67 [83.00, 84.27] | 90.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | gold | UFeats | not measured | 78.48 [78.06, 78.91] | 95.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | gold | UPOS | not measured | 92.03 [91.73, 92.32] | 98.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | gold | XPOS | not measured | 65.93 [65.43, 66.42] | 92.69 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lv: UD_Latvian-LVTB | raw | AllTags | not measured | 64.02 [63.51, 64.51] | 91.60 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.29 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | CLAS | not measured | 79.91 [79.15, 80.62] | 89.78 (Trankit large, UD 2.5; test changed) — **MISS** | 83.37 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | LAS | not measured | 82.85 [82.18, 83.48] | 91.25 (Trankit large, UD 2.5; test changed) — **MISS** | 85.77 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | Lemmas | not measured | 90.83 [90.49, 91.16] | 96.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.12 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | MLAS | not measured | 56.14 [55.35, 56.98] | 82.69 (Trankit large, UD 2.5; test changed) — **MISS** | 76.71 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | Sentences | not measured | 94.39 [93.46, 95.24] | 99.10 (Trankit base, UD 2.5; test changed) — **MISS** | 98.15 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | Tokens | not measured | 98.79 [98.60, 98.97] | 99.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | UAS | not measured | 87.56 [86.95, 88.15] | 93.63 (Trankit large, UD 2.5; test changed) — **MISS** | 88.91 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | UFeats | not measured | 79.94 [79.53, 80.40] | 95.28 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | UPOS | not measured | 91.98 [91.63, 92.29] | 97.61 (Trankit large, UD 2.5; test changed) — **MISS** | 96.70 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | Words | not measured | 98.79 [98.60, 98.97] | 99.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.81 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lv: UD_Latvian-LVTB | raw | XPOS | not measured | 66.51 [65.99, 66.99] | 92.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.72 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| lzh: UD_Classical_Chinese-Kyoto | gold | AllTags | not measured | 54.93 [54.32, 55.53] | 89.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | CLAS | not measured | 63.68 [62.84, 64.54] | not published | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | LAS | not measured | 63.79 [62.96, 64.62] | 79.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | Lemmas | not measured | 98.51 [98.36, 98.66] | 99.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | MLAS | not measured | 53.02 [52.23, 53.89] | 75.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | UAS | not measured | 72.31 [71.47, 73.16] | 84.45 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | UFeats | not measured | 85.19 [84.71, 85.62] | 94.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | UPOS | not measured | 84.48 [84.01, 84.93] | 93.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | gold | XPOS | not measured | 58.82 [58.24, 59.41] | 92.07 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| lzh: UD_Classical_Chinese-Kyoto | raw | AllTags | not measured | 54.83 [54.26, 55.39] | 89.98 (Trankit large, UD 2.5; test changed) — **MISS** | 85.89 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lzh: UD_Classical_Chinese-Kyoto | raw | CLAS | not measured | 63.95 [63.12, 64.79] | 75.60 (Trankit large, UD 2.5; test changed) — **MISS** | 64.54 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lzh: UD_Classical_Chinese-Kyoto | raw | LAS | not measured | 64.12 [63.30, 64.96] | 76.82 (Trankit large, UD 2.5; test changed) — **MISS** | 65.80 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lzh: UD_Classical_Chinese-Kyoto | raw | Lemmas | not measured | 96.66 [96.40, 96.91] | 99.67 (Trankit large, UD 2.5; test changed) — **MISS** | 97.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lzh: UD_Classical_Chinese-Kyoto | raw | MLAS | not measured | 54.27 [53.49, 55.12] | 73.16 (Trankit large, UD 2.5; test changed) — **MISS** | 61.94 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lzh: UD_Classical_Chinese-Kyoto | raw | Sentences | not measured | 94.40 [93.81, 94.96] | 70.58 (Trankit large, UD 2.5; test changed) — within or ahead | 49.57 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lzh: UD_Classical_Chinese-Kyoto | raw | Tokens | not measured | 97.90 [97.67, 98.11] | 99.70 (Trankit large, UD 2.5; test changed) — **MISS** | 97.85 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lzh: UD_Classical_Chinese-Kyoto | raw | UAS | not measured | 71.96 [71.14, 72.75] | 82.03 (Trankit large, UD 2.5; test changed) — **MISS** | 71.32 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lzh: UD_Classical_Chinese-Kyoto | raw | UFeats | not measured | 84.25 [83.79, 84.68] | 94.43 (Trankit large, UD 2.5; test changed) — **MISS** | 91.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lzh: UD_Classical_Chinese-Kyoto | raw | UPOS | not measured | 84.19 [83.73, 84.68] | 92.89 (Trankit large, UD 2.5; test changed) — **MISS** | 89.16 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| lzh: UD_Classical_Chinese-Kyoto | raw | Words | not measured | 97.90 [97.67, 98.11] | 99.70 (Trankit large, UD 2.5; test changed) — **MISS** | 97.85 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| lzh: UD_Classical_Chinese-Kyoto | raw | XPOS | not measured | 58.63 [58.06, 59.17] | 91.90 (Trankit large, UD 2.5; test changed) — **MISS** | 88.17 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mdf: UD_Moksha-JR | gold | AllTags | not measured | 12.50 [11.26, 13.69] | not published | not published |
| mdf: UD_Moksha-JR | gold | CLAS | not measured | 19.82 [18.27, 21.49] | not published | not published |
| mdf: UD_Moksha-JR | gold | LAS | not measured | 25.61 [23.95, 27.43] | not published | not published |
| mdf: UD_Moksha-JR | gold | Lemmas | not measured | 54.49 [53.07, 55.74] | not published | not published |
| mdf: UD_Moksha-JR | gold | MLAS | not measured | 3.51 [2.85, 4.21] | not published | not published |
| mdf: UD_Moksha-JR | gold | UAS | not measured | 46.61 [44.25, 49.02] | not published | not published |
| mdf: UD_Moksha-JR | gold | UFeats | not measured | 33.08 [31.94, 34.17] | not published | not published |
| mdf: UD_Moksha-JR | gold | UPOS | not measured | 48.38 [47.03, 49.73] | not published | not published |
| mdf: UD_Moksha-JR | gold | XPOS | not measured | 33.10 [31.59, 34.54] | not published | not published |
| mdf: UD_Moksha-JR | raw | AllTags | not measured | 21.77 [20.35, 23.14] | not published | not published |
| mdf: UD_Moksha-JR | raw | CLAS | not measured | 25.35 [23.57, 27.09] | not published | not published |
| mdf: UD_Moksha-JR | raw | LAS | not measured | 30.66 [28.78, 32.54] | not published | not published |
| mdf: UD_Moksha-JR | raw | Lemmas | not measured | 55.83 [54.40, 57.02] | not published | not published |
| mdf: UD_Moksha-JR | raw | MLAS | not measured | 5.54 [4.72, 6.43] | not published | not published |
| mdf: UD_Moksha-JR | raw | Sentences | not measured | 51.73 [46.72, 57.11] | not published | not published |
| mdf: UD_Moksha-JR | raw | Tokens | not measured | 99.93 [99.82, 100.00] | not published | not published |
| mdf: UD_Moksha-JR | raw | UAS | not measured | 51.48 [49.02, 53.81] | not published | not published |
| mdf: UD_Moksha-JR | raw | UFeats | not measured | 38.54 [37.34, 39.79] | not published | not published |
| mdf: UD_Moksha-JR | raw | UPOS | not measured | 52.79 [51.42, 54.27] | not published | not published |
| mdf: UD_Moksha-JR | raw | Words | not measured | 99.79 [99.58, 99.95] | not published | not published |
| mdf: UD_Moksha-JR | raw | XPOS | not measured | 41.83 [40.35, 43.45] | not published | not published |
| mr: UD_Marathi-UFAL | gold | CLAS | not measured | 57.49 [49.18, 65.92] | not published | not published |
| mr: UD_Marathi-UFAL | gold | LAS | not measured | 63.59 [55.41, 71.76] | 64.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| mr: UD_Marathi-UFAL | gold | Lemmas | not measured | 73.30 [69.70, 77.23] | 87.86 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mr: UD_Marathi-UFAL | gold | MLAS | not measured | 4.93 [2.60, 7.48] | 40.50 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mr: UD_Marathi-UFAL | gold | UAS | not measured | 74.76 [66.75, 82.95] | 73.06 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| mr: UD_Marathi-UFAL | gold | UFeats | not measured | 38.59 [34.34, 42.44] | 76.94 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mr: UD_Marathi-UFAL | gold | UPOS | not measured | 80.58 [76.30, 84.38] | 88.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mr: UD_Marathi-UFAL | raw | CLAS | not measured | 47.72 [40.09, 55.63] | 59.67 (Trankit large, UD 2.5; test same size) — **MISS** | 54.73 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| mr: UD_Marathi-UFAL | raw | LAS | not measured | 51.52 [42.56, 60.16] | 63.36 (Trankit large, UD 2.5; test same size) — **MISS** | 59.56 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| mr: UD_Marathi-UFAL | raw | Lemmas | not measured | 69.54 [65.28, 74.07] | 84.67 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 82.84 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mr: UD_Marathi-UFAL | raw | MLAS | not measured | 6.22 [3.70, 9.22] | 38.10 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 37.04 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mr: UD_Marathi-UFAL | raw | Sentences | not measured | 83.15 [72.09, 92.31] | 92.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.36 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| mr: UD_Marathi-UFAL | raw | Tokens | not measured | 100.00 [100.00, 100.00] | 99.20 (Trankit large, UD 2.5; test same size) — within or ahead | 97.99 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| mr: UD_Marathi-UFAL | raw | UAS | not measured | 60.91 [51.46, 69.66] | 72.79 (Trankit large, UD 2.5; test same size) — **MISS** | 68.87 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| mr: UD_Marathi-UFAL | raw | UFeats | not measured | 38.32 [33.85, 43.07] | 74.45 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 73.53 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mr: UD_Marathi-UFAL | raw | UPOS | not measured | 74.11 [69.57, 78.53] | 87.79 (Trankit large, UD 2.5; test same size) — **MISS** | 85.78 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mr: UD_Marathi-UFAL | raw | Words | not measured | 86.29 [81.97, 90.71] | 97.22 (Trankit large, UD 2.5; test same size) — **MISS** | 92.89 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | gold | AllTags | not measured | 88.74 [88.04, 89.38] | 95.43 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mt: UD_Maltese-MUDT | gold | CLAS | not measured | 71.21 [69.65, 72.69] | not published | not published |
| mt: UD_Maltese-MUDT | gold | LAS | not measured | 78.33 [77.05, 79.52] | 80.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mt: UD_Maltese-MUDT | gold | MLAS | not measured | 63.68 [62.10, 65.29] | 68.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mt: UD_Maltese-MUDT | gold | UAS | not measured | 83.92 [82.73, 85.04] | 85.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mt: UD_Maltese-MUDT | gold | UFeats | not measured | 100.00 [100.00, 100.00] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| mt: UD_Maltese-MUDT | gold | UPOS | not measured | 93.68 [93.10, 94.18] | 95.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mt: UD_Maltese-MUDT | gold | XPOS | not measured | 90.00 [89.37, 90.64] | 95.90 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| mt: UD_Maltese-MUDT | raw | AllTags | not measured | 65.68 [64.63, 66.79] | 95.35 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 95.35 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | CLAS | not measured | 42.42 [40.64, 44.15] | 70.76 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 70.76 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | LAS | not measured | 45.83 [44.35, 47.41] | 80.04 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 78.22 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | MLAS | not measured | 35.62 [34.05, 37.16] | 68.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 67.20 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | Sentences | not measured | 64.56 [60.41, 69.05] | 86.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 85.35 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | Tokens | not measured | 74.36 [73.25, 75.49] | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | UAS | not measured | 49.18 [47.60, 50.89] | 84.86 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 83.62 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | UFeats | not measured | 74.36 [73.25, 75.49] | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | UPOS | not measured | 69.86 [68.71, 70.99] | 95.78 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.72 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | Words | not measured | 74.36 [73.25, 75.49] | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| mt: UD_Maltese-MUDT | raw | XPOS | not measured | 66.72 [65.56, 67.86] | 95.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.61 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| myv: UD_Erzya-JR | gold | AllTags | not measured | 51.29 [50.32, 52.26] | 74.86 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | gold | CLAS | not measured | 40.87 [39.41, 42.54] | not published | not published |
| myv: UD_Erzya-JR | gold | LAS | not measured | 47.25 [45.94, 48.65] | 64.70 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | gold | Lemmas | not measured | 70.08 [69.19, 70.92] | 84.87 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | gold | MLAS | not measured | 19.12 [18.03, 20.26] | 42.76 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | gold | UAS | not measured | 60.64 [59.17, 62.12] | 74.25 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | gold | UFeats | not measured | 57.01 [56.11, 57.93] | 79.93 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | gold | UPOS | not measured | 75.05 [74.17, 75.92] | 89.25 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | gold | XPOS | not measured | 76.79 [75.93, 77.59] | 88.73 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| myv: UD_Erzya-JR | raw | AllTags | not measured | 52.66 [51.72, 53.53] | 74.38 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 69.52 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | CLAS | not measured | 47.96 [46.52, 49.39] | 52.10 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 52.10 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | LAS | not measured | 54.23 [52.86, 55.57] | 63.92 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 57.35 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | Lemmas | not measured | 71.48 [70.56, 72.36] | 84.72 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 84.72 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | MLAS | not measured | 22.89 [21.77, 24.10] | 42.32 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 36.09 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | Sentences | not measured | 88.78 [86.67, 90.57] | 97.02 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 96.92 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | Tokens | not measured | 99.54 [99.36, 99.69] | 99.63 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 99.63 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| myv: UD_Erzya-JR | raw | UAS | not measured | 68.25 [66.78, 69.66] | 73.33 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 70.08 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | UFeats | not measured | 58.35 [57.40, 59.22] | 79.36 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 75.88 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | UPOS | not measured | 76.37 [75.55, 77.17] | 88.50 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 84.84 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| myv: UD_Erzya-JR | raw | Words | not measured | 98.98 [98.69, 99.22] | 99.12 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | 99.08 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| myv: UD_Erzya-JR | raw | XPOS | not measured | 78.29 [77.49, 79.07] | 88.04 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 83.29 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| nds: UD_Low_Saxon-LSDC | gold | CLAS | not measured | 53.25 [51.60, 54.90] | not published | not published |
| nds: UD_Low_Saxon-LSDC | gold | LAS | not measured | 60.37 [58.92, 61.90] | 65.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | gold | Lemmas | not measured | 80.89 [79.99, 81.80] | 84.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | gold | MLAS | not measured | 24.10 [22.94, 25.34] | 36.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | gold | UAS | not measured | 69.57 [68.00, 71.18] | 75.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | gold | UFeats | not measured | 62.06 [61.10, 63.09] | 72.23 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | gold | UPOS | not measured | 83.92 [83.02, 84.85] | 90.80 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | raw | CLAS | not measured | 63.87 [62.41, 65.69] | not published | not published |
| nds: UD_Low_Saxon-LSDC | raw | LAS | not measured | 69.59 [68.18, 71.11] | 64.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| nds: UD_Low_Saxon-LSDC | raw | Lemmas | not measured | 81.66 [80.81, 82.53] | 83.84 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | raw | MLAS | not measured | 30.05 [28.72, 31.52] | 36.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | raw | Sentences | not measured | 91.08 [88.59, 93.39] | 90.23 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| nds: UD_Low_Saxon-LSDC | raw | Tokens | not measured | 99.65 [99.48, 99.81] | not published | not published |
| nds: UD_Low_Saxon-LSDC | raw | UAS | not measured | 77.79 [76.34, 79.26] | 73.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| nds: UD_Low_Saxon-LSDC | raw | UFeats | not measured | 62.89 [61.93, 63.94] | 71.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | raw | UPOS | not measured | 86.22 [85.42, 87.04] | 90.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nds: UD_Low_Saxon-LSDC | raw | Words | not measured | 99.41 [99.14, 99.65] | 99.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| nl: UD_Dutch-Alpino | gold | AllTags | 94.31 [93.48, 95.00] | 94.13 [93.32, 94.87] | 96.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | gold | CLAS | not measured | 82.88 [81.34, 84.21] | not published | not published |
| nl: UD_Dutch-Alpino | gold | LAS | 87.63 [86.63, 88.59] | 87.24 [86.15, 88.24] | 93.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | gold | Lemmas | 91.88 [91.32, 92.47] | 92.34 [91.80, 92.92] | 96.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | gold | MLAS | not measured | 76.21 [74.34, 77.73] | 86.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | gold | UAS | 90.41 [89.40, 91.32] | 90.23 [89.24, 91.11] | 95.30 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | gold | UFeats | 96.73 [96.16, 97.22] | 96.62 [96.06, 97.12] | 97.75 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | gold | UPOS | 96.89 [96.24, 97.44] | 96.91 [96.21, 97.44] | 98.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | gold | XPOS | 95.09 [94.32, 95.77] | 95.05 [94.26, 95.72] | 97.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| nl: UD_Dutch-Alpino | raw | AllTags | 94.31 [93.58, 95.01] | 94.50 [93.79, 95.22] | 96.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.97 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | CLAS | not measured | 87.99 [86.70, 89.11] | 89.56 (Trankit large, UD 2.5; test same size) — **MISS** | 83.94 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | LAS | 89.06 [87.93, 89.96] | 91.11 [90.20, 91.92] | 92.49 (Trankit large, UD 2.5; test same size) — **MISS** | 88.67 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | Lemmas | 91.54 [90.97, 92.14] | 92.05 [91.49, 92.67] | 96.39 (Trankit large, UD 2.5; test same size) — **MISS** | 95.33 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| nl: UD_Dutch-Alpino | raw | MLAS | not measured | 82.24 [80.73, 83.63] | 85.14 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 79.65 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | Sentences | 94.81 [92.78, 96.42] | 95.90 [94.35, 97.34] | 91.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | 90.08 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | Tokens | 99.66 [99.46, 99.82] | 99.66 [99.46, 99.82] | 99.87 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.87 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| nl: UD_Dutch-Alpino | raw | UAS | 91.67 [90.65, 92.52] | 93.37 [92.55, 94.08] | 94.41 (Trankit large, UD 2.5; test same size) — **MISS** | 91.42 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | UFeats | 96.78 [96.26, 97.27] | 96.80 [96.29, 97.28] | 97.52 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.00 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | UPOS | 96.66 [96.01, 97.23] | 96.74 [96.11, 97.27] | 97.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.75 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| nl: UD_Dutch-Alpino | raw | Words | 99.66 [99.46, 99.82] | 99.66 [99.46, 99.82] | 99.87 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.87 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| nl: UD_Dutch-Alpino | raw | XPOS | 95.03 [94.32, 95.67] | 95.27 [94.61, 95.94] | 96.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.50 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| no: UD_Norwegian-Bokmaal | gold | AllTags | not measured | 79.66 [79.14, 80.14] | 97.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | gold | CLAS | not measured | 77.86 [77.09, 78.70] | not published | not published |
| no: UD_Norwegian-Bokmaal | gold | LAS | not measured | 82.98 [82.32, 83.67] | 93.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | gold | Lemmas | not measured | 94.34 [94.06, 94.60] | 98.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | gold | MLAS | not measured | 54.53 [53.68, 55.36] | 87.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | gold | UAS | not measured | 86.61 [85.96, 87.26] | 94.69 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | gold | UFeats | not measured | 82.49 [82.00, 82.96] | 97.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | gold | UPOS | not measured | 95.93 [95.54, 96.29] | 98.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | gold | XPOS | not measured | 96.63 [96.25, 96.98] | 99.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| no: UD_Norwegian-Bokmaal | raw | AllTags | not measured | 79.29 [78.77, 79.79] | 97.61 (Trankit large, UD 2.5; test same size) — **MISS** | 96.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | CLAS | not measured | 85.08 [84.24, 85.81] | 92.82 (Trankit large, UD 2.5; test same size) — **MISS** | 89.11 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | LAS | not measured | 88.26 [87.58, 88.85] | 94.33 (Trankit large, UD 2.5; test same size) — **MISS** | 91.85 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | Lemmas | not measured | 93.79 [93.49, 94.10] | 98.67 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 98.61 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | MLAS | not measured | 59.86 [58.97, 60.75] | 90.15 (Trankit large, UD 2.5; test same size) — **MISS** | 85.13 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | Sentences | not measured | 92.95 [91.84, 94.03] | 99.20 (Trankit base, UD 2.5; test same size) — **MISS** | 97.70 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | Tokens | not measured | 99.50 [99.38, 99.60] | 99.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | UAS | not measured | 90.80 [90.15, 91.37] | 95.54 (Trankit large, UD 2.5; test same size) — **MISS** | 93.26 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | UFeats | not measured | 81.74 [81.25, 82.21] | 98.07 (Trankit large, UD 2.5; test same size) — **MISS** | 97.09 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | UPOS | not measured | 95.96 [95.58, 96.30] | 98.85 (Trankit large, UD 2.5; test same size) — **MISS** | 98.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | Words | not measured | 99.50 [99.38, 99.60] | 99.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| no: UD_Norwegian-Bokmaal | raw | XPOS | not measured | 96.89 [96.51, 97.22] | 99.88 (Trankit large, UD 2.5; test same size) — **MISS** | 99.04 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| olo: UD_Livvi-KKPP | gold | AllTags | not measured | 3.83 [2.79, 4.84] | not published | not published |
| olo: UD_Livvi-KKPP | gold | CLAS | not measured | 24.64 [21.78, 27.57] | not published | not published |
| olo: UD_Livvi-KKPP | gold | LAS | not measured | 29.37 [26.68, 32.19] | not published | not published |
| olo: UD_Livvi-KKPP | gold | Lemmas | not measured | 47.98 [45.38, 50.23] | not published | not published |
| olo: UD_Livvi-KKPP | gold | MLAS | not measured | 1.41 [0.71, 2.23] | not published | not published |
| olo: UD_Livvi-KKPP | gold | UAS | not measured | 48.05 [44.69, 51.76] | not published | not published |
| olo: UD_Livvi-KKPP | gold | UFeats | not measured | 10.35 [8.70, 11.98] | not published | not published |
| olo: UD_Livvi-KKPP | gold | UPOS | not measured | 36.96 [34.50, 39.59] | not published | not published |
| olo: UD_Livvi-KKPP | gold | XPOS | not measured | 35.69 [32.79, 38.82] | not published | not published |
| olo: UD_Livvi-KKPP | raw | AllTags | not measured | 10.52 [9.33, 11.82] | not published | not published |
| olo: UD_Livvi-KKPP | raw | CLAS | not measured | 29.94 [26.69, 33.16] | not published | not published |
| olo: UD_Livvi-KKPP | raw | LAS | not measured | 32.98 [29.67, 36.26] | not published | not published |
| olo: UD_Livvi-KKPP | raw | Lemmas | not measured | 46.98 [44.31, 49.50] | not published | not published |
| olo: UD_Livvi-KKPP | raw | MLAS | not measured | 1.92 [1.11, 2.81] | not published | not published |
| olo: UD_Livvi-KKPP | raw | Sentences | not measured | 26.94 [19.19, 35.54] | not published | not published |
| olo: UD_Livvi-KKPP | raw | Tokens | not measured | 98.86 [98.15, 99.49] | not published | not published |
| olo: UD_Livvi-KKPP | raw | UAS | not measured | 51.34 [47.43, 55.19] | not published | not published |
| olo: UD_Livvi-KKPP | raw | UFeats | not measured | 14.75 [13.25, 16.50] | not published | not published |
| olo: UD_Livvi-KKPP | raw | UPOS | not measured | 44.37 [41.50, 47.23] | not published | not published |
| olo: UD_Livvi-KKPP | raw | Words | not measured | 98.86 [98.15, 99.49] | not published | not published |
| olo: UD_Livvi-KKPP | raw | XPOS | not measured | 42.23 [39.39, 45.42] | not published | not published |
| orv: UD_Old_East_Slavic-RNC | gold | AllTags | not measured | 0.18 [0.13, 0.23] | 82.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | gold | CLAS | not measured | 59.04 [57.82, 60.34] | not published | not published |
| orv: UD_Old_East_Slavic-RNC | gold | LAS | not measured | 66.62 [65.53, 67.76] | 78.58 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | gold | Lemmas | not measured | 79.36 [78.68, 80.01] | 90.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | gold | MLAS | not measured | 25.71 [24.47, 26.88] | 60.21 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | gold | UAS | not measured | 71.64 [70.28, 72.92] | 82.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | gold | UFeats | not measured | 64.39 [63.59, 65.16] | 90.38 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | gold | UPOS | not measured | 90.98 [90.54, 91.45] | 98.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | gold | XPOS | not measured | 0.77 [0.61, 0.96] | 91.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| orv: UD_Old_East_Slavic-RNC | raw | AllTags | not measured | 0.18 [0.13, 0.23] | 82.43 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 64.40 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | CLAS | not measured | 58.45 [56.77, 60.09] | 51.90 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 51.90 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| orv: UD_Old_East_Slavic-RNC | raw | LAS | not measured | 65.14 [63.69, 66.63] | 77.10 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 60.81 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| orv: UD_Old_East_Slavic-RNC | raw | Lemmas | not measured | 76.43 [75.40, 77.44] | 90.60 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 81.04 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | MLAS | not measured | 26.36 [25.18, 27.55] | 58.83 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 33.25 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | Sentences | not measured | 77.71 [74.93, 80.37] | 94.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.29 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | Tokens | not measured | 94.29 [93.17, 95.25] | 99.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | UAS | not measured | 69.06 [67.51, 70.59] | 80.58 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 68.10 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| orv: UD_Old_East_Slavic-RNC | raw | UFeats | not measured | 62.63 [61.64, 63.62] | 90.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 72.39 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | UPOS | not measured | 87.23 [86.10, 88.24] | 97.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 91.04 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | Words | not measured | 94.29 [93.17, 95.25] | 99.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| orv: UD_Old_East_Slavic-RNC | raw | XPOS | not measured | 0.74 [0.59, 0.92] | 91.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.53 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ota: UD_Ottoman_Turkish-BOUN | gold | AllTags | not measured | 22.38 [21.42, 23.47] | 71.24 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | CLAS | not measured | 49.18 [47.57, 51.01] | not published | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | LAS | not measured | 53.30 [51.69, 55.05] | 53.06 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | Lemmas | not measured | 71.93 [70.73, 73.16] | 82.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | MLAS | not measured | 24.74 [23.36, 26.21] | 33.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | UAS | not measured | 63.25 [61.59, 65.00] | 64.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | UFeats | not measured | 65.77 [64.62, 66.99] | 80.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | UPOS | not measured | 87.06 [86.18, 87.91] | 86.18 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | gold | XPOS | not measured | 36.50 [35.42, 37.62] | 90.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | AllTags | not measured | 22.74 [21.81, 23.78] | 70.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | CLAS | not measured | 56.78 [55.03, 58.62] | not published | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | LAS | not measured | 60.16 [58.55, 61.82] | 52.37 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | Lemmas | not measured | 71.73 [70.53, 72.92] | 81.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | MLAS | not measured | 29.74 [28.17, 31.21] | 32.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | Sentences | not measured | 86.01 [82.52, 89.41] | 87.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | Tokens | not measured | 99.52 [99.24, 99.77] | not published | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | UAS | not measured | 70.12 [68.62, 71.73] | 63.60 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | UFeats | not measured | 68.04 [66.90, 69.24] | 80.51 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | UPOS | not measured | 86.51 [85.54, 87.45] | 85.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | Words | not measured | 99.32 [99.02, 99.61] | 99.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| ota: UD_Ottoman_Turkish-BOUN | raw | XPOS | not measured | 36.82 [35.72, 37.92] | 90.42 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pa: UD_Punjabi-Rang | gold | CLAS | not measured | 45.92 [39.05, 53.05] | not published | not published |
| pa: UD_Punjabi-Rang | gold | LAS | not measured | 49.00 [42.61, 55.25] | not published | not published |
| pa: UD_Punjabi-Rang | gold | Lemmas | not measured | 79.00 [74.77, 82.86] | not published | not published |
| pa: UD_Punjabi-Rang | gold | MLAS | not measured | 12.24 [8.85, 16.26] | not published | not published |
| pa: UD_Punjabi-Rang | gold | UAS | not measured | 66.33 [60.36, 72.85] | not published | not published |
| pa: UD_Punjabi-Rang | gold | UFeats | not measured | 58.33 [53.18, 62.91] | not published | not published |
| pa: UD_Punjabi-Rang | gold | UPOS | not measured | 74.33 [69.86, 79.44] | not published | not published |
| pa: UD_Punjabi-Rang | raw | CLAS | not measured | 43.55 [36.02, 51.61] | not published | not published |
| pa: UD_Punjabi-Rang | raw | LAS | not measured | 47.67 [41.55, 54.04] | not published | not published |
| pa: UD_Punjabi-Rang | raw | Lemmas | not measured | 79.00 [74.52, 82.86] | not published | not published |
| pa: UD_Punjabi-Rang | raw | MLAS | not measured | 10.75 [5.78, 16.62] | not published | not published |
| pa: UD_Punjabi-Rang | raw | Sentences | not measured | 48.78 [28.57, 68.29] | not published | not published |
| pa: UD_Punjabi-Rang | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| pa: UD_Punjabi-Rang | raw | UAS | not measured | 62.33 [54.87, 69.20] | not published | not published |
| pa: UD_Punjabi-Rang | raw | UFeats | not measured | 61.67 [57.29, 66.08] | not published | not published |
| pa: UD_Punjabi-Rang | raw | UPOS | not measured | 71.33 [66.67, 75.68] | not published | not published |
| pa: UD_Punjabi-Rang | raw | Words | not measured | 100.00 [100.00, 100.00] | not published | not published |
| pcm: UD_Naija-NSC | gold | CLAS | not measured | 72.92 [71.48, 74.35] | not published | not published |
| pcm: UD_Naija-NSC | gold | LAS | not measured | 77.80 [76.59, 79.04] | 90.03 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pcm: UD_Naija-NSC | gold | Lemmas | not measured | 98.18 [97.90, 98.45] | 99.31 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pcm: UD_Naija-NSC | gold | MLAS | not measured | 62.01 [60.37, 63.61] | 84.13 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pcm: UD_Naija-NSC | gold | UAS | not measured | 83.76 [82.61, 84.95] | 92.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pcm: UD_Naija-NSC | gold | UFeats | not measured | 94.78 [94.33, 95.23] | 98.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pcm: UD_Naija-NSC | gold | UPOS | not measured | 93.41 [92.94, 93.86] | 98.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pcm: UD_Naija-NSC | raw | CLAS | not measured | 70.47 [68.80, 71.95] | 88.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 88.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | LAS | not measured | 70.52 [69.09, 71.90] | 89.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.56 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | Lemmas | not measured | 91.69 [91.06, 92.29] | 99.28 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.14 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | MLAS | not measured | 59.40 [57.82, 60.86] | 86.43 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 86.43 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | Sentences | not measured | 79.29 [76.93, 81.60] | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 100.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | Tokens | not measured | 92.84 [92.26, 93.41] | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | UAS | not measured | 75.89 [74.49, 77.33] | 92.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 92.39 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | UFeats | not measured | 87.98 [87.21, 88.74] | 98.90 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 98.84 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | UPOS | not measured | 87.23 [86.44, 88.00] | 98.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.55 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pcm: UD_Naija-NSC | raw | Words | not measured | 92.84 [92.26, 93.41] | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pl: UD_Polish-MPDT | gold | AllTags | not measured | 58.11 [57.06, 59.12] | 88.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | gold | CLAS | not measured | 59.62 [57.97, 61.33] | not published | not published |
| pl: UD_Polish-MPDT | gold | LAS | not measured | 65.29 [63.82, 66.70] | 78.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | gold | Lemmas | not measured | 84.84 [84.10, 85.62] | 96.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | gold | MLAS | not measured | 29.31 [27.92, 30.67] | 63.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | gold | UAS | not measured | 72.40 [70.93, 73.72] | 82.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | gold | UFeats | not measured | 68.09 [67.09, 69.07] | 91.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | gold | UPOS | not measured | 87.29 [86.49, 88.03] | 96.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | gold | XPOS | not measured | 66.19 [65.19, 67.25] | 90.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | AllTags | not measured | 59.11 [58.14, 60.11] | 86.51 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | CLAS | not measured | 63.52 [61.84, 65.04] | not published | not published |
| pl: UD_Polish-MPDT | raw | LAS | not measured | 67.83 [66.27, 69.25] | 75.06 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | Lemmas | not measured | 83.03 [82.17, 83.94] | 94.70 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | MLAS | not measured | 31.42 [30.02, 32.70] | 60.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | Sentences | not measured | 75.73 [71.70, 79.64] | 87.46 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | Tokens | not measured | 98.93 [98.65, 99.20] | not published | not published |
| pl: UD_Polish-MPDT | raw | UAS | not measured | 73.27 [71.78, 74.66] | 78.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | UFeats | not measured | 68.13 [67.15, 69.12] | 89.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | UPOS | not measured | 86.08 [85.21, 86.88] | 94.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | Words | not measured | 96.80 [96.29, 97.28] | 98.22 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pl: UD_Polish-MPDT | raw | XPOS | not measured | 66.98 [66.01, 68.05] | 88.43 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | gold | CLAS | not measured | 58.36 [56.05, 60.78] | not published | not published |
| pro: UD_Old_Occitan-CorAG | gold | LAS | not measured | 66.97 [64.71, 69.20] | 80.85 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | gold | MLAS | not measured | 42.81 [40.62, 44.80] | 69.04 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | gold | UAS | not measured | 72.61 [70.28, 74.89] | 83.90 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | gold | UFeats | not measured | 88.11 [87.08, 89.06] | 97.02 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | gold | UPOS | not measured | 88.64 [87.74, 89.47] | 96.83 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | raw | CLAS | not measured | 57.05 [54.28, 59.82] | not published | not published |
| pro: UD_Old_Occitan-CorAG | raw | LAS | not measured | 62.08 [59.46, 64.73] | 74.80 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | raw | MLAS | not measured | 40.76 [38.51, 43.02] | 63.84 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | raw | Sentences | not measured | 57.74 [51.69, 63.62] | 83.64 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | raw | Tokens | not measured | 95.35 [94.57, 96.17] | not published | not published |
| pro: UD_Old_Occitan-CorAG | raw | UAS | not measured | 66.36 [63.69, 69.18] | 77.79 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | raw | UFeats | not measured | 80.84 [79.60, 82.01] | 95.37 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | raw | UPOS | not measured | 82.51 [81.42, 83.58] | 95.25 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pro: UD_Old_Occitan-CorAG | raw | Words | not measured | 91.16 [90.34, 91.98] | 98.55 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| pt: UD_Portuguese-Bosque | gold | CLAS | not measured | 80.24 [79.51, 81.07] | not published | not published |
| pt: UD_Portuguese-Bosque | gold | LAS | 84.01 [83.39, 84.63] | 84.54 [83.90, 85.14] | 90.93 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pt: UD_Portuguese-Bosque | gold | Lemmas | 97.49 [97.30, 97.70] | 97.59 [97.39, 97.79] | 98.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pt: UD_Portuguese-Bosque | gold | MLAS | not measured | 70.29 [69.36, 71.22] | 81.55 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pt: UD_Portuguese-Bosque | gold | UAS | 87.64 [87.07, 88.25] | 88.22 [87.63, 88.77] | 93.31 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pt: UD_Portuguese-Bosque | gold | UFeats | 94.80 [94.49, 95.09] | 94.48 [94.16, 94.80] | 97.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pt: UD_Portuguese-Bosque | gold | UPOS | 96.56 [96.32, 96.80] | 96.10 [95.82, 96.36] | 98.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| pt: UD_Portuguese-Bosque | raw | CLAS | not measured | 85.27 [84.54, 85.99] | 87.75 (Trankit large, UD 2.5; test changed) — **MISS** | 82.66 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| pt: UD_Portuguese-Bosque | raw | LAS | 86.47 [85.84, 87.04] | 87.98 [87.39, 88.55] | 90.91 (Trankit large, UD 2.5; test changed) — **MISS** | 87.75 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| pt: UD_Portuguese-Bosque | raw | Lemmas | 96.96 [96.73, 97.19] | 97.29 [97.09, 97.50] | 98.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 98.04 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pt: UD_Portuguese-Bosque | raw | MLAS | not measured | 76.55 [75.69, 77.41] | 81.09 (Trankit large, UD 2.5; test changed) — **MISS** | 77.07 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| pt: UD_Portuguese-Bosque | raw | Sentences | 92.27 [90.68, 93.82] | 93.50 [92.14, 94.81] | 97.48 (Trankit base, UD 2.5; test changed) — **MISS** | 93.10 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| pt: UD_Portuguese-Bosque | raw | Tokens | 99.86 [99.79, 99.92] | 99.86 [99.79, 99.92] | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.85 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| pt: UD_Portuguese-Bosque | raw | UAS | 89.38 [88.77, 89.91] | 90.68 [90.16, 91.19] | 93.31 (Trankit large, UD 2.5; test changed) — **MISS** | 90.59 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| pt: UD_Portuguese-Bosque | raw | UFeats | 95.06 [94.75, 95.34] | 95.19 [94.90, 95.48] | 96.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.44 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pt: UD_Portuguese-Bosque | raw | UPOS | 96.59 [96.32, 96.83] | 96.75 [96.50, 96.98] | 97.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.45 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| pt: UD_Portuguese-Bosque | raw | Words | 99.26 [99.14, 99.38] | 99.26 [99.14, 99.38] | 99.76 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.76 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| qte: UD_Telugu_English-TECT | gold | AllTags | not measured | 67.47 [60.24, 74.85] | not published | not published |
| qte: UD_Telugu_English-TECT | gold | CLAS | not measured | 64.31 [55.17, 73.90] | not published | not published |
| qte: UD_Telugu_English-TECT | gold | LAS | not measured | 68.67 [61.11, 76.54] | not published | not published |
| qte: UD_Telugu_English-TECT | gold | MLAS | not measured | 37.65 [29.43, 46.81] | not published | not published |
| qte: UD_Telugu_English-TECT | gold | UAS | not measured | 86.14 [79.29, 92.36] | not published | not published |
| qte: UD_Telugu_English-TECT | gold | UFeats | not measured | 99.40 [98.18, 100.00] | not published | not published |
| qte: UD_Telugu_English-TECT | gold | UPOS | not measured | 68.07 [61.08, 75.00] | not published | not published |
| qte: UD_Telugu_English-TECT | gold | XPOS | not measured | 68.07 [60.95, 75.61] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | AllTags | not measured | 80.12 [72.88, 87.72] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | CLAS | not measured | 62.90 [51.38, 73.90] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | LAS | not measured | 69.88 [60.00, 79.53] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | MLAS | not measured | 55.65 [44.44, 67.78] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | Sentences | not measured | 83.58 [71.64, 94.12] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | UAS | not measured | 85.54 [76.79, 92.99] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | UFeats | not measured | 99.40 [98.18, 100.00] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | UPOS | not measured | 80.72 [73.84, 87.80] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | Words | not measured | 100.00 [100.00, 100.00] | not published | not published |
| qte: UD_Telugu_English-TECT | raw | XPOS | not measured | 80.12 [72.88, 87.72] | not published | not published |
| ro: UD_Romanian-RRT | gold | AllTags | not measured | 73.10 [72.40, 73.85] | 97.19 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | gold | CLAS | not measured | 76.60 [75.57, 77.61] | not published | not published |
| ro: UD_Romanian-RRT | gold | LAS | not measured | 80.28 [79.46, 81.10] | 89.53 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | gold | Lemmas | not measured | 94.09 [93.64, 94.54] | 98.36 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | gold | MLAS | not measured | 48.34 [47.20, 49.55] | 82.89 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | gold | UAS | not measured | 85.60 [84.82, 86.33] | 92.77 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | gold | UFeats | not measured | 78.09 [77.38, 78.83] | 97.64 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | gold | UPOS | not measured | 93.35 [92.92, 93.78] | 98.14 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | gold | XPOS | not measured | 83.58 [82.95, 84.21] | 97.48 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| ro: UD_Romanian-RRT | raw | AllTags | not measured | 78.66 [77.97, 79.39] | 97.11 (Trankit large, UD 2.5; test changed) — **MISS** | 96.74 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | CLAS | not measured | 79.77 [78.67, 80.94] | 86.61 (Trankit large, UD 2.5; test changed) — **MISS** | 82.99 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | LAS | not measured | 82.32 [81.33, 83.35] | 89.50 (Trankit large, UD 2.5; test changed) — **MISS** | 86.86 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | Lemmas | not measured | 92.22 [91.71, 92.76] | 98.10 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | 97.42 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | MLAS | not measured | 57.93 [56.55, 59.29] | 82.78 (Trankit large, UD 2.5; test changed) — **MISS** | 79.90 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | Sentences | not measured | 89.05 [86.86, 91.15] | 98.49 (Trankit large, UD 2.5; test changed) — **MISS** | 95.72 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | Tokens | not measured | 96.96 [96.60, 97.31] | 99.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | UAS | not measured | 86.02 [85.08, 86.94] | 93.60 (Trankit large, UD 2.5; test changed) — **MISS** | 90.70 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | UFeats | not measured | 81.68 [81.06, 82.39] | 97.44 (Trankit base, UD 2.5; test changed) — **MISS** | 97.10 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | UPOS | not measured | 91.67 [91.16, 92.16] | 97.90 (Trankit large, UD 2.5; test changed) — **MISS** | 97.50 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | Words | not measured | 96.96 [96.60, 97.31] | 99.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 99.73 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ro: UD_Romanian-RRT | raw | XPOS | not measured | 85.00 [84.38, 85.66] | 97.35 (Trankit large, UD 2.5; test changed) — **MISS** | 96.80 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| ru: UD_Russian-GSD | gold | AllTags | not measured | 66.51 [65.46, 67.52] | 92.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | gold | CLAS | not measured | 73.03 [70.98, 74.74] | not published | not published |
| ru: UD_Russian-GSD | gold | LAS | not measured | 75.27 [72.81, 77.18] | 90.19 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | gold | Lemmas | not measured | 87.93 [87.13, 88.70] | 97.58 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | gold | MLAS | not measured | 41.80 [40.05, 43.56] | 81.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | gold | UAS | not measured | 80.60 [78.19, 82.47] | 93.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | gold | UFeats | not measured | 70.83 [69.82, 71.76] | 94.18 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | gold | UPOS | not measured | 91.68 [91.13, 92.27] | 98.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | gold | XPOS | not measured | 92.12 [91.33, 92.87] | 97.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ru: UD_Russian-GSD | raw | AllTags | not measured | 65.75 [63.92, 67.53] | 94.73 (Trankit large, UD 2.5; test same size) — **MISS** | 92.76 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | CLAS | not measured | 82.94 [80.87, 84.72] | 88.71 (Trankit large, UD 2.5; test same size) — **MISS** | 83.91 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| ru: UD_Russian-GSD | raw | LAS | not measured | 78.76 [75.58, 81.39] | 89.75 (Trankit large, UD 2.5; test same size) — **MISS** | 85.89 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | Lemmas | not measured | 83.84 [81.63, 85.79] | 97.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 95.51 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | MLAS | not measured | 51.94 [49.99, 53.69] | 82.92 (Trankit large, UD 2.5; test same size) — **MISS** | 77.00 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | Sentences | not measured | 92.26 [90.07, 94.24] | 99.25 (Trankit large, UD 2.5; test same size) — **MISS** | 97.33 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | Tokens | not measured | 92.86 [90.99, 94.49] | 99.79 (Trankit large, UD 2.5; test same size) — **MISS** | 99.58 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | UAS | not measured | 82.15 [78.95, 84.74] | 92.70 (Trankit large, UD 2.5; test same size) — **MISS** | 89.56 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | UFeats | not measured | 69.12 [67.53, 70.70] | 95.78 (Trankit large, UD 2.5; test same size) — **MISS** | 93.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | UPOS | not measured | 86.36 [84.48, 88.05] | 98.25 (Trankit large, UD 2.5; test same size) — **MISS** | 97.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | Words | not measured | 92.86 [90.99, 94.49] | 99.79 (Trankit large, UD 2.5; test same size) — **MISS** | 99.58 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ru: UD_Russian-GSD | raw | XPOS | not measured | 87.73 [85.60, 89.58] | 97.89 (Trankit large, UD 2.5; test same size) — **MISS** | 97.41 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sa: UD_Sanskrit-Vedic | gold | CLAS | not measured | 32.81 [31.76, 33.95] | not published | not published |
| sa: UD_Sanskrit-Vedic | gold | LAS | not measured | 34.05 [33.06, 35.15] | 68.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sa: UD_Sanskrit-Vedic | gold | Lemmas | not measured | 75.77 [75.04, 76.50] | 93.50 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sa: UD_Sanskrit-Vedic | gold | MLAS | not measured | 17.76 [17.00, 18.55] | 60.40 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sa: UD_Sanskrit-Vedic | gold | UAS | not measured | 50.53 [49.31, 51.78] | 77.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sa: UD_Sanskrit-Vedic | gold | UFeats | not measured | 51.29 [50.48, 52.14] | 90.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sa: UD_Sanskrit-Vedic | gold | UPOS | not measured | 70.59 [69.80, 71.37] | 93.83 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sa: UD_Sanskrit-Vedic | raw | CLAS | not measured | 19.56 [18.68, 20.43] | 49.90 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 49.90 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | LAS | not measured | 21.48 [20.62, 22.37] | 56.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 50.64 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | Lemmas | not measured | 75.57 [74.82, 76.32] | 93.45 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 88.92 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | MLAS | not measured | 11.55 [10.94, 12.20] | 49.07 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 43.27 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | Sentences | not measured | 7.22 [6.04, 8.52] | 32.93 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** | 32.93 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | Tokens | not measured | 100.00 [100.00, 100.00] | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| sa: UD_Sanskrit-Vedic | raw | UAS | not measured | 32.91 [31.88, 33.99] | 65.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 62.52 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | UFeats | not measured | 51.03 [50.19, 51.82] | 89.24 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.70 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | UPOS | not measured | 70.54 [69.75, 71.32] | 93.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.19 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| sa: UD_Sanskrit-Vedic | raw | Words | not measured | 100.00 [100.00, 100.00] | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead | 100.00 (Stanza 1.5.1, UD 2.12; test changed) — within or ahead |
| say: UD_Zaar-Autogramm | gold | CLAS | not measured | 37.35 [35.92, 38.67] | not published | not published |
| say: UD_Zaar-Autogramm | gold | LAS | not measured | 49.53 [48.16, 50.82] | 73.34 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | gold | Lemmas | not measured | 76.93 [76.12, 77.76] | 87.42 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | gold | MLAS | not measured | 17.13 [16.12, 18.16] | 53.71 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | gold | UAS | not measured | 67.81 [66.22, 69.22] | 83.04 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | gold | UFeats | not measured | 75.96 [75.11, 76.84] | 91.37 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | gold | UPOS | not measured | 66.51 [65.55, 67.43] | 90.67 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | CLAS | not measured | 24.67 [23.21, 26.09] | not published | not published |
| say: UD_Zaar-Autogramm | raw | LAS | not measured | 36.26 [34.74, 37.74] | 62.81 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | Lemmas | not measured | 68.34 [67.29, 69.42] | 82.47 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | MLAS | not measured | 12.24 [11.27, 13.22] | 43.14 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | Sentences | not measured | 63.56 [60.64, 66.19] | 98.93 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | Tokens | not measured | 97.49 [97.07, 97.92] | not published | not published |
| say: UD_Zaar-Autogramm | raw | UAS | not measured | 49.09 [47.38, 50.77] | 70.99 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | UFeats | not measured | 66.79 [65.80, 67.82] | 85.78 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | UPOS | not measured | 62.38 [61.35, 63.50] | 84.94 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| say: UD_Zaar-Autogramm | raw | Words | not measured | 87.48 [86.71, 88.26] | 94.06 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| sd: UD_Sindhi-Isra | gold | AllTags | not measured | 54.53 [53.45, 55.53] | 69.11 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | gold | CLAS | not measured | 62.26 [60.63, 63.73] | not published | not published |
| sd: UD_Sindhi-Isra | gold | LAS | not measured | 68.88 [67.40, 70.23] | 76.42 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | gold | Lemmas | not measured | 95.63 [95.19, 96.02] | 94.88 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sd: UD_Sindhi-Isra | gold | MLAS | not measured | 25.46 [24.16, 26.65] | 41.22 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | gold | UAS | not measured | 79.54 [78.22, 80.86] | 85.51 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | gold | UFeats | not measured | 58.35 [57.29, 59.41] | 71.23 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | gold | UPOS | not measured | 88.18 [87.42, 88.98] | 93.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | gold | XPOS | not measured | 87.16 [86.35, 87.93] | 91.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | AllTags | not measured | 56.13 [55.01, 57.17] | 68.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | CLAS | not measured | 61.01 [59.53, 62.49] | not published | not published |
| sd: UD_Sindhi-Isra | raw | LAS | not measured | 68.19 [66.86, 69.52] | 74.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | Lemmas | not measured | 95.89 [95.44, 96.28] | 94.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sd: UD_Sindhi-Isra | raw | MLAS | not measured | 27.07 [25.83, 28.33] | 39.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | Sentences | not measured | 71.46 [68.49, 74.43] | 88.28 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | Tokens | not measured | 99.99 [99.96, 100.00] | not published | not published |
| sd: UD_Sindhi-Isra | raw | UAS | not measured | 78.38 [77.10, 79.68] | 83.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | UFeats | not measured | 60.15 [59.02, 61.21] | 71.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | UPOS | not measured | 88.87 [88.14, 89.57] | 93.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sd: UD_Sindhi-Isra | raw | Words | not measured | 99.99 [99.96, 100.00] | 99.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sd: UD_Sindhi-Isra | raw | XPOS | not measured | 87.00 [86.22, 87.72] | 91.83 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sdh: UD_Southern_Kurdish-Garrusi | gold | CLAS | not measured | 48.57 [38.78, 58.56] | not published | not published |
| sdh: UD_Southern_Kurdish-Garrusi | gold | LAS | not measured | 54.41 [43.84, 65.00] | 68.38 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sdh: UD_Southern_Kurdish-Garrusi | gold | Lemmas | not measured | 59.56 [51.64, 66.43] | 75.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sdh: UD_Southern_Kurdish-Garrusi | gold | MLAS | not measured | 23.81 [16.41, 32.22] | 45.63 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sdh: UD_Southern_Kurdish-Garrusi | gold | UAS | not measured | 72.79 [60.51, 84.80] | 76.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sdh: UD_Southern_Kurdish-Garrusi | gold | UFeats | not measured | 61.03 [54.17, 69.05] | 78.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sdh: UD_Southern_Kurdish-Garrusi | gold | UPOS | not measured | 62.50 [55.40, 70.97] | 83.82 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | CLAS | not measured | 59.70 [49.09, 73.26] | not published | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | LAS | not measured | 63.43 [52.99, 76.52] | 62.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | Lemmas | not measured | 58.96 [50.22, 65.49] | 67.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | MLAS | not measured | 46.77 [37.17, 58.76] | 40.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | Sentences | not measured | 100.00 [100.00, 100.00] | 92.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | UAS | not measured | 72.39 [61.09, 83.40] | 68.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | UFeats | not measured | 72.39 [64.79, 81.27] | 72.92 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | UPOS | not measured | 74.63 [68.10, 83.33] | 77.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sdh: UD_Southern_Kurdish-Garrusi | raw | Words | not measured | 95.52 [90.99, 98.92] | 90.25 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sk: UD_Slovak-SNK | gold | AllTags | not measured | 62.68 [61.73, 63.55] | 89.19 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | gold | CLAS | not measured | 77.40 [76.24, 78.53] | not published | not published |
| sk: UD_Slovak-SNK | gold | LAS | not measured | 80.00 [79.04, 80.95] | 91.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | gold | Lemmas | not measured | 87.30 [86.65, 87.95] | 96.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | gold | MLAS | not measured | 45.69 [44.38, 46.86] | 81.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | gold | UAS | not measured | 84.02 [83.09, 84.86] | 93.75 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | gold | UFeats | not measured | 70.16 [69.26, 71.01] | 93.31 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | gold | UPOS | not measured | 91.25 [90.73, 91.76] | 97.83 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | gold | XPOS | not measured | 67.51 [66.60, 68.37] | 90.18 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sk: UD_Slovak-SNK | raw | AllTags | not measured | 68.26 [67.41, 69.12] | 90.20 (Trankit large, UD 2.5; test changed) — **MISS** | 87.35 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sk: UD_Slovak-SNK | raw | CLAS | not measured | 87.18 [86.26, 88.16] | 95.04 (Trankit large, UD 2.5; test changed) — **MISS** | 84.43 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sk: UD_Slovak-SNK | raw | LAS | not measured | 88.48 [87.67, 89.31] | 95.60 (Trankit large, UD 2.5; test changed) — **MISS** | 86.58 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sk: UD_Slovak-SNK | raw | Lemmas | not measured | 91.29 [90.75, 91.76] | 96.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.08 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sk: UD_Slovak-SNK | raw | MLAS | not measured | 55.91 [54.76, 57.19] | 87.64 (Trankit large, UD 2.5; test changed) — **MISS** | 74.72 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sk: UD_Slovak-SNK | raw | Sentences | not measured | 86.76 [84.86, 88.51] | 98.49 (Trankit large, UD 2.5; test changed) — **MISS** | 84.31 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sk: UD_Slovak-SNK | raw | Tokens | not measured | 99.97 [99.93, 100.00] | 99.96 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.96 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sk: UD_Slovak-SNK | raw | UAS | not measured | 90.75 [90.04, 91.51] | 96.97 (Trankit large, UD 2.5; test changed) — **MISS** | 89.13 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sk: UD_Slovak-SNK | raw | UFeats | not measured | 75.33 [74.50, 76.16] | 95.22 (Trankit large, UD 2.5; test changed) — **MISS** | 90.95 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sk: UD_Slovak-SNK | raw | UPOS | not measured | 92.17 [91.70, 92.63] | 97.90 (Trankit large, UD 2.5; test changed) — **MISS** | 96.34 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sk: UD_Slovak-SNK | raw | Words | not measured | 99.93 [99.88, 99.97] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.96 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sk: UD_Slovak-SNK | raw | XPOS | not measured | 71.71 [70.86, 72.58] | 90.82 (Trankit large, UD 2.5; test changed) — **MISS** | 88.19 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | gold | AllTags | not measured | 74.45 [73.88, 75.05] | 96.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | gold | CLAS | not measured | 77.48 [76.61, 78.37] | not published | not published |
| sl: UD_Slovenian-SSJ | gold | LAS | not measured | 81.88 [81.19, 82.57] | 93.10 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | gold | Lemmas | not measured | 92.93 [92.58, 93.27] | 98.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | gold | MLAS | not measured | 50.13 [49.19, 51.12] | 87.50 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | gold | UAS | not measured | 86.44 [85.81, 87.04] | 94.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | gold | UFeats | not measured | 78.15 [77.59, 78.70] | 97.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | gold | UPOS | not measured | 93.28 [92.91, 93.60] | 98.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | gold | XPOS | not measured | 78.26 [77.75, 78.80] | 97.10 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sl: UD_Slovenian-SSJ | raw | AllTags | not measured | 77.13 [76.59, 77.70] | 97.60 (Trankit large, UD 2.5; test changed) — **MISS** | 95.37 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | CLAS | not measured | 85.70 [84.98, 86.50] | 94.88 (Trankit large, UD 2.5; test changed) — **MISS** | 88.28 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | LAS | not measured | 88.30 [87.67, 89.00] | 96.06 (Trankit large, UD 2.5; test changed) — **MISS** | 90.80 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | Lemmas | not measured | 93.06 [92.68, 93.44] | 98.69 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.69 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | MLAS | not measured | 58.69 [57.81, 59.66] | 91.78 (Trankit large, UD 2.5; test changed) — **MISS** | 83.80 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | Sentences | not measured | 96.78 [95.80, 97.68] | 100.00 (Trankit large, UD 2.5; test changed) — **MISS** | 99.26 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | Tokens | not measured | 99.20 [98.99, 99.41] | 99.97 (Trankit large, UD 2.5; test changed) — **MISS** | 99.93 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | UAS | not measured | 91.24 [90.64, 91.85] | 96.91 (Trankit large, UD 2.5; test changed) — **MISS** | 92.45 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | UFeats | not measured | 80.00 [79.46, 80.54] | 97.99 (Trankit large, UD 2.5; test changed) — **MISS** | 96.09 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | UPOS | not measured | 93.49 [93.10, 93.84] | 99.24 (Trankit large, UD 2.5; test changed) — **MISS** | 98.41 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | Words | not measured | 99.20 [98.99, 99.41] | 99.97 (Trankit large, UD 2.5; test changed) — **MISS** | 99.93 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sl: UD_Slovenian-SSJ | raw | XPOS | not measured | 80.09 [79.60, 80.64] | 97.83 (Trankit large, UD 2.5; test changed) — **MISS** | 95.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | gold | AllTags | not measured | 62.16 [61.15, 63.19] | 85.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | gold | CLAS | not measured | 52.19 [50.64, 53.68] | not published | not published |
| sme: UD_North_Sami-Giella | gold | LAS | not measured | 55.43 [53.91, 56.87] | 71.52 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | gold | Lemmas | not measured | 73.86 [72.93, 74.78] | 87.05 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | gold | MLAS | not measured | 30.82 [29.58, 32.05] | 60.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | gold | UAS | not measured | 64.22 [62.61, 65.69] | 76.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | gold | UFeats | not measured | 69.28 [68.34, 70.28] | 89.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | gold | UPOS | not measured | 81.63 [80.70, 82.42] | 91.94 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | gold | XPOS | not measured | 84.71 [83.93, 85.40] | 93.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sme: UD_North_Sami-Giella | raw | AllTags | not measured | 65.57 [64.57, 66.61] | 85.48 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 83.69 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | CLAS | not measured | 60.76 [59.17, 62.39] | 64.19 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 64.19 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | LAS | not measured | 63.12 [61.54, 64.64] | 71.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 66.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | Lemmas | not measured | 74.79 [73.90, 75.67] | 88.54 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 88.54 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | MLAS | not measured | 38.33 [36.89, 39.78] | 60.71 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 57.21 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | Sentences | not measured | 96.12 [94.45, 97.45] | 99.65 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.65 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | Tokens | not measured | 98.83 [98.49, 99.14] | 99.71 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.71 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | UAS | not measured | 70.90 [69.24, 72.42] | 76.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 72.26 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sme: UD_North_Sami-Giella | raw | UFeats | not measured | 71.50 [70.52, 72.50] | 89.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 88.08 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | UPOS | not measured | 84.08 [83.28, 84.85] | 91.83 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 90.73 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | Words | not measured | 98.83 [98.49, 99.14] | 99.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 99.71 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sme: UD_North_Sami-Giella | raw | XPOS | not measured | 87.64 [86.90, 88.36] | 93.50 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 92.77 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sms: UD_Skolt_Sami-Giellagas | gold | AllTags | not measured | 13.36 [11.90, 14.74] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | CLAS | not measured | 17.59 [15.73, 19.70] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | LAS | not measured | 22.10 [20.21, 24.31] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | Lemmas | not measured | 60.88 [59.19, 62.36] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | MLAS | not measured | 1.20 [0.68, 1.78] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | UAS | not measured | 38.47 [35.74, 41.39] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | UFeats | not measured | 42.75 [41.38, 44.19] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | UPOS | not measured | 35.15 [33.65, 36.84] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | gold | XPOS | not measured | 35.67 [33.71, 37.69] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | AllTags | not measured | 16.68 [15.23, 18.12] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | CLAS | not measured | 12.62 [10.80, 14.79] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | LAS | not measured | 16.40 [14.47, 18.50] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | Lemmas | not measured | 61.05 [59.43, 62.51] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | MLAS | not measured | 0.97 [0.58, 1.41] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | Sentences | not measured | 16.62 [11.32, 22.16] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | UAS | not measured | 28.87 [26.11, 31.77] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | UFeats | not measured | 42.27 [40.76, 43.69] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | UPOS | not measured | 39.33 [37.74, 41.08] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | Words | not measured | 100.00 [100.00, 100.00] | not published | not published |
| sms: UD_Skolt_Sami-Giellagas | raw | XPOS | not measured | 41.30 [39.05, 43.34] | not published | not published |
| sq: UD_Albanian-STAF | gold | CLAS | not measured | 60.05 [52.05, 69.94] | not published | not published |
| sq: UD_Albanian-STAF | gold | LAS | not measured | 67.74 [62.23, 74.29] | 64.52 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | gold | Lemmas | not measured | 80.65 [73.78, 86.16] | 81.99 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | gold | MLAS | not measured | 25.67 [19.54, 32.65] | 33.09 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sq: UD_Albanian-STAF | gold | UAS | not measured | 79.03 [73.76, 85.76] | 77.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | gold | UFeats | not measured | 61.02 [52.66, 68.93] | 70.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sq: UD_Albanian-STAF | gold | UPOS | not measured | 84.68 [80.45, 89.31] | 88.71 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | raw | CLAS | not measured | 60.78 [53.28, 70.55] | not published | not published |
| sq: UD_Albanian-STAF | raw | LAS | not measured | 68.30 [61.94, 75.59] | 65.86 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | raw | Lemmas | not measured | 77.55 [70.55, 83.11] | 80.65 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | raw | MLAS | not measured | 27.45 [19.69, 36.47] | 33.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | raw | Sentences | not measured | 84.21 [66.67, 97.44] | 92.68 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | raw | Tokens | not measured | 99.59 [98.53, 100.00] | not published | not published |
| sq: UD_Albanian-STAF | raw | UAS | not measured | 78.10 [73.22, 83.78] | 77.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| sq: UD_Albanian-STAF | raw | UFeats | not measured | 60.68 [51.60, 69.45] | 70.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sq: UD_Albanian-STAF | raw | UPOS | not measured | 82.18 [77.75, 86.79] | 87.90 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sq: UD_Albanian-STAF | raw | Words | not measured | 95.51 [92.25, 97.95] | 98.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | AllTags | not measured | 77.26 [76.37, 78.21] | 95.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | CLAS | not measured | 78.75 [77.56, 79.97] | not published | not published |
| sr: UD_Serbian-SET | gold | LAS | not measured | 81.81 [80.86, 82.86] | 91.77 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | Lemmas | not measured | 91.91 [91.34, 92.48] | 97.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | MLAS | not measured | 56.11 [54.67, 57.59] | 84.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | UAS | not measured | 85.84 [84.96, 86.71] | 94.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | UFeats | not measured | 80.98 [80.12, 81.87] | 96.19 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | UPOS | not measured | 93.67 [93.13, 94.19] | 99.06 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | gold | XPOS | not measured | 81.84 [81.03, 82.68] | 96.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sr: UD_Serbian-SET | raw | AllTags | not measured | 81.57 [80.81, 82.38] | 95.84 (Trankit large, UD 2.5; test same size) — **MISS** | 93.76 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | CLAS | not measured | 86.02 [84.98, 87.10] | 91.92 (Trankit large, UD 2.5; test same size) — **MISS** | 85.92 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sr: UD_Serbian-SET | raw | LAS | not measured | 88.01 [87.10, 88.97] | 93.13 (Trankit large, UD 2.5; test same size) — **MISS** | 88.36 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sr: UD_Serbian-SET | raw | Lemmas | not measured | 92.61 [92.05, 93.13] | 97.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 96.30 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | MLAS | not measured | 66.16 [64.76, 67.54] | 85.76 (Trankit large, UD 2.5; test same size) — **MISS** | 78.82 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | Sentences | not measured | 92.20 [90.01, 94.20] | 100.00 (Trankit large, UD 2.5; test same size) — **MISS** | 98.46 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | Tokens | not measured | 98.80 [98.53, 99.06] | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | UAS | not measured | 90.90 [90.04, 91.74] | 95.57 (Trankit large, UD 2.5; test same size) — **MISS** | 91.44 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sr: UD_Serbian-SET | raw | UFeats | not measured | 84.64 [83.87, 85.41] | 96.40 (Trankit large, UD 2.5; test same size) — **MISS** | 94.31 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | UPOS | not measured | 94.92 [94.49, 95.34] | 99.06 (Trankit large, UD 2.5; test same size) — **MISS** | 98.44 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | Words | not measured | 98.80 [98.53, 99.06] | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.99 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sr: UD_Serbian-SET | raw | XPOS | not measured | 84.12 [83.36, 84.87] | 96.22 (Trankit large, UD 2.5; test same size) — **MISS** | 94.16 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | gold | AllTags | not measured | 86.03 [85.49, 86.53] | 96.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | gold | CLAS | not measured | 79.60 [78.66, 80.53] | not published | not published |
| sv: UD_Swedish-Talbanken | gold | LAS | not measured | 82.89 [82.06, 83.67] | 91.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | gold | Lemmas | not measured | 96.25 [95.98, 96.50] | 98.66 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | gold | MLAS | not measured | 64.57 [63.52, 65.53] | 85.23 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | gold | UAS | not measured | 86.74 [85.99, 87.46] | 93.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | gold | UFeats | not measured | 90.07 [89.62, 90.48] | 97.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | gold | UPOS | not measured | 96.74 [96.45, 97.00] | 98.79 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | gold | XPOS | not measured | 90.35 [89.90, 90.79] | 97.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| sv: UD_Swedish-Talbanken | raw | AllTags | not measured | 87.64 [87.17, 88.10] | 97.43 (Trankit large, UD 2.5; test same size) — **MISS** | 96.24 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | CLAS | not measured | 85.59 [84.73, 86.44] | 92.08 (Trankit large, UD 2.5; test same size) — **MISS** | 85.91 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sv: UD_Swedish-Talbanken | raw | LAS | not measured | 87.66 [86.83, 88.46] | 92.97 (Trankit large, UD 2.5; test same size) — **MISS** | 87.85 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sv: UD_Swedish-Talbanken | raw | Lemmas | not measured | 96.46 [96.20, 96.73] | 98.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 97.92 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | MLAS | not measured | 72.70 [71.71, 73.66] | 88.67 (Trankit large, UD 2.5; test same size) — **MISS** | 81.88 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | Sentences | not measured | 95.84 [94.86, 96.79] | 99.38 (Trankit base, UD 2.5; test same size) — **MISS** | 97.49 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | Tokens | not measured | 99.36 [99.22, 99.50] | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | UAS | not measured | 90.67 [89.94, 91.38] | 94.41 (Trankit large, UD 2.5; test same size) — **MISS** | 90.32 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| sv: UD_Swedish-Talbanken | raw | UFeats | not measured | 91.02 [90.62, 91.42] | 98.04 (Trankit large, UD 2.5; test same size) — **MISS** | 97.29 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | UPOS | not measured | 96.83 [96.55, 97.10] | 99.06 (Trankit large, UD 2.5; test same size) — **MISS** | 98.28 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | Words | not measured | 99.36 [99.22, 99.50] | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.97 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| sv: UD_Swedish-Talbanken | raw | XPOS | not measured | 90.73 [90.30, 91.15] | 98.09 (Trankit large, UD 2.5; test same size) — **MISS** | 97.13 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| te: UD_Telugu-MTG | gold | AllTags | not measured | 94.45 [92.55, 96.23] | 94.04 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| te: UD_Telugu-MTG | gold | CLAS | not measured | 77.67 [73.36, 82.27] | not published | not published |
| te: UD_Telugu-MTG | gold | LAS | not measured | 81.41 [77.48, 85.31] | 85.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| te: UD_Telugu-MTG | gold | MLAS | not measured | 73.09 [68.09, 78.28] | 77.86 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| te: UD_Telugu-MTG | gold | UAS | not measured | 90.71 [87.60, 93.74] | 91.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| te: UD_Telugu-MTG | gold | UFeats | not measured | 98.89 [97.83, 99.72] | 98.89 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| te: UD_Telugu-MTG | gold | UPOS | not measured | 94.45 [92.55, 96.23] | 94.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| te: UD_Telugu-MTG | gold | XPOS | not measured | 94.45 [92.55, 96.23] | 94.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| te: UD_Telugu-MTG | raw | AllTags | not measured | 93.42 [91.41, 95.40] | 94.32 (Trankit base, UD 2.5; test same size) — within or ahead | 93.97 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | CLAS | not measured | 81.14 [76.68, 85.38] | 84.46 (Trankit large, UD 2.5; test same size) — within or ahead | 78.13 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | LAS | not measured | 84.13 [80.47, 87.77] | 87.08 (Trankit large, UD 2.5; test same size) — within or ahead | 81.64 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | MLAS | not measured | 75.81 [71.10, 80.64] | 80.65 (Trankit large, UD 2.5; test same size) — **MISS** | 74.50 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | Sentences | not measured | 89.68 [85.09, 94.04] | 98.62 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 98.62 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| te: UD_Telugu-MTG | raw | Tokens | not measured | 99.79 [99.35, 100.00] | 99.79 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.79 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | UAS | not measured | 91.20 [88.35, 94.05] | 93.19 (Trankit large, UD 2.5; test same size) — within or ahead | 90.09 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | UFeats | not measured | 98.54 [97.49, 99.57] | 99.10 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.10 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | UPOS | not measured | 93.42 [91.41, 95.40] | 94.32 (Trankit base, UD 2.5; test same size) — within or ahead | 93.97 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | Words | not measured | 99.79 [99.35, 100.00] | 99.79 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.79 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| te: UD_Telugu-MTG | raw | XPOS | not measured | 93.42 [91.41, 95.40] | 94.32 (Trankit base, UD 2.5; test same size) — within or ahead | 93.97 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| th: UD_Thai-TUD | gold | CLAS | not measured | 57.88 [56.17, 59.81] | not published | not published |
| th: UD_Thai-TUD | gold | LAS | not measured | 61.77 [60.27, 63.62] | 71.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| th: UD_Thai-TUD | gold | MLAS | not measured | 48.08 [46.44, 49.91] | 61.35 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| th: UD_Thai-TUD | gold | UAS | not measured | 73.57 [72.28, 74.97] | 81.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| th: UD_Thai-TUD | gold | UFeats | not measured | 100.00 [100.00, 100.00] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| th: UD_Thai-TUD | gold | UPOS | not measured | 85.27 [84.38, 86.17] | 89.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| th: UD_Thai-TUD | raw | CLAS | not measured | 42.94 [40.91, 44.91] | not published | not published |
| th: UD_Thai-TUD | raw | LAS | not measured | 47.69 [45.84, 49.59] | 50.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| th: UD_Thai-TUD | raw | MLAS | not measured | 34.82 [32.93, 36.61] | 41.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| th: UD_Thai-TUD | raw | Sentences | not measured | 26.10 [20.93, 31.02] | 11.36 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| th: UD_Thai-TUD | raw | Tokens | not measured | 88.26 [87.23, 89.24] | not published | not published |
| th: UD_Thai-TUD | raw | UAS | not measured | 57.38 [55.53, 59.28] | 58.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| th: UD_Thai-TUD | raw | UFeats | not measured | 88.26 [87.23, 89.24] | 87.14 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| th: UD_Thai-TUD | raw | UPOS | not measured | 73.95 [72.71, 75.22] | 77.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| th: UD_Thai-TUD | raw | Words | not measured | 88.26 [87.23, 89.24] | 87.14 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| tr: UD_Turkish-BOUN | gold | AllTags | not measured | 37.77 [37.03, 38.61] | 73.41 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | gold | CLAS | not measured | 65.35 [63.77, 66.74] | not published | not published |
| tr: UD_Turkish-BOUN | gold | LAS | 67.92 [66.56, 69.24] | 67.13 [65.67, 68.45] | 74.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | gold | Lemmas | 86.40 [85.75, 87.00] | 86.00 [85.31, 86.61] | 93.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | gold | MLAS | not measured | 45.80 [44.46, 47.03] | 52.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | gold | UAS | 75.25 [73.92, 76.56] | 74.44 [73.05, 75.76] | 80.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | gold | UFeats | 80.57 [79.80, 81.33] | 80.29 [79.54, 81.01] | 83.03 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | gold | UPOS | 91.50 [90.98, 92.05] | 91.28 [90.72, 91.81] | 93.14 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | gold | XPOS | not measured | 44.67 [43.81, 45.55] | 89.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| tr: UD_Turkish-BOUN | raw | AllTags | not measured | 37.44 [36.68, 38.26] | 73.03 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 73.03 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| tr: UD_Turkish-BOUN | raw | CLAS | not measured | 63.21 [61.68, 64.58] | 63.79 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 63.79 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| tr: UD_Turkish-BOUN | raw | LAS | 63.03 [61.61, 64.41] | 64.31 [62.89, 65.78] | 67.27 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 65.17 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| tr: UD_Turkish-BOUN | raw | Lemmas | 84.51 [83.78, 85.20] | 84.42 [83.71, 85.09] | 90.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 90.37 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| tr: UD_Turkish-BOUN | raw | MLAS | not measured | 46.89 [45.51, 48.21] | 49.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 48.49 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| tr: UD_Turkish-BOUN | raw | Sentences | 81.28 [78.81, 83.58] | 78.82 [76.14, 81.22] | 90.08 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 90.08 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| tr: UD_Turkish-BOUN | raw | Tokens | 99.56 [99.41, 99.71] | 99.56 [99.41, 99.71] | 99.58 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.58 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| tr: UD_Turkish-BOUN | raw | UAS | 69.75 [68.39, 71.06] | 71.15 [69.82, 72.44] | 73.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 71.53 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| tr: UD_Turkish-BOUN | raw | UFeats | 80.24 [79.44, 81.03] | 80.23 [79.44, 81.03] | 81.66 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 81.66 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| tr: UD_Turkish-BOUN | raw | UPOS | 89.08 [88.46, 89.72] | 88.62 [87.99, 89.32] | 90.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.92 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| tr: UD_Turkish-BOUN | raw | Words | 96.71 [96.34, 97.08] | 96.71 [96.34, 97.08] | 96.57 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | 96.32 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| tr: UD_Turkish-BOUN | raw | XPOS | not measured | 43.51 [42.62, 44.33] | 86.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 85.74 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | gold | AllTags | not measured | 54.59 [53.68, 55.49] | 81.17 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | gold | CLAS | not measured | 46.41 [45.08, 47.78] | not published | not published |
| ug: UD_Uyghur-UDT | gold | LAS | not measured | 51.62 [50.37, 52.94] | 66.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | gold | Lemmas | not measured | 91.36 [90.75, 91.99] | 95.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | gold | MLAS | not measured | 23.01 [21.96, 24.06] | 51.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | gold | UAS | not measured | 65.46 [64.08, 66.72] | 77.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | gold | UFeats | not measured | 65.89 [65.03, 66.75] | 88.51 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | gold | UPOS | not measured | 78.97 [78.20, 79.76] | 90.26 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | gold | XPOS | not measured | 81.13 [80.38, 81.97] | 92.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| ug: UD_Uyghur-UDT | raw | AllTags | not measured | 56.36 [55.37, 57.27] | 80.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 80.06 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | CLAS | not measured | 53.55 [52.19, 55.00] | 66.06 (Trankit large, UD 2.5; test same size) — **MISS** | 58.96 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | LAS | not measured | 58.25 [56.97, 59.60] | 70.14 (Trankit large, UD 2.5; test same size) — **MISS** | 63.42 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | Lemmas | not measured | 91.17 [90.51, 91.81] | 95.30 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 95.30 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | MLAS | not measured | 27.61 [26.39, 28.77] | 51.63 (Trankit large, UD 2.5; test same size) — **MISS** | 48.01 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | Sentences | not measured | 77.41 [74.83, 80.10] | 88.95 (Trankit large, UD 2.5; test same size) — **MISS** | 84.69 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | Tokens | not measured | 99.57 [99.35, 99.75] | 99.42 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.42 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| ug: UD_Uyghur-UDT | raw | UAS | not measured | 72.12 [70.81, 73.47] | 80.19 (Trankit large, UD 2.5; test same size) — **MISS** | 74.80 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | UFeats | not measured | 67.20 [66.28, 68.12] | 88.03 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 87.58 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | UPOS | not measured | 80.12 [79.30, 80.95] | 89.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 89.26 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| ug: UD_Uyghur-UDT | raw | Words | not measured | 99.57 [99.35, 99.75] | 99.54 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | 99.42 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| ug: UD_Uyghur-UDT | raw | XPOS | not measured | 82.75 [82.01, 83.52] | 91.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 91.19 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| uk: UD_Ukrainian-ParlaMint | gold | AllTags | not measured | 74.30 [73.28, 75.29] | 93.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | gold | CLAS | not measured | 79.88 [78.76, 81.01] | not published | not published |
| uk: UD_Ukrainian-ParlaMint | gold | LAS | not measured | 81.97 [80.94, 82.94] | 91.92 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | gold | Lemmas | not measured | 95.04 [94.60, 95.51] | 99.12 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | gold | MLAS | not measured | 52.19 [50.73, 53.66] | 82.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | gold | UAS | not measured | 86.02 [85.07, 86.91] | 93.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | gold | UFeats | not measured | 76.60 [75.66, 77.59] | 95.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | gold | UPOS | not measured | 94.32 [93.84, 94.79] | 98.60 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | gold | XPOS | not measured | 95.96 [95.55, 96.34] | 98.49 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | AllTags | not measured | 76.62 [75.63, 77.58] | 93.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | CLAS | not measured | 84.71 [83.72, 85.70] | not published | not published |
| uk: UD_Ukrainian-ParlaMint | raw | LAS | not measured | 87.15 [86.25, 87.96] | 91.91 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | Lemmas | not measured | 95.83 [95.46, 96.22] | 99.07 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | MLAS | not measured | 57.47 [56.07, 58.80] | 82.96 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | Sentences | not measured | 91.06 [89.16, 92.92] | 99.81 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | Tokens | not measured | 99.96 [99.91, 100.00] | not published | not published |
| uk: UD_Ukrainian-ParlaMint | raw | UAS | not measured | 90.59 [89.79, 91.34] | 93.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | UFeats | not measured | 78.33 [77.39, 79.27] | 95.01 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | UPOS | not measured | 94.91 [94.48, 95.32] | 98.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uk: UD_Ukrainian-ParlaMint | raw | Words | not measured | 99.96 [99.91, 100.00] | 99.95 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| uk: UD_Ukrainian-ParlaMint | raw | XPOS | not measured | 96.74 [96.40, 97.08] | 98.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | gold | AllTags | not measured | 52.94 [50.05, 55.77] | 60.21 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | gold | CLAS | not measured | 47.89 [44.82, 50.61] | not published | not published |
| uz: UD_Uzbek-UzUDT | gold | LAS | not measured | 52.10 [49.27, 54.95] | 57.97 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | gold | Lemmas | not measured | 78.41 [76.68, 80.28] | 29.22 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | gold | MLAS | not measured | 21.68 [19.53, 24.18] | 30.19 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | gold | UAS | not measured | 67.80 [65.04, 70.66] | 73.63 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | gold | UFeats | not measured | 61.87 [59.49, 64.40] | 70.49 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | gold | UPOS | not measured | 84.77 [83.18, 86.33] | 86.24 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | gold | XPOS | not measured | 83.36 [80.32, 86.10] | 85.58 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | AllTags | not measured | 54.86 [52.05, 57.56] | 60.26 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | raw | CLAS | not measured | 52.43 [49.35, 55.39] | not published | not published |
| uz: UD_Uzbek-UzUDT | raw | LAS | not measured | 56.59 [53.81, 59.44] | 55.78 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | Lemmas | not measured | 80.14 [78.47, 81.87] | 29.27 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | MLAS | not measured | 26.44 [23.96, 29.29] | 28.87 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | Sentences | not measured | 70.95 [64.41, 76.45] | 72.26 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| uz: UD_Uzbek-UzUDT | raw | UAS | not measured | 71.68 [68.90, 74.38] | 71.16 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | UFeats | not measured | 63.97 [61.65, 66.26] | 70.68 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — **MISS** | not published |
| uz: UD_Uzbek-UzUDT | raw | UPOS | not measured | 86.54 [85.03, 88.04] | 86.15 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | Words | not measured | 100.00 [100.00, 100.00] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| uz: UD_Uzbek-UzUDT | raw | XPOS | not measured | 85.33 [82.30, 88.01] | 85.25 (UDPipe 2 2.17 models 251125, UD 2.17; test changed) — within or ahead | not published |
| vi: UD_Vietnamese-VTB | gold | AllTags | not measured | 86.23 [85.62, 86.85] | 88.59 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| vi: UD_Vietnamese-VTB | gold | CLAS | not measured | 57.91 [56.54, 59.19] | not published | not published |
| vi: UD_Vietnamese-VTB | gold | LAS | not measured | 61.70 [60.51, 62.95] | 65.51 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| vi: UD_Vietnamese-VTB | gold | Lemmas | not measured | 99.30 [99.15, 99.44] | 99.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| vi: UD_Vietnamese-VTB | gold | MLAS | not measured | 48.86 [47.61, 50.15] | 55.15 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| vi: UD_Vietnamese-VTB | gold | UAS | not measured | 73.55 [72.37, 74.73] | 75.84 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| vi: UD_Vietnamese-VTB | gold | UPOS | not measured | 89.63 [89.04, 90.16] | 89.92 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| vi: UD_Vietnamese-VTB | gold | XPOS | not measured | 87.77 [87.21, 88.33] | 88.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| vi: UD_Vietnamese-VTB | raw | AllTags | not measured | 66.08 [65.06, 67.23] | 88.07 (Trankit large, UD 2.5; test changed) — **MISS** | 80.56 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | CLAS | not measured | 34.08 [32.58, 35.61] | 63.67 (Trankit large, UD 2.5; test changed) — **MISS** | 46.60 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | LAS | not measured | 38.80 [37.32, 40.34] | 65.37 (Trankit large, UD 2.5; test changed) — **MISS** | 51.49 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | Lemmas | not measured | 74.75 [73.78, 75.75] | 95.22 (Trankit base, UD 2.5; test changed) — **MISS** | 81.66 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | MLAS | not measured | 29.54 [28.16, 30.91] | 60.22 (Trankit large, UD 2.5; test changed) — **MISS** | 42.58 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | Sentences | not measured | 90.38 [88.34, 92.39] | 99.25 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.25 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | Tokens | not measured | 75.25 [74.26, 76.27] | 95.22 (Trankit base, UD 2.5; test changed) — **MISS** | 88.02 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | UAS | not measured | 44.71 [43.10, 46.33] | 71.07 (Trankit large, UD 2.5; test changed) — **MISS** | 58.82 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | UPOS | not measured | 68.68 [67.63, 69.75] | 89.70 (Trankit large, UD 2.5; test changed) — **MISS** | 81.48 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | Words | not measured | 75.25 [74.26, 76.27] | 95.22 (Trankit base, UD 2.5; test changed) — **MISS** | 88.02 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| vi: UD_Vietnamese-VTB | raw | XPOS | not measured | 67.17 [66.14, 68.31] | 88.14 (Trankit large, UD 2.5; test changed) — **MISS** | 80.61 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | gold | AllTags | not measured | 65.36 [64.21, 66.41] | 92.39 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | gold | CLAS | not measured | 44.54 [42.87, 46.19] | not published | not published |
| wo: UD_Wolof-WTB | gold | LAS | not measured | 52.57 [51.05, 54.17] | 80.16 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | gold | Lemmas | not measured | 87.49 [86.82, 88.16] | 95.94 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | gold | MLAS | not measured | 27.49 [26.14, 28.82] | 68.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | gold | UAS | not measured | 63.82 [62.37, 65.41] | 85.75 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | gold | UFeats | not measured | 72.32 [71.33, 73.19] | 94.33 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | gold | UPOS | not measured | 79.61 [78.71, 80.53] | 95.29 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | gold | XPOS | not measured | 79.43 [78.38, 80.41] | 95.14 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| wo: UD_Wolof-WTB | raw | AllTags | not measured | 64.26 [63.16, 65.31] | 91.44 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 91.38 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | CLAS | not measured | 48.57 [46.94, 50.19] | 71.22 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 71.22 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | LAS | not measured | 56.10 [54.60, 57.59] | 78.02 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 77.32 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | Lemmas | not measured | 84.43 [83.62, 85.26] | 95.18 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.63 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | MLAS | not measured | 30.16 [28.77, 31.48] | 66.05 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 64.89 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | Sentences | not measured | 91.55 [88.99, 93.80] | 94.33 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 94.33 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | Tokens | not measured | 99.93 [99.84, 99.99] | 99.95 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead | 99.95 (Stanza 1.5.1, UD 2.12; test same size) — within or ahead |
| wo: UD_Wolof-WTB | raw | UAS | not measured | 66.01 [64.54, 67.58] | 83.49 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 83.28 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | UFeats | not measured | 70.25 [69.32, 71.19] | 93.47 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 93.19 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | UPOS | not measured | 78.63 [77.67, 79.69] | 94.36 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 94.36 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | Words | not measured | 95.18 [94.64, 95.76] | 99.35 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** | 99.35 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| wo: UD_Wolof-WTB | raw | XPOS | not measured | 78.48 [77.53, 79.42] | 94.24 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | 94.16 (Stanza 1.5.1, UD 2.12; test same size) — **MISS** |
| yi: UD_Yiddish-YiTB | gold | CLAS | not measured | 75.56 [73.82, 77.36] | not published | not published |
| yi: UD_Yiddish-YiTB | gold | LAS | not measured | 81.00 [79.64, 82.38] | 86.48 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | gold | Lemmas | not measured | 91.14 [90.52, 91.75] | 93.87 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | gold | MLAS | not measured | 65.50 [63.65, 67.37] | 76.74 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | gold | UAS | not measured | 86.40 [85.18, 87.61] | 90.34 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | gold | UFeats | not measured | 100.00 [100.00, 100.00] | 100.00 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — within or ahead | not published |
| yi: UD_Yiddish-YiTB | gold | UPOS | not measured | 91.95 [91.35, 92.53] | 94.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | CLAS | not measured | 76.89 [75.13, 78.55] | not published | not published |
| yi: UD_Yiddish-YiTB | raw | LAS | not measured | 80.77 [79.38, 82.11] | 85.56 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | Lemmas | not measured | 88.37 [87.63, 89.06] | 93.53 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | MLAS | not measured | 68.47 [66.73, 70.19] | 75.85 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | Sentences | not measured | 94.73 [93.41, 96.01] | 97.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | Tokens | not measured | 98.32 [97.95, 98.70] | not published | not published |
| yi: UD_Yiddish-YiTB | raw | UAS | not measured | 85.44 [84.17, 86.74] | 89.38 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | UFeats | not measured | 96.17 [95.63, 96.70] | 99.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | UPOS | not measured | 89.78 [89.11, 90.49] | 94.50 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| yi: UD_Yiddish-YiTB | raw | Words | not measured | 96.17 [95.63, 96.70] | 99.61 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | AllTags | 85.15 [84.47, 85.82] | 83.66 [82.95, 84.39] | 95.20 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | CLAS | not measured | 59.72 [58.32, 61.19] | not published | not published |
| zh: UD_Chinese-GSDSimp | gold | LAS | 63.29 [61.88, 64.70] | 61.42 [60.21, 62.70] | 83.60 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | Lemmas | 99.80 [99.71, 99.88] | 99.78 [99.67, 99.86] | 99.90 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | MLAS | not measured | 47.94 [46.40, 49.45] | 77.62 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | UAS | 69.15 [67.81, 70.55] | 67.22 [66.03, 68.48] | 86.76 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | UFeats | 96.03 [95.69, 96.39] | 95.86 [95.48, 96.23] | 99.28 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | UPOS | 88.77 [88.18, 89.35] | 87.55 [86.92, 88.22] | 95.73 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | gold | XPOS | 87.86 [87.24, 88.47] | 87.14 [86.47, 87.79] | 95.97 (UDPipe 2 2.17 models 251125, UD 2.17; test same size) — **MISS** | not published |
| zh: UD_Chinese-GSDSimp | raw | AllTags | 77.97 [77.04, 78.87] | 77.47 [76.54, 78.38] | 94.32 (Trankit large, UD 2.5; test changed) — **MISS** | 87.95 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | CLAS | not measured | 58.05 [56.40, 59.67] | 83.23 (Trankit large, UD 2.5; test changed) — **MISS** | 67.39 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | LAS | 58.84 [57.23, 60.35] | 60.98 [59.49, 62.45] | 84.63 (Trankit large, UD 2.5; test changed) — **MISS** | 70.65 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | Lemmas | 89.45 [88.61, 90.27] | 89.41 [88.61, 90.21] | 97.66 (Trankit large, UD 2.5; test changed) — **MISS** | 92.07 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | MLAS | not measured | 49.33 [47.69, 50.97] | 79.84 (Trankit large, UD 2.5; test changed) — **MISS** | 63.96 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | Sentences | 85.31 [82.10, 88.41] | 83.77 [80.32, 87.30] | 99.70 (Trankit base, UD 2.5; test changed) — **MISS** | 99.10 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | Tokens | 89.62 [88.81, 90.43] | 89.62 [88.81, 90.43] | 97.66 (Trankit large, UD 2.5; test changed) — **MISS** | 92.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | UAS | 62.78 [61.24, 64.26] | 64.42 [62.89, 65.87] | 87.01 (Trankit large, UD 2.5; test changed) — **MISS** | 73.41 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | UFeats | 85.94 [85.11, 86.79] | 85.98 [85.14, 86.82] | 97.24 (Trankit large, UD 2.5; test changed) — **MISS** | 91.47 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | UPOS | 80.96 [80.07, 81.81] | 80.66 [79.76, 81.48] | 94.99 (Trankit large, UD 2.5; test changed) — **MISS** | 88.82 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | Words | 89.62 [88.81, 90.43] | 89.62 [88.81, 90.43] | 97.66 (Trankit large, UD 2.5; test changed) — **MISS** | 92.14 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zh: UD_Chinese-GSDSimp | raw | XPOS | 80.11 [79.28, 80.98] | 79.75 [78.91, 80.63] | 94.78 (Trankit large, UD 2.5; test changed) — **MISS** | 88.61 (Stanza 1.5.1, UD 2.12; test changed) — **MISS** |
| zza: UD_Zazaki-ZSD | gold | CLAS | not measured | 32.89 [25.34, 41.83] | not published | not published |
| zza: UD_Zazaki-ZSD | gold | LAS | not measured | 39.60 [31.87, 48.95] | not published | not published |
| zza: UD_Zazaki-ZSD | gold | Lemmas | not measured | 76.00 [69.52, 81.85] | not published | not published |
| zza: UD_Zazaki-ZSD | gold | MLAS | not measured | 8.05 [3.88, 13.74] | not published | not published |
| zza: UD_Zazaki-ZSD | gold | UAS | not measured | 56.00 [46.58, 66.49] | not published | not published |
| zza: UD_Zazaki-ZSD | gold | UFeats | not measured | 51.20 [45.45, 57.41] | not published | not published |
| zza: UD_Zazaki-ZSD | gold | UPOS | not measured | 63.20 [57.20, 69.49] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | CLAS | not measured | 33.00 [23.68, 41.45] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | LAS | not measured | 36.10 [26.64, 45.28] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | Lemmas | not measured | 67.63 [62.40, 72.44] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | MLAS | not measured | 8.08 [4.65, 11.95] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | Sentences | not measured | 83.33 [70.00, 93.55] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | Tokens | not measured | 100.00 [100.00, 100.00] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | UAS | not measured | 51.04 [39.65, 61.45] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | UFeats | not measured | 44.81 [40.29, 49.63] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | UPOS | not measured | 56.85 [52.63, 61.44] | not published | not published |
| zza: UD_Zazaki-ZSD | raw | Words | not measured | 88.80 [83.68, 93.88] | not published | not published |

## Entity samples (gold tokens)

Deterministic evenly spaced samples, at most 3,001 sentences per corpus. The published UD references above do not supply comparable entity predictions; these rows are information. The paired spaCy view below uses identical sampled sentences.

| Language | Metric | Before [95% CI] | Current [95% CI] |
|---|---|---|---|
| da | micro-F1 | not measured | 82.86 [78.88, 86.39] |
| da | micro-precision | not measured | 83.91 [79.61, 88.03] |
| da | micro-recall | not measured | 81.84 [77.26, 85.71] |
| de | micro-F1 | 82.68 [81.18, 84.04] | 80.96 [79.47, 82.28] |
| de | micro-precision | 85.82 [84.28, 87.23] | 81.02 [79.37, 82.39] |
| de | micro-recall | 79.77 [78.03, 81.32] | 80.89 [79.30, 82.45] |
| en | micro-F1 | 79.07 [76.69, 81.48] | 77.46 [74.74, 79.96] |
| en | micro-precision | 86.13 [83.63, 88.42] | 81.17 [77.98, 84.08] |
| en | micro-recall | 73.07 [70.21, 76.07] | 74.08 [71.08, 76.90] |
| es | micro-F1 | 85.58 [84.44, 86.59] | 84.78 [83.64, 85.84] |
| es | micro-precision | 85.97 [84.88, 87.00] | 84.98 [83.82, 86.02] |
| es | micro-recall | 85.19 [84.02, 86.29] | 84.59 [83.41, 85.69] |
| fr | micro-F1 | 81.56 [80.48, 82.75] | 80.96 [79.84, 82.17] |
| fr | micro-precision | 82.11 [80.98, 83.29] | 80.92 [79.71, 82.07] |
| fr | micro-recall | 81.02 [79.97, 82.24] | 81.01 [79.87, 82.22] |
| hr | micro-F1 | not measured | 81.87 [79.66, 84.04] |
| hr | micro-precision | not measured | 82.03 [79.67, 84.46] |
| hr | micro-recall | not measured | 81.71 [79.41, 83.67] |
| id | micro-F1 | not measured | 78.39 [74.93, 81.32] |
| id | micro-precision | not measured | 82.68 [79.44, 85.53] |
| id | micro-recall | not measured | 74.52 [70.15, 78.59] |
| it | micro-F1 | 86.30 [85.27, 87.25] | 84.74 [83.65, 85.74] |
| it | micro-precision | 86.57 [85.54, 87.48] | 84.84 [83.74, 85.84] |
| it | micro-recall | 86.03 [84.96, 87.02] | 84.64 [83.48, 85.69] |
| nl | micro-F1 | 85.18 [84.06, 86.17] | 83.70 [82.61, 84.70] |
| nl | micro-precision | 84.85 [83.74, 85.88] | 83.16 [82.06, 84.23] |
| nl | micro-recall | 85.52 [84.42, 86.50] | 84.24 [83.13, 85.25] |
| no | micro-F1 | not measured | 73.18 [70.65, 75.53] |
| no | micro-precision | not measured | 69.24 [66.62, 71.93] |
| no | micro-recall | not measured | 77.60 [75.15, 79.85] |
| pl | micro-F1 | not measured | 67.67 [65.13, 70.05] |
| pl | micro-precision | not measured | 72.38 [69.44, 75.06] |
| pl | micro-recall | not measured | 63.54 [60.98, 66.21] |
| pt | micro-F1 | 88.28 [86.43, 90.13] | 88.30 [86.58, 90.01] |
| pt | micro-precision | 90.14 [88.16, 92.00] | 89.04 [87.14, 90.98] |
| pt | micro-recall | 86.50 [84.43, 88.59] | 87.57 [85.56, 89.54] |
| ro | micro-F1 | not measured | 67.70 [66.54, 68.89] |
| ro | micro-precision | not measured | 70.80 [69.39, 72.13] |
| ro | micro-recall | not measured | 64.87 [63.55, 66.15] |
| sk | micro-F1 | not measured | 65.10 [61.62, 68.69] |
| sk | micro-precision | not measured | 63.51 [59.46, 67.53] |
| sk | micro-recall | not measured | 66.78 [63.39, 70.22] |
| sl | micro-F1 | not measured | 68.03 [64.66, 71.79] |
| sl | micro-precision | not measured | 70.97 [67.36, 74.92] |
| sl | micro-recall | not measured | 65.32 [61.72, 69.26] |
| sr | micro-F1 | not measured | 89.92 [87.89, 91.79] |
| sr | micro-precision | not measured | 91.79 [89.78, 93.63] |
| sr | micro-recall | not measured | 88.12 [85.75, 90.43] |
| sv | micro-F1 | not measured | 69.35 [61.24, 76.73] |
| sv | micro-precision | not measured | 88.19 [81.97, 93.48] |
| sv | micro-recall | not measured | 57.14 [47.95, 65.96] |
| tr | micro-F1 | 83.55 [81.59, 85.40] | 82.84 [80.81, 84.79] |
| tr | micro-precision | 85.68 [83.58, 87.77] | 82.65 [80.52, 84.71] |
| tr | micro-recall | 81.51 [79.30, 83.56] | 83.03 [80.86, 85.07] |
| zh | micro-F1 | 84.84 [81.89, 87.94] | 84.04 [81.02, 86.97] |
| zh | micro-precision | 86.51 [83.49, 89.63] | 84.59 [81.42, 87.57] |
| zh | micro-recall | 83.22 [79.76, 86.69] | 83.49 [79.86, 86.79] |

## Paired spaCy 3.8.18 comparison (gate)

144 rows: 117 ahead, 14 within the paired interval, 13 behind. Same gold, mode and sampled entity sentences; 1,000 paired resamples, seed 20261106. spaCy runs its documented default configuration on the same training data. A row passes when it is ahead or within the paired interval; the 13 rows behind are gaps. Positive differences favor this model.

| Language / dataset | Input | Metric | Current [95% CI] | spaCy [95% CI] | Difference [95% CI] | Verdict |
|---|---|---|---|---|---|---|
| de: de_gsd-ud-test | gold | UPOS | 95.78 [95.43, 96.12] | 94.59 [94.22, 94.97] | 1.19 [0.80, 1.55] | ahead |
| de: de_gsd-ud-test | gold | UFeats | 86.58 [85.91, 87.19] | 84.71 [84.07, 85.40] | 1.87 [1.16, 2.60] | ahead |
| de: de_gsd-ud-test | gold | Lemmas | 96.64 [96.37, 96.92] | 95.65 [95.32, 95.97] | 0.99 [0.71, 1.27] | ahead |
| de: de_gsd-ud-test | gold | UAS | 87.09 [86.26, 87.84] | 81.88 [80.78, 82.88] | 5.21 [4.40, 6.16] | ahead |
| de: de_gsd-ud-test | gold | LAS | 83.49 [82.59, 84.31] | 77.31 [76.19, 78.32] | 6.18 [5.35, 7.14] | ahead |
| de: de_gsd-ud-test | raw | Tokens | 97.40 [97.09, 97.69] | 98.03 [97.67, 98.36] | -0.63 [-1.04, -0.21] | behind |
| de: de_gsd-ud-test | raw | Sentences | 88.65 [86.73, 90.58] | 78.73 [76.35, 81.37] | 9.91 [7.13, 12.72] | ahead |
| de: de_gsd-ud-test | raw | Words | 97.06 [96.70, 97.39] | 95.51 [95.03, 95.96] | 1.55 [1.22, 1.89] | ahead |
| de: de_gsd-ud-test | raw | UPOS | 93.33 [92.87, 93.75] | 90.13 [89.52, 90.69] | 3.20 [2.76, 3.67] | ahead |
| de: de_gsd-ud-test | raw | UFeats | 85.16 [84.51, 85.74] | 80.11 [79.31, 80.89] | 5.05 [4.33, 5.79] | ahead |
| de: de_gsd-ud-test | raw | Lemmas | 94.07 [93.64, 94.48] | 91.28 [90.71, 91.79] | 2.80 [2.36, 3.25] | ahead |
| de: de_gsd-ud-test | raw | UAS | 85.50 [84.64, 86.25] | 75.30 [74.13, 76.35] | 10.19 [9.36, 11.13] | ahead |
| de: de_gsd-ud-test | raw | LAS | 82.38 [81.55, 83.18] | 70.77 [69.60, 71.89] | 11.61 [10.73, 12.52] | ahead |
| en: en_ewt-ud-test | gold | UPOS | 96.49 [96.19, 96.76] | 94.29 [93.88, 94.65] | 2.20 [1.86, 2.57] | ahead |
| en: en_ewt-ud-test | gold | UFeats | 96.37 [96.09, 96.62] | 95.18 [94.87, 95.49] | 1.19 [0.89, 1.50] | ahead |
| en: en_ewt-ud-test | gold | Lemmas | 97.26 [97.01, 97.52] | 95.80 [95.45, 96.12] | 1.47 [1.21, 1.76] | ahead |
| en: en_ewt-ud-test | gold | UAS | 88.34 [87.65, 89.01] | 85.40 [84.51, 86.30] | 2.93 [2.07, 3.81] | ahead |
| en: en_ewt-ud-test | gold | LAS | 85.12 [84.40, 85.87] | 81.96 [81.00, 82.88] | 3.16 [2.30, 4.00] | ahead |
| en: en_ewt-ud-test | raw | Tokens | 97.13 [96.83, 97.43] | 95.70 [95.17, 96.16] | 1.43 [1.00, 1.93] | ahead |
| en: en_ewt-ud-test | raw | Sentences | 75.85 [74.20, 77.60] | 72.66 [70.73, 74.67] | 3.19 [1.22, 5.36] | ahead |
| en: en_ewt-ud-test | raw | Words | 98.68 [98.45, 98.90] | 97.48 [96.99, 97.96] | 1.21 [0.78, 1.69] | ahead |
| en: en_ewt-ud-test | raw | UPOS | 95.95 [95.59, 96.27] | 92.45 [91.86, 93.05] | 3.49 [2.96, 4.08] | ahead |
| en: en_ewt-ud-test | raw | UFeats | 95.94 [95.61, 96.27] | 93.37 [92.80, 93.92] | 2.57 [2.07, 3.12] | ahead |
| en: en_ewt-ud-test | raw | Lemmas | 96.39 [96.04, 96.74] | 93.69 [93.08, 94.28] | 2.70 [2.18, 3.28] | ahead |
| en: en_ewt-ud-test | raw | UAS | 88.78 [88.07, 89.47] | 81.34 [80.35, 82.35] | 7.44 [6.58, 8.27] | ahead |
| en: en_ewt-ud-test | raw | LAS | 86.55 [85.82, 87.26] | 78.53 [77.47, 79.56] | 8.01 [7.20, 8.84] | ahead |
| es: es_ancora-ud-test | gold | UPOS | 98.00 [97.87, 98.14] | 98.18 [98.03, 98.32] | -0.18 [-0.33, -0.03] | behind |
| es: es_ancora-ud-test | gold | UFeats | 96.74 [96.58, 96.91] | 97.74 [97.58, 97.89] | -1.00 [-1.17, -0.83] | behind |
| es: es_ancora-ud-test | gold | Lemmas | 98.53 [98.42, 98.64] | 98.72 [98.60, 98.83] | -0.19 [-0.30, -0.07] | behind |
| es: es_ancora-ud-test | gold | UAS | 89.50 [89.08, 89.94] | 89.04 [88.53, 89.51] | 0.46 [0.02, 0.91] | ahead |
| es: es_ancora-ud-test | gold | LAS | 86.97 [86.51, 87.42] | 85.95 [85.40, 86.43] | 1.03 [0.56, 1.50] | ahead |
| es: es_ancora-ud-test | raw | Tokens | 99.84 [99.76, 99.90] | 99.90 [99.85, 99.94] | -0.06 [-0.13, 0.01] | within |
| es: es_ancora-ud-test | raw | Sentences | 97.05 [96.24, 97.78] | 97.70 [96.94, 98.43] | -0.66 [-1.59, 0.28] | within |
| es: es_ancora-ud-test | raw | Words | 99.83 [99.75, 99.89] | 96.60 [96.38, 96.80] | 3.23 [3.02, 3.45] | ahead |
| es: es_ancora-ud-test | raw | UPOS | 98.20 [98.07, 98.34] | 94.81 [94.55, 95.05] | 3.40 [3.15, 3.64] | ahead |
| es: es_ancora-ud-test | raw | UFeats | 96.83 [96.66, 96.99] | 94.57 [94.32, 94.81] | 2.25 [2.01, 2.50] | ahead |
| es: es_ancora-ud-test | raw | Lemmas | 98.58 [98.46, 98.70] | 95.37 [95.13, 95.59] | 3.21 [2.98, 3.45] | ahead |
| es: es_ancora-ud-test | raw | UAS | 91.93 [91.49, 92.31] | 84.87 [84.27, 85.43] | 7.06 [6.56, 7.55] | ahead |
| es: es_ancora-ud-test | raw | LAS | 89.70 [89.24, 90.11] | 81.82 [81.22, 82.40] | 7.89 [7.37, 8.38] | ahead |
| fr: fr_gsd-ud-test | gold | UPOS | 96.99 [96.60, 97.39] | 97.07 [96.68, 97.47] | -0.09 [-0.49, 0.34] | within |
| fr: fr_gsd-ud-test | gold | UFeats | 96.06 [95.58, 96.49] | 96.41 [95.93, 96.85] | -0.35 [-0.90, 0.19] | within |
| fr: fr_gsd-ud-test | gold | Lemmas | 98.03 [97.72, 98.31] | 97.49 [97.13, 97.81] | 0.54 [0.22, 0.88] | ahead |
| fr: fr_gsd-ud-test | gold | UAS | 89.52 [88.61, 90.43] | 89.10 [88.14, 90.12] | 0.42 [-0.58, 1.43] | within |
| fr: fr_gsd-ud-test | gold | LAS | 86.89 [85.93, 87.88] | 85.98 [84.84, 87.07] | 0.91 [-0.11, 1.95] | within |
| fr: fr_gsd-ud-test | raw | Tokens | 99.88 [99.79, 99.96] | 98.85 [98.49, 99.16] | 1.02 [0.74, 1.37] | ahead |
| fr: fr_gsd-ud-test | raw | Sentences | 92.65 [90.07, 94.98] | 91.82 [89.08, 94.23] | 0.83 [-1.20, 3.09] | within |
| fr: fr_gsd-ud-test | raw | Words | 99.48 [99.29, 99.65] | 94.64 [94.01, 95.17] | 4.83 [4.31, 5.44] | ahead |
| fr: fr_gsd-ud-test | raw | UPOS | 97.10 [96.71, 97.46] | 91.80 [91.06, 92.46] | 5.30 [4.66, 6.01] | ahead |
| fr: fr_gsd-ud-test | raw | UFeats | 96.26 [95.82, 96.68] | 91.16 [90.40, 91.86] | 5.11 [4.40, 5.91] | ahead |
| fr: fr_gsd-ud-test | raw | Lemmas | 97.96 [97.64, 98.25] | 92.20 [91.44, 92.88] | 5.76 [5.13, 6.48] | ahead |
| fr: fr_gsd-ud-test | raw | UAS | 91.97 [91.12, 92.76] | 81.64 [80.34, 82.90] | 10.33 [9.24, 11.53] | ahead |
| fr: fr_gsd-ud-test | raw | LAS | 89.78 [88.84, 90.63] | 78.10 [76.76, 79.31] | 11.68 [10.48, 12.84] | ahead |
| it: it_markit-ud-test | gold | UPOS | 95.81 [95.39, 96.26] | 92.20 [91.61, 92.77] | 3.61 [3.05, 4.19] | ahead |
| it: it_markit-ud-test | gold | UFeats | 92.12 [91.51, 92.73] | 90.11 [89.46, 90.77] | 2.01 [1.47, 2.63] | ahead |
| it: it_markit-ud-test | gold | Lemmas | 86.38 [85.63, 87.08] | 83.80 [82.97, 84.52] | 2.58 [2.15, 3.03] | ahead |
| it: it_markit-ud-test | gold | UAS | 82.05 [81.01, 83.15] | 75.39 [74.12, 76.63] | 6.66 [5.42, 7.96] | ahead |
| it: it_markit-ud-test | gold | LAS | 77.63 [76.52, 78.78] | 69.27 [67.94, 70.57] | 8.36 [7.06, 9.75] | ahead |
| it: it_markit-ud-test | raw | Tokens | 99.93 [99.86, 99.98] | 95.76 [95.28, 96.24] | 4.17 [3.69, 4.66] | ahead |
| it: it_markit-ud-test | raw | Sentences | 90.11 [87.27, 92.93] | 91.62 [88.51, 94.91] | -1.51 [-5.49, 1.94] | within |
| it: it_markit-ud-test | raw | Words | 99.73 [99.58, 99.86] | 88.66 [87.84, 89.43] | 11.07 [10.30, 11.88] | ahead |
| it: it_markit-ud-test | raw | UPOS | 96.12 [95.75, 96.52] | 81.37 [80.37, 82.39] | 14.75 [13.79, 15.79] | ahead |
| it: it_markit-ud-test | raw | UFeats | 92.84 [92.29, 93.42] | 81.05 [80.07, 82.01] | 11.79 [10.85, 12.68] | ahead |
| it: it_markit-ud-test | raw | Lemmas | 86.65 [85.93, 87.35] | 73.64 [72.57, 74.71] | 13.01 [12.18, 13.89] | ahead |
| it: it_markit-ud-test | raw | UAS | 86.39 [85.36, 87.29] | 62.66 [61.07, 64.19] | 23.73 [22.07, 25.38] | ahead |
| it: it_markit-ud-test | raw | LAS | 82.22 [81.04, 83.28] | 56.71 [55.14, 58.22] | 25.51 [23.86, 27.23] | ahead |
| de: germeval-2014 | entities (gold) | micro-F1 | 80.96 [79.47, 82.28] | 65.30 [63.38, 67.02] | 15.66 [13.73, 17.43] | ahead |
| de: germeval-2014 | entities (gold) | micro-precision | 81.02 [79.37, 82.39] | 70.48 [68.42, 72.31] | 10.54 [8.39, 12.66] | ahead |
| de: germeval-2014 | entities (gold) | micro-recall | 80.89 [79.30, 82.45] | 60.82 [58.79, 62.78] | 20.07 [18.06, 21.89] | ahead |
| en: en_ewt-ud | entities (gold) | micro-F1 | 77.46 [74.74, 79.96] | 62.32 [59.32, 65.10] | 15.14 [12.00, 18.48] | ahead |
| en: en_ewt-ud | entities (gold) | micro-precision | 81.17 [77.98, 84.08] | 67.97 [64.61, 70.86] | 13.20 [9.69, 17.10] | ahead |
| en: en_ewt-ud | entities (gold) | micro-recall | 74.08 [71.08, 76.90] | 57.54 [54.28, 60.60] | 16.54 [12.93, 20.15] | ahead |
| es: wikiner-es-wp3 | entities (gold) | micro-F1 | 84.78 [83.64, 85.84] | 83.21 [82.13, 84.22] | 1.57 [0.47, 2.70] | ahead |
| es: wikiner-es-wp3 | entities (gold) | micro-precision | 84.98 [83.82, 86.02] | 83.79 [82.71, 84.80] | 1.19 [0.01, 2.35] | ahead |
| es: wikiner-es-wp3 | entities (gold) | micro-recall | 84.59 [83.41, 85.69] | 82.64 [81.49, 83.69] | 1.95 [0.84, 3.08] | ahead |
| fr: wikiner-fr-wp3 | entities (gold) | micro-F1 | 80.96 [79.84, 82.17] | 75.52 [74.20, 76.84] | 5.44 [4.14, 6.87] | ahead |
| fr: wikiner-fr-wp3 | entities (gold) | micro-precision | 80.92 [79.71, 82.07] | 76.75 [75.45, 78.02] | 4.17 [2.76, 5.63] | ahead |
| fr: wikiner-fr-wp3 | entities (gold) | micro-recall | 81.01 [79.87, 82.22] | 74.33 [72.91, 75.76] | 6.67 [5.35, 8.12] | ahead |
| it: wikiner-it-wp3 | entities (gold) | micro-F1 | 84.74 [83.65, 85.74] | 83.58 [82.47, 84.52] | 1.16 [0.02, 2.24] | ahead |
| it: wikiner-it-wp3 | entities (gold) | micro-precision | 84.84 [83.74, 85.84] | 84.08 [83.00, 85.06] | 0.76 [-0.43, 1.90] | within |
| it: wikiner-it-wp3 | entities (gold) | micro-recall | 84.64 [83.48, 85.69] | 83.09 [81.90, 84.07] | 1.55 [0.41, 2.64] | ahead |
| nl: wikiner-nl-wp3 | entities (gold) | micro-F1 | 83.70 [82.61, 84.70] | 86.64 [85.63, 87.59] | -2.94 [-4.05, -1.85] | behind |
| nl: wikiner-nl-wp3 | entities (gold) | micro-precision | 83.16 [82.06, 84.23] | 86.28 [85.24, 87.33] | -3.12 [-4.21, -2.02] | behind |
| nl: wikiner-nl-wp3 | entities (gold) | micro-recall | 84.24 [83.13, 85.25] | 87.00 [85.96, 87.99] | -2.76 [-3.93, -1.65] | behind |
| pt: pt_bosque-ud | entities (gold) | micro-F1 | 88.30 [86.58, 90.01] | 75.27 [72.77, 77.68] | 13.03 [10.84, 15.42] | ahead |
| pt: pt_bosque-ud | entities (gold) | micro-precision | 89.04 [87.14, 90.98] | 76.96 [74.27, 79.75] | 12.08 [9.54, 14.63] | ahead |
| pt: pt_bosque-ud | entities (gold) | micro-recall | 87.57 [85.56, 89.54] | 73.66 [70.89, 76.30] | 13.91 [11.49, 16.67] | ahead |
| tr: turkish-wikiner | entities (gold) | micro-F1 | 82.84 [80.81, 84.79] | 65.82 [63.46, 68.33] | 17.01 [14.38, 19.32] | ahead |
| tr: turkish-wikiner | entities (gold) | micro-precision | 82.65 [80.52, 84.71] | 68.49 [65.67, 71.28] | 14.15 [11.16, 16.97] | ahead |
| tr: turkish-wikiner | entities (gold) | micro-recall | 83.03 [80.86, 85.07] | 63.36 [60.70, 65.94] | 19.67 [16.92, 22.08] | ahead |
| zh: zh_gsdsimp-ud | entities (gold) | micro-F1 | 84.04 [81.02, 86.97] | 61.86 [57.51, 66.06] | 22.17 [18.04, 26.66] | ahead |
| zh: zh_gsdsimp-ud | entities (gold) | micro-precision | 84.59 [81.42, 87.57] | 67.08 [62.38, 71.86] | 17.52 [13.00, 22.28] | ahead |
| zh: zh_gsdsimp-ud | entities (gold) | micro-recall | 83.49 [79.86, 86.79] | 57.40 [52.59, 62.05] | 26.08 [21.24, 31.29] | ahead |
| nl: nl_alpino-ud-test | gold | UPOS | 96.91 [96.21, 97.44] | 94.42 [93.78, 95.02] | 2.49 [2.00, 3.00] | ahead |
| nl: nl_alpino-ud-test | gold | UFeats | 96.62 [96.06, 97.12] | 93.13 [92.47, 93.70] | 3.49 [3.04, 3.95] | ahead |
| nl: nl_alpino-ud-test | gold | Lemmas | 92.34 [91.80, 92.92] | 91.83 [91.24, 92.37] | 0.52 [0.15, 0.91] | ahead |
| nl: nl_alpino-ud-test | gold | UAS | 90.23 [89.24, 91.11] | 84.09 [82.87, 85.31] | 6.14 [5.08, 7.25] | ahead |
| nl: nl_alpino-ud-test | gold | LAS | 87.24 [86.15, 88.24] | 79.85 [78.57, 81.13] | 7.40 [6.20, 8.55] | ahead |
| nl: nl_alpino-ud-test | raw | Tokens | 99.66 [99.46, 99.82] | 99.82 [99.70, 99.93] | -0.16 [-0.34, 0.01] | within |
| nl: nl_alpino-ud-test | raw | Sentences | 95.90 [94.35, 97.34] | 89.60 [86.73, 92.03] | 6.30 [3.78, 9.05] | ahead |
| nl: nl_alpino-ud-test | raw | Words | 99.66 [99.46, 99.82] | 99.82 [99.70, 99.93] | -0.16 [-0.34, 0.01] | within |
| nl: nl_alpino-ud-test | raw | UPOS | 96.74 [96.11, 97.27] | 94.36 [93.70, 94.95] | 2.38 [1.88, 2.89] | ahead |
| nl: nl_alpino-ud-test | raw | UFeats | 96.80 [96.29, 97.28] | 92.97 [92.32, 93.58] | 3.83 [3.33, 4.36] | ahead |
| nl: nl_alpino-ud-test | raw | Lemmas | 92.05 [91.49, 92.67] | 91.65 [91.06, 92.23] | 0.40 [0.01, 0.86] | ahead |
| nl: nl_alpino-ud-test | raw | UAS | 93.37 [92.55, 94.08] | 83.70 [82.50, 84.92] | 9.67 [8.60, 10.84] | ahead |
| nl: nl_alpino-ud-test | raw | LAS | 91.11 [90.20, 91.92] | 79.57 [78.38, 80.82] | 11.54 [10.38, 12.58] | ahead |
| pt: pt_bosque-ud-test | gold | UPOS | 96.10 [95.82, 96.36] | 95.92 [95.66, 96.19] | 0.18 [-0.12, 0.45] | within |
| pt: pt_bosque-ud-test | gold | UFeats | 94.48 [94.16, 94.80] | 94.11 [93.79, 94.41] | 0.37 [0.04, 0.68] | ahead |
| pt: pt_bosque-ud-test | gold | Lemmas | 97.59 [97.39, 97.79] | 96.90 [96.69, 97.10] | 0.69 [0.50, 0.90] | ahead |
| pt: pt_bosque-ud-test | gold | UAS | 88.22 [87.63, 88.77] | 86.05 [85.48, 86.64] | 2.17 [1.54, 2.81] | ahead |
| pt: pt_bosque-ud-test | gold | LAS | 84.54 [83.90, 85.14] | 81.96 [81.32, 82.63] | 2.58 [1.95, 3.26] | ahead |
| pt: pt_bosque-ud-test | raw | Tokens | 99.86 [99.79, 99.92] | 99.87 [99.80, 99.93] | -0.01 [-0.06, 0.05] | within |
| pt: pt_bosque-ud-test | raw | Sentences | 93.50 [92.14, 94.81] | 84.48 [82.18, 86.78] | 9.02 [6.41, 11.50] | ahead |
| pt: pt_bosque-ud-test | raw | Words | 99.26 [99.14, 99.38] | 88.51 [88.07, 88.99] | 10.76 [10.24, 11.20] | ahead |
| pt: pt_bosque-ud-test | raw | UPOS | 96.75 [96.50, 96.98] | 84.15 [83.63, 84.67] | 12.60 [12.06, 13.09] | ahead |
| pt: pt_bosque-ud-test | raw | UFeats | 95.19 [94.90, 95.48] | 82.16 [81.65, 82.69] | 13.03 [12.45, 13.58] | ahead |
| pt: pt_bosque-ud-test | raw | Lemmas | 97.29 [97.09, 97.50] | 85.35 [84.89, 85.85] | 11.94 [11.47, 12.38] | ahead |
| pt: pt_bosque-ud-test | raw | UAS | 90.68 [90.16, 91.19] | 70.23 [69.40, 71.06] | 20.45 [19.54, 21.32] | ahead |
| pt: pt_bosque-ud-test | raw | LAS | 87.98 [87.39, 88.55] | 65.85 [65.01, 66.74] | 22.12 [21.20, 23.03] | ahead |
| tr: tr_boun-ud-test | gold | UPOS | 91.28 [90.72, 91.81] | 88.04 [87.40, 88.67] | 3.24 [2.56, 3.86] | ahead |
| tr: tr_boun-ud-test | gold | UFeats | 80.29 [79.54, 81.01] | 78.30 [77.53, 79.14] | 2.00 [1.32, 2.67] | ahead |
| tr: tr_boun-ud-test | gold | Lemmas | 86.00 [85.31, 86.61] | 80.24 [79.46, 80.94] | 5.76 [5.11, 6.36] | ahead |
| tr: tr_boun-ud-test | gold | UAS | 74.44 [73.05, 75.76] | 67.44 [66.03, 68.71] | 7.00 [5.68, 8.35] | ahead |
| tr: tr_boun-ud-test | gold | LAS | 67.13 [65.67, 68.45] | 57.87 [56.54, 59.16] | 9.25 [7.98, 10.53] | ahead |
| tr: tr_boun-ud-test | raw | Tokens | 99.56 [99.41, 99.71] | 99.42 [99.26, 99.60] | 0.14 [0.00, 0.27] | ahead |
| tr: tr_boun-ud-test | raw | Sentences | 78.82 [76.14, 81.22] | 82.09 [79.45, 84.67] | -3.28 [-6.27, -0.42] | behind |
| tr: tr_boun-ud-test | raw | Words | 96.71 [96.34, 97.08] | 97.06 [96.71, 97.41] | -0.35 [-0.70, 0.02] | within |
| tr: tr_boun-ud-test | raw | UPOS | 88.62 [87.99, 89.32] | 86.82 [86.12, 87.52] | 1.80 [1.14, 2.48] | ahead |
| tr: tr_boun-ud-test | raw | UFeats | 80.23 [79.44, 81.03] | 77.20 [76.40, 78.07] | 3.02 [2.35, 3.70] | ahead |
| tr: tr_boun-ud-test | raw | Lemmas | 84.42 [83.71, 85.09] | 78.83 [78.04, 79.58] | 5.59 [4.98, 6.21] | ahead |
| tr: tr_boun-ud-test | raw | UAS | 71.15 [69.82, 72.44] | 63.96 [62.51, 65.35] | 7.19 [5.92, 8.56] | ahead |
| tr: tr_boun-ud-test | raw | LAS | 64.31 [62.89, 65.78] | 55.86 [54.49, 57.22] | 8.45 [7.26, 9.75] | ahead |
| zh: zh_gsdsimp-ud-test | gold | UPOS | 87.55 [86.92, 88.22] | 90.35 [89.68, 91.02] | -2.80 [-3.57, -1.97] | behind |
| zh: zh_gsdsimp-ud-test | gold | UFeats | 95.86 [95.48, 96.23] | 98.43 [98.21, 98.64] | -2.57 [-2.98, -2.17] | behind |
| zh: zh_gsdsimp-ud-test | gold | Lemmas | 99.78 [99.67, 99.86] | 99.91 [99.84, 99.96] | -0.13 [-0.22, -0.05] | behind |
| zh: zh_gsdsimp-ud-test | gold | UAS | 67.22 [66.03, 68.48] | 73.11 [71.58, 74.68] | -5.89 [-7.60, -4.07] | behind |
| zh: zh_gsdsimp-ud-test | gold | LAS | 61.42 [60.21, 62.70] | 68.24 [66.60, 69.90] | -6.83 [-8.61, -4.99] | behind |
| zh: zh_gsdsimp-ud-test | raw | Tokens | 89.62 [88.81, 90.43] | 39.43 [38.65, 40.19] | 50.19 [48.99, 51.37] | ahead |
| zh: zh_gsdsimp-ud-test | raw | Sentences | 83.77 [80.32, 87.30] | 46.93 [42.54, 51.47] | 36.84 [31.24, 42.38] | ahead |
| zh: zh_gsdsimp-ud-test | raw | Words | 89.62 [88.81, 90.43] | 39.43 [38.65, 40.19] | 50.19 [48.99, 51.37] | ahead |
| zh: zh_gsdsimp-ud-test | raw | UPOS | 80.66 [79.76, 81.48] | 34.92 [34.21, 35.61] | 45.73 [44.63, 46.91] | ahead |
| zh: zh_gsdsimp-ud-test | raw | UFeats | 85.98 [85.14, 86.82] | 38.38 [37.61, 39.14] | 47.60 [46.37, 48.85] | ahead |
| zh: zh_gsdsimp-ud-test | raw | Lemmas | 89.41 [88.61, 90.21] | 39.43 [38.65, 40.19] | 49.97 [48.77, 51.15] | ahead |
| zh: zh_gsdsimp-ud-test | raw | UAS | 64.42 [62.89, 65.87] | 9.12 [8.48, 9.79] | 55.29 [53.62, 57.00] | ahead |
| zh: zh_gsdsimp-ud-test | raw | LAS | 60.98 [59.49, 62.45] | 8.59 [7.96, 9.24] | 52.38 [50.74, 54.05] | ahead |

## Frozen-encoder parser recovery: original nine languages

English and Italian retain their exact previous files and reuse the corresponding previous test results. The other seven final heads were read once in each input mode after development selection; no test-driven continuation. Other heads, the encoder and all other languages are unchanged. Intervals use 1,000 resamples, seed 20261106.

| Dataset | Input | Metric | Earlier nine-language model [95% CI] | Joint 84-language model [95% CI] | After parser recovery [95% CI] |
|---|---|---|---|---|---|
| de_gsd-ud-test | gold | UPOS | 96.16 [95.83, 96.49] | 95.78 [95.43, 96.12] | 95.78 [95.43, 96.12] |
| de_gsd-ud-test | gold | XPOS | 96.07 [95.71, 96.43] | 95.84 [95.48, 96.19] | 95.84 [95.48, 96.19] |
| de_gsd-ud-test | gold | AllTags | 83.34 [82.57, 84.04] | 83.09 [82.34, 83.75] | 83.09 [82.34, 83.75] |
| de_gsd-ud-test | gold | UFeats | 86.81 [86.11, 87.44] | 86.58 [85.91, 87.19] | 86.58 [85.91, 87.19] |
| de_gsd-ud-test | gold | Lemmas | 96.25 [95.95, 96.53] | 96.64 [96.37, 96.92] | 96.64 [96.37, 96.92] |
| de_gsd-ud-test | gold | UAS | 87.01 [86.18, 87.75] | 85.90 [84.99, 86.68] | 87.09 [86.26, 87.84] |
| de_gsd-ud-test | gold | LAS | 83.32 [82.43, 84.17] | 82.17 [81.24, 83.02] | 83.49 [82.59, 84.31] |
| de_gsd-ud-test | gold | CLAS | not measured | 77.15 [76.03, 78.29] | 78.67 [77.54, 79.74] |
| de_gsd-ud-test | gold | MLAS | not measured | 58.17 [56.95, 59.56] | 59.58 [58.38, 60.86] |
| de_gsd-ud-test | raw | Tokens | 97.40 [97.09, 97.69] | 97.40 [97.09, 97.69] | 97.40 [97.09, 97.69] |
| de_gsd-ud-test | raw | Sentences | 88.70 [86.70, 90.74] | 88.63 [86.65, 90.56] | 88.65 [86.73, 90.58] |
| de_gsd-ud-test | raw | Words | 97.06 [96.70, 97.39] | 97.06 [96.70, 97.39] | 97.06 [96.70, 97.39] |
| de_gsd-ud-test | raw | UPOS | 93.27 [92.79, 93.69] | 93.33 [92.87, 93.75] | 93.33 [92.87, 93.75] |
| de_gsd-ud-test | raw | XPOS | 93.38 [92.91, 93.83] | 93.35 [92.91, 93.79] | 93.35 [92.91, 93.79] |
| de_gsd-ud-test | raw | AllTags | 81.04 [80.35, 81.70] | 81.69 [80.99, 82.33] | 81.69 [80.99, 82.33] |
| de_gsd-ud-test | raw | UFeats | 84.39 [83.77, 85.03] | 85.16 [84.51, 85.74] | 85.16 [84.51, 85.74] |
| de_gsd-ud-test | raw | Lemmas | 93.36 [92.90, 93.78] | 94.07 [93.64, 94.48] | 94.07 [93.64, 94.48] |
| de_gsd-ud-test | raw | UAS | 84.71 [83.86, 85.52] | 84.31 [83.43, 85.14] | 85.50 [84.64, 86.25] |
| de_gsd-ud-test | raw | LAS | 81.17 [80.27, 82.00] | 81.05 [80.15, 81.88] | 82.38 [81.55, 83.18] |
| de_gsd-ud-test | raw | CLAS | not measured | 79.72 [78.63, 80.79] | 81.12 [80.13, 82.07] |
| de_gsd-ud-test | raw | MLAS | not measured | 59.29 [58.13, 60.48] | 60.36 [59.24, 61.44] |
| en_ewt-ud-test | gold | UPOS | 96.58 [96.26, 96.86] | 96.49 [96.19, 96.76] | 96.49 [96.19, 96.76] |
| en_ewt-ud-test | gold | XPOS | 96.02 [95.69, 96.32] | 95.73 [95.40, 96.04] | 95.73 [95.40, 96.04] |
| en_ewt-ud-test | gold | AllTags | 93.66 [93.25, 94.02] | 93.47 [93.07, 93.83] | 93.47 [93.07, 93.83] |
| en_ewt-ud-test | gold | UFeats | 96.44 [96.17, 96.69] | 96.37 [96.09, 96.62] | 96.37 [96.09, 96.62] |
| en_ewt-ud-test | gold | Lemmas | 97.09 [96.81, 97.36] | 97.26 [97.01, 97.52] | 97.26 [97.01, 97.52] |
| en_ewt-ud-test | gold | UAS | 88.56 [87.81, 89.26] | 88.34 [87.65, 89.01] | 88.34 [87.65, 89.01] |
| en_ewt-ud-test | gold | LAS | 85.43 [84.61, 86.19] | 85.12 [84.40, 85.87] | 85.12 [84.40, 85.87] |
| en_ewt-ud-test | gold | CLAS | not measured | 81.48 [80.57, 82.45] | 81.48 [80.57, 82.45] |
| en_ewt-ud-test | gold | MLAS | not measured | 75.10 [74.10, 76.11] | 75.10 [74.10, 76.11] |
| en_ewt-ud-test | raw | Tokens | 97.13 [96.83, 97.43] | 97.13 [96.83, 97.43] | 97.13 [96.83, 97.43] |
| en_ewt-ud-test | raw | Sentences | 75.28 [73.54, 77.14] | 75.85 [74.20, 77.60] | 75.85 [74.20, 77.60] |
| en_ewt-ud-test | raw | Words | 98.68 [98.45, 98.90] | 98.68 [98.45, 98.90] | 98.68 [98.45, 98.90] |
| en_ewt-ud-test | raw | UPOS | 95.95 [95.59, 96.29] | 95.95 [95.59, 96.27] | 95.95 [95.59, 96.27] |
| en_ewt-ud-test | raw | XPOS | 95.22 [94.83, 95.58] | 95.18 [94.78, 95.54] | 95.18 [94.78, 95.54] |
| en_ewt-ud-test | raw | AllTags | 93.14 [92.69, 93.57] | 93.39 [92.93, 93.80] | 93.39 [92.93, 93.80] |
| en_ewt-ud-test | raw | UFeats | 95.76 [95.42, 96.07] | 95.94 [95.61, 96.27] | 95.94 [95.61, 96.27] |
| en_ewt-ud-test | raw | Lemmas | 96.14 [95.81, 96.48] | 96.39 [96.04, 96.74] | 96.39 [96.04, 96.74] |
| en_ewt-ud-test | raw | UAS | 87.32 [86.58, 88.01] | 88.78 [88.07, 89.47] | 88.78 [88.07, 89.47] |
| en_ewt-ud-test | raw | LAS | 85.02 [84.25, 85.77] | 86.55 [85.82, 87.26] | 86.55 [85.82, 87.26] |
| en_ewt-ud-test | raw | CLAS | not measured | 83.38 [82.51, 84.30] | 83.38 [82.51, 84.30] |
| en_ewt-ud-test | raw | MLAS | not measured | 78.46 [77.50, 79.41] | 78.46 [77.50, 79.41] |
| es_ancora-ud-test | gold | UPOS | 98.10 [97.98, 98.24] | 98.00 [97.87, 98.14] | 98.00 [97.87, 98.14] |
| es_ancora-ud-test | gold | XPOS | 84.88 [84.41, 85.31] | 84.37 [83.90, 84.82] | 84.37 [83.90, 84.82] |
| es_ancora-ud-test | gold | AllTags | 83.67 [83.20, 84.13] | 83.21 [82.74, 83.67] | 83.21 [82.74, 83.67] |
| es_ancora-ud-test | gold | UFeats | 96.50 [96.33, 96.66] | 96.74 [96.58, 96.91] | 96.74 [96.58, 96.91] |
| es_ancora-ud-test | gold | Lemmas | 97.72 [97.58, 97.85] | 98.53 [98.42, 98.64] | 98.53 [98.42, 98.64] |
| es_ancora-ud-test | gold | UAS | 88.14 [87.68, 88.63] | 87.61 [87.18, 88.08] | 89.50 [89.08, 89.94] |
| es_ancora-ud-test | gold | LAS | 85.37 [84.89, 85.88] | 84.66 [84.20, 85.15] | 86.97 [86.51, 87.42] |
| es_ancora-ud-test | gold | CLAS | not measured | 79.49 [78.89, 80.12] | 82.53 [81.95, 83.14] |
| es_ancora-ud-test | gold | MLAS | not measured | 72.39 [71.73, 73.14] | 75.87 [75.23, 76.53] |
| es_ancora-ud-test | raw | Tokens | 99.84 [99.76, 99.90] | 99.84 [99.76, 99.90] | 99.84 [99.76, 99.90] |
| es_ancora-ud-test | raw | Sentences | 96.66 [95.79, 97.49] | 96.54 [95.70, 97.31] | 97.05 [96.24, 97.78] |
| es_ancora-ud-test | raw | Words | 99.83 [99.75, 99.89] | 99.83 [99.75, 99.89] | 99.83 [99.75, 99.89] |
| es_ancora-ud-test | raw | UPOS | 98.09 [97.96, 98.24] | 98.20 [98.07, 98.34] | 98.20 [98.07, 98.34] |
| es_ancora-ud-test | raw | XPOS | 85.63 [85.17, 86.09] | 85.87 [85.43, 86.33] | 85.87 [85.43, 86.33] |
| es_ancora-ud-test | raw | AllTags | 84.50 [84.05, 84.94] | 84.84 [84.40, 85.29] | 84.84 [84.40, 85.29] |
| es_ancora-ud-test | raw | UFeats | 96.57 [96.40, 96.73] | 96.83 [96.66, 96.99] | 96.83 [96.66, 96.99] |
| es_ancora-ud-test | raw | Lemmas | 97.75 [97.61, 97.89] | 98.58 [98.46, 98.70] | 98.58 [98.46, 98.70] |
| es_ancora-ud-test | raw | UAS | 90.08 [89.60, 90.51] | 90.92 [90.49, 91.33] | 91.93 [91.49, 92.31] |
| es_ancora-ud-test | raw | LAS | 87.42 [86.90, 87.92] | 88.35 [87.88, 88.78] | 89.70 [89.24, 90.11] |
| es_ancora-ud-test | raw | CLAS | not measured | 84.50 [83.92, 85.06] | 86.36 [85.78, 86.89] |
| es_ancora-ud-test | raw | MLAS | not measured | 78.68 [78.03, 79.32] | 80.63 [79.97, 81.23] |
| fr_gsd-ud-test | gold | UPOS | 97.21 [96.82, 97.58] | 96.99 [96.60, 97.39] | 96.99 [96.60, 97.39] |
| fr_gsd-ud-test | gold | UFeats | 96.24 [95.75, 96.67] | 96.06 [95.58, 96.49] | 96.06 [95.58, 96.49] |
| fr_gsd-ud-test | gold | Lemmas | 97.56 [97.23, 97.88] | 98.03 [97.72, 98.31] | 98.03 [97.72, 98.31] |
| fr_gsd-ud-test | gold | UAS | 89.36 [88.49, 90.23] | 88.98 [88.06, 89.81] | 89.52 [88.61, 90.43] |
| fr_gsd-ud-test | gold | LAS | 86.55 [85.57, 87.49] | 85.83 [84.90, 86.72] | 86.89 [85.93, 87.88] |
| fr_gsd-ud-test | gold | CLAS | not measured | 80.98 [79.72, 82.20] | 82.47 [81.26, 83.85] |
| fr_gsd-ud-test | gold | MLAS | not measured | 73.78 [72.25, 75.26] | 75.00 [73.45, 76.63] |
| fr_gsd-ud-test | raw | Tokens | 99.88 [99.79, 99.96] | 99.88 [99.79, 99.96] | 99.88 [99.79, 99.96] |
| fr_gsd-ud-test | raw | Sentences | 92.40 [89.74, 94.66] | 92.18 [89.49, 94.59] | 92.65 [90.07, 94.98] |
| fr_gsd-ud-test | raw | Words | 99.48 [99.29, 99.65] | 99.48 [99.29, 99.65] | 99.48 [99.29, 99.65] |
| fr_gsd-ud-test | raw | UPOS | 97.06 [96.71, 97.42] | 97.10 [96.71, 97.46] | 97.10 [96.71, 97.46] |
| fr_gsd-ud-test | raw | UFeats | 96.07 [95.59, 96.52] | 96.26 [95.82, 96.68] | 96.26 [95.82, 96.68] |
| fr_gsd-ud-test | raw | Lemmas | 97.42 [97.09, 97.77] | 97.96 [97.64, 98.25] | 97.96 [97.64, 98.25] |
| fr_gsd-ud-test | raw | UAS | 90.67 [89.74, 91.58] | 91.34 [90.43, 92.18] | 91.97 [91.12, 92.76] |
| fr_gsd-ud-test | raw | LAS | 88.07 [87.07, 89.05] | 88.96 [88.02, 89.84] | 89.78 [88.84, 90.63] |
| fr_gsd-ud-test | raw | CLAS | not measured | 85.67 [84.45, 86.80] | 86.65 [85.41, 87.83] |
| fr_gsd-ud-test | raw | MLAS | not measured | 79.66 [78.13, 81.14] | 80.57 [79.06, 81.91] |
| it_markit-ud-test | gold | UPOS | 95.69 [95.26, 96.12] | 95.81 [95.39, 96.26] | 95.81 [95.39, 96.26] |
| it_markit-ud-test | gold | XPOS | 96.31 [95.90, 96.68] | 96.26 [95.87, 96.66] | 96.26 [95.87, 96.66] |
| it_markit-ud-test | gold | AllTags | 89.51 [88.83, 90.21] | 89.79 [89.12, 90.43] | 89.79 [89.12, 90.43] |
| it_markit-ud-test | gold | UFeats | 91.91 [91.27, 92.50] | 92.12 [91.51, 92.73] | 92.12 [91.51, 92.73] |
| it_markit-ud-test | gold | Lemmas | 86.37 [85.60, 87.06] | 86.38 [85.63, 87.08] | 86.38 [85.63, 87.08] |
| it_markit-ud-test | gold | UAS | 81.82 [80.77, 82.86] | 82.05 [81.01, 83.15] | 82.05 [81.01, 83.15] |
| it_markit-ud-test | gold | LAS | 77.27 [76.19, 78.38] | 77.63 [76.52, 78.78] | 77.63 [76.52, 78.78] |
| it_markit-ud-test | gold | CLAS | not measured | 72.13 [70.70, 73.64] | 72.13 [70.70, 73.64] |
| it_markit-ud-test | gold | MLAS | not measured | 57.58 [56.03, 59.25] | 57.58 [56.03, 59.25] |
| it_markit-ud-test | raw | Tokens | 99.93 [99.86, 99.98] | 99.93 [99.86, 99.98] | 99.93 [99.86, 99.98] |
| it_markit-ud-test | raw | Sentences | 90.61 [87.84, 93.57] | 90.11 [87.27, 92.93] | 90.11 [87.27, 92.93] |
| it_markit-ud-test | raw | Words | 99.73 [99.58, 99.86] | 99.73 [99.58, 99.86] | 99.73 [99.58, 99.86] |
| it_markit-ud-test | raw | UPOS | 95.98 [95.54, 96.36] | 96.12 [95.75, 96.52] | 96.12 [95.75, 96.52] |
| it_markit-ud-test | raw | XPOS | 96.56 [96.18, 96.92] | 96.45 [96.05, 96.80] | 96.45 [96.05, 96.80] |
| it_markit-ud-test | raw | AllTags | 90.13 [89.50, 90.80] | 90.50 [89.88, 91.10] | 90.50 [89.88, 91.10] |
| it_markit-ud-test | raw | UFeats | 92.40 [91.81, 93.01] | 92.84 [92.29, 93.42] | 92.84 [92.29, 93.42] |
| it_markit-ud-test | raw | Lemmas | 86.46 [85.69, 87.17] | 86.65 [85.93, 87.35] | 86.65 [85.93, 87.35] |
| it_markit-ud-test | raw | UAS | 85.40 [84.26, 86.44] | 86.39 [85.36, 87.29] | 86.39 [85.36, 87.29] |
| it_markit-ud-test | raw | LAS | 81.12 [79.93, 82.18] | 82.22 [81.04, 83.28] | 82.22 [81.04, 83.28] |
| it_markit-ud-test | raw | CLAS | not measured | 77.84 [76.30, 79.29] | 77.84 [76.30, 79.29] |
| it_markit-ud-test | raw | MLAS | not measured | 64.34 [62.76, 65.86] | 64.34 [62.76, 65.86] |
| nl_alpino-ud-test | gold | UPOS | 96.89 [96.24, 97.44] | 96.91 [96.21, 97.44] | 96.91 [96.21, 97.44] |
| nl_alpino-ud-test | gold | XPOS | 95.09 [94.32, 95.77] | 95.05 [94.26, 95.72] | 95.05 [94.26, 95.72] |
| nl_alpino-ud-test | gold | AllTags | 94.31 [93.48, 95.00] | 94.13 [93.32, 94.87] | 94.13 [93.32, 94.87] |
| nl_alpino-ud-test | gold | UFeats | 96.73 [96.16, 97.22] | 96.62 [96.06, 97.12] | 96.62 [96.06, 97.12] |
| nl_alpino-ud-test | gold | Lemmas | 91.88 [91.32, 92.47] | 92.34 [91.80, 92.92] | 92.34 [91.80, 92.92] |
| nl_alpino-ud-test | gold | UAS | 90.41 [89.40, 91.32] | 87.18 [86.11, 88.11] | 90.23 [89.24, 91.11] |
| nl_alpino-ud-test | gold | LAS | 87.63 [86.63, 88.59] | 83.40 [82.24, 84.47] | 87.24 [86.15, 88.24] |
| nl_alpino-ud-test | gold | CLAS | not measured | 78.51 [77.05, 79.84] | 82.88 [81.34, 84.21] |
| nl_alpino-ud-test | gold | MLAS | not measured | 70.51 [68.69, 72.12] | 76.21 [74.34, 77.73] |
| nl_alpino-ud-test | raw | Tokens | 99.66 [99.46, 99.82] | 99.66 [99.46, 99.82] | 99.66 [99.46, 99.82] |
| nl_alpino-ud-test | raw | Sentences | 94.81 [92.78, 96.42] | 95.56 [94.03, 97.03] | 95.90 [94.35, 97.34] |
| nl_alpino-ud-test | raw | Words | 99.66 [99.46, 99.82] | 99.66 [99.46, 99.82] | 99.66 [99.46, 99.82] |
| nl_alpino-ud-test | raw | UPOS | 96.66 [96.01, 97.23] | 96.74 [96.11, 97.27] | 96.74 [96.11, 97.27] |
| nl_alpino-ud-test | raw | XPOS | 95.03 [94.32, 95.67] | 95.27 [94.61, 95.94] | 95.27 [94.61, 95.94] |
| nl_alpino-ud-test | raw | AllTags | 94.31 [93.58, 95.01] | 94.50 [93.79, 95.22] | 94.50 [93.79, 95.22] |
| nl_alpino-ud-test | raw | UFeats | 96.78 [96.26, 97.27] | 96.80 [96.29, 97.28] | 96.80 [96.29, 97.28] |
| nl_alpino-ud-test | raw | Lemmas | 91.54 [90.97, 92.14] | 92.05 [91.49, 92.67] | 92.05 [91.49, 92.67] |
| nl_alpino-ud-test | raw | UAS | 91.67 [90.65, 92.52] | 91.93 [91.05, 92.69] | 93.37 [92.55, 94.08] |
| nl_alpino-ud-test | raw | LAS | 89.06 [87.93, 89.96] | 89.09 [88.06, 89.97] | 91.11 [90.20, 91.92] |
| nl_alpino-ud-test | raw | CLAS | not measured | 85.23 [83.88, 86.40] | 87.99 [86.70, 89.11] |
| nl_alpino-ud-test | raw | MLAS | not measured | 79.56 [77.95, 81.03] | 82.24 [80.73, 83.63] |
| pt_bosque-ud-test | gold | UPOS | 96.56 [96.32, 96.80] | 96.10 [95.82, 96.36] | 96.10 [95.82, 96.36] |
| pt_bosque-ud-test | gold | UFeats | 94.80 [94.49, 95.09] | 94.48 [94.16, 94.80] | 94.48 [94.16, 94.80] |
| pt_bosque-ud-test | gold | Lemmas | 97.49 [97.30, 97.70] | 97.59 [97.39, 97.79] | 97.59 [97.39, 97.79] |
| pt_bosque-ud-test | gold | UAS | 87.64 [87.07, 88.25] | 85.88 [85.20, 86.50] | 88.22 [87.63, 88.77] |
| pt_bosque-ud-test | gold | LAS | 84.01 [83.39, 84.63] | 81.83 [81.11, 82.53] | 84.54 [83.90, 85.14] |
| pt_bosque-ud-test | gold | CLAS | not measured | 77.38 [76.47, 78.25] | 80.24 [79.51, 81.07] |
| pt_bosque-ud-test | gold | MLAS | not measured | 67.49 [66.45, 68.46] | 70.29 [69.36, 71.22] |
| pt_bosque-ud-test | raw | Tokens | 99.86 [99.79, 99.92] | 99.86 [99.79, 99.92] | 99.86 [99.79, 99.92] |
| pt_bosque-ud-test | raw | Sentences | 92.27 [90.68, 93.82] | 92.57 [91.08, 93.99] | 93.50 [92.14, 94.81] |
| pt_bosque-ud-test | raw | Words | 99.26 [99.14, 99.38] | 99.26 [99.14, 99.38] | 99.26 [99.14, 99.38] |
| pt_bosque-ud-test | raw | UPOS | 96.59 [96.32, 96.83] | 96.75 [96.50, 96.98] | 96.75 [96.50, 96.98] |
| pt_bosque-ud-test | raw | UFeats | 95.06 [94.75, 95.34] | 95.19 [94.90, 95.48] | 95.19 [94.90, 95.48] |
| pt_bosque-ud-test | raw | Lemmas | 96.96 [96.73, 97.19] | 97.29 [97.09, 97.50] | 97.29 [97.09, 97.50] |
| pt_bosque-ud-test | raw | UAS | 89.38 [88.77, 89.91] | 89.73 [89.18, 90.26] | 90.68 [90.16, 91.19] |
| pt_bosque-ud-test | raw | LAS | 86.47 [85.84, 87.04] | 86.86 [86.27, 87.44] | 87.98 [87.39, 88.55] |
| pt_bosque-ud-test | raw | CLAS | not measured | 83.83 [83.04, 84.61] | 85.27 [84.54, 85.99] |
| pt_bosque-ud-test | raw | MLAS | not measured | 75.21 [74.33, 76.14] | 76.55 [75.69, 77.41] |
| tr_boun-ud-test | gold | UPOS | 91.50 [90.98, 92.05] | 91.28 [90.72, 91.81] | 91.28 [90.72, 91.81] |
| tr_boun-ud-test | gold | XPOS | not measured | 44.67 [43.81, 45.55] | 44.67 [43.81, 45.55] |
| tr_boun-ud-test | gold | AllTags | not measured | 37.77 [37.03, 38.61] | 37.77 [37.03, 38.61] |
| tr_boun-ud-test | gold | UFeats | 80.57 [79.80, 81.33] | 80.29 [79.54, 81.01] | 80.29 [79.54, 81.01] |
| tr_boun-ud-test | gold | Lemmas | 86.40 [85.75, 87.00] | 86.00 [85.31, 86.61] | 86.00 [85.31, 86.61] |
| tr_boun-ud-test | gold | UAS | 75.25 [73.92, 76.56] | 72.36 [70.98, 73.75] | 74.44 [73.05, 75.76] |
| tr_boun-ud-test | gold | LAS | 67.92 [66.56, 69.24] | 64.97 [63.60, 66.26] | 67.13 [65.67, 68.45] |
| tr_boun-ud-test | gold | CLAS | not measured | 62.95 [61.49, 64.24] | 65.35 [63.77, 66.74] |
| tr_boun-ud-test | gold | MLAS | not measured | 43.83 [42.52, 45.02] | 45.80 [44.46, 47.03] |
| tr_boun-ud-test | raw | Tokens | 99.56 [99.41, 99.71] | 99.56 [99.41, 99.71] | 99.56 [99.41, 99.71] |
| tr_boun-ud-test | raw | Sentences | 81.28 [78.81, 83.58] | 79.89 [77.32, 82.20] | 78.82 [76.14, 81.22] |
| tr_boun-ud-test | raw | Words | 96.71 [96.34, 97.08] | 96.71 [96.34, 97.08] | 96.71 [96.34, 97.08] |
| tr_boun-ud-test | raw | UPOS | 89.08 [88.46, 89.72] | 88.62 [87.99, 89.32] | 88.62 [87.99, 89.32] |
| tr_boun-ud-test | raw | XPOS | not measured | 43.51 [42.62, 44.33] | 43.51 [42.62, 44.33] |
| tr_boun-ud-test | raw | AllTags | not measured | 37.44 [36.68, 38.26] | 37.44 [36.68, 38.26] |
| tr_boun-ud-test | raw | UFeats | 80.24 [79.44, 81.03] | 80.23 [79.44, 81.03] | 80.23 [79.44, 81.03] |
| tr_boun-ud-test | raw | Lemmas | 84.51 [83.78, 85.20] | 84.42 [83.71, 85.09] | 84.42 [83.71, 85.09] |
| tr_boun-ud-test | raw | UAS | 69.75 [68.39, 71.06] | 69.30 [67.85, 70.58] | 71.15 [69.82, 72.44] |
| tr_boun-ud-test | raw | LAS | 63.03 [61.61, 64.41] | 62.33 [60.88, 63.71] | 64.31 [62.89, 65.78] |
| tr_boun-ud-test | raw | CLAS | not measured | 61.35 [59.79, 62.70] | 63.21 [61.68, 64.58] |
| tr_boun-ud-test | raw | MLAS | not measured | 45.06 [43.56, 46.41] | 46.89 [45.51, 48.21] |
| zh_gsdsimp-ud-test | gold | UPOS | 88.77 [88.18, 89.35] | 87.55 [86.92, 88.22] | 87.55 [86.92, 88.22] |
| zh_gsdsimp-ud-test | gold | XPOS | 87.86 [87.24, 88.47] | 87.14 [86.47, 87.79] | 87.14 [86.47, 87.79] |
| zh_gsdsimp-ud-test | gold | AllTags | 85.15 [84.47, 85.82] | 83.66 [82.95, 84.39] | 83.66 [82.95, 84.39] |
| zh_gsdsimp-ud-test | gold | UFeats | 96.03 [95.69, 96.39] | 95.86 [95.48, 96.23] | 95.86 [95.48, 96.23] |
| zh_gsdsimp-ud-test | gold | Lemmas | 99.80 [99.71, 99.88] | 99.78 [99.67, 99.86] | 99.78 [99.67, 99.86] |
| zh_gsdsimp-ud-test | gold | UAS | 69.15 [67.81, 70.55] | 64.08 [62.81, 65.38] | 67.22 [66.03, 68.48] |
| zh_gsdsimp-ud-test | gold | LAS | 63.29 [61.88, 64.70] | 57.55 [56.30, 58.83] | 61.42 [60.21, 62.70] |
| zh_gsdsimp-ud-test | gold | CLAS | not measured | 56.22 [54.82, 57.68] | 59.72 [58.32, 61.19] |
| zh_gsdsimp-ud-test | gold | MLAS | not measured | 44.42 [42.92, 45.86] | 47.94 [46.40, 49.45] |
| zh_gsdsimp-ud-test | raw | Tokens | 89.62 [88.81, 90.43] | 89.62 [88.81, 90.43] | 89.62 [88.81, 90.43] |
| zh_gsdsimp-ud-test | raw | Sentences | 85.31 [82.10, 88.41] | 83.64 [80.04, 86.90] | 83.77 [80.32, 87.30] |
| zh_gsdsimp-ud-test | raw | Words | 89.62 [88.81, 90.43] | 89.62 [88.81, 90.43] | 89.62 [88.81, 90.43] |
| zh_gsdsimp-ud-test | raw | UPOS | 80.96 [80.07, 81.81] | 80.66 [79.76, 81.48] | 80.66 [79.76, 81.48] |
| zh_gsdsimp-ud-test | raw | XPOS | 80.11 [79.28, 80.98] | 79.75 [78.91, 80.63] | 79.75 [78.91, 80.63] |
| zh_gsdsimp-ud-test | raw | AllTags | 77.97 [77.04, 78.87] | 77.47 [76.54, 78.38] | 77.47 [76.54, 78.38] |
| zh_gsdsimp-ud-test | raw | UFeats | 85.94 [85.11, 86.79] | 85.98 [85.14, 86.82] | 85.98 [85.14, 86.82] |
| zh_gsdsimp-ud-test | raw | Lemmas | 89.45 [88.61, 90.27] | 89.41 [88.61, 90.21] | 89.41 [88.61, 90.21] |
| zh_gsdsimp-ud-test | raw | UAS | 62.78 [61.24, 64.26] | 62.91 [61.40, 64.43] | 64.42 [62.89, 65.87] |
| zh_gsdsimp-ud-test | raw | LAS | 58.84 [57.23, 60.35] | 58.91 [57.38, 60.42] | 60.98 [59.49, 62.45] |
| zh_gsdsimp-ud-test | raw | CLAS | not measured | 55.70 [53.91, 57.39] | 58.05 [56.40, 59.67] |
| zh_gsdsimp-ud-test | raw | MLAS | not measured | 47.57 [45.78, 49.21] | 49.33 [47.69, 50.97] |

### Paired recovery regression

Against the joint 84-language model, Turkish-BOUN raw **sentence F1** changes from **79.89 to 78.82**, difference **-1.073 [-1.998, -0.134]** (1,000 paired sentence resamples, seed 20261106). This is the only significantly negative difference among the 139 recomputed UD rows. Turkish raw LAS rises from 62.33 to 64.31, difference 1.978 [1.413, 2.578]. Selection was fixed to full-dev gold LAS before these test results; no test-driven reversion or additional training was performed.
