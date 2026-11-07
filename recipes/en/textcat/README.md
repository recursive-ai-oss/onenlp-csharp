# English text classifier (multi-class)

Trains a text classifier on any JSON Lines file of labelled texts and scores it. The same commands work for your own data and your own labels.

```
dotnet run -c Release --project recipes/en/textcat -- train train.jsonl dev.jsonl out/            # resumes from out/last.ckpt; writes out/model.onenlp
dotnet run -c Release --project recipes/en/textcat -- predict out/model.onenlp test.jsonl pred.jsonl
dotnet run -c Release --project recipes/en/textcat -- bench out/model.onenlp test.jsonl
```

Each data line is `{"text": "...", "cats": {"label_a": 1, "label_b": 0, ...}}`; exactly one label is 1 in the multi-class form, and any set of labels is
learned. `python tools/fetch/textcat/fetch.py banking77 <train-dir> <eval-dir>` writes the BANKING77 files used below. Everything is the public API:

```csharp
Pipeline pipeline = Pipeline.Blank(Language.English).Add(new TextClassifier(FastEncoder.English(7)) { OwnsEncoder = true });
pipeline.Train(JsonlReader.Read("train.jsonl", null, new JsonlOptions { Tokenizer = pipeline.Tokenizer }).ToList(),
    new TrainOptions { DevExamples = dev, Score = DevScores.Accuracy(), Seed = 7 });
PipelineFile.Save(pipeline, "model.onenlp");
Doc doc = pipeline.Process("my card has not arrived");        // doc.Cats: label -> probability
```

## Design

`TextClassifier` (kind `textcat`) averages the encoder's vectors over the document's tokens (a matrix product, so the pooled vector does not depend on
the batch) and applies a linear layer with a softmax (multi-class) or independent sigmoids (multi-label, see `recipes/en/textcat-multilabel`). A hidden
ReLU layer is available (`--hidden`) and is off by default: it lowered development accuracy. The encoder is the fast tier's hash embeddings with
window layers (`FastEncoder.English()`); any `IEncoder` works, including the accurate tier.

Mean pooling of a convolutional or windowed encoder follows the sentence-classification setup of Kim 2014 (a convolution over word vectors, then pooling
over positions; here a mean instead of a max) and the bag-of-embeddings view of Joulin et al. 2016 (fastText: averaged embeddings, one linear layer).

## Results (BANKING77 official test, 3,080 texts, one run, seed 7)

| Metric | Ours | spaCy 3.8.18 `textcat` (default config, `TextCatBOW.v3`, same data) | Paired difference |
|---|---|---|---|
| Accuracy | 89.87 [88.80, 90.94] | 86.69 [85.42, 87.92] | +3.18 [2.08, 4.35] |
| Macro-F1 | 89.87 [88.71, 90.81] | 86.17 [84.78, 87.22] | |

Intervals are paired bootstrap 95% (1000 resamples over documents, seed 20260101, `tools/OneNlp.Eval cats`). Both numbers were reproduced with spaCy's own
`Scorer.score_cats` on the same prediction files (`tools/baselines/spacy/textcat/crosscheck.py`): accuracy 89.870 and 86.688, macro-F 89.868 and 86.175.
Published numbers for BANKING77 are for much larger pretrained encoders and are not comparable. Speed on one thread (indicative): 3,907 documents/s,
48.8k tokens/s. Training took 321 s on 4 threads (1,736 steps).
