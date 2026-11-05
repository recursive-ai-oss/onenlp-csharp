# Dutch fast-tier recipe

Trains the three parts of the Dutch fast-tier pipeline with the public `Train` API and writes CoNLL-U predictions for scoring:

| Part | Command | Components | Data |
|---|---|---|---|
| `tagging.onenlp` | `train-tagging` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer on one `FastEncoder` | UD Dutch-Alpino r2.18 train, dev |
| `parser.onenlp` | `train-parser` | biaffine dependency parser with sentence starts, fast encoder and a two-layer bidirectional LSTM | UD Dutch-Alpino r2.18 train, dev |
| `ner.onenlp` | `ner-train` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG, MISC) on a fast encoder | WikiNER Dutch wp3 train, dev |

Data comes from `tools/fetch/nl/fetch.sh`; hashes are in `tools/fetch/nl/MANIFEST.md`. The test files are read only by `predict` and `ner-eval`. Every part selects on its development file: early stopping with patience 6 and the best weights restored.

```
R="dotnet run -c Release --project recipes/nl/fast --"
$R train-tagging --train nl_alpino-ud-train.conllu --dev nl_alpino-ud-dev.conllu --out tagging-dir [encoder options] --threads 4
$R train-parser  --train nl_alpino-ud-train.conllu --dev nl_alpino-ud-dev.conllu --out parser-dir  [encoder options] --threads 4
$R ner-convert wikiner-nl-wp3-train.iob2 train.jsonl       # likewise dev and test
$R ner-train train.jsonl dev.jsonl ner-dir [encoder options] --threads 4
# put the three model.onenlp files in one directory as tagging.onenlp, parser.onenlp, ner.onenlp, then
$R predict --models models --gold nl_alpino-ud-test.conllu --mode gold --out pred-gold.conllu     # the treebank's own words and sentences
$R predict --models models --gold nl_alpino-ud-test.conllu --mode raw  --out pred-raw.conllu      # raw text
$R ner-eval models/ner.onenlp test.jsonl out-dir [--ref reference-tags.tsv]
$R segment --gold nl_alpino-ud-dev.conllu --out seg.conllu         # tokenizer and sentencizer alone
```

Score the CoNLL-U files with `tools/OneNlp.Eval ud` (`--ref-pred` for a paired bootstrap against another system's predictions).

Every train command checkpoints (`--checkpoint-every`) and resumes: run it again with the same arguments and it continues, with the same result as an uninterrupted run. `--seconds S` stops cleanly after about `S` seconds, which is how the long WikiNER run was split into chunks of at most 25 minutes.

Encoder options (`--width --depth --norm-rows --prefix-rows --suffix-rows --shape-rows --prefix --suffix --cased-rows --embed-width --hashes`) replace single settings of `FastEncoder`; without any, the encoder is `FastEncoder.English()`'s configuration. The settings of the shipped model are in `PROVENANCE.md`.

## Multiword tokens

UD Dutch-Alpino has one multiword token in its training and development files, so the tokenizer cuts none and `predict` writes no multiword-token lines.

`train-tagging` also takes `--cosine-epochs N` (a fixed budget of N epochs with the rate falling along a half cosine after the warm-up; use it with a large `--patience`). The shipped tagging model uses `--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --lr 0.003 --cosine-epochs 20 --patience 100` (see `PROVENANCE.md`): one bidirectional LSTM layer over each document lets a token see words beyond its window neighbours.
