# Turkish fast-tier recipe

Trains the three parts of the Turkish fast-tier pipeline with the public `Train` API and writes CoNLL-U predictions for scoring:

| Part | Command | Components | Data |
|---|---|---|---|
| `splitter.onenlp` | `train-splitter` | multiword-token splitter over the whole-word Turkish tokenizer (`NeuralMultiwordSplitter`): a B/I tagger over the characters of each candidate token and its sentence | UD Turkish-BOUN r2.18 train, dev |
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
# put the four model.onenlp files in one directory as splitter.onenlp, tagging.onenlp, parser.onenlp, ner.onenlp, then
$R predict --models models --gold tr_boun-ud-test.conllu --mode gold --out pred-gold.conllu     # gold tokens and sentences
$R predict --models models --gold tr_boun-ud-test.conllu --mode raw  --out pred-raw.conllu      # raw text
$R ner-eval models/ner.onenlp test.jsonl out-dir [--ref reference-tags.tsv]
$R segment --gold tr_boun-ud-dev.conllu --out seg.conllu         # tokenizer and sentencizer alone
```

Score the CoNLL-U files with `tools/OneNlp.Eval ud` (`--ref-pred` for a paired bootstrap against another system's predictions).

Every train command checkpoints (`--checkpoint-every`) and resumes: run it again with the same arguments and it continues, with the same result as an uninterrupted run. `--seconds S` stops cleanly after about `S` seconds.

Encoder options (`--width --depth --norm-rows --prefix-rows --suffix-rows --shape-rows --prefix --suffix --cased-rows --embed-width --hashes --turkic`) replace single settings of `FastEncoder`; without any, the encoder is the configuration shared by the English heads with the Turkish lower-casing of the norm attribute (`I` is `ı`, `İ` is `i`; `--turkic 0` switches it off). The settings of the shipped model are in `PROVENANCE.md`.

## Multiword tokens

UD Turkish writes a copula or a suffix spelled onto its word (`vardır`, `yaparken`) as one multiword token of two words. `train-splitter` trains the component that decides, for each letters-only surface token, whether and where it is cut (`docs/segmentation.md`): reference documents are the treebank's sentences as units (a unit per character of a candidate token), labelled B where a UD word starts. The tagger is a width-128 `FastEncoder` with one bidirectional LSTM of 64 units per direction; the chosen model uses `--dropout 0.3 --lr 0.001`. The splitter is the first part of the package (`splitter.onenlp`, the pipeline's tokenizer); the heads tag and parse the pieces, and `predict --mode raw` writes the multiword-token line with the pieces as its words (`NeuralMultiwordSplitter.TryMatch`), so that scoring sees UD words. `predict --tokenizer whole` replaces the splitter by a tokenizer that never cuts; `segment` without `--models` uses the table-based tokenizer, with `--models DIR` the splitter in `DIR/splitter.onenlp`; `--cut N` sets the minimum confidence of a cut for the comparison.

```
$R train-splitter --train tr_boun-ud-train.conllu --dev tr_boun-ud-dev.conllu --out splitter-dir --dropout 0.3 --lr 0.001 --threads 4
$R predict --models models --parts splitter,tagging,parser --gold tr_boun-ud-test.conllu --mode raw --out pred-raw.conllu
```
