# Third-party notices: RecursiveAI.OneNlp.Models.Hindi

The code and the weights of this package are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The package depends on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Training data (the weights are derived from it)

The weights are a trained model, not a copy of the data. They were trained on:

- **FineWeb-2**, Hindi (`hin_Deva`), Hugging Face, <https://huggingface.co/datasets/HuggingFaceFW/fineweb-2>, released under the Open Data Commons Attribution License 1.0 (<https://opendatacommons.org/licenses/by/1-0/>) and subject to the Common Crawl terms of use (<https://commoncrawl.org/terms-of-use>). Web text from the Common Crawl; 59,799 sentences were labelled and used.

The labels come from the accurate-tier model `RecursiveAI.OneNlp.Models.Multilingual.Large`, which is derived from:

- **mmBERT-base** (Marone et al. 2025, JHU CLSP), MIT licence.
- **Universal Dependencies** r2.18 treebanks English-EWT, German-GSD, French-GSD, Spanish-AnCora, Portuguese-Bosque, Italian-ParlaMint and Italian-MarkIT, Dutch-Alpino, Turkish-BOUN, Chinese-GSDSimp, licensed Creative Commons Attribution-ShareAlike 4.0 (CC BY 4.0 for AnCora and MarkIT); credit to the treebanks' authors and to the Universal Dependencies project (<https://universaldependencies.org>). The weights are derived from share-alike data and carry this credit.
- **Universal Dependencies** r2.18 Marathi-UFAL, Marathi-CMUPAN, Sindhi-Isra, Punjabi-Rang and Punjabi-CS, licensed Creative Commons Attribution-ShareAlike 4.0, credited to the treebanks' authors and to the Universal Dependencies project; they trained the Marathi, Sindhi and Indo-Aryan label heads on the encoder above, and a Sanskrit-Vedic head that was tried was not used. Carries the same share-alike lineage.
- **Named-entity corpora** UNER (CC BY-SA 4.0), GermEval 2014 (CC BY), WikiNER (CC BY 4.0 labels over Wikipedia text, CC BY-SA) and Turkish-WikiNER (CC BY-SA 4.0), as credited in that package's notices.

No other data, pretrained weights or label generators were used. The Hindi test treebank (UD Hindi-PUD) was used for evaluation only.
