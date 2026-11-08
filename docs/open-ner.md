# Open-type entities

Entity types named at run time: `Nlp.Entities(text, "person", "organization")`, with a threshold `Nlp.Entities(text, ["contract party"], 0.1f)`, or `OpenEntityRecognizer.Recognize(doc, types)` on a document you tokenized.

## Model

- The encoder reads the text once. Candidate spans (up to 12 tokens, flat in the output) get a vector from the span's first, last and mean token vectors and its width; every type name is run through the same encoder and mean-pooled and passed through a feed-forward layer to one vector. A span scores a type with a dot product of the two (scaled by 1/sqrt(width), plus a learned offset) in a shared 128-wide space, and a sigmoid turns it into a probability (Zaratiana et al. 2023, sections 3.2 to 3.4; the paper only).
- Training uses binary cross-entropy per token-type cell. A type an example lists with no spans is a negative; a type it does not list teaches nothing, since one data set may label "person" and not "actor". Up to `negativeTypes` negatives are drawn per example.
- Prediction keeps spans at or above the threshold (0.5 by default, per call with `threshold`), and where two overlap the more probable wins. The label is the type string as you gave it. Type vectors are cached, so a fixed list of types costs one encoder pass per type, once.
- Types: 1 to 256 distinct strings of at most 200 characters, in any language the encoder reads (the shipped head is trained on English).

## Happy path

`Nlp.Entities(string text, params string[] types)` finds the registered open-type pipeline (`ModelPackages.RegisterOpenEntities`, called by the model package) and throws, naming `RecursiveAI.OneNlp.Models.Multilingual`, when there is none. Nothing is downloaded. A pipeline's `Recognize` is thread-safe.

The default threshold of 0.5 was the best on the Few-NERD and UNER dev sets, and it suits common type names: "organization" finds "Acme Corp." (0.76) and "Beta LLC" (0.80). An unusual name such as "contract party" scores far lower on the same text (0.15 to 0.17), so it returns nothing at 0.5. Pass a lower threshold to `Nlp.Entities(text, types, threshold)`, and check the results, or use a type name the model has seen. To hold the recognizer yourself, `MultilingualModel.LoadOpenEntities()` returns a pipeline whose `Components` include an `IOpenEntityRecognizer`; its `Recognize(doc, types, threshold)` takes the same threshold (null for the component's default).

## Data and limits

The recipe (`recipes/openner`) trains on Few-NERD and UNER English (CC BY-SA 4.0) and on entity labels written by Qwen2.5-7B-Instruct-AWQ (Apache-2.0) for FineWeb and Wikipedia passages, after a precision filter. The model therefore has share-alike lineage. CrossNER (MIT) only measures. Results, hand-check numbers and every hash are in `recipes/openner/PROVENANCE.md` and `results.md`.

The model is English only and well below the best published open-type recognizers on zero-shot sets (CrossNER 21 to 41 F1 on our run; recall is the weak side). When the types are fixed and you have labels, train the ordinary `EntityRecognizer`.

A saved head names its encoder, as every head does; load it with the encoder's directory available.

## Published comparison

Strict token-span-and-type micro F1, threshold **0.5**, frozen student-v1, shipped
human + Qwen head. Intervals are sentence-bootstrap percentile 95% CIs (1,000
resamples, seed 1). All seven packed-model scores and intervals reproduce
`recipes/openner/results.md` to the reported precision; neither weights nor
thresholds were changed.

The consumer `tools/OneNlp.OpenNerGate` links the recipe's scorer but loads
`MultilingualModel.LoadOpenEntities()` from packages. The measured combination is
**Models.Multilingual and FrozenEncoder 0.3.0-preview.1**, core and Neural
**0.4.0-preview.1**. The newer scratch feed contained no multilingual package;
the previously packed model was used, not repacked. Its embedded head (2,385,609
bytes, SHA-256 `25d7cd3f8f3cc67b73aaca5d49db00dc88dc1758ffb34f43658aeb2fefff1d68`),
frozen encoder (`aee56231d97134e455fe80a79738eb294c7974c61311412256fa9cd854eeeae6`)
and tokenizer (`197d4cc5406ee12cc50c8b5511f2393cc32d9db321545979ce041c1199178356`)
were checked against the current manifests **before inference**. A newly packed
multilingual 0.4.0-preview.1 is not verified by this run.

### Models and reference

Reference: Zaratiana et al., *GLiNER: Generalist Model for Named Entity Recognition
using Bidirectional Transformer*, [arXiv:2311.08526v1](https://arxiv.org/abs/2311.08526v1),
14 November 2023, **Table 1, §4.1** (zero-shot OOD benchmark); encoder and training
in **§3.2**, evaluation in **§3.4**, decoding in **§2.3**. The paper is [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/);
PDF SHA-256 `2b6efdc8337d12814308e480378587059a34a960d1da816f192b8cc0aaea04d0`.
Only the paper was read; no GLiNER implementation or weights were used.

| Model | Parameter count | Encoder |
|---|---:|---|
| OneNLP open-type | 35.5M encoder weights + **595,969 head parameters** (about 36.1M total) | Frozen student-v1, 6 layers × 384, distilled from mmBERT-base |
| GLiNER-S (smallest, primary published reference) | **50M**, as reported | DeBERTa-v3 family, S size in Table 1 |
| GLiNER-M (information) | **90M**, as reported | DeBERTa-v3 family, M size in Table 1 |
| GLiNER-L (information) | **0.3B**, as reported | DeBERTa-v3 family, L size in Table 1 |

These are the paper's counts, not measurements of downloaded GLiNER weights.
Table 1 and §3.2 identify the encoder family and size variants, not exact
checkpoint revisions. Their encoders are trained on Pile-NER; ours is frozen,
and our head trains on Few-NERD, UNER and filtered Qwen labels. This is a
comparison to other small open-type models, not a controlled architecture study.

### Rows

“Position” compares the published **S** point estimate with our CI: above our
upper bound is **behind**, below our lower bound is **ahead**, otherwise **within
noise**. It is descriptive, not a paired significance test. Published GLiNER
numbers have no CI. “Not comparable” means protocol equivalence is not established;
it is not a passing quality verdict.

| Evaluation row | Sentences scored | OneNLP F1 [95% CI] | GLiNER-S | GLiNER-M (info) | GLiNER-L (info) | Position vs S | Protocol verdict |
|---|---:|---|---:|---:|---:|---|---|
| UNER English-EWT test, fixed PER/ORG/LOC | 2,077 | **67.14 [64.34, 69.97]** | — | — | — | — | Not comparable: no UNER row in the paper |
| Few-NERD supervised test, seen types | 37,637 | **49.89 [49.63, 50.15]** | — | — | — | — | Not comparable: no Few-NERD row in the paper |
| CrossNER politics test, zero-shot | 651 | **41.27 [38.58, 44.09]** | 61.5 | 68.6 | 72.6 | **Behind** | Not comparable (protocol caveats below) |
| CrossNER science test, zero-shot | 543 | **36.50 [33.09, 40.06]** | 55.6 | 58.1 | 62.6 | **Behind** | Not comparable (protocol caveats below) |
| CrossNER music test, zero-shot | 465 | **26.01 [23.67, 28.35]** | 60.9 | 69.4 | 69.6 | **Behind** | Not comparable (protocol caveats below) |
| CrossNER literature test, zero-shot | 416 | **30.38 [27.88, 33.05]** | 60.0 | 59.7 | 64.4 | **Behind** | Not comparable (protocol caveats below) |
| CrossNER AI test, zero-shot | 431 | **21.10 [17.79, 24.52]** | 50.7 | 51.8 | 57.2 | **Behind** | Not comparable (protocol caveats below) |

All five CrossNER rows are numerically behind **even the smallest** published
GLiNER model, and behind M and L as well. The gaps to S are respectively 20.23,
19.10, 34.89, 29.62 and 29.60 F1 points. There is no evidence here that the
published-small-model quality target is met; the protocol caveats do not turn
these losses into passes. UNER and Few-NERD lack a same-dataset published
GLiNER comparison in this paper.

The fixed-type black-box reference remains **spaCy 3.8.18, 62.32 [59.45, 65.20]**
on UNER English-EWT test, trained with the documented default configuration on
the same UNER training data. Its point estimate is below our lower CI bound
(**ahead** by the point-vs-CI convention), but the intervals overlap and no paired
spaCy comparison was run here. It is not an open-type or zero-shot baseline.

### Protocol audit

- **Splits:** we read the fetched CrossNER domain test files, never train on them.
  The paper §3.4 names the OOD benchmark comprising CrossNER and MIT; it does
  not give file hashes, sentence counts or exact split revisions. Identical
  input rows cannot be certified from the paper alone.
- **Filtering:** the unchanged recipe excludes empty sentences, sentences over
  120 tokens and tokens containing whitespace. It scores gold tokens, joined
  by single spaces, not a raw-text tokenization pipeline. Counts in the table
  are after filtering: all fetched UNER and CrossNER sentences remain; Few-NERD
  retains 37,637 of 37,648 (11 excluded). The paper does not specify these
  filters or enough input fingerprints to certify the same CrossNER inputs.
- **Labels:** our scorer asks for the union of types in the retained file on
  every sentence, including absent types as negatives, ordered ordinally.
  `tools/openner/fetch.py` expands labels to words, e.g. `politicalparty` →
  `political party`, `programlang` → `programming language`, `otherperson` →
  `person`, `organization` → `organisation`. The paper does not enumerate
  the exact strings or inventories requested for each CrossNER row, so label
  equivalence is unverified. Our type counts are politics 9, science 17, music
  13, literature 12, AI 14; UNER 3; Few-NERD 67 derived labels (not the 66
  original fine labels). “Zero-shot” here means no CrossNER examples used
  to train or select the head, not that every label word was unseen in training.
- **Threshold and decoding:** paper §2.3 keeps scores **>0.5**, with greedy flat
  non-overlap; ours keeps **≥0.5**, also greedy flat non-overlap, up to 12 tokens.
  These have the same nominal threshold, but the exact-boundary convention
  differs. No threshold was retuned on test.
- **Metric:** paper §3.4 says exact-match entity F1. Ours is micro F1 over exact
  token range **and type**, sentence-bootstrapped, not relaxed matching. The
  paper supplies no bootstrap intervals or precise aggregation details.
- **Few-NERD:** our converter adds both fine and coarse labels for a mention
  where they differ, while prediction is flat and non-overlapping. Thus this
  seen-types row is the recipe's own two-level target, **not** a conventional
  fine-only Few-NERD benchmark score. It must not be compared to a published
  fine-only score without a separate, matching evaluation.

See the [results table](results-table.md#open-type-ner) for the same rows, and
`tools/OneNlp.OpenNerGate/README.md` for the command. This CPU-only measurement
uses Linux x64, four threads; no training, GPU inference or speed claim is made.

### Measurement environment and package fingerprints

Measured 8 November 2026 on Linux x64, AMD Ryzen 9 7950X3D (16 physical cores,
32 logical; AVX2, AVX-512F and FMA available), four reserved execution threads.
.NET SDK 10.0.403, runtime 10.0.14, OneModel and OneModel.Training 1.0.0. No GPU
was used. Load averages (1/5/15 min) were 2.97/3.72/7.08 before and
7.58/5.77/7.11 after; during-run load was not sampled. These are quality results,
not timings. This measurement does not add a managed speed row: no managed
open-type comparator is available.

Package SHA-256 fingerprints of the inputs to restore:

| Package | SHA-256 |
|---|---|
| Models.Multilingual 0.3.0-preview.1 | `0ce6681c4ec43e66990ed06b274a2a366cc9cd4b9eb44710e1d4adad5880a2ad` |
| Models.Multilingual.FrozenEncoder 0.3.0-preview.1 | `2aa706a43562471d0870654eda6d14c1511b76f5652bfd07ed5305f84eda775b` |
| OneNlp 0.4.0-preview.1 | `bb1824efb015ffd09b696233f3b6ae85d3a189d85c001e8dba5d7f8df5f162bd` |
| OneNlp.Neural 0.4.0-preview.1 | `93bc0858c9fcae426424f60903377955403e8aa2307e68feda1d52a987d1abd5` |

Data fingerprints are in `recipes/openner/PROVENANCE.md` and the fetched
`MANIFEST.tsv`. The loaded dependency graph was checked to use the core,
Neural and OneModel versions above, rather than the older transitive runtime.
