# Provenance: RecursiveAI.OneNlp.Models.Multilingual

One fine-tuned six-layer student encoder shared by nine languages, a set of heads for each language, two small trained tokenizers, and an open-type entity head on the frozen student. `weights.manifest.props` (here and in the parts) lists name, size and sha256 of every file; a pack fails when one differs. Weights are not stored in git. The recipes are `recipes/accurate/student-multilingual` (language heads and the fine-tuned encoder, a console project over the public `Train` API) and `recipes/openner` (the open-type head); run them to reproduce the model.

## Encoders

- **Frozen encoder** (`student-v1.onnx`, `RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder`): sha256 `aee56231d97134e455fe80a79738eb294c7974c61311412256fa9cd854eeeae6`, 141,913,197 bytes. Distilled from `jhu-clsp/mmBERT-base` (MIT) in PyTorch, on a GPU, then exported once; provenance in that package's `PROVENANCE.md`. The open-type head reads it unchanged.
- **Fine-tuned encoder** (`student-multilingual.onnx`, `...Multilingual.Encoder`): the same architecture with all weights fine-tuned by the language-head run below, exported by `TrainableTransformerEncoder`; sha256 `008a34d3ef405cd5d1ec96f8e9274e4142156679c646ca671f3eb9c7f3d487f8`, 141,913,197 bytes. The fine-tuned encoder was chosen over the frozen one for the language heads because a fine-tuned student scored 95.88 against 93.01 for a frozen one on English-EWT UPOS in development (`recipes/accurate/student-v1/RESULTS.md`); the open-type head was trained on the frozen one and ships with it.

## Language heads (`heads/<language>.onenlp`)

- Heads per language: tagger (only where the treebank has XPOS: not French, Portuguese or Turkish), morphologizer (UPOS, UFeats), edit-tree lemmatizer, biaffine parser, and an entity recognizer where entity data exists. Each head trains on its own language's sentences only. The encoder reads the words of multiword tokens (French `du` is read as `de le`).
- Training: seed 1; 2,000 steps of about 3,000 words, learning rate 2e-4 (linear warm-up over 40 steps, then decay), AdamW, language-balanced sampling (a smaller language repeats up to twice), evaluation every 250 steps on a development sample (60 documents per treebank, 300 per entity corpus); the weights of the best development mean score are exported (step 2000, mean 81.843, still rising: the run is short). A learning rate of 1e-4 was run too and stopped at step 1,250 when it trailed (77.85 against 80.40 at the same step); the choice was made on development scores only.
- Machine: a 32-core x64 host, 4 threads, in resumable chunks of at most 30 minutes (about 3.5 s per step). Training state sha256 is in the recipe log; packaged files:

| File | Size | sha256 |
|---|---:|---|
| `heads/en.onenlp` | 20,396,489 | `4171eb102c0ce704cb05cb01ab0992d1d7b4c4523c53578d3fa925378d6ae5ae` |
| `heads/de.onenlp` | 19,697,041 | `7b2e74b4aab1a85ca1c5c5cb67276eaceea975631c5343cde96c8bc11bf03b10` |
| `heads/fr.onenlp` | 22,117,803 | `773691be1a1fbc1b0e547b388a603ce156f87dfcc13805b616b4448015867c4a` |
| `heads/es.onenlp` | 18,291,796 | `ea93e1068942b1743f8f9be41fce1e5b1a436e91b14d81a66288f6ac5a0c9b67` |
| `heads/pt.onenlp` | 17,743,694 | `d3355b19477329212c5040fa423d7d5e7ccc36e81a86980ef1b2345de49eb415` |
| `heads/it.onenlp` | 17,177,866 | `18094fd7c838153209d27364e46f0cf3fe6e5e0e7e6bd8257448ae31999f04f4` |
| `heads/nl.onenlp` | 19,221,813 | `ab81af09f6ad5096ff5ee6c3a65a2d321332be1268dbfd3ec61de23f642d8e53` |
| `heads/tr.onenlp` | 27,154,575 | `d01e84c3be1ec0980deb827d4506916a2e4e734c401844a76cca93e9b63e1421` |
| `heads/zh.onenlp` | 16,690,068 | `ddc4195820735762815f5179782164cdb852532113a027ac423cb2e8a9b3c39e` |

## Data (all fetched by `tools/fetch/<language>/fetch.sh`; URLs, sizes and SHA-256 of every file in `tools/fetch/<language>/MANIFEST.md`)

| Language | Syntax and morphology (train, dev) | Entities (train, dev) | Licence |
|---|---|---|---|
| en | UD English-EWT r2.18 | UNER English-EWT 8ed072de | CC BY-SA 4.0 |
| de | UD German-GSD r2.18 | GermEval 2014 | CC BY-SA 4.0; CC BY 4.0 |
| fr | UD French-GSD r2.18 | WikiNER French wp3 | CC BY-SA 4.0; CC BY 4.0 (text from Wikipedia, CC BY-SA) |
| es | UD Spanish-AnCora r2.18 | WikiNER Spanish wp3 | CC BY 4.0 (LICENSE.txt); as above |
| pt | UD Portuguese-Bosque r2.18 | UNER Portuguese-Bosque | CC BY-SA 4.0 |
| it | UD Italian ParlaMint (train) + MarkIT (897 sentences together) | WikiNER Italian wp3 | CC BY-SA 4.0; CC BY 4.0 |
| nl | UD Dutch-Alpino r2.18 | WikiNER Dutch wp3 | CC BY-SA 4.0; as above |
| tr | UD Turkish-BOUN r2.18 | Turkish-WikiNER | CC BY-SA 4.0 |
| zh | UD Chinese-GSDSimp r2.18 | UNER Chinese-GSDSIMP | CC BY-SA 4.0 |

Evaluation files (UD test splits, entity test splits) have the role `eval` in the manifests and are read only by the prediction step after training; each test split was read once for the final model. Entity test sets are capped at 3,000 sentences sampled evenly, so a language's entity row is not the fast tier's full-test row. Label generator for these heads: none (gold labels only). Hindi has no allowed training data and is not in this package.

## Tokenizers (`zh.onenlp`, `tr.onenlp`, `...Segmenters`)

The Chinese word segmenter (sha256 `6520915c3d6cfa6d238012e52cdae03641154b8d9fc81e31ee8e1ffcd934b3cb`, 43,476,326 bytes) and the Turkish multiword-token splitter (`2a4d8d9a7076c4c5ef0b9135593edcaa3e405c1d4af2dddae36fca31f48e00a2`, 3,453,592 bytes) are the tokenizer-only parts of the fast-tier Chinese and Turkish models, trained in C# on UD Chinese-GSDSimp and UD Turkish-BOUN (their own `PROVENANCE.md` files). Other languages use the UAX #29 tokenizer with per-language data.

## Open-type entity head (`open-entities.onenlp`)

sha256 `25d7cd3f8f3cc67b73aaca5d49db00dc88dc1758ffb34f43658aeb2fefff1d68`, 2,385,609 bytes: model B of `recipes/openner` (original file sha256 `7898e075ace9f641592ca06e9089e4e7c46af67cdbe5f0ff9169d08b7afc08b4`, re-saved to read the encoder from the package; no weight changed). Data: Few-NERD (CC BY-SA 4.0), UNER English-EWT, and 40,000 windows of FineWeb and Wikipedia text labelled by Qwen2.5-7B-Instruct-AWQ (Apache-2.0, open weights, run on the project's own GPU; no hosted model). Details, hand checks of the labels and GPU hours: `recipes/openner/PROVENANCE.md`.

## Lineage

**Share-alike: yes** (Universal Dependencies, UNER, Few-NERD, WikiNER text and Wikipedia text are CC BY-SA). Credits are in `THIRD-PARTY-NOTICES.md`. Weights licence: `MIT OR Apache-2.0`.
