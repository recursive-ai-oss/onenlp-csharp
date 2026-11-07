# The distilled accurate tier

`RecursiveAI.OneNlp.Models.Multilingual`: one six-layer, width-384 student of mmBERT-base (35.5 M weights, distilled in PyTorch from mmBERT-base and exported once; `recipes/accurate/student-v1`), **fine-tuned** with per-language heads for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese (tagger, morphologizer, edit-tree lemmatizer, biaffine parser, entity recognizer), plus an open-type entity head on the **frozen** student. The frozen student is also published alone (`RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder`, `FrozenStudentEncoder.Create()`).

- **Recipe:** `recipes/accurate/student-multilingual` (`train`, with `--export DIR` writing the encoder and one head file per language; `export-open` re-saves the open-type head over the frozen encoder). The languages are data: `--langs` names codes, `--languages tools/fetch/ud/languages.tsv` supplies the gating treebank of any other language whose data has been fetched, so adding a language is a line in that file and a fetch.
- **Fine-tuned for the heads, frozen for the open-type head.** On English-EWT UPOS a fine-tuned student scored 95.88 and a frozen one 93.01 (`recipes/accurate/student-v1/RESULTS.md`); the open-type head was trained, and checked, on the frozen student and keeps it.
- **Training run:** seed 1, 2,000 steps of about 3,000 words over the nine languages (language-balanced), learning rate 2e-4 chosen on development against 1e-4 (80.40 against 77.85 mean development score at step 1,250); the development score was still rising at the end, so the model is under-trained, not converged. About 2 hours on four cores. Provenance: the package's `PROVENANCE.md`.
- **Size and speed (indicative, one thread, a loaded 16-core x64 machine, load about 9):** the packages total 482 MB (main 172, Encoder 131, FrozenEncoder 136, Segmenters 43). Full pipeline (tokenize, tag, lemmatize, parse, entities): 1,153 tokens/s on English and 1,162 on German test sentences (400 sentences each, best of three), peak RSS about 0.8 GB. The first `Nlp.Analyze` of a short sentence took 37 ms and the process stayed at 47 MB (the weights are mapped from the assembly, so the pages a sentence does not touch are not read yet).
- **Training cost against PyTorch (indicative):** one fine-tuning step of the student, 24 sentences of 32 pieces, 8 threads, same machine: 0.41 s in C# (forward 0.11, backward 0.24, clipping 0.03, AdamW 0.04) against 0.13 s in PyTorch 2.11 on CPU, 3.2 times. Gradient clipping and AdamW account for a disproportionate part (about 0.07 s against 0.01 s); both are on the OneModel backlog.

## Results

Every row is information: the base-size tier is the one that gates. Per language and input mode, with the fast tier's rows and the published Stanza, UDPipe 2 and Trankit numbers beside ours: `distilled-tier-rows.md`. Test splits were read once for this model; development sets chose the learning rate and the checkpoint.

### Entities (micro F1, test, gold tokens, bootstrap 95% interval over sentences, 1,000 resamples, seed 20261106)

Entity test sets are capped at 3,000 sentences sampled evenly, so rows are not the fast tier's full-test rows.

| Language | Corpus | Distilled tier | Fast tier (full test) |
|---|---|---|---|
| English | UNER EWT | 68.89 [66.01, 71.52] | 63.09 [60.01, 65.89] |
| German | GermEval 2014 | 71.78 [70.07, 73.51] | 66.28 |
| French | WikiNER | 74.99 [73.65, 76.20] | 82.48 [81.74, 83.28] |
| Spanish | WikiNER | 78.62 [77.36, 79.77] | 82.88 [82.12, 83.64] |
| Portuguese | UNER Bosque | 80.87 [78.42, 83.26] | 73.13 [70.58, 75.60] |
| Italian | WikiNER | 77.04 [75.70, 78.31] | 86.35 [85.73, 87.01] |
| Dutch | WikiNER | 77.47 [76.14, 78.70] | 87.47 [86.91, 87.98] |
| Turkish | Turkish-WikiNER | 71.93 [69.74, 74.38] | 67.40 [64.90, 69.81] |
| Chinese | UNER GSDSimp | 72.78 [68.50, 76.90] | 62.80 [58.46, 67.23] |

### Where it stands

- It is ahead of the fast tier on UPOS in English raw text, Dutch and Turkish and on entities in English, German, Portuguese, Turkish and Chinese; it trails it on lemmas everywhere (the lemmatizer here has less capacity than the fast tier's), on parsing (LAS 4 to 15 points), on French, Spanish, Italian and Dutch entities, and on Chinese tagging and parsing (UPOS 85.56 against 90.41, LAS 54.05 against 68.76).
- It trails the published UDPipe 2 and Stanza numbers by a wide margin on every row: a short run of a six-layer student. The base-size tier (`accurate-tier.md`) is the model for accuracy.
- Sentence splitting comes from the parser and is weakest in English (65.4 on EWT raw text) and Chinese.
- Hindi has no allowed training data and is not in this package.
