# Third-party notices: RecursiveAI.OneNlp.Models.Spanish

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **Universal Dependencies Spanish-AnCora r2.18**, <https://github.com/UniversalDependencies/UD_Spanish-AnCora>, from the AnCora corpus (Taulé, Martí and Recasens, "AnCora: Multilevel Annotated Corpora for Catalan and Spanish", LREC 2008; converted to Universal Dependencies by the UD Spanish team), licensed Creative Commons Attribution 4.0 International (<https://creativecommons.org/licenses/by/4.0/>) by the treebank's `LICENSE.txt`. Used for tags, morphology, lemmas, the dependency parser and sentence starts.
- **WikiNER, Spanish wp3**, Nothman, Ringland, Radford, Murphy and Curran, "Learning multilingual named entity recognition from Wikipedia" (Artificial Intelligence 194, 2013), <https://figshare.com/articles/dataset/Learning_multilingual_named_entity_recognition_from_Wikipedia/5462500>, licensed Creative Commons Attribution 4.0 International. Its labels are silver annotations projected from Wikipedia links over Wikipedia text, which is licensed Creative Commons Attribution-ShareAlike (<https://creativecommons.org/licenses/by-sa/4.0/>); the entity recognizer is derived from that text and is released under the licence above with this credit. Used for the entity recognizer.

No other data, pretrained weights or label generators were used. Evaluation data (the official test splits) was not used for training or for choosing settings.
