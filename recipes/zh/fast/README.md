# Chinese fast-tier recipe

Trains the four parts of the Chinese fast-tier pipeline with the public `Train` API and writes CoNLL-U predictions for scoring:

| Part | Command | Components | Data |
|---|---|---|---|
| `segmenter.onenlp` | `train-segmenter` | a character tagger (`B` word start, `I` continuation) that is the pipeline's tokenizer (`NeuralWordSegmenter`), on a fast encoder with neighbour, wider-context and character-pair tables and a two-layer bidirectional LSTM | UD Chinese-GSDSimp r2.18 train, dev |
| `tagging.onenlp` | `train-tagging` | sentencizer, tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer on one `FastEncoder`; trained on the treebank's gold words | UD Chinese-GSDSimp r2.18 train, dev |
| `parser.onenlp` | `train-parser` | biaffine dependency parser with sentence starts, on gold words | UD Chinese-GSDSimp r2.18 train, dev |
| `ner.onenlp` | `ner-train` | entity recognizer (BILUO tagging, constrained decoding; PER, LOC, ORG), on gold words | UNER Chinese-GSDSIMP train, dev |

Data comes from `tools/fetch/zh/fetch.sh`; hashes are in `tools/fetch/zh/MANIFEST.md`. The test files are read only by `predict` and `ner-eval`. Every part selects on its development file.

```
R="dotnet run -c Release --project recipes/zh/fast --"
D=zh_gsdsimp-ud
$R train-segmenter --train $D-train.conllu --dev $D-dev.conllu --out seg-dir --width 192 --depth 2 --norm-rows 16384 --prefix-rows 4096 --suffix-rows 4096 --shape-rows 1024 --neighbour-rows 8192 --context-rows 8192 --bigram-rows 32768 --embed-width 64 --relu 1 --recurrent 2 --recurrent-size 96 --dropout 0.5 --lr 0.001 --batch-words 1000 --cosine-epochs 12 --patience 100 --threads 4
$R train-tagging --train $D-train.conllu --dev $D-dev.conllu --out tagging-dir --plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --prefix 1 --suffix 1 --lr 0.003 --cosine-epochs 20 --patience 100 --threads 4
$R train-parser  --train $D-train.conllu --dev $D-dev.conllu --out parser-dir --prefix 1 --suffix 1 --epochs 40 --threads 4
$R ner-convert uner-train.iob2 ner-train.jsonl      # likewise dev and test
$R ner-train ner-train.jsonl ner-dev.jsonl ner-dir --prefix 1 --suffix 1 --hashes 2 --norm-rows 32768 --embed-width 96 --dropout 0.5 --patience 8 --epochs 40 --threads 4
# put the four model.onenlp files in one directory as segmenter.onenlp, tagging.onenlp, parser.onenlp, ner.onenlp, then
$R predict --models models --gold $D-test.conllu --mode gold --out pred-gold.conllu     # gold words and sentences
$R predict --models models --gold $D-test.conllu --mode raw  --out pred-raw.conllu      # raw text, the segmenter cuts the words
$R ner-eval models/ner.onenlp ner-test.jsonl out-dir [--ref reference-tags.tsv]          # gold words
$R ner-eval models/ner.onenlp ner-test.jsonl out-dir --raw models/segmenter.onenlp       # raw text, character spans
$R segment --models models --gold $D-dev.conllu --out seg.conllu                         # segmenter and sentencizer alone
$R speed --models models --gold $D-test.conllu [--parts segmenter,tagging] [--repeat 3]
```

Score the CoNLL-U files with `tools/OneNlp.Eval ud` (`--ref-pred` for a paired bootstrap against another system's predictions). Every train command checkpoints (`--checkpoint-every`) and resumes: run it again with the same arguments and it continues, with the same result as an uninterrupted run. `--seconds S` stops cleanly after about `S` seconds.

Encoder options (`--width --depth --norm-rows --prefix-rows --suffix-rows --shape-rows --prefix --suffix --cased-rows --embed-width --hashes --recurrent --recurrent-size --neighbour-rows --bigram-rows --context-rows --plain --relu`) replace single settings of `FastEncoder`.

## Word segmentation

The segmenter is trained on documents of eight consecutive treebank sentences whose tokens are the characters (`NeuralWordSegmenter.Units`) labelled `B` or `I` (`docs/segmentation.md`). The development score is word F1 over units. The other three parts are trained on the treebank's gold words, as spaCy's `gold_preproc` reference is, and read the words the segmenter cuts at run time; a segmentation error therefore costs the raw-text rows what it costs.
