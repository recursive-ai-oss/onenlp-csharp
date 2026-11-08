# The base-size accurate tier

`RecursiveAI.OneNlp.Models.Multilingual.Large` shares one fine-tuned mmBERT-base encoder across **84 UD languages**, with entity heads for **19**. It is distinct from `.Models.Multilingual`, the distilled model covering nine languages. The full language list is `MultilingualLargeModel.Languages`; annotation availability and data gaps are in `docs/ud-licences.md`. A referenced fast-tier package is preferred by the plain `Nlp` verbs; explicit `Tier.Accurate` prefers this base-size package over the distilled one.

**The published-reference quality bar is not met.** Every metric, language, input mode and confidence interval is reported in [accurate-tier-rows.md](accurate-tier-rows.md), including separate counts for the best published system per row and Stanza 1.5.1 pretrained/charlm without a transformer. Missing references are not passes. The table also includes the previous nine-language model and a paired comparison with the same-data spaCy 3.8.18 baselines. No multilingual average substitutes for those rows. The final read covers **72,685 UD test sentences / 1,202,477 gold words**, in both gold-token and raw-text modes, plus 19 entity samples. There are **1,719 metric rows**: **1,207/1,314 best-published comparisons miss**, and **542/650 Stanza-only comparisons miss** (different view populations, not comparable pass percentages). The paired spaCy information view has 113 rows ahead, 16 overlapping and 15 behind, out of 144.

## Reproducible recipe

Recipe: `recipes/accurate/multilingual`, shared reader/sampler: `recipes/accurate/common`. The packed `PROVENANCE.md`, `corpus.json`, `lexicon-dev.tsv`, manifests and notices identify the exact inputs and selected weights.

- **Base:** mmBERT-base revision `c5955035435e2bf121cde7f3c8863ef52ff35d82` (MIT), continued from the previous nine-language checkpoint. Encoder tensors and matching heads are copied by name; new heads and all optimizer moments start fresh. The frozen token table is unchanged.
- **Data:** UD r2.18, 109 training treebanks; UNER, GermEval, WikiNER, Turkish-WikiNER, KPWr-NER and RONEC entity data. Only train/dev roles train or select anything. Test-only treebanks never train. Extra-treebank training examples repeating a gating test text are excluded. Official split overlap in the original nine is documented in their manifests. ShareAlike lineage: **yes**, with all dataset credits, including Ligurian-GLT under C-UDA 1.0, in the notices.
- **Training:** C# on OneModel and OneModel.Training 1.0.0; seed 3, AdamW, batch target 3,000 words, language-temperature exponent 0.3, parser loss weight 1.5. Peak learning rate 6e-5, a 1,200-step cosine schedule. Training stopped at **step 1,050**, also the best development checkpoint; no test-driven continuation or language boosts.
- **Selection:** every 150 steps, mean of available per-language development metrics. The selection curve was 59.00, 69.63, 73.35, 75.38, 76.81, 77.79, **78.24**. This is a development selection statistic, not a test-quality claim. Small ordered dev samples are domain-biased and noisy; exact sampling limits are in provenance.
- **Heads:** XPOS tagger, UPOS/features morphologizer, edit-tree lemmatizer, biaffine dependency parser and entity recognizer, where annotations exist. One shared encoder receives each language's documents, with updates restricted to that language's heads.
- **Lemma tables:** after checkpoint selection, tune training-only `(lower-cased form, predicted UPOS)` tables on dev: minimum count 1/2, minimum share 0.6/0.8/0.9/1.0. A strict improvement is required. Of 79 lemma heads, 68 select a table and 11 retain edit trees alone; five languages have no lemma head. No-dev languages do not select a table.
- **Raw text:** Chinese uses the trained Chinese fast-tier word segmenter; Japanese, Classical Chinese, Thai and Vietnamese have segmenters trained from their allowed UD training splits. Other languages use the documented word and sentence rules. Multiword-token pieces carry their expanded word forms in `Token.Norm`, matching the encoder's training input.

**Finnish is FTB**, not TDT: FTB has more training sentences in the selected corpus. Its dev slice begins with colloquial/dialectal material, and FTB/TDT XPOS and compound-lemma conventions differ. FTB scores are never compared with a TDT reference. Italian trains on the combined ParlaMint/MarkIT data but its gating test is MarkIT; the table names the actual test treebank.

## Serving and numerical checks

The selected encoder is exported into the original ONNX graph and runs on OneModel, not an external native runtime. The graph has **1,228,334,485 bytes**, SHA-256 `f1528693273734e7c91273f33098cabbaf2e09e44449b63768204114cf7e230c`.

Development export checks compared **11,869 word rows** across 149 files: zero form differences and two Egyptian dependency-head differences. Feeding identical serialized heads with the module and graph outputs reproduces those two punctuation-arc changes: hidden-state maximum absolute difference **2.98023224e-5**, RMSE **3.04120242e-6** (94 × 768 values; module eight threads, graph two). This is a recorded floating-point difference, not exact prediction parity. Saved-pipeline checks for English, Czech, Chinese, Japanese, Classical Chinese, Thai and Vietnamese compare 695 raw words with identical annotations. Debug/Release checks compare 3,116 rows in 38 files with zero differences.

## Packages and NativeAOT

Reference the root package only. It depends on **18 model parts**: six consecutive, byte-preserving encoder pieces (`Encoder1/2`, `TokenTable1–4`, historical names), Chinese heads/segmenter, and eleven additional head groups. There are **91 payload files totalling 3,508,907,198 bytes**. No tensor is quantized, reduced or dropped to fit a package.

The encoder and original nine-language heads are embedded. The other 75 heads are package assets copied transitively by `buildTransitive` targets to `weights/multilingual-large/heads/` in both build and publish output. **Deploy the complete publish directory, not just the executable.** No runtime download or separate model installation occurs. Assigned-code switches select files; arbitrary paths are not accepted.

An all-embedded layout failed NativeAOT `ilc` with “Format of the executable (.exe) or library (.dll) is invalid.” A clean-cache no-model publish succeeded; a clean-cache all-embedded model publish still failed with free disk. The copied-assets layout passes both JIT and NativeAOT, loading all 84 language heads, checking six happy-path texts and processing Czech, Japanese and Finnish through explicitly selected trained components. Both output directories contain all 75 additional head files. NativeAOT/trim warnings: **zero**. Every model package is below 250,000,000 bytes; exact payload hashes and sizes are in the package manifests.

### Indicative one-language memory and startup

`tests/OneNlp.ModelConsumer.Large` against the packed packages, `--single-language`, German first `Nlp.Analyze` including language detection, model load and the first inference; `/usr/bin/time -v` in a fresh process:

| Runtime | First Analyze | Peak RSS | Process elapsed |
|---|---:|---:|---:|
| JIT | 7,016 ms | 4,007,432 KiB | 7.50 s |
| NativeAOT | 6,063 ms | 3,991,940 KiB | 6.74 s |

These are **shared-host indicative observations, not throughput benchmarks or a speed-gate result**. Linux x64, AMD Ryzen 9 7950X3D (16 cores/32 logical CPUs, AVX2/AVX-512 available), four logical processors exposed to the process; .NET SDK 10.0.403/runtime 10.0.14, OneModel 1.0.0, CPU only. The encompassing build/consumer job's one-minute load was 35.62 before and 33.03 after; load during the short RSS probes was not sampled. Materializing every language in the same process peaked at 8,515 MiB in the JIT smoke and 8,294 MiB in AOT; applications loading one language need not materialize all heads.

## Evaluation conventions and gaps

- Gold-token and raw-text rows are distinct. UD scoring uses the same public metrics tool, 1,000 sentence/document bootstrap resamples, seed 20261106. Published references have no paired predictions: their value is compared with our upper interval bound, not a paired difference.
- Stanza 1.5.1, UDPipe 2 and Trankit published rows use older UD releases; each cell records the release and split-change flag. A changed split is information, not a claim of identical evaluation data. Only published numbers are used for non-commercial reference models.
- Entity scores use deterministic evenly spaced samples of at most 3,001 test sentences per language. The paired spaCy comparison recreates the exact sample indices and checks sentence/token alignment. Corpora differ by language; there is no pooled entity gate.
- XPOS and AllTags are reported when a treebank contains any XPOS annotation; Turkish-BOUN has only partial XPOS annotation. Metrics are not removed because they score poorly.
- Entirely blank gold columns are not scored: lemmas in Maltese-MUDT, Old Occitan-CorAG, Telugu English-TECT, Telugu-MTG and Thai-TUD; features in Korean-Kaist and Vietnamese-VTB. The score tool filters those absent-column rows without changing any retained interval or model prediction.
- Missing annotations/heads and missing references are not perfect accuracy or implicit passes. Hindi has no allowed accurate-tier UD training split; the complete excluded-language list and reasons remain in the licence/coverage documentation.
- The original nine were not protected by per-language resampling boosts in this expansion. Their before/after intervals, including regressions, are retained alongside all newly covered languages.
