# Provenance: open-ner (open-type entity recognizer)

- **Model:** `OpenEntityRecognizer` head (2,385,789 bytes) on the frozen distilled encoder `student-v1` (`recipes/accurate/student-v1`). The head file does not contain the encoder; it names it. Model B below is the one to ship.
- **Recipe:** `recipes/openner` (`run-chunk.sh`, `Program.cs train`). Two runs, both on the frozen encoder, AdamW learning rate 0.002, 1,500 words per batch, 8 threads, 6 epochs, hidden 256, embedding 128, up to 12 negative types per example, seed 1:
  - **A** (human labels only): Few-NERD train (60,000 sentences drawn with seed 1) + UNER English-EWT train (12,543). sha256 `4de46c5a6395efd0ac085641241e1d7263b9632c7263b898aea71d8fdc620459`. About 1.3 h in three chunks of at most 25 min.
  - **B** (shipped): Few-NERD train (40,000) + UNER English-EWT train (12,543) + 40,000 windows of Qwen-labelled text. sha256 `7898e075ace9f641592ca06e9089e4e7c46af67cdbe5f0ff9169d08b7afc08b4`. About 1.4 h in four chunks, early stopping off, the best-on-dev weights kept (final epoch).
- **Produced:** 2026-11-07, shared 32-core worker (CPU only for training), .NET SDK 10.0.403, RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0. The labelling ran on the worker's RTX 4070 (driver 595.99.02), vLLM 0.11.0.
- **SA lineage:** yes (Few-NERD, UNER and Wikipedia text are CC BY-SA). Weights licence: MIT OR Apache-2.0, with the credit in `THIRD-PARTY-NOTICES.md`.
- **Weights:** outside git; rebuilt by the recipe; copies in the model cache under `open-ner-student-v1/` with `SHA256SUMS`.

## Training data

| Dataset | Role | File | Licence | sha256 |
|---|---|---|---|---|
| Few-NERD (Ding et al. 2021), supervised split | train (131,767 sentences; 40,000 used by B) | `few-nerd-train.jsonl` | CC BY-SA 4.0 | `b77c692bc986ae7c7bcbb888a8630901b9ed4d710f1293d780d8f4fe9296ce72` |
| Few-NERD | dev (18,824; 800 used) | `few-nerd-dev.jsonl` | CC BY-SA 4.0 | `aa8eb7655a914f451f8f77dc42db9d23f5e4d33b7e837011d527226718ab9520` |
| UNER English-EWT (commit 8ed072de) | train | `uner-en-ewt-train.jsonl` | CC BY-SA 4.0 | `69b3c43d73721acd5db5476ce6f5b5dc6335c0993981d5e9dc47e0153ab7038c` |
| UNER English-EWT | dev | `uner-en-ewt-dev.jsonl` | CC BY-SA 4.0 | `8a4b037d8a4579834463cba85e16dd0a63e253599ba6bd655da1ab536705e9ab` |
| Text labelled by Qwen2.5-7B-Instruct-AWQ (below) | train (182,056 windows, 3,462 types; 40,000 used by B) | `synth-qwen-train.jsonl` | the text is FineWeb (ODC-By 1.0) and Wikipedia (CC BY-SA 3.0 / GFDL); the labels are model output | `c7ae7b3f1f1a5f391dda26a71172597898fc850d3025a25cee4d05570db83339` |

## Label generator

- **Model:** Qwen2.5-7B-Instruct-AWQ, revision `b25037543e9394b818fdfca67ab2a00ecc7dd641`; model card `license: apache-2.0`, LICENSE file Apache License 2.0 (read 2026-11-06). Run with vLLM 0.11.0, greedy decoding, at most 450 new tokens. No hosted model was used.
- **Prompt and scheme:** `tools/openner/label.py`: the model lists every named entity of a passage with a free-form type of up to four words. Types and mentions are parsed from its JSON; a mention counts when it occurs verbatim in the passage on word boundaries.
- **Text:** 107,841 passages of 250 to 900 characters from FineWeb sample-10BT (rev 9bb295dd…) and English Wikipedia 20231101 (rev b04c8d1c…), drawn with a seeded shuffle (`tools/openner/fetch_text.py`, seed 48; `prep.py`, seed 4801), with every passage dropped that shares a sentence with any evaluation or dev set (the 19.4 M-key list of the distillation work plus the Few-NERD, CrossNER and UNER files) and every passage of the 20,000-passage pilot. Passage file sha256 `6e86d3afd1be8d42a9a8217472d7b1f0e0c5be3828a96790571a23ef4e28e18b`.
- **Filter (part of the recipe):** `tools/openner/filter_labels.py` drops a mention text with no upper-case letter and no digit, and the types `acronym`, `adjective`, `job title`, `position`, `event`; `to_tokens.py` then cuts passages into windows of at most 100 tokens, keeps types seen at least 3 times and lists the passage's types on every window, so a type absent from a window is a negative.
- **Hand checks of the labels** (same strictness: dates and weekdays right; common nouns, titles, roles, generic phrases, nested pieces and bad boundaries wrong):
  - pilot slice, 200 passages, unfiltered: 79.1 % of 805 mentions correct (recall about 73 %); the same slice after the filter: 88.1 %;
  - fresh slice, 200 other passages (seed 20261108), filtered: 93.1 % of 613 mentions correct (42 wrong). The fresh slice was judged mostly from the mention text with 25 characters of context, so context-only errors (a team named by its city) were more likely missed than in the pilot check; the filter's gain is the likely 5 to 10 points, not the full 14.
- **GPU time:** pilot 0.333 h and full run 1.983 h, 2.316 GPU-hours in all (`gpu-hours.tsv`). A second labelling pass with an agreement filter was not run: the filtered labels already met the 80% hand-checked precision target, and a second pass would have doubled the GPU time.

## Pretrained components

`student-v1` encoder (frozen), distilled from mmBERT-base (MIT); see `recipes/accurate/student-v1/PROVENANCE.md`.

## Evaluation data (never trained on)

| Dataset | Role | File | Licence | sha256 |
|---|---|---|---|---|
| UNER English-EWT test | eval (fixed types) | `uner-en-ewt-test.jsonl` | CC BY-SA 4.0 | `ad1939a3eb9993457eb3d0505b883e245e9a9b0c42e09f61a8a4166629dc753a` |
| Few-NERD supervised test | eval (seen types) | `few-nerd-test.jsonl` | CC BY-SA 4.0 | `9d73a3d8efc7094e916d07b45e1872cb5eb107d60a0570fabc34b39462a3e673` |
| CrossNER politics, science, music, literature, AI test | eval (zero-shot, information) | `crossner-*-test.jsonl` | MIT (repository LICENSE); Wikipedia text | `748f13f6…`, `699a7ab7…`, `646284a4…`, `73d2b88f…`, `7cadd7b3…` |

The threshold (0.5) was chosen on the Few-NERD and UNER dev files (0.3, 0.4 and 0.5 tried for both runs: 0.5 was best or within 0.1 on UNER, and within 0.4 on Few-NERD). The test files were read for the reported runs only. Dropped, not used: MIT Movie and Restaurant (licence "other").

## Results (span F1, strict match of token range and type, bootstrap 95 % CI over sentences, 1,000 resamples, seed 1)

See `results.md`.
