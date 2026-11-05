# Provenance: `student-v1`, the distilled multilingual encoder

A six-layer, width-384 post-norm transformer with a factorised 256,000 x 96 token table (35.5 M weights, 142 MB as ONNX), distilled from mmBERT-base. It is an encoder only: no labels were used. Architecture and objective are in `docs/accurate-encoder.md`; the code is `tools/distill/` (plain PyTorch; `transformers` loads the teacher and `tokenizers` tokenizes).

## Teacher

| | |
|---|---|
| Model | `jhu-clsp/mmBERT-base`, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82` |
| Licence | MIT (model card) |
| Use | Teacher outputs only (hidden states of selected layers), computed on the fly. The student does not contain teacher weights except the starting token table: the teacher's embedding matrix reduced to 96 columns by principal components. mmBERT's `tokenizer.json` is used unchanged. |

## Text

All text is plain web or encyclopedic text, with no labels. Per language, at most 24 M characters of web text and 6 M of Wikipedia; 113 languages (`tools/distill/languages.tsv`).

| Source | Revision | Licence | Totals used |
|---|---|---|---|
| FineWeb-2 (`HuggingFaceFW/fineweb-2`) | `af9c13333eb981300149d5ca60a8e9d659b276b9` | ODC-By 1.0, subject to Common Crawl's terms of use | 1,203,113 documents, 2.61 G characters |
| FineWeb (English, `sample-10BT`) | `9bb295ddab0e05d785b879661af7260fed5140fc` | ODC-By 1.0 | included in the web total for English |
| Wikipedia (`wikimedia/wikipedia`, 20231101) | `b04c8d1ceb2f5cd4588862100d08de323dccfbaa` | CC BY-SA 3.0 and GFDL | 358,976 documents, 0.63 G characters |

**Share-alike lineage: yes.** The Wikipedia text is CC BY-SA, so the student has SA lineage. It ships (if it ships) under the family training-data rule with credit to FineWeb-2, Common Crawl and Wikipedia.

## Evaluation data never trains

`tools/distill/build_eval_keys.py` reads every evaluation and training file of the project as plain text (UD and UNER treebanks, the language-ID evaluation and training sets, the parity corpus, UDHR) and writes the set of normalised 64-bit text keys (`tools/distill/textkeys.py`) that no distillation document may contain. `fetch_data.py` drops a document if any of its segments has a key in the set.

- Key set: 19,363,767 keys, sha256 of the array `98bd44539aed7cafc26748fc7e935dabec174d6915f3190d8c2820300cbce60e`.
- Sources counted (texts / keys): UD evaluation 17,148 / 15,839; UD training 98,056 / 89,161; UNER evaluation 4,667 / 4,342; UNER training 12,687 / 11,408; English-EWT r2.18 evaluation 2,077 / 1,444 and training 14,545 / 11,329; UNER English-EWT training 14,544 / 11,328; NER evaluation 33,747 / 33,401 and training 665,759 / 645,579; language-ID evaluation 294,819 / 101,240 and training 10,000,628 / 9,381,572; parity corpus (Tatoeba 13,628,386 / 9,461,730; UDHR 50,897 / 32,779).
- Dropped as overlapping: 184,194 documents over the 113 languages (of 2,209,473 read).
- Dropped as too short or low quality: 194,988 and 268,202.
- The report is `eval-keys-report.json` and the per-language counts are `<iso>.stats.json` beside the fetched text, both on the build machine; the numbers above are their sums.

## Training

- Objective: hidden-state distillation, student layers 1 to 5 to teacher hidden states 4, 7, 11, 15, 18 and the last layer to the final state (`docs/accurate-encoder.md`).
- Seed 1; batch 32,768 tokens; peak learning rate 4e-4, 3,000 warm-up steps, then decay over the last chunk; EMA 0.999 (the exported weights are the EMA).
- 53,628 steps (about 1.76 G tokens), 8.05 GPU-hours on one RTX 4070 in five resumable chunks of at most two hours; torch 2.11.0+cu128. Loss curve: `losses.jsonl` in the run directory.
- Output: `student-v1.onnx`, 141,913,197 bytes, sha256 `aee56231d97134e455fe80a79738eb294c7974c61311412256fa9cd854eeeae6`; `student-v1.config.json` sha256 `17fe1d63…`.

## Evaluation sets used for the reported rows

UD English-EWT r2.18 (dev for selection, test once), UD French-GSD r2.18 (dev), UNER English-EWT commit `8ed072de` (dev for selection, test once). Results: `recipes/accurate/student-v1/RESULTS.md`.
