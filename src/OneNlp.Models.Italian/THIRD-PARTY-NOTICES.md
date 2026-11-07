# Third-party notices: RecursiveAI.OneNlp.Models.Italian

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **Universal Dependencies Italian-ParlaMint r2.18**, <https://github.com/UniversalDependencies/UD_Italian-ParlaMint>, annotations of Italian Senate transcripts from the ParlaMint corpora, licensed Creative Commons Attribution-ShareAlike 4.0 International (<https://creativecommons.org/licenses/by-sa/4.0/>); 326 training sentences, 7 in 8 of them for training and the rest for development (`tools/fetch/fetch_lang.py`, `derive_italian`). The weights are derived from share-alike data and carry this credit.
- **Universal Dependencies Italian-MarkIT r2.18**, <https://github.com/UniversalDependencies/UD_Italian-MarkIT>, grammar example sentences, licensed Creative Commons Attribution 4.0 International (<https://creativecommons.org/licenses/by/4.0/>); its training split is used for training and its development split for development.

Both were used for tags, morphology, lemmas, the dependency parser and sentence starts.
- **WikiNER, Italian wp3**, Nothman, Ringland, Radford, Murphy and Curran, "Learning multilingual named entity recognition from Wikipedia" (Artificial Intelligence 194, 2013), <https://figshare.com/articles/dataset/Learning_multilingual_named_entity_recognition_from_Wikipedia/5462500>, licensed Creative Commons Attribution 4.0 International. Its labels are silver annotations projected from Wikipedia links over Wikipedia text, which is licensed Creative Commons Attribution-ShareAlike (<https://creativecommons.org/licenses/by-sa/4.0/>); the entity recognizer is derived from that text and is released under the licence above with this credit. Used for the entity recognizer.

No other data, pretrained weights or label generators were used. Evaluation data (the official test splits) was not used for training or for choosing settings.
