# Provenance: Hindi fast tier (`recipes/hi/fast`)

## Models

| File | Size | sha256 | Contents |
|---|---:|---|---|
| `tagging.onenlp` | 35,774,549 | `8f73bba91b4d4b6b4c17c875f55d6e6db543b08f0ba2b7016b763c78632b8877` | sentencizer, morphologizer (UPOS, UFeats) |
| `parser.onenlp` | 48,363,702 | `1f405d76f9e49a902eaed4571606cd0766d2f095b2429393f13a85143a3962e8` | biaffine parser, sentence starts |
| `ner.onenlp` | 35,677,809 | `8ece57eac0ecb3640a599c72d0b6c78abe7216b1d1549cdb1dc8b4f243217efc` | entity recognizer (PER, ORG, LOC), tagging decoder |

All three are trained with the public `Train` API on Hindi text labelled by the accurate tier; none was trained on any human Hindi annotation. **Share-alike lineage: yes**, through the labeller (below).

## Data

| Dataset | Version, source | Licence | Role |
|---|---|---|---|
| FineWeb-2, `hin_Deva` | `HuggingFaceFW/fineweb-2` revision af9c13333eb981300149d5ca60a8e9d659b276b9; the Hindi distillation sample (12,449 documents, 24,000,435 characters; filtered by language score, length and against the evaluation keys of the distillation work, which dropped 1,780 documents) | ODC-By 1.0 and Common Crawl terms of use | train and dev: documents 0 to 3,199 of the sample labelled (61,268 sentences, 1,118,047 tokens after dropping 144 repeats); a document whose index is divisible by 20 is dev |
| UD Hindi-PUD r2.18 | `hi_pud-ud-test.conllu`, sha256 `3562b7f49420912acad70a45a483f683dcb44624ea92f82e21b29f2ee41a9dff` | CC BY-SA 3.0 | eval only; read once for the final model; no training sentence shares an 8-word run with it |
| Hand-annotated sentences | `testdata/hi/selfdev.conllu` (17 sentences), `testdata/hi/nerspot.iob2` (91 sentences): our own labels over held-out FineWeb-2 sentences | text ODC-By 1.0, labels ours | dev only |

Training reads only the ensemble's train and dev files. No UD Hindi-HDTB, Naamapadam, Samanantar or other non-commercial Hindi data is in the lineage of the labeller or of the student: the labeller's heads were trained on the treebanks and entity corpora of nine other languages only.

## Label generator

The accurate tier of the first base-size release: mmBERT-base (MIT; Marone et al. 2025) fine-tuned on UD r2.18 (English-EWT, German-GSD, French-GSD, Spanish-AnCora, Portuguese-Bosque, Italian-ParlaMint and MarkIT, Dutch-Alpino, Turkish-BOUN, Chinese-GSDSimp: CC BY-SA 4.0 and CC BY 4.0) and UNER, GermEval 2014, WikiNER and Turkish-WikiNER (CC BY 4.0 and CC BY-SA 4.0), with a set of heads per language. The heads files had sha256 `e96bd7a9...` (en), `8dfc9548...` (de), `4faaafed...` (fr), `5c659c6e...` (es), `f61b0e00...` (pt), `6b9ea8a5...` (it), `8ed30...` (nl), `1caadd9e...` (tr), `35a37a77...` (zh); the full values are in the history of `src/OneNlp.Models.Multilingual.Large/weights.manifest.props`, and the lineage is in that package's `PROVENANCE.md`. Weights licence MIT; no hosted-LLM labels. Share-alike lineage: yes. The labeller is not redistributed by this package.

## Recipe

- Labelling: 400-document chunks, 9 source heads, one encoder pass per batch of 32 sentences, 8 threads, about 12 minutes per chunk; 8 chunks.
- Ensemble: `--pos-agree 0.6 --uas-agree 0.5` (README.md). Kept for syntax: 32,756 of 61,268 sentences (31,036 train, 1,720 dev; 429,089 tokens; mean plurality share 0.760, mean medoid agreement 0.614). Kept for entities: 53,100 (50,245 train, 2,855 dev) with 14,750 PER, 1,622 ORG and 7,712 LOC; 8,168 sentences had a contested span and were left out.
- Tagging: one window layer and a bidirectional LSTM of 64 (`--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64`), learning rate 0.003 on a half cosine over 12 epochs, 1,000 words per batch, dropout 0.1, seed 1. Best development step 2,050 of 4,920 (development agreement with the labeller 83.2).
- Parser: default fast encoder, biaffine head with a two-layer LSTM of 192, 10 epochs, 1,500 words per batch, learning rate 0.002, dropout 0.25, seed 7. Best step 1,993 of 2,848 (development LAS against the labeller 52.9).
- Entities: default fast encoder, tagging decoder, 8 epochs, learning rate 0.003, dropout 0.4, seed 7. Best step 5,290 of 7,054 (development F1 against the labeller 79.07).
- Machine: x64 host (AMD Ryzen 9 7950X3D), 4 threads for training, resumable chunks of at most 25 minutes. Wall time: tagging 13.3 min, parser 28 min (two runs), entities 22.9 min.
- PyTorch timing: no new head design, so not taken.
- CPU ledger: labelling 8 chunks about 12.4 CPU-hours (8 reserved cores each, an upper bound), training about 4.4, evaluation and checks about 4: about 21 CPU-hours.

## Black-box baseline

spaCy 3.8.18 (thinc 8.3.14) `spacy train` on the same labelled sentences (syntax: 31,036 train, 1,720 dev; entities: 50,245 and 2,855). Configs written by `spacy init config --lang hi --pipeline morphologizer,parser --optimize efficiency` (sha256 `a3124bb44d726fac590e2c94c67084fe1d419d91aa3feecf2822e48fa027e0bc`) and `--pipeline ner` (`c914dca7257cd7f0cb0a2039e07a5d25c103f67b71ddc6204f4c1f643b88459e`). One core on an arm64 Mac mini: 23.6 minutes for syntax (16 epochs), 4.8 minutes for entities.

## Results

In `docs/results-table.md`, section "Hindi".
