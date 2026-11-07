# English multi-label text classifier

Trains a classifier that gives each document any number of categories, on any JSON Lines file, and scores it. The same commands work for your own data
and your own labels.

```
python tools/fetch/textcat/fetch.py eurlex <train-dir> <eval-dir>                          # LexGLUE EUR-LEX, first 512 words of each document
dotnet run -c Release --project recipes/en/textcat-multilabel -- train train.jsonl dev.jsonl out/ --epochs 6   # resumes from out/last.ckpt
dotnet run -c Release --project recipes/en/textcat-multilabel -- predict out/model.onenlp test.jsonl pred.jsonl
```

Each data line is `{"text": "...", "cats": {"label_a": 1, "label_b": 0, ...}}`; a label missing from a line counts as 0. The public API:

```csharp
Pipeline pipeline = Pipeline.Blank(Language.English).Add(new TextClassifier(FastEncoder.English(7), multiLabel: true) { OwnsEncoder = true });
pipeline.Train(examples, new TrainOptions { DevExamples = dev, Score = DevScores.CatsMicroF1(), Seed = 7, Epochs = 6 });
Doc doc = pipeline.Process(text);                 // doc.Cats: label -> probability; keep the labels at or above your threshold
```

## Design

The same `TextClassifier` as the multi-class recipe (mean-pooled encoder vectors, a linear layer), with one independent sigmoid per label and binary
cross-entropy instead of a softmax (`multiLabel: true`). The encoder is `FastEncoder.English()`. Choosing the labels of a document is the caller's
threshold on `doc.Cats`; the scores below use 0.5 for the F1 rows.

## Data

LexGLUE EUR-LEX (Chalkidis et al. 2022, built on MultiEURLEX, Chalkidis, Fergadiotis and Androutsopoulos 2021): English EU legislation, each document
labelled by the EU Publications Office with EuroVoc concepts (100 labels), the official split of 55,000 train / 5,000 development / 5,000 test documents,
read from the Parquet files of `coastalcph/lex_glue` at a pinned revision. The card's licence is CC BY 4.0, and the documents are EUR-Lex texts reusable under
Commission Decision 2011/833/EU with acknowledgement of the source (c) European Union. Each document is cut to its first 512 whitespace words.
`PROVENANCE.md` lists hashes.

## Results (EUR-LEX official test, 5,000 documents, one run, seed 7)

| Metric | Ours | spaCy 3.8.18 `textcat_multilabel` (default config, same data) | Verdict |
|---|---|---|---|
| Micro-F1 | 59.60 [58.92, 60.30] | 44.86 [44.07, 45.69] | ahead |
| Macro-F1 | 40.93 [39.78, 41.78] | 15.66 [15.27, 16.05] | ahead |
| Macro ROC-AUC | 91.81 [91.47, 92.15] | 67.29 [66.65, 67.93] | ahead |

Intervals are bootstrap 95% (1000 resamples over documents, seed 20260101, `tools/OneNlp.Eval cats --mode multi`). Scores were reproduced with spaCy's
own `Scorer.score_cats` on the same prediction files (`tools/baselines/spacy/textcat/crosscheck.py`): micro-F 59.597 and 44.861, macro-F 40.926 and 15.661,
macro-AUC 91.813 and 67.294.

spaCy's default config stopped early (4,400 steps, validation score 67.1, 10.5 minutes); its recall is low at a 0.5 threshold on 100 sparse labels.

For information, the LexGLUE paper (Chalkidis et al. 2022, ACL; table on the dataset card) reports on the same test split, with the full document and
a threshold tuned differently: TF-IDF+SVM micro-F1 71.3, macro-F1 51.4; BERT-base 71.4 and 57.2. Those are bag-of-words SVM and pretrained-transformer numbers
under a different truncation and training budget; ours is a from-scratch fast-tier model with a 512-word cut, and is below them.

## Training

6 epochs (the cap; development micro-F1 66.1, 70.1, 68.3, 68.6, 67.1 and 70.4 after epochs 1 to 6; the best, the last, is kept), 92,932 steps, batch 2,000
words, 4 threads, about 3 hours of wall time in 25-minute resumable chunks on a loaded shared machine.
