# spaCy baseline (black box)

The reference the English fast tier is compared against: spaCy's released command-line tool, trained on the same English-EWT data, with the configuration spaCy generates by default. Only spaCy's documented CLI and public Python API are used. Nothing here reads spaCy's code.

- **Version:** spaCy 3.8.18 (thinc 8.3.14), installed from PyPI into its own virtual environment.
- **Configs:** `tagger_parser.cfg` and `ner.cfg` are exactly what `spacy init config` wrote; the data paths are passed on the command line (`--paths.train`, `--paths.dev`) and nothing else is overridden, so the files can be checked against a fresh run.

  ```
  spacy init config tagger_parser.cfg --lang en --pipeline tagger,morphologizer,trainable_lemmatizer,parser --optimize efficiency
  spacy init config ner.cfg           --lang en --pipeline ner --optimize efficiency
  ```

  | File | SHA-256 |
  |---|---|
  | `tagger_parser.cfg` | `0e6bdce9ef61afcd8e9f4e92ad4c81a4b96ebf8560e6806f7e7ca3635719dc68` |
  | `ner.cfg` | `a10ef56c6562a685251ab9e428e10434e38cbd88070cdb88394ae8fc8c0dade0` |

  Training settings come from spaCy: seed 0, dropout 0.1, `max_steps` 20000, patience 1600, evaluation every 200 steps, Adam at 0.001, batches by words, CPU, no pretrained vectors.
- **Data:** UD English-EWT r2.18 for the tagger, morphologizer, lemmatizer and parser; UNER English-EWT for NER. Fetched and hashed by `tools/fetch/en/fetch.sh`.

## Running it

```
tools/fetch/en/fetch.sh <data-root>
tools/baselines/spacy/run.sh setup     <data-root> <work-dir>     # seqeval and the CoNLL 2018 script, run as black boxes
tools/baselines/spacy/run.sh convert   <data-root> <work-dir>
tools/baselines/spacy/run.sh train-ud  <data-root> <work-dir>     # long: see the results file
tools/baselines/spacy/run.sh train-ner <data-root> <work-dir>
tools/baselines/spacy/run.sh score-ud  <data-root> <work-dir>
tools/baselines/spacy/run.sh score-ner <data-root> <work-dir>
```

`ONENLP_ORACLES` names the directory with the virtual environments (`spacy/`, `seqeval/`, `ud-tools/`); `THREADS` sets BLAS threads (spaCy's training loop itself uses one core).

## How the pieces work

- **Conversion.** `spacy convert --converter conllu --n-sents 10` turns the CoNLL-U files into spaCy's binary format. Ten sentences per document, rather than the default of one, so the parser sees sentence boundaries inside running text; with one sentence per document it can never learn to split text, and the sentence rows would be meaningless. UNER is converted by `prepare_ner.py` (one sentence per document, gold tokens) with spaCy's public `Doc` and `DocBin`.
- **UD scoring.** `predict_ud.py` runs the trained pipeline and writes CoNLL-U; `conll18_ud_eval.py` (the shared-task script, Mozilla Public License 2.0, version 1.2, from `universaldependencies.org/conll18/`) scores it.
  - `raw`: the text of each test document is rebuilt from the gold forms and `SpaceAfter` and given to spaCy, which tokenizes it with its own tokenizer and splits sentences with its parser (the shared-task setting; every column is affected by tokenization).
  - `gold`: gold words and gold sentences are given; only the annotations are predicted.
  - spaCy does not produce multiword-token lines; in `gold` mode the gold ranges are copied through.
- **NER scoring.** `predict_ner.py` runs the pipeline on the gold UNER tokens; `score_ner.py` computes entity-level micro F1 with seqeval 1.2.2 (MIT, conlleval-style).

Results, training times and job ids are in `PROVENANCE.md` next to this file.
