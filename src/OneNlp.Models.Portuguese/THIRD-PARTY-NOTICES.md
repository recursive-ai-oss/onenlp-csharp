# Third-party notices: RecursiveAI.OneNlp.Models.Portuguese

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **Universal Dependencies Portuguese-Bosque r2.18**, <https://github.com/UniversalDependencies/UD_Portuguese-Bosque>, licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>). Used for morphology, lemmas, the dependency parser and sentence starts. The weights derived from it are released under the licence above with this credit.
- **Universal NER Portuguese-Bosque** (commit f2c2ad67), Mayhew et al., "Universal NER: A Gold-Standard Multilingual Named Entity Recognition Benchmark" (NAACL 2024), <https://github.com/UniversalNER/UNER_Portuguese-Bosque>, licensed Creative Commons Attribution-ShareAlike 4.0 International. Used for the entity recognizer.

No other data, pretrained weights or label generators were used. Evaluation data (the official test splits) was not used for training or for choosing settings.
