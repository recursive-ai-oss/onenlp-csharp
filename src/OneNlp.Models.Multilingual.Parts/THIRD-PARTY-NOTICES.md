# Third-party notices: RecursiveAI.OneNlp.Models.Multilingual parts

The code and the weights of these packages are licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. They depend on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## The distilled encoder (the weights are derived from these)

The encoder is a student distilled from a teacher model on plain text; it was trained with no labels. The weights are a trained model, not a copy of the data or of the teacher.

- **mmBERT-base** (`jhu-clsp/mmBERT-base`, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82`), Marone et al., 2025, MIT licence, <https://huggingface.co/jhu-clsp/mmBERT-base>. The student's starting token table is the teacher's embedding matrix reduced to 96 columns, and the `tokenizer.json` shipped here is the teacher's, unchanged. Notice: Copyright the mmBERT authors; permission is granted under the MIT licence, whose text is at the link above.
- **FineWeb-2** (`HuggingFaceFW/fineweb-2`) and **FineWeb** (`HuggingFaceFW/fineweb`, English), Hugging Face, ODC-By 1.0 (<https://opendatacommons.org/licenses/by/1-0/>), whose text comes from Common Crawl and is subject to Common Crawl's terms of use (<https://commoncrawl.org/terms-of-use>). Credit: FineWeb-2 and Common Crawl.
- **Wikipedia** (`wikimedia/wikipedia`, 20231101), Wikimedia Foundation and contributors, licensed Creative Commons Attribution-ShareAlike 3.0 (<https://creativecommons.org/licenses/by-sa/3.0/>) and the GNU Free Documentation License. The weights derived from it are released under the licence above with this credit.
