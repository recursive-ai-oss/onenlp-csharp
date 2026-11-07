# Third-party notices: RecursiveAI.OneNlp.Models.Turkish

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **Universal Dependencies Turkish-BOUN r2.18**, <https://github.com/UniversalDependencies/UD_Turkish-BOUN>, licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>). Used for tags, morphology, lemmas, the dependency parser and sentence starts, and to choose the tokenizer's multiword-token table. The weights derived from it are released under the licence above with this credit.
- **Turkish-WikiNER**, Altinok, "Turkish-WikiNER" (turkish-nlp-suite), <https://huggingface.co/datasets/turkish-nlp-suite/turkish-wikiNER>, licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>). Its text comes from Wikipedia (CC BY-SA). Used for the entity recognizer.

- **FineWeb-2** (HuggingFaceFW/fineweb-2, Turkish, revision af9c1333), licensed ODC-By 1.0 and subject to the Common Crawl terms of use, and **Wikipedia** (wikimedia/wikipedia, Turkish, 20231101), licensed CC BY-SA 3.0 and GFDL. About 60,000 sentences of this text, parsed by earlier parsers of the same recipe, were extra training sentences for the dependency parser (the labels are the earlier models' own output, not a third party's). Credit: FineWeb-2 and Common Crawl; Wikipedia and its contributors.

No other data, pretrained weights or label generators were used. Evaluation data (the official test splits) was not used for training or for choosing settings.
