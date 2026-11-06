# Third-party notices: RecursiveAI.OneNlp.Models.Chinese

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **Universal Dependencies Chinese-GSDSimp r2.18**, <https://github.com/UniversalDependencies/UD_Chinese-GSDSimp>, the Simplified-script conversion of UD Chinese-GSD (Google Universal Dependency Treebank; Qi and Yu, CoNLL 2018 shared task, and the UD Chinese team), licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>) by the treebank's `LICENSE.txt`. Used for word segmentation, tags, morphology, lemmas, the dependency parser and sentence starts.
- **Universal NER Chinese-GSDSIMP v1**, <https://github.com/UniversalNER/UNER_Chinese-GSDSIMP>, Mayhew et al., "Universal NER: A Gold-Standard Multilingual Named Entity Recognition Benchmark" (NAACL 2024), licensed Creative Commons Attribution-ShareAlike 4.0 International. Used for the entity recognizer.

The weights are derived from CC BY-SA data and are released under the licence above with this credit.

No other data, pretrained weights or label generators were used. Evaluation data (the official test splits) was not used for training or for choosing settings.
