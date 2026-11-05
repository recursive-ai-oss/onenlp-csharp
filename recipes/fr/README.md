# French fast-tier recipes

Three console projects over the public `Train` API, adapted from the English recipes; each trains one saved pipeline on one dataset and writes CoNLL-U (or entity) predictions for `tools/OneNlp.Eval`.

| Project | Trains | Data | Command |
|---|---|---|---|
| `fast-tagging` | morphologizer (UPOS, UFeats) and edit-tree lemmatizer on one `FastEncoder` | UD French-GSD | `train --train fr_gsd-ud-train.conllu --dev fr_gsd-ud-dev.conllu --out DIR --epochs 40 --seed 1 --threads 8 --budget-seconds 1500` |
| `parser` | biaffine parser with sentence starts | UD French-GSD | `train --train ... --dev ... --out DIR --epochs 40 --seconds 1500 --threads 8 --seed 7` |
| `ner` | entity recognizer | WikiNER French wp3 | `convert in.iob2 out.jsonl` for each split, then `train train.jsonl dev.jsonl DIR --epochs 40 --threads 8 --seed 7 --checkpoint-every 50` (see `ner/run.sh`) |

Each `train` resumes from the last checkpoint in its output directory when run again with the same arguments, so a job limited to 30 minutes is chained until `model.onenlp` appears. `predict --model DIR/model.onenlp --gold test.conllu --mode raw|gold [--mwt] --out P.conllu` writes predictions; with `--mwt` the pieces of a contraction (`d` + `u`) are written as a multiword token whose words are `de` and `le`. Fetch the data with `tools/fetch/fr/fetch.sh`. Results and provenance: `PROVENANCE.md`.
