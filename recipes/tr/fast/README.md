# Turkish fast-tier recipe

Trains the three parts of the Turkish fast-tier pipeline with the public `Train` API and writes CoNLL-U predictions for scoring:

| Part | Command | Components | Data |
|---|---|---|---|
| `tagging.onenlp` | `train-tagging` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer on one `FastEncoder` | UD Turkish-BOUN r2.18 train, dev |
| `parser.onenlp` | `train-parser` | biaffine dependency parser with sentence starts, fast encoder and a two-layer bidirectional LSTM | UD Turkish-BOUN r2.18 train, dev |
| `ner.onenlp` | `ner-train` | entity recognizer (BILUO tagging, constrained decoding; PER, ORG, LOC) on a fast encoder | Turkish-WikiNER train, dev (PERSON, ORG, GPE and LOC kept, GPE and LOC both LOC; the other types are O) |

Data comes from `tools/fetch/tr/fetch.sh`; hashes are in `tools/fetch/tr/MANIFEST.md`. The test files are read only by `predict` and `ner-eval`. Every part selects on its development file: early stopping with patience 6 and the best weights restored.

```
R="dotnet run -c Release --project recipes/tr/fast --"
$R train-tagging --train tr_boun-ud-train.conllu --dev tr_boun-ud-dev.conllu --out tagging-dir [encoder options] --threads 4
$R train-parser  --train tr_boun-ud-train.conllu --dev tr_boun-ud-dev.conllu --out parser-dir  [encoder options] --threads 4
$R ner-convert turkish-wikiner-train.iob2 train.jsonl       # likewise dev and test
$R ner-train train.jsonl dev.jsonl ner-dir [encoder options] --threads 4
# put the three model.onenlp files in one directory as tagging.onenlp, parser.onenlp, ner.onenlp, then
$R predict --models models --gold tr_boun-ud-test.conllu --mode gold --out pred-gold.conllu     # gold tokens and sentences
$R predict --models models --gold tr_boun-ud-test.conllu --mode raw  --out pred-raw.conllu      # raw text
$R ner-eval models/ner.onenlp test.jsonl out-dir [--ref reference-tags.tsv]
$R segment --gold tr_boun-ud-dev.conllu --out seg.conllu         # tokenizer and sentencizer alone
```

Score the CoNLL-U files with `tools/OneNlp.Eval ud` (`--ref-pred` for a paired bootstrap against another system's predictions).

Every train command checkpoints (`--checkpoint-every`) and resumes: run it again with the same arguments and it continues, with the same result as an uninterrupted run. `--seconds S` stops cleanly after about `S` seconds.

Encoder options (`--width --depth --norm-rows --prefix-rows --suffix-rows --shape-rows --prefix --suffix --cased-rows --embed-width --hashes --turkic`) replace single settings of `FastEncoder`; without any, the encoder is the configuration shared by the English heads with the Turkish lower-casing of the norm attribute (`I` is `ı`, `İ` is `i`; `--turkic 0` switches it off). The settings of the shipped model are in `PROVENANCE.md`.

## Multiword tokens

UD Turkish writes a copula or a suffix spelled onto its word (`vardır`, `yaparken`) as one multiword token of two words. The tokenizer cuts such a surface form into two pieces from a table of word endings (`docs/segmentation.md`), the heads tag and parse the pieces, and `predict --mode raw` writes the multiword-token line with the two pieces as its words (`SegmentationData.TryMatchMultiword`), so that scoring sees UD words.
