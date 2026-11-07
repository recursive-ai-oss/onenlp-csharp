# Accurate-tier recipes: shared corpus code

`OneNlp.Recipes.AccurateCommon` is the part of the accurate-tier recipes that does not depend on a model: which languages and files a run trains on, how the
examples are read, and how often each language is repeated. The multilingual recipe (`../multilingual`) and the distilled-encoder recipe
(`../student-multilingual`) read the same corpus.

## The corpus is data

`corpus.json` has one entry per language. It is written by `tools/accurate/make_corpus_spec.py` from the fetched files, so the sizes are counted, not typed:

```
python3 tools/accurate/make_corpus_spec.py DATA corpus.json [--cs src/OneNlp/ModelPackages.AccurateLanguages.g.cs]
python3 tools/accurate/lexicon_proposals.py DATA corpus.json lexicon-proposals.tsv
python3 tools/accurate/corpus_report.py corpus.json tools/fetch/ud/treebanks.tsv tools/fetch/ud/languages.tsv lexicon-proposals.tsv docs/accurate-corpus.md
```

An entry holds the language code and name; its treebanks (the gating one first, then extra ones, each with train, dev and test paths relative to the data root and
its sizes); its entity sets; and four decisions that the recipes obey:

| Field | Meaning |
|---|---|
| `devFromTrain` | No treebank has a dev split: every 10th training sentence is held out as dev (at least 200 training sentences). |
| `hasDev` | Model selection sees this language. A language with fewer than 200 sentences and no dev split trains on all of them and is scored on test only. |
| `small` | Fewer than 500 training sentences in all. The sampler caps its repetition. |
| `segmenter` | `character-tagger` when raw text is not separated by spaces or its words contain spaces (ja, lzh, th, vi, zh): a trained `NeuralWordSegmenter` goes in front (`tools/accurate/train_segmenters.sh`). |

Rules the reader applies (`CorpusReader.ReadUd`): training sentences of an extra treebank that repeat a sentence of the gating test split are dropped; a held-out
sentence never reaches training; test files are opened only by scoring code. A language is added by fetching its data and running the script again.

## Sampling

`SamplingPlan.Compute` is temperature sampling over languages (Conneau et al. 2020, section 3.1): a pass holds as many words as the natural corpus, spread so that a
language's share is proportional to its words to the power 0.3, then each language's repetition is clipped to [0.25, 6] (small languages [0.25, 8]). The recipe prints the table at
its start and applies it as `--repeat`; `plan` prints it without a model:

```
dotnet RecursiveAI.OneNlp.Recipes.Accurate.dll plan --spec corpus.json --data DATA [--plan 0.3] [--boost xx=1.5,...] [--langs a,b]
```

`--parser-weight W` scales the loss of every parser head (`ScopedHead.LossWeight`); `--boost` raises the share of named languages between runs, from their development scores.
