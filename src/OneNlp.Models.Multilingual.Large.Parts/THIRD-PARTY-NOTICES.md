# Third-party notices: the weights of RecursiveAI.OneNlp.Models.Multilingual.Large

The code of this package is licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The weights are a fine-tuned copy of a third-party model, trained on the data below.

## Base model

- **mmBERT-base** (Marone et al., "mmBERT: A Modern Multilingual Encoder with Annealed Language Learning", 2025, arXiv:2509.06888), Johns Hopkins University CLSP, <https://huggingface.co/jhu-clsp/mmBERT-base>, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82`, licensed MIT. The token embedding table and `tokenizer.json` are the model's own, unchanged; the transformer layers were fine-tuned.

## Training data (the fine-tuned weights are derived from it)

- **Universal Dependencies r2.18** (<https://universaldependencies.org>): English-EWT, German-GSD, French-GSD, Portuguese-Bosque, Dutch-Alpino, Turkish-BOUN and Chinese-GSDSimp (Creative Commons Attribution-ShareAlike 4.0), Spanish-AnCora (Creative Commons Attribution 4.0), Italian-ParlaMint and Italian-MarkIT (Creative Commons Attribution-ShareAlike 4.0 and Attribution 4.0 respectively; see the treebank READMEs). Tags, morphology, lemmas and dependency parses.
- **Universal NER v1** (Mayhew et al., NAACL 2024, <https://www.universalner.org>): English-EWT, Portuguese-Bosque and Chinese-GSDSimp, Creative Commons Attribution-ShareAlike 4.0.
- **GermEval 2014 NER** (Benikova, Biemann and Reznicek, LREC 2014), Creative Commons Attribution 4.0.
- **WikiNER** (Nothman et al., "Learning multilingual named entity recognition from Wikipedia", Artificial Intelligence 2013), Spanish, French, Italian and Dutch, Creative Commons Attribution 4.0 (the text is Wikipedia, Creative Commons Attribution-ShareAlike).
- **Turkish-WikiNER** (Altinok 2023), Creative Commons Attribution-ShareAlike 4.0.

The ShareAlike sources make the weights derived from them subject to the same terms. No other data, label generators or pretrained weights were used. The official test splits and every other evaluation set were not used for training or for choosing settings.
