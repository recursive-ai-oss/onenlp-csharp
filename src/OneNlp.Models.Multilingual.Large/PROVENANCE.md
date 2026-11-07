# Provenance: RecursiveAI.OneNlp.Models.Multilingual.Large

One mmBERT-base encoder fine-tuned on nine languages at once, with a set of heads for each language. The recipe is `recipes/accurate/multilingual` (a console project over the public `Train` API); run it to reproduce the model.

## Base model

`jhu-clsp/mmBERT-base`, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82`, MIT licence (Marone et al. 2025). Exported once to ONNX (`tools/accurate/export_onnx.py`, sha256 `6a9f485066d95de7eec7fb29c0e3839a9c4ddbefd6f87d46ece33b2cb52a550b`) and loaded by `TrainableTransformerEncoder`. The 256,000 x 768 token table stays frozen: the four `token-table-NN.bin` files are the export's own rows, split by `tools/accurate/split_token_table.py` (whole-table sha256 `2204983bb7c6d94a8fe361c7236fb63c9ab19fc0a70eaf21934d220d0242f590`). The tokenizer is the model's `tokenizer.json`.

## Training

- Seed 1; 600 steps of about 3,000 words, learning rate 1e-4 (linear warm-up, then decay), AdamW, language-balanced sampling (a smaller language repeats up to twice), dropout as the recipe sets; evaluation every 250 steps on a development sample (60 documents per treebank, 300 per entity corpus); the weights of the best development score (step 500, mean 86.788) are exported.
- Heads per language: tagger (only where the treebank has XPOS: not French or Portuguese), morphologizer, edit-tree lemmatizer, biaffine parser, and an entity recognizer where entity data exists. Each head trains on its own language's sentences only (`ScopedHead`).
- The encoder reads the words of multiword tokens (`ExpandWordForms`): French `du` is read as `de le`.
- Machine: a 32-core x64 host, 8 threads, in resumable chunks of at most 30 minutes. The first run of 1,000 steps was restarted from the start after a checkpoint shape error; the runs before it are experiments and do not feed the weights.
- Weights sha256 (training state at the end of the run): `1964ecf5a7e4a262188424ef3a77b13b05c5f2d125bcde5dcdddbe58ef238d46`. Packaged files and their hashes are in the `weights.manifest.props` files.

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

Evaluation files (UD test splits, entity test splits) are role `eval` in the manifests and are read only by the `--predict-split test` step after training; entity test sets are capped at 3,000 sentences sampled evenly. Label generator: none (gold labels only). **Lineage: CC BY-SA (Universal Dependencies, UNER, WikiNER text from Wikipedia). Credit is in THIRD-PARTY-NOTICES.** Hindi has no allowed training data and is not in this package.
