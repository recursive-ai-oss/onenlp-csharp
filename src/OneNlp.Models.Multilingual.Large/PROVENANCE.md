# Provenance: RecursiveAI.OneNlp.Models.Multilingual.Large

One mmBERT-base encoder fine-tuned on nine languages at once, with a set of heads for each language. The recipe is `recipes/accurate/multilingual` (a console project over the public `Train` API); run it to reproduce the model.

## Base model

`jhu-clsp/mmBERT-base`, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82`, MIT licence (Marone et al. 2025). Exported once to ONNX (`tools/accurate/export_onnx.py`, sha256 `6a9f485066d95de7eec7fb29c0e3839a9c4ddbefd6f87d46ece33b2cb52a550b`) and loaded by `TrainableTransformerEncoder`. The 256,000 x 768 token table stays frozen (whole-table sha256 `2204983bb7c6d94a8fe361c7236fb63c9ab19fc0a70eaf21934d220d0242f590`), so its rows in the shipped graph are the export's own. The tokenizer is the model's `tokenizer.json`.

## Training

- **First run:** seed 1; 600 steps of about 3,000 words, learning rate 1e-4 (linear warm-up, then decay), AdamW, language-balanced sampling (a smaller language repeats up to twice), dropout as the recipe sets; evaluation every 250 steps on a development sample (60 documents per treebank, 300 per entity corpus); best development score at step 500 (mean 86.788). Training state sha256 at the end of the run: `1964ecf5a7e4a262188424ef3a77b13b05c5f2d125bcde5dcdddbe58ef238d46`.
- **Continued run (the shipped weights):** seed 2; 300 more steps of 3,000 words from the first run's weights and optimizer state; learning rate 6e-5 (10 steps of warm-up, half cosine down to 6e-6); per-language multiplicities of the treebanks (zh 1.8, tr 1.8, it 3.5, nl 0.65, de 0.45, en 0.5, pt 0.5, fr 0.2, es 0.15) and 700 entity documents per language; evaluation every 100 steps on the same development sample (mean 86.887, 87.683, 87.907); the weights of step 300 are shipped. Training state sha256 at the end: `786d97381db49f4a9b246880691ce7d7b5b1e518bdc7efc449c995f35fb4b0bc`. About 24 CPU-hours, a 32-core x64 host, 8 threads, resumable chunks of 26 minutes.
- Lemma tables (`LemmaLexicon`): built from each language's training treebank only (never development or test files), with settings chosen on development sentences by `tools/accurate/lexicon_dev.py`.
- The Chinese pipeline's word segmenter is the one of the Chinese fast tier (`RecursiveAI.OneNlp.Models.Chinese`, UD Chinese-GSDSimp train and dev, CC BY-SA 4.0), unchanged.
- Heads per language: tagger (only where the treebank has XPOS: not French or Portuguese), morphologizer, edit-tree lemmatizer, biaffine parser, and an entity recognizer where entity data exists. Each head trains on its own language's sentences only (`ScopedHead`).
- The encoder reads the words of multiword tokens (`ExpandWordForms`): French `du` is read as `de le`.
- The encoder is served from its ONNX graph: the fine-tuned weights written back into the original export (sha256 of the written graph `4cb0c3d25988b67e8be039b9ceed32297bd322482b58404086858a5457625fab`, 1,228,334,485 bytes), cut into six pieces. Packaged files and their hashes are in the `weights.manifest.props` files.

## Data (all fetched by `tools/fetch/<language>/fetch.sh`; URLs, sizes and SHA-256 of every file in `tools/fetch/<language>/MANIFEST.md`)

| Language | Syntax and morphology (train, dev) | Entities (train, dev) | Licence |
|---|---|---|---|
| en | UD English-EWT r2.18 | UNER English-EWT 8ed072de | CC BY-SA 4.0 |
| de | UD German-GSD r2.18 | GermEval 2014 | CC BY-SA 4.0; CC BY |
| fr | UD French-GSD r2.18 | WikiNER French wp3 | CC BY-SA 4.0; CC BY 4.0 (text from Wikipedia, CC BY-SA) |
| es | UD Spanish-AnCora r2.18 | WikiNER Spanish wp3 | CC BY 4.0 (LICENSE.txt); as above |
| pt | UD Portuguese-Bosque r2.18 | UNER Portuguese-Bosque | CC BY-SA 4.0 |
| it | UD Italian ParlaMint (train) + MarkIT (897 sentences together) | WikiNER Italian wp3 | CC BY-SA 4.0; CC BY 4.0 |
| nl | UD Dutch-Alpino r2.18 | WikiNER Dutch wp3 | CC BY-SA 4.0; as above |
| tr | UD Turkish-BOUN r2.18 | Turkish-WikiNER | CC BY-SA 4.0 |
| zh | UD Chinese-GSDSimp r2.18 | UNER Chinese-GSDSIMP | CC BY-SA 4.0 |

Evaluation files (UD test splits, entity test splits) are role `eval` in the manifests and are read only by the final `predict-package --split test` step (once, for the shipped model); entity test sets are capped at 3,000 sentences sampled evenly. Label generator: none (gold labels only). **Lineage: CC BY-SA (Universal Dependencies, UNER, WikiNER text from Wikipedia). Credit is in THIRD-PARTY-NOTICES.** Hindi has no allowed training data and is not in this package.
