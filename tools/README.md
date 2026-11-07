# Tools

Programs and scripts that support the tests, the measurements and the training recipes. None of them ship in a package. Comparators and reference implementations (spaCy, Stanza, Lingua, Microsoft.Recognizers.Text, Duckling, Catalyst, Microsoft.ML.Tokenizers, Hugging Face `tokenizers`, `tiktoken`, sentence-transformers) are only ever run here, as installed packages through their documented APIs or command lines, at the versions pinned in the scripts. Datasets, models and reference outputs are fetched by script, pinned by version and SHA-256, and never committed, except small outputs on text we wrote.

Scripts take their paths from arguments or environment variables; run them from the repository root unless a script says otherwise. Long runs (training, corpus sweeps, timings) belong on a machine you can leave busy; timings should run alone on the machine.

## Evaluation

- `OneNlp.Eval/` : the metrics program. UD scores (the CoNLL 2018 definitions), span F1, classification and span-group scores, paired bootstrap intervals, recognizer scoring and the `recognize` driver. Method: [docs/measurement.md](../docs/measurement.md).
- `eval/` : cross-checks of those scorers against the reference scorers (run as black boxes), and the spaCy segmentation baseline.
- `recognizers-gold/` : the sources and builder of the recognizer gold sets, and the benchmark-text builder. See [docs/recognizer-gold.md](../docs/recognizer-gold.md).
- `ratio/` : training time of the C# recipes against plain PyTorch on CPU.

## Comparators and baselines (black box)

- `baselines/spacy/` : spaCy trained with its default configuration on our data, per language and task; the provenance file records versions and configuration hashes.
- `oracles/` : Microsoft.Recognizers.Text (`Recognizers/`), Duckling (`duckling/`), SearchPioneer.Lingua and `lingua-language-detector` (`Lingua/`), and Catalyst (`Catalyst/`) drivers, with the scripts that score them against the gold sets:
  - `comparators_lang.sh <lang> <culture> <gold|test> <out>` runs Microsoft.Recognizers.Text over a language's gold file; `score_dev.sh <lang> <out> [gold|test]` scores ours and compares; `score_recognizers.sh <out> <duckling dir>` does both comparators for English.
  - `bench_recognizers.sh <work dir>` (English) and `bench_recognizers_lang.sh <lang> <culture> <work dir>` time ours against Microsoft.Recognizers.Text on text made from the development split. The comparator is very slow in Turkish and Hindi, so those two languages default to a quarter-megabyte of text; `BENCH_MB`, `BENCH_MB_DATETIME` and `BENCH_TIMEOUT` override the sizes and the per-run time limit. Run them as indicative timings unless the machine is otherwise idle.
- `bench/` : language-ID benchmark inputs and the native-speed information rows.

## Tokenizer parity and goldens

- `parity/` and `OneNlp.Parity/` : the parity corpus builder, the oracle that writes reference encodings, and the checker. See [docs/parity.md](../docs/parity.md).
- `stage_goldens/`, `tokenizer-goldens/`, `modelgolden/` : reference outputs for tokenizer stages and models, and seed files for the fuzz targets.
- `gen_normalization_tables.py`, `gen_bert_clean_table.py`, `gen_script_table.py` : generate the Unicode data tables compiled into the core from the Unicode Character Database.

## Data

- `fetch/` : fetch and pin treebanks, tokenizer files and parity inputs (`fetch_lang.py`, `fetch_tokenizers.py`, `fetch_parity_inputs.py`, the per-language manifests, the scorer cross-check). Licences per source: [docs/ud-licences.md](../docs/ud-licences.md).
- `selftrain/` : overlap checks that keep self-labelled text from containing any evaluation sentence.

## Models and training support

- `OneNlp.LangId/` : trains and scores the language-ID model. See [docs/language-id.md](../docs/language-id.md).
- `accurate/`, `OneNlp.Accurate/`, `distill/` : the accurate tier. PyTorch development tooling for distilling the student encoder and exporting encoders to ONNX, and the C# programs that check the exports and time fine-tuning steps. See [docs/accurate-encoder.md](../docs/accurate-encoder.md).
- `embeddings/` : fetch, export, shard and score the sentence-embedding models (`mteb` runs). See [docs/embeddings.md](../docs/embeddings.md).
- `fasttier/`, `OneNlp.Toy/`, `toy_*` : small training proofs and the PyTorch timing for the fast tier.
- `OneNlp.ModelBench/` : cold-start time and per-stage throughput of a model package (indicative timings).
- The training recipes themselves are in [`recipes/`](../recipes/), and are documented in [docs/training.md](../docs/training.md).

## Fuzzing

The fuzz targets and how to run them are in [`fuzz/`](../fuzz/README.md).
