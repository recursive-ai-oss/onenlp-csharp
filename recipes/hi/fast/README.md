# Hindi fast-tier recipe

Hindi has no treebank or entity corpus that we may train on (Hindi-HDTB and the Samanantar-sourced entity sets are non-commercial; `docs/ud-licences.md`), and Hindi-PUD is a test set. So the Hindi student is trained on **labels that the accurate tier makes on Hindi web text**, in two steps that this program runs:

1. `label`: the shared multilingual encoder reads each Hindi sentence once; the heads of nine other languages (English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish, Chinese; each trained on its own treebank and entity data) all write their analysis of it. This is zero-shot cross-lingual transfer through a multilingual encoder, with several source languages instead of one (McDonald, Petrov and Hall 2011, multi-source transfer of delexicalized parsers; Conneau et al. 2020 and Wu and Dredze 2019 for transfer through a shared multilingual encoder).
2. `ensemble`: the nine analyses are combined by agreement (Zhou and Li 2005, tri-training: independently trained models agreeing is the confidence).
   - UPOS is the plurality label; a UFeats value is kept when more than half of the sources give it.
   - The tree is the medoid of the nine trees (highest mean unlabelled agreement with the others), with each relation voted among the sources that share its head.
   - A sentence is kept for syntax when the mean plurality share of its UPOS is at least 0.6, the medoid's mean agreement is at least 0.5, and it has one root.
   - Entities are spans (type and boundaries) that at least half of the sources produce; a sentence with a span that two or more but fewer than half produce is left out of the entity files.
   - The thresholds were chosen once, before any student was trained, to keep about half of the sentences. They were not tuned.

The student then trains on the kept sentences with `train-tagging` (a morphologizer: UPOS and UFeats), `train-parser` and `ner-train`: the same fast-tier heads and `Train` API as the other languages. There is **no lemmatizer and no XPOS head**: nothing transfers across the nine languages (edit trees are suffix rules of other languages, XPOS sets are language-specific), and a lemmatizer is shipped only if it beats the identity lemma on a check we may use, which none could. The Lemmas row is scored with the identity lemma (`--identity-lemmas`) and is not a model output.

## Data

- **Text:** FineWeb-2 `hin_Deva` (ODC-By 1.0, Common Crawl terms), the Hindi sample of the distillation text (`tools/distill/fetch_data.py`), of which the first 3,200 documents are labelled. Sentences are cut with the Hindi tokenizer and sentencizer (the danda `।` and the full stop). They are kept when they have 5 to 50 tokens, nearly all letters Devanagari and mostly words, and dropped when they share an 8-word run with a Hindi-PUD sentence or repeat. Hits against Hindi-PUD: 0 (the distillation text had already been filtered against the evaluation keys).
- **Evaluation:** Hindi-PUD is read once, for the final model (`evaluate.sh final`). Selection and checks use 5% of the labelled documents held out (agreement with the labeller only), 17 sentences we annotated ourselves (`testdata/hi/selfdev.conllu`: UPOS, heads and relations in the UD Hindi style) and 91 sentences with hand-labelled entities (`testdata/hi/nerspot.iob2`), both from held-out web documents. The first ranked the source languages and the labelling model, and nothing else.

## Commands

```
R="dotnet run -c Release --project recipes/hi/fast --"
recipes/hi/fast/label-chunk.sh WEIGHTS TEXT.jsonl PUD.conllu OUT K       # chunk K of 400 documents; 8 chunks were used
$R ensemble --in OUT/chunk-0000.jsonl.gz,... --out ENS --pos-agree 0.6 --uas-agree 0.5
recipes/hi/fast/train-part.sh tagging|parser|ner ENS STUDENT              # resumable
recipes/hi/fast/evaluate.sh dev STUDENT-MODELS WEIGHTS OUT               # hand-annotated sentences
recipes/hi/fast/evaluate.sh final STUDENT-MODELS WEIGHTS OUT PUD.conllu  # Hindi-PUD, once
tools/baselines/spacy/hi_selflabelled.sh ...                              # the same-labels baseline
```

`WEIGHTS` is the directory of the accurate tier's exported files (encoder and `heads/<language>.onenlp`). `label` reads both layouts (trainable parts, or the ONNX graph in pieces) and changes nothing in them. Put the three `model.onenlp` files in one directory as `tagging.onenlp`, `parser.onenlp` and `ner.onenlp` for `predict`, `speed` and the package. `score-sources.sh` and `variants.sh` score the source languages and the pooling variants on the hand-annotated sentences.

## Choices that the evidence settled

- **Labelling model.** The newer base-size accurate tier (continued training, lemma tables) was tried on the hand-annotated sentences: ensemble UPOS 77.88, UAS 60.58, LAS 46.15, against 79.33, 63.94, 50.48 for the weights the labels came from (within noise on 208 tokens, nominally lower), so the labelling stayed with the earlier weights.
- **Source languages.** Seven pooling variants (all nine, without English, five to seven of the best single sources) were within noise of each other on those sentences, so all nine are pooled; no weighting.
- **Segmentation.** UAX #29 plus the sentencizer need no Hindi data: the danda is its own token and ends a sentence. No `SegmentationData.Hindi` exists.
