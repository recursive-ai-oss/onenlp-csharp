# Provenance: self-trained English tagging and entity encoders

| Part | File | Size | sha256 |
|---|---|---|---|
| tagging (tagger, morphologizer, lemmatizer) | `tagging.onenlp` | 51,981,603 | `f52cb26243c8e16c3fbc45924d51cdaef234c6a057ffd1e5cf15c72baa645dae` |
| entities | `ner.onenlp` | 52,112,428 | `3b38c0ec5d4a2def130aed26eb663f431a0130c55c9d958ef8c8a22462cbba55` |

- **Teachers:** the previous packaged English model, `english.onenlp` sha256 `d62f2199b06d26f5fdff0e7a8b4bd150ea017484997665a6458f5c0e0e033255` (tagging part `86009eed5179efd38421e1d67cc29416fd29e0e54ad488820d13259269970cb9`, entity part `fe3e09746b5fd5245d348760a779cbee0f2ec57e0201a5265a7b41a0399e9cce`). Its labels are ours; the model was trained on UD English-EWT r2.18 and UNER English-EWT, so the lineage is share-alike (CC BY-SA 4.0).
- **Gold data:** UD English-EWT r2.18 train and dev, UNER English-EWT commit 8ed072de train and dev (CC BY-SA 4.0), as in `recipes/en/fast-tagging` and `ner`. Test splits are evaluation only.
- **Unlabelled text:** FineWeb sample-10BT (ODC-By 1.0, subject to the Common Crawl terms of use; 9,384 documents) and English Wikipedia 20231101 (CC BY-SA 3.0 and GFDL; 1,181 documents), cut and filtered by the distillation text fetch (`tools/distill/fetch_data.py` in the accurate-tier work): documents with a sentence that matches an evaluation or development sentence were dropped there (582 documents), and the overlap check of step 2 was run again on the labelled sentences. Share-alike lineage: yes.
- **Overlap check** (`results/dedup-report.jsonl`): tagging labels 290,427 sentences, 266,357 long enough to carry a key, 22 removed; parser trees 112,847 / 104,476 / 12 removed; entity labels 290,427 / 266,357 / 22 removed. The models were trained on the cleaned files only. The check key set holds 19.4M keys from the UD, UNER, NER, language-identification and parity text of this repository.
- **Recipe:** tagging `--width 96 --depth 0 --extra-train <cleaned labels> --epochs 8`, 4 threads, early stopping with patience 6, checkpoints every epoch, chunks of 25 minutes (about 100 minutes of wall time in total); entities `--width 128 --depth 0 --ner-context 1 --ner-hidden 128 --extra-docs 120000 --epochs 12`, 1 thread, about 55 minutes. Defaults otherwise as in `recipes/en/joint` (ReLU, 64-wide rows, 16,384-row neighbour and context tables, dropout 0.25, seed 1).
- **Development scores** (UD EWT dev, gold tokens, UPOS / XPOS / UFeats / AllTags / Lemmas): 94.06 / 93.23 / 94.88 / 91.23 / 96.45, against 93.79 / 93.14 / 94.66 / 91.04 / 96.27 for the teacher. UNER dev micro F1 61.37 against 59.15.
- **Test scores** (read once, final configuration): `results/test-rows-gold.md`, `results/test-rows-raw.md`, `results/test-rows-ner.txt`; the reference is spaCy 3.8.18 trained with its default configuration on the same data.
- **Licence of the weights:** `MIT OR Apache-2.0`, with the credits in `THIRD-PARTY-NOTICES.md`.
