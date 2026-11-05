# Third-party notices: RecursiveAI.OneNlp.Models.French

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **Universal Dependencies French-GSD r2.18**, <https://github.com/UniversalDependencies/UD_French-GSD>, licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>). The treebank's README notes that the underlying sentences were provided by Google "AS IS" and that Google claims no ownership. Used for morphology, lemmas, the dependency parser, sentence starts and the contraction statistics of the tokenizer. The weights derived from it are released under the licence above with this credit.
- **WikiNER French (wp3)**, Nothman, Ringland, Radford, Murphy and Curran, "Learning multilingual named entity recognition from Wikipedia" (Artificial Intelligence 194, 2013), <https://figshare.com/articles/dataset/Learning_multilingual_named_entity_recognition_from_Wikipedia/5462500>, licensed Creative Commons Attribution 4.0 International (<https://creativecommons.org/licenses/by/4.0/>) per its figshare record. The labels are projected from Wikipedia links over Wikipedia text, which is licensed CC BY-SA 4.0 and GFDL, so the entity recognizer has share-alike lineage. Used for the entity recognizer, on a split of our own by article.

No other data, pretrained weights or label generators were used. Evaluation data (the official UD test split, and the WikiNER test articles) was not used for training or for choosing settings.
