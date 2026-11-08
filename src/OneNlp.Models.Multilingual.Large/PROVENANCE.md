# Provenance: RecursiveAI.OneNlp.Models.Multilingual.Large

One mmBERT-base encoder shared by UD heads for **84 languages**, with entity heads for **19**. Weights are fp32, including the unchanged frozen token table. The code and our fine-tuned weights are released under MIT OR Apache-2.0, with the base model's MIT notice and all training-data credits retained. **ShareAlike lineage: yes.** No label generator was used.

## Inputs and roles

- **Base:** Johns Hopkins University CLSP **mmBERT-base**, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82`, <https://huggingface.co/jhu-clsp/mmBERT-base>, MIT. The encoder architecture and tokenizer are unchanged.
- **Warm start:** the previous nine-language model's selected checkpoint, encoder and matching heads only; SHA-256 `786d97381db49f4a9b246880691ce7d7b5b1e518bdc7efc449c995f35fb4b0bc` (see the earlier recipe's provenance for its own selection). New heads and optimizer moments start fresh. The new run's selected checkpoint is `bc97bebed5cb9eb17198fc88ddcff6f20cff9d5574158d1daadc23b37870f051`.
- **UD r2.18:** 109 training treebanks from the allowed corpus. The complete language/treebank membership, train/dev/eval paths and sampling inputs are in the included `corpus.json`; the per-split URLs, licences and SHA-256 values are in `UD-MANIFEST.md` and `provenance/<language>/MANIFEST.md`. The latter include the derived Italian ParlaMint/MarkIT training split. A manifest may list other fetched files; only the training and development roles selected by `corpus.json` enter this model.
- **Entities:** UNER v1 (English, Chinese, Portuguese, Croatian, Danish, Indonesian, Norwegian, Serbian, Slovak, Slovenian, Swedish); GermEval 2014; WikiNER (French, Spanish, Italian, Dutch); Turkish-WikiNER; KPWr-NER n82 (CC BY 3.0); RONEC 2.0 (MIT). Exact revisions, conversion/mapping rules, roles, URLs and hashes are in the included per-language manifests (`provenance/ner/` for the added ten languages). Dataset credits and licences are in `THIRD-PARTY-NOTICES.md`.
- **Word segmenters:** Japanese-GSD, Classical Chinese-Kyoto, Thai-TUD and Vietnamese-VTB r2.18, trained through the public C# training API, seed 3; Chinese uses the earlier Chinese-GSDSimp segmenter. No additional text or labels. Their tokenizer weights are included in the corresponding language's head file.
- **Ligurian-GLT:** the 20 training sentences, contributed by Stefano Lusito and Jean Maillard, under C-UDA 1.0 (computational use; results unrestricted). The dataset is credited in the notices; its text is not redistributed. No lemma table is selected without development evidence.

Train/dev roles only enter training and selection. Where no dev file exists and there are at least 200 training sentences, every tenth training sentence is held out. Smaller languages without dev data do not participate in selection. Extra-treebank training sentences repeating a gating test text are dropped. The original nine retain their previous official splits, including documented official train/test fragment overlap. Test data never selects a checkpoint, a sampling weight, a lemma table or a hyperparameter.

## Recipe and selection

`recipes/accurate/multilingual`, sharing `recipes/accurate/common` with other accurate-tier recipes. C# on `RecursiveAI.OneModel` and `.Training` **1.0.0**, .NET SDK **10.0.403**, runtime **10.0.14**. CPU only, AMD Ryzen 9 7950X3D, eight threads for training, resumable jobs of at most 30 minutes. No PyTorch training or GPU work in this continuation.

- Seed **3**, AdamW, batch target **3,000 words**, language temperature exponent **0.3**, repetition capped as the corpus sampling plan specifies; no per-language boosts. Parser loss weight **1.5**.
- **1,200-step** half-cosine schedule from **6e-5** to **6e-6**, warm-up **40** steps. The total compute budget stopped the run at **step 1,050** (a partial epoch); this was also the best checkpoint. Dev selection scores at steps 150/300/450/600/750/900/1050: **58.998 / 69.628 / 73.346 / 75.379 / 76.814 / 77.786 / 78.238**. Selection uses at most ten four-sentence UD documents per language plus about 100 entity sentences; these small, ordered samples are noisy and can be domain-biased.
- Entity label initialization uses about **2,500** sentences per set, with **700** documents used for updates before language sampling. The reader samples evenly through each entity file with a binary64 cursor. Accumulation can include one additional sentence at a configured limit; this behavior is retained for reproducibility. Reported entity test samples contain at most **3,001** sentences, and paired comparisons reproduce the identical sample indices and verify word alignment.
- Lemma tables are chosen after restoring the selected weights, on the same held-out UD documents, using the model's own UPOS. Grid: minimum count 1 or 2; minimum share 0.6, 0.8, 0.9 or 1.0. Tables use the complete pre-sampling training references; a strict improvement is required, with ties retaining no table, then the first grid setting. `lexicon-dev.tsv` records every selection: 68 selected tables among 79 lemmatizer heads, 11 with no table; five languages have no lemma head. No-dev languages retain the edit trees alone.
- Fine-tuned modules export to the original ONNX graph, served by OneModel. Graph SHA-256: **`f1528693273734e7c91273f33098cabbaf2e09e44449b63768204114cf7e230c`**, **1,228,334,485 bytes**. On development probes, module and graph predictions differ only in two Egyptian dependency heads (11,869 compared word rows, zero form differences); the same serialized heads reproduce those changes when fed the two encoder outputs (max absolute hidden-state difference 2.98e-5, RMSE 3.04e-6). Saved-pipeline checks cover English, Czech and all five unspaced-script tokenizers, with identical annotations.

## Frozen-encoder parser adaptation

After joint training, the original nine languages' parser heads were adapted on the **frozen exported encoder**. Only parser parameters entered `Train`; taggers, morphologizers, lemma models and tables, entity heads, tokenizers and all other languages' heads retained identical configuration and checkpoint bytes. The graph and tokenizer hashes above are unchanged. No new data or labels were introduced; the same `corpus.json` train/dev roles and split hashes apply, including the Italian combined training split. ShareAlike lineage and credits are unchanged.

Reproduction: `recipes/accurate/multilingual/PARSER-RECOVERY.md`, command `recover-parser`. Warm start: each language's jointly trained parser. AdamW starts fresh, seed **66**, constant learning rate **0.0002**, weight decay **0.01**, dropout **0.1**, batch target **1,500 words**, four consecutive training sentences per document, at most eight epochs, patience three evaluations. Runs stop at step boundaries within a bounded compute budget; partial epochs are retained. The best completed full-dev epoch is restored before selection; where no epoch completed, the final partial-epoch weights receive their first full-dev evaluation. `last.ckpt` remains the optimizer resume point, separately from the selected export.

**Full** development sets, encoded sentence by sentence with gold boundaries, select the head. The candidate is kept only when its paired LAS difference's lower 95% bound is positive (1,000 sentence bootstrap resamples, seed 66). English and Italian therefore retain their exact original files, even though English's point estimate rises. Tests do not select heads.

| Language | Previous full-dev LAS | Candidate LAS | Candidate minus previous, paired 95% CI | Kept | Optimizer steps / completed epochs |
|---|---:|---:|---|---|---|
| en | 84.6230 | 84.9014 | 0.2784 [-0.0235, 0.5790] | previous | 28 / 0 |
| de | 84.1747 | 85.4247 | 1.2500 [0.7961, 1.7052] | candidate | 181 / 1 |
| fr | 86.8705 | 87.8167 | 0.9462 [0.6946, 1.2047] | candidate | 62 / 0 |
| es | 84.5426 | 86.8932 | 2.3506 [2.1234, 2.5851] | candidate | 300 / 0 |
| pt | 81.5868 | 83.9456 | 2.3588 [1.9621, 2.7734] | candidate | 358 / 3 |
| it | 80.1521 | 80.1259 | -0.0262 [-0.5216, 0.4331] | previous | 21 / 1 |
| nl | 83.4503 | 87.3754 | 3.9251 [3.2922, 4.5342] | candidate | 645 / 5 |
| tr | 66.5229 | 68.2399 | 1.7170 [1.0842, 2.3297] | candidate | 483 / 7 |
| zh | 56.2811 | 59.8342 | 3.5531 [2.8425, 4.1938] | candidate | 275 / 4 |

Training used .NET SDK **10.0.403**, runtime **10.0.14**, four threads on the same AMD Ryzen 9 7950X3D CPU. German, French, Spanish, Portuguese, Dutch, Turkish and Chinese updates used OneModel/OneModel.Training **1.0.0**; English and Italian trials used **1.2.0-preview.2** after the dependency update. Those two trials were rejected. Final inference and export checks use **1.2.0-preview.2**. No GPU or teacher was used. Selected parser epochs are German 1, Portuguese 3, Dutch 4, Turkish 5, Chinese 3; French and Spanish use their final partial epoch. File hashes and sizes in the affected manifests identify the selected files; parser export checks compare actual head/label predictions and non-parser tensor/configuration hashes before and after save/reload.

## Outputs and evaluation

The root and each component package carry `weights.manifest.props` (file name, SHA-256 and byte size). One graph is cut into six byte-preserving pieces; 11 additional bounded head parts supplement the original root and Chinese parts. No tensor is shrunk or quantized to meet the 250 MB package limit. Additional heads are shipped as NuGet content assets in `weights/multilingual-large/heads/`, copied transitively on build and publish; the graph and original nine-language heads remain embedded. No runtime downloads or external model installation are needed. Deploy the complete publish directory. Reference the root package only; its dependency graph brings in the parts.

The jointly trained model was evaluated once on every language's gating UD r2.18 test split, in gold-token and raw-text modes, and on the available deterministic entity test samples. Every UD metric row, confidence interval, before/after value for the original nine, and both reference views (best published per matching row; Stanza 1.5.1 pretrained/charlm without a transformer) are in `docs/accurate-tier-rows.md`. Published systems use other UD releases; split-change flags accompany their rows. A missing published score is not a pass. The spaCy 3.8.18 comparison is paired over the same sentences, with configuration hashes in `tools/baselines/spacy/PROVENANCE.md` and `tools/baselines/spacy/lang/PROVENANCE.md`.

F1/accuracy intervals: 1,000 sentence/document bootstrap resamples, seed **20261106**. The models' quality gaps are reported, not averaged away. No accuracy claim is made for a missing annotation or entity head.
