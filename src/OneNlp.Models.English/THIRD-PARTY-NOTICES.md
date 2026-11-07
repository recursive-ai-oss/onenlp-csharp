# Third-party notices: RecursiveAI.OneNlp.Models.English

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights were trained on the following datasets. Both are licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>); the weights are a trained model, not a copy of the data, and are released under the licence above with this credit:

- **Universal Dependencies English Web Treebank (UD English-EWT) r2.18**, <https://github.com/UniversalDependencies/UD_English-EWT>. Silveira et al., "A Gold Standard Dependency Corpus for English" (LREC 2014), and later releases. Used for tags, morphology, lemmas, the dependency parser and sentence starts. The treebank's texts come from the English Web Treebank.
- **Universal NER (UNER) English-EWT**, commit 8ed072de2fd24022cc62458997fe96a8fe191ea4, <https://github.com/UniversalNER/UNER_English-EWT>. Mayhew et al., "Universal NER: A Gold-Standard Multilingual Named Entity Recognition Benchmark" (NAACL 2024). Used for the entity recognizer.

The tagging and entity models were also trained on unlabelled web and encyclopedia text, labelled by an earlier version of this package's own models (self-training; `recipes/en/selftrain`):

- **FineWeb** (Hugging Face), sample-10BT of the English edition, released under the Open Data Commons Attribution License 1.0 (<https://opendatacommons.org/licenses/by/1-0/>) and subject to the Common Crawl terms of use.
- **Wikipedia** (`wikimedia/wikipedia`, 20231101 English dump), text by the Wikipedia contributors under CC BY-SA 3.0 (<https://creativecommons.org/licenses/by-sa/3.0/>) and the GFDL.

No other data, pretrained weights or label generators were used. Sentences overlapping any evaluation or development set were removed from that text before training. Evaluation data (the official test splits) was not used for training or for choosing settings.
