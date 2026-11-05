# Third-party notices: RecursiveAI.OneNlp.Models.Dutch

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **Universal Dependencies Dutch-GSD r2.18**, <https://github.com/UniversalDependencies/UD_Dutch-GSD>, licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>). Used for tags, morphology, lemmas, the dependency parser and sentence starts. The weights derived from it are released under the licence above with this credit.
- **GermEval 2014 NER Shared Task (NoSta-D)**, Benikova, Biemann and Reznicek, "NoSta-D Named Entity Annotation for Dutch: Guidelines and Dataset" (LREC 2014), <https://sites.google.com/site/germeval2014ner/>, licensed Creative Commons Attribution 4.0 International (<https://creativecommons.org/licenses/by/4.0/>). Used for the entity recognizer.

No other data, pretrained weights or label generators were used. Evaluation data (the official test splits) was not used for training or for choosing settings.
