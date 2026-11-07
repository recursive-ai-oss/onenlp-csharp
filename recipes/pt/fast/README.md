# Portuguese fast-tier recipe

Trains the three parts of the Portuguese fast-tier pipeline with the public `Train` API and writes CoNLL-U predictions for scoring:

| Part | Command | Components | Data |
|---|---|---|---|
| `tagging.onenlp` | `train-tagging` | sentencizer, morphologizer (UPOS, UFeats), edit-tree lemmatizer on one `FastEncoder` | UD Portuguese-Bosque r2.18 train, dev |
| `parser.onenlp` | `train-parser` | biaffine dependency parser with sentence starts, fast encoder and a two-layer bidirectional LSTM | UD Portuguese-Bosque r2.18 train, dev |
| `ner.onenlp` | `ner-train` | entity recognizer (BILUO tagging, constrained decoding; PER, ORG, LOC) on a fast encoder | Universal NER Portuguese-Bosque train, dev |

Bosque has no XPOS column, so there is no tagger head. Data comes from `tools/fetch/pt/fetch.sh`; hashes are in `tools/fetch/pt/MANIFEST.md`. The test files are read only by `predict` and `ner-eval`. Every part selects on its development file: early stopping with patience 6 and the best weights restored.

```
R="dotnet run -c Release --project recipes/pt/fast --"
$R prepare pt_bosque-ud-train.conllu train.conllu          # likewise the dev file
$R train-tagging --train train.conllu --dev dev.conllu --out tagging-dir [encoder options] --threads 4
$R train-parser  --train train.conllu --dev dev.conllu --out parser-dir  [encoder options] --threads 4
$R ner-convert pt_bosque-ud-train.conllu pt_bosque-ud-train.iob2 ner-train.jsonl     # likewise dev and test
$R ner-train ner-train.jsonl ner-dev.jsonl ner-dir --depth 1 --plain 1 --relu 1 --recurrent 1 --recurrent-size 64 --threads 4
# put the three model.onenlp files in one directory as tagging.onenlp, parser.onenlp, ner.onenlp, then
$R predict --models models --gold pt_bosque-ud-test.conllu --lexicon pt_bosque-ud-train.conllu --mode gold --out pred-gold.conllu
$R predict --models models --gold pt_bosque-ud-test.conllu --lexicon pt_bosque-ud-train.conllu --mode raw  --out pred-raw.conllu
$R ner-eval models/ner.onenlp ner-test.jsonl out-dir [--ref reference-tags.tsv]
$R segment --gold pt_bosque-ud-dev.conllu --lexicon pt_bosque-ud-train.conllu --out seg.conllu   # tokenizer and sentencizer alone
```

Score the CoNLL-U files with `tools/OneNlp.Eval ud` (`--ref-pred` for a paired bootstrap against another system's predictions).

Every train command checkpoints (`--checkpoint-every`) and resumes: run it again with the same arguments and it continues, with the same result as an uninterrupted run. `--seconds S` stops cleanly after about `S` seconds.

Encoder options (`--width --depth --norm-rows --prefix-rows --suffix-rows --shape-rows --prefix --suffix --cased-rows --embed-width --hashes`) replace single settings of `FastEncoder`; without any, the encoder is `FastEncoder.English()`'s configuration. The settings of the shipped model are in `PROVENANCE.md`.

## Multiword tokens

UD Portuguese writes `do` as one multiword token of the words `de` and `o`. The tokenizer cuts such a surface form into two pieces (`d`|`o`, `docs/segmentation.md`), the heads tag and parse the pieces, and `predict` writes the multiword-token line with the treebank's words from `SegmentationData.Portuguese.TryMatchMultiword`, so that scoring sees UD words.

Two kinds of multiword token cannot be pieces: `à` (`a` + `a`, one character) stays one token, and `trata-se` becomes the two tokens `trata` and `-se` (the CoNLL-U reader would cut it into `trat` and `a-se`). `prepare` rewrites the treebank to those tokens for training and development; `predict --lexicon` writes the words back, taking the analysis of `à` and the spelling of an enclitic's word (`-lo` is the word `o`) from the most frequent ones in the training file.

`train-tagging` also takes `--cosine-epochs N` (a fixed budget of N epochs with the rate falling along a half cosine after the warm-up; use it with a large `--patience`). The shipped tagging model uses `--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --lr 0.003 --cosine-epochs 20 --patience 100` (see `PROVENANCE.md`): one bidirectional LSTM layer over each document lets a token see words beyond its window neighbours.
