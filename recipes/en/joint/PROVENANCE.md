# Provenance: en-joint (English fast tier, one shared encoder)

- **Model:** `english.onenlp`, 23,749,404 bytes, sha256 `6bb03663e81f6d874f83bffde41c65b06e376c028050dcd92f73bfe615dc50b9`. Kept out of git; rebuilt by the recipe.
- **Recipe:** `recipes/en/joint`, seed 1, options `--neighbour-rows 16384 --context-rows 16384 --no-bilinear --width 128 --depth 1 --embed-width 64 --ner-context 2 --lstm 192 --lstm-layers 1` (now the defaults). 40 epochs allowed, early stopping on the development score with patience 8; best development score 77.395 at step 13,203.
- **Produced:** 2026-11-05, Apple M2 (arm64), one training thread, chunks of 25 minutes; .NET SDK 10.0.403, RecursiveAI.OneModel 1.0.0 and OneModel.Training 1.0.0.
- **Data:** UD English-EWT r2.18 train and dev (CC BY-SA 4.0) and UNER English-EWT commit 8ed072de train and dev (CC BY-SA 4.0); file hashes in `tools/fetch/en/MANIFEST.md`. The official test files were read once, for the rows below, after this configuration was chosen on the development sets.
- **Share-alike lineage:** yes. **Label generator:** none. **Pretrained components:** none.
- **Choice of configuration (development sets only):** encoders of width 48 to 192 with 0 to 2 window layers, with and without wider-context and word-pair tables, parsers with and without the LSTM; the joint model with one LSTM layer was kept. A model with no window layer and 48 columns was about twice as fast on the tagging path and 0.6 points lower on the development tagging mean (see `docs/results.md`).
- **Licence of the weights:** MIT OR Apache-2.0 (the sources are credited in the package notices).
- **Training time against PyTorch CPU:** not measured for the joint recipe (the shapes of its heads were measured separately, all under 2x).

## Results

UD English-EWT r2.18 test and UNER English-EWT test; paired bootstrap 95% CI over sentences (1,000 resamples, seed 1) from `tools/OneNlp.Eval`; reference spaCy 3.8.18 trained on the same data with its default configuration (`tools/baselines/spacy/PROVENANCE.md`). The rows are in `docs/results.md`.
