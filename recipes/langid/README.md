# Language-ID recipe

Reproduces `langid.bin` (embedded from the directory `ONENLP_LANGID_WEIGHTS` names; see `src/OneNlp/LanguageId/weights.manifest.props`) and the numbers in `docs/language-id.md`. Paths are arguments or environment variables; `$DATA` holds the corpora, `$WORK` the models and predictions.

```
# 1. data: pick corpora, download (hashes in tools/fetch/langid/SHA256SUMS), build train/ and eval/
python3 tools/fetch/langid/fetch.py choose probe.tsv tools/fetch/langid/sources.tsv    # only to re-pick; sources.tsv is committed
python3 tools/fetch/langid/fetch.py fetch tools/fetch/langid/sources.tsv $DATA/raw
python3 tools/fetch/langid/fetch.py prepare tools/fetch/langid/sources.tsv $DATA/raw tatoeba_sentences.csv $DATA

# 2. reference predictions: Lingua Python and Lingua .NET, high and low accuracy
bash tools/oracles/Lingua/run_all.sh . $WORK $DATA $LINGUA_VENV

# 3. train, predict, score (see config.txt for the exact arguments of the shipped model)
bash tools/OneNlp.LangId/experiment.sh . $WORK $DATA "d16:--epochs 5 --samples 100000 --bits Latin=16"
bash tools/OneNlp.LangId/final.sh . $WORK $DATA d16 $LINGUA_VENV
```

`probe.tsv` comes from `tools/fetch/langid/probe.py`, which looks for the Leipzig archive names with HEAD requests (the download server has no index). `final.sh` writes the tables in `results/`.

Training takes about four minutes on four cores. Re-running with the same data and arguments gives the same file (single-threaded per group, fixed seed).
