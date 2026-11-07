# Third-party notices: RecursiveAI.OneNlp.Models.Multilingual and its parts

The code and the weights of these packages are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. They depend on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## The distilled encoder (the weights are derived from these)

The encoder is a student distilled from a teacher model on plain text, with no labels. The weights are a trained model, not a copy of the data or of the teacher.

- **mmBERT-base** (`jhu-clsp/mmBERT-base`, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82`), Marone et al., "mmBERT: A Modern Multilingual Encoder with Annealed Language Learning", 2025 (arXiv:2509.06888), MIT licence, <https://huggingface.co/jhu-clsp/mmBERT-base>. The student's starting token table is the teacher's embedding matrix reduced to 96 columns, and the `tokenizer.json` shipped here is the teacher's, unchanged.
- **FineWeb-2** (`HuggingFaceFW/fineweb-2`) and **FineWeb** (`HuggingFaceFW/fineweb`, English), Hugging Face, ODC-By 1.0 (<https://opendatacommons.org/licenses/by/1-0/>); the text comes from Common Crawl and is subject to Common Crawl's terms of use (<https://commoncrawl.org/terms-of-use>). Credit: FineWeb-2 and Common Crawl.
- **Wikipedia** (`wikimedia/wikipedia`, 20231101), Wikimedia Foundation and contributors, Creative Commons Attribution-ShareAlike 3.0 (<https://creativecommons.org/licenses/by-sa/3.0/>) and the GNU Free Documentation License.

## Fine-tuning data for the language heads and the fine-tuned encoder (`RecursiveAI.OneNlp.Models.Multilingual` and `.Encoder`)

- **Universal Dependencies r2.18** (<https://universaldependencies.org>): English-EWT, German-GSD, French-GSD, Portuguese-Bosque, Dutch-Alpino, Turkish-BOUN and Chinese-GSDSimp (Creative Commons Attribution-ShareAlike 4.0), Spanish-AnCora (Creative Commons Attribution 4.0), Italian-ParlaMint and Italian-MarkIT (Creative Commons Attribution-ShareAlike 4.0 and Attribution 4.0 respectively; see the treebank READMEs). Tags, morphology, lemmas and dependency parses.
- **Universal NER v1** (Mayhew et al., NAACL 2024, <https://www.universalner.org>): English-EWT, Portuguese-Bosque and Chinese-GSDSimp, Creative Commons Attribution-ShareAlike 4.0.
- **GermEval 2014 NER** (Benikova, Biemann and Reznicek, LREC 2014), Creative Commons Attribution 4.0.
- **WikiNER** (Nothman et al., "Learning multilingual named entity recognition from Wikipedia", Artificial Intelligence 2013), Spanish, French, Italian and Dutch, Creative Commons Attribution 4.0 (the text is Wikipedia, Creative Commons Attribution-ShareAlike).
- **Turkish-WikiNER** (Altinok 2023), Creative Commons Attribution-ShareAlike 4.0.
- The Chinese word segmenter and the Turkish multiword-token splitter (`.Segmenters`) were trained on the Universal Dependencies treebanks above (Chinese-GSDSimp, Turkish-BOUN).

## The open-type entity recognizer (`open-entities.onenlp`)

Trained on the frozen encoder with:

- **Few-NERD** (Ding et al., ACL 2021), Creative Commons Attribution-ShareAlike 4.0, and Universal NER English-EWT (above);
- passages of **FineWeb** (ODC-By 1.0) and **Wikipedia** (above), labelled with entity types by **Qwen2.5-7B-Instruct-AWQ** (`Qwen/Qwen2.5-7B-Instruct-AWQ`, revision `b25037543e9394b818fdfca67ab2a00ecc7dd641`, Apache License 2.0, <https://huggingface.co/Qwen/Qwen2.5-7B-Instruct-AWQ>). The label generator's weights are not part of this package, and no hosted model was used.

The ShareAlike sources make the weights derived from them subject to the same terms. No other data, label generators or pretrained weights were used. The official test splits and every other evaluation set were not used for training or for choosing settings.
