# Italian fast-tier recipe

Trains the three parts of the Italian fast-tier pipeline with the public `Train` API and writes CoNLL-U predictions for scoring:

| Part | Command | Components | Data |
|---|---|---|---|
| `tagging.onenlp` | `train-tagging` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer on one `FastEncoder` | the derived `it_gating` train and dev files (UD Italian-ParlaMint and Italian-MarkIT r2.18, 897 and 381 sentences) |
| `parser.onenlp` | `train-parser` | biaffine dependency parser with sentence starts, fast encoder and a two-layer bidirectional LSTM | the derived `it_gating` train and dev files (UD Italian-ParlaMint and Italian-MarkIT r2.18, 897 and 381 sentences) |
| `ner.onenlp` | `ner-train` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG, MISC) on a fast encoder | WikiNER Italian wp3 train, dev |

Data comes from `tools/fetch/it/fetch.sh`; hashes are in `tools/fetch/it/MANIFEST.md`. The test files are read only by `predict` and `ner-eval`. Every part selects on its development file: early stopping with patience 6 and the best weights restored.

```
R="dotnet run -c Release --project recipes/it/fast --"
$R train-tagging --train it_gating-ud-train.conllu --dev it_gating-ud-dev.conllu --out tagging-dir --plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --lr 0.003 --cosine-epochs 20 --patience 100 --threads 4
$R train-parser  --train it_gating-ud-train.conllu --dev it_gating-ud-dev.conllu --out parser-dir  [encoder options] --threads 4
$R ner-convert wikiner-it-wp3-train.iob2 ner-train.jsonl --pieces       # likewise dev; test without --pieces (and with, for ner-eval below)
$R ner-train ner-train.jsonl ner-dev.jsonl ner-dir [encoder options] --threads 4 --checkpoint-every 100
# put the three model.onenlp files in one directory as tagging.onenlp, parser.onenlp, ner.onenlp, then
$R predict --models models --gold it_parlamint-ud-test.conllu --mode gold --out pred-gold.conllu     # gold words and sentences
$R predict --models models --gold it_parlamint-ud-test.conllu --mode raw  --out pred-raw.conllu      # raw text
$R ner-eval models/ner.onenlp ner-test.jsonl out-dir [--ref reference-tags.tsv] --pieces-file ner-test-pieces.jsonl
$R segment --gold it_gating-ud-dev.conllu --out seg.conllu         # tokenizer and sentencizer alone
```

Score the CoNLL-U files with `tools/OneNlp.Eval ud` (`--ref-pred` for a paired bootstrap against another system's predictions).

Every train command checkpoints (`--checkpoint-every`) and resumes: run it again with the same arguments and it continues, with the same result as an uninterrupted run. `--seconds S` stops cleanly after about `S` seconds.

Encoder options (`--width --depth --norm-rows --prefix-rows --suffix-rows --shape-rows --prefix --suffix --cased-rows --embed-width --hashes`) replace single settings of `FastEncoder`; without any, the encoder is `FastEncoder.English()`'s configuration. The settings of the shipped model are in `PROVENANCE.md`.

## Multiword tokens

UD Italian writes `del` as one multiword token of the words `di` and `il`, and a verb with enclitic pronouns (`vederlo`) as the verb and the pronouns. The tokenizer cuts such a surface form into pieces (`de`|`l`, `veder`|`lo`; `docs/segmentation.md`), the heads tag and parse the pieces, and `predict` writes the multiword-token line with the treebank's words from `SegmentationData.TryMatchMultiword`, so that scoring sees UD words.

WikiNER keeps `del` whole. A model that reads raw text sees the pieces, so the entity recognizer is trained on `ner-convert --pieces` files (the first piece keeps the tag, later pieces continue the entity), and `ner-eval --pieces-file` gives the model the pieces and scores each original token with the tag of its first piece.

`train-tagging` also takes `--cosine-epochs N` (a fixed budget of N epochs with the rate falling along a half cosine after the warm-up; use it with a large `--patience`). The shipped tagging model uses the flags in the command above: one bidirectional LSTM layer over each document lets a token see words beyond its window neighbours. The parser and entity parts use the default encoder.
