# The base-size accurate tier

`RecursiveAI.OneNlp.Models.Multilingual.Large` shares one fine-tuned mmBERT-base encoder across **84 UD languages**, with entity heads for **19**. It is distinct from `.Models.Multilingual`, the distilled model covering nine languages. The full language list is `MultilingualLargeModel.Languages`; annotation availability and data gaps are in `docs/ud-licences.md`. A referenced fast-tier package is preferred by the plain `Nlp` verbs; explicit `Tier.Accurate` prefers this base-size package over the distilled one.

**Gate:** the accurate tier is measured against spaCy 3.8.18 trained on the same data: of 144 paired rows, 117 are ahead, 14 within noise and **13 behind** (Chinese UPOS, UFeats, lemmas, UAS and LAS; Spanish UPOS, UFeats and lemmas; Dutch WikiNER entity F1, precision and recall; German raw tokens; Turkish raw sentences). **Information, not a gate:** published research systems (Stanza, UDPipe 2, Trankit) are ahead on most rows, typically by 3–9 LAS points: 1,205 of 1,314 best-published comparisons and 534 of 650 Stanza 1.5.1 comparisons are misses. Every metric, language, input mode and confidence interval is reported in [accurate-tier-rows.md](accurate-tier-rows.md), including separate counts for the best published system per row and Stanza 1.5.1 pretrained/charlm without a transformer. Missing references are not passes. The table also includes the previous nine-language model and a paired comparison with the same-data spaCy 3.8.18 baselines. No multilingual average substitutes for those rows. The complete results cover **72,685 UD test sentences / 1,202,477 gold words**, in both gold-token and raw-text modes, plus 19 entity samples. There are **1,719 metric rows**: **1,205/1,314 best-published comparisons miss**, and **534/650 Stanza-only comparisons miss** (different view populations, not comparable pass percentages). The paired spaCy information view has 117 rows ahead, 14 overlapping and 13 behind, out of 144.

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

## Parser-only recovery on the frozen encoder

The seven original languages whose full-dev evidence supports adaptation now use recovered parser heads: **de, fr, es, pt, nl, tr, zh**. English and Italian keep their exact jointly trained files. Only parser parameters changed; the graph, token table, tokenizer, all tag/morphology/lemma/entity parameters and the other 75 languages are unchanged. Recipe and complete provenance: `recipes/accurate/multilingual/PARSER-RECOVERY.md` and the package's `PROVENANCE.md`.

Each candidate was selected on the **full** development split with a paired sentence-bootstrap LAS comparison against the jointly trained parser (1,000 resamples, seed 66). The lower difference bound must exceed zero. Best full-dev epochs are restored after wall-time cancellation; where no epoch completes, the final partial epoch is evaluated in full. No test-driven continuation or selection follows.

The seven selected heads were then evaluated once in gold-token and raw-text modes on 6,356 official test sentences. All seven improve LAS in **both** modes, with paired 95% difference intervals excluding zero against the joint 84-language model. English and Italian reuse the previous test results because their exact files and encoder are retained. Complete before/joint/after intervals for all nine, all metrics and both published-reference views are in [accurate-tier-rows.md](accurate-tier-rows.md).

| Language | Gold LAS: earlier nine → joint → recovered | Raw LAS: joint → recovered |
|---|---|---|
| en | 85.43 → 85.12 → 85.12 (unchanged) | 86.55 → 86.55 (unchanged) |
| de | 83.32 → 82.17 → 83.49 | 81.05 → 82.38 |
| fr | 86.55 → 85.83 → 86.89 | 88.96 → 89.78 |
| es | 85.37 → 84.66 → 86.97 | 88.35 → 89.70 |
| pt | 84.01 → 81.83 → 84.54 | 86.86 → 87.98 |
| it | 77.27 → 77.63 → 77.63 (unchanged) | 82.22 → 82.22 (unchanged) |
| nl | 87.63 → 83.40 → 87.24 | 89.09 → 91.11 |
| tr | 67.92 → 64.97 → 67.13 | 62.33 → 64.31 |
| zh | 63.29 → 57.55 → 61.42 | 58.91 → 60.98 |

**Sentence-boundary trade-off:** Turkish raw sentence F1 falls from 79.89 to 78.82. Its paired difference against the joint model is **-1.073 [-1.998, -0.134]**, despite raw LAS improving by 1.978 [1.413, 2.578]. This is the only significantly negative paired row among the 139 recomputed UD metric rows. All 76 checked non-parser metric intervals are identical; sentence classification is part of the adapted parser. The selection rule was fixed to full-dev gold LAS before test, and no post-test weight selection was made. This is not a regression-free update.

This is recovery relative to joint training, **not a published-system gate pass**. Chinese gold LAS remains 1.88 points below the earlier nine-language point; the original and recovered marginal intervals overlap, and both rows remain visible. Published-reference misses are still reported without relaxation. Save/reload checks reproduce the selected parsers' actual full-dev head/label predictions and preserve every non-parser head's configuration and tensor hash. Package part boundaries and payload sizes are unchanged. The affected root and Chinese packages have been built and size-checked; validation through the packed JIT consumer remains pending after its restore ran out of disk space.

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
| JIT | 3,694 ms | 4,028,732 KiB | 3.94 s |
| NativeAOT | 2,874 ms | 3,993,404 KiB | 3.22 s |

These are **shared-host indicative observations, not throughput benchmarks or a speed-gate result**. Linux x64, AMD Ryzen 9 7950X3D (16 cores/32 logical CPUs, AVX2/AVX-512 available), four logical processors exposed to the process; .NET SDK 10.0.403/runtime 10.0.14, OneModel 1.0.0, CPU only. One-minute load before/during/after was 8.04/8.04/7.96 for JIT and 9.83/9.36/9.36 for AOT. No swapping occurred. These are fresh processes with warm filesystem caches. Materializing every language in the same process peaked at 8,304 MiB in the JIT smoke and 8,323 MiB in AOT; applications loading one language need not materialize all heads.

### OneModel runtime update: memory and exactness

OneModel **1.2.0** replaces 1.0.0 in the neural package. Single-resource embedded graphs now load in place: the distilled encoders and the English sentence embedder transfer their model streams to the encoder/embedder, which keeps them open until disposal. The existing stream-loading overloads retain their buffered lifetime contract; the new `leaveOpen` overloads explicitly select the borrowed/owned in-place path. Tokenizer streams remain caller-owned. Training checkpoints use OneModel's streamed loader rather than a local checkpoint-format implementation.

A follow-up **controlled, interleaved indicative comparison** supersedes the earlier single-process observations, which compared different machine loads and suggested a 2.5–3× slowdown. Each version uses the same console harness over the source model projects, loading one German pipeline with `Load("de")` and calling `Process` on “Angela Merkel besuchte gestern die Universität in Heidelberg.” The first-process interval includes graph/head loading and the first inference, but **not language detection**; it is not the `Nlp.Analyze` workload in the preceding table. Both builds embed the same current weight files, verified against the same manifests, including the recovered German parser head. Filesystem caches were warm.

Each cell below is the **median of three fresh processes [minimum, maximum]**. The versions alternate within each model's pair, reversing order in the second repetition. Loading is timed separately from first inference; RSS and total process elapsed time come from `/usr/bin/time -v`. Builds finish before the measurements start.

| Model | OneModel | Load pipeline | First process | Peak RSS | Process elapsed |
|---|---|---:|---:|---:|---:|
| `.Multilingual.Large`, German | 1.0.0 | 2,189 [2,104, 2,520] ms | 2,401 [2,324, 2,734] ms | 4,027,688 [4,026,976, 4,035,176] KiB | 2.70 [2.62, 3.02] s |
| `.Multilingual.Large`, German | 1.2.0 | 2,319 [2,082, 2,467] ms | 2,554 [2,293, 2,713] ms | 4,001,736 [3,995,144, 4,008,996] KiB | 2.88 [2.57, 3.01] s |
| `.Multilingual`, German | 1.0.0 | 959 [885, 1,203] ms | 1,116 [1,037, 1,363] ms | 816,136 [813,972, 830,876] KiB | 1.21 [1.10, 1.45] s |
| `.Multilingual`, German | 1.2.0 | 959 [767, 1,251] ms | 1,116 [915, 1,412] ms | 660,048 [658,404, 660,796] KiB | 1.18 [0.97, 1.46] s |

The earlier 2.5–3× startup difference **does not reproduce**: median first-process ratios (1.2.0 / 1.0.0) are **1.064** for the base-size model and **1.000** for the distilled model, with overlapping ranges. Three repetitions do not establish equivalence or rule out smaller differences. Temporary load-boundary timestamps in a separate diagnostic batch found German head loading at 67–83 ms before and 61–100 ms after; the much larger variation was in encoder loading, and its direction varied between repetitions. There is no evidence here for a checkpoint-loading fix. No loading code, model bytes, public API or format was changed for this follow-up.

The distilled model's median peak falls by **152.4 MiB**. The base-size model shows no substantial memory saving: **its six resource pieces still require a full serialized-graph buffer**. OneModel's in-place stream API recognizes a single `UnmanagedMemoryStream`, not a concatenation of resources. The three-shard multilingual E5 embedder has the same limitation. No tensors, weights or package layout changed to work around it.

These observations were made on 2026-11-08, Linux x64, AMD Ryzen 9 7950X3D (16 cores/32 logical CPUs; AVX2 and AVX-512 available), SDK 10.0.403/runtime 10.0.14, CPU only. Four logical processors were exposed; graph sessions used their default four intra-op threads and eager components used one. The measurement job reserved four cores, **not the whole machine**: unrelated queued jobs overlapped it. The following one-minute load samples were taken before/during/after each process (during sampled every 250 ms). Interleaving matches the load much better than the old observations, but **this is not an exclusive-machine benchmark or an isolated latency claim**. An attempted fully idle window was unavailable; no startup speed improvement is claimed.

| Model | Repetition | 1.0.0 load: before / during / after | 1.2.0 load: before / during / after |
|---|---:|---|---|
| `.Multilingual` | 1 | 8.51 / 8.51 / 8.51 | 8.51 / 8.51 / 8.51 |
| `.Multilingual` | 2 | 8.43 / 8.43 / 8.43 | 8.47 / 8.43–8.47 / 8.43 |
| `.Multilingual` | 3 | 8.64 / 8.64 / 8.64 | 8.64 / 8.64 / 8.64 |
| `.Multilingual.Large` | 1 | 8.51 / 8.47–8.51 / 8.47 | 8.47 / 8.47 / 8.47 |
| `.Multilingual.Large` | 2 | 8.43 / 8.43–8.64 / 8.64 | 8.43 / 8.43 / 8.43 |
| `.Multilingual.Large` | 3 | 8.64 / 8.64–8.75 / 8.75 | 8.75 / 8.75–8.93 / 8.93 |

The German annotation JSON, including confidences, is byte-identical across all six processes per model. SHA-256: distilled `e7bc02dfa37b9ab61feeda8cfdbf9ccc31b00f2d5da3003717655b2243d2e0e4`; base-size `34bcb371f68193c4a9f5b92c8f611cfce0058a496939fdfd5b67c0a98460d363`. A JIT consumer of the packed distilled model, with direct OneModel/OneModel.Training 1.2.0 references, gives the same distilled hash. Stream ownership, checkpoint, training/resume and embedder tests pass (51 tests, no failures or skips).

Exact-output checks across these runtime versions found:

- English fast-tier predictions on **2,077 EWT r2.18 test sentences / 25,094 gold words**: byte-identical JSON in gold-token/sentence and raw-text modes, including tags, morphology, lemmas, dependency heads/labels, entities and confidences. Thus UPOS and LAS inputs are unchanged, not merely within tolerance.
- Both German accurate pipelines on the nine-word probe above: byte-identical annotation JSON, including confidences.
- BGE sentence embeddings on **2,103 fixed texts**: byte-identical float32 vectors across versions, zero token-ID mismatches against cached sentence-transformers **6.2.0** references, and the same maximum absolute reference difference, **4.02797e-7**, before and after. The English embedded model's process peak was 454,104 → 323,116 KiB (four single-text probes; process elapsed 0.40 → 0.77 s, likewise not isolated).
- Multilingual E5 on the same **2,103-text** corpus: float32 bytes and token IDs match the cached pre-update packaged-model run exactly. The unchanged maximum absolute difference from sentence-transformers 6.2.0 / PyTorch 2.15.0 CPU is **3.203749656677246e-7**; zero token-ID mismatches. Its resource shards remain on the buffered loading path.
- The fixed-seed toy training recipe retained its 1.0.0 checkpoint SHA-256, `50ecab9ecc658026aba8868555f658010006f59d6093685b347073f742764d25`, at one and four processors. Resume still reproduces the uninterrupted checkpoint bytes; warm start and restore-best paths pass.

The source-linked sample consumer and the base-size consumer pass on JIT and NativeAOT with no trim/AOT warnings. The latter loads all 84 language heads and publishes all **75 copied head assets**. These checks do not replace validation of the final packed release.

## Evaluation conventions and gaps

- Gold-token and raw-text rows are distinct. UD scoring uses the same public metrics tool, 1,000 sentence/document bootstrap resamples, seed 20261106. Published references have no paired predictions: their value is compared with our upper interval bound, not a paired difference.
- Stanza 1.5.1, UDPipe 2 and Trankit published rows use older UD releases; each cell records the release and split-change flag. A changed split is information, not a claim of identical evaluation data. Only published numbers are used for non-commercial reference models.
- Entity scores use deterministic evenly spaced samples of at most 3,001 test sentences per language. The paired spaCy comparison recreates the exact sample indices and checks sentence/token alignment. Corpora differ by language; there is no pooled entity gate.
- XPOS and AllTags are reported when a treebank contains any XPOS annotation; Turkish-BOUN has only partial XPOS annotation. Metrics are not removed because they score poorly.
- Entirely blank gold columns are not scored: lemmas in Maltese-MUDT, Old Occitan-CorAG, Telugu English-TECT, Telugu-MTG and Thai-TUD; features in Korean-Kaist and Vietnamese-VTB. The score tool filters those absent-column rows without changing any retained interval or model prediction.
- Missing annotations/heads and missing references are not perfect accuracy or implicit passes. Hindi has no allowed accurate-tier UD training split; the complete excluded-language list and reasons remain in the licence/coverage documentation.
- The original nine were not protected by per-language resampling boosts in this expansion. Their before/after intervals, including regressions, are retained alongside all newly covered languages.
