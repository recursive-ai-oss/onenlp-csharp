# Provenance: Hindi fast tier (`recipes/hi/fast`)

## Models

| File | Size | sha256 | Contents |
|---|---:|---|---|
| `tagging.onenlp` | 35,844,344 | `d638169ee7098a874e1243b10e1e16b4e076c209d378ae4a45061bbc89d00163` | sentencizer, morphologizer (UPOS, UFeats) |
| `parser.onenlp` | 47,032,181 | `c2f45ec9565e7ce68c358d55091f3678b59d502ed6eef4db2fa37c462a3abd76` | biaffine parser, sentence starts |
| `ner.onenlp` | 35,677,809 | `90e572a291a7f541ecf536ec24ba036c65b9c8bc33c8cda4335eba6ac0c9217d` | entity recognizer (PER, ORG, LOC), tagging decoder |

All three are trained with the public `Train` API on Hindi text labelled by the accurate tier and by heads trained on related languages; none was trained on any human Hindi annotation. **Share-alike lineage: yes**, through the labeller (below). The first round of this model (labels from nine unrelated languages only) had the hashes `8f73bba9...`, `1f405d76...` and `8ece57eb...`; it is replaced.

## Data

| Dataset | Version, source | Licence | Role |
|---|---|---|---|
| FineWeb-2, `hin_Deva` | `HuggingFaceFW/fineweb-2` revision af9c13333eb981300149d5ca60a8e9d659b276b9; the Hindi distillation sample (12,449 documents, 24,000,435 characters; filtered by language score, length and against the evaluation keys of the distillation work, which dropped 1,780 documents) | ODC-By 1.0 and Common Crawl terms of use | train and dev: documents 0 to 3,299 of the sample labelled; 89 of them dropped by the adult-content filter, then 59,985 sentences (1,094,693 tokens) kept by shape, 186 repeats removed: 59,799; a document whose index is divisible by 20 is dev |
| UD Marathi-UFAL, Marathi-CMUPAN, Sindhi-Isra, Punjabi-Rang, Punjabi-CS | r2.18, `tools/fetch/ud/MANIFEST.md` (sha256 per file) | CC BY-SA 4.0 | train and dev of the related-language label heads (Marathi: 373 + 7,616 sentences; Sindhi: up to 2,000; Punjabi: 81 + 73); their test splits unused |
| UD Hindi-PUD r2.18 | `hi_pud-ud-test.conllu`, sha256 `3562b7f49420912acad70a45a483f683dcb44624ea92f82e21b29f2ee41a9dff` | CC BY-SA 3.0 | eval only; read once for the final model; no training sentence shares an 8-word run with it (0 hits) |
| Hand-annotated sentences | `testdata/hi/selfdev.conllu` (100 sentences: 17 held-out web sentences and 83 written by us), `testdata/hi/selfdev.lemmas.tsv` (124 lemmas), `testdata/hi/nerspot.iob2` (91 sentences): our own labels | web text ODC-By 1.0; the rest ours | dev only |

Training reads only the ensemble's train and dev files. No UD Hindi-HDTB, Naamapadam, Samanantar or other non-commercial Hindi data is in the lineage of the labeller or of the student: the labeller's heads were trained on the treebanks and entity corpora of other languages only.

### Adult-content filter

`AdultFilter.cs` (README.md, "Adult-content filter"). On the Hindi sample it flags 352 of 12,449 documents (2.83%). Applied to the first round's labelled sentences it would have removed 3,061 of 61,270 (5.0%). In this round: 89 of 3,300 documents (2.7%) and 125 further sentences were dropped before labelling. The word list is in the source; it is a rule and not a classifier, so some explicit text may remain and some harmless text is lost.

## Label generator

The base-size accurate tier (`RecursiveAI.OneNlp.Models.Multilingual.Large`): mmBERT-base (MIT; Marone et al. 2025) fine-tuned on UD r2.18 (English-EWT, German-GSD, French-GSD, Spanish-AnCora, Portuguese-Bosque, Italian-ParlaMint and MarkIT, Dutch-Alpino, Turkish-BOUN, Chinese-GSDSimp: CC BY-SA 4.0 and CC BY 4.0) and UNER, GermEval 2014, WikiNER and Turkish-WikiNER (CC BY 4.0 and CC BY-SA 4.0), with a set of heads per language. The weights used are the shipped ones (their encoder pieces concatenate to sha256 `4cb0c3d25988b67e...`; heads `de` `526253083ee913ec`, `en` `da50a621c0b263b1`, `es` `d280cc5531e3cc72`, `fr` `af2b0fa5351cc5dc`, `it` `b3d27ed4390835ec`, `nl` `4d1fcb2db1ec32ca`, `pt` `d58000bc782e0cd8`, `tr` `ab3402132c324626`, `zh` `3aa9e974b2debcc7`; the full values are in that package's `weights.manifest.props`). Its lineage is in that package's `PROVENANCE.md`. The first round used the earlier fine-tuned weights of the same recipe.

On top of the frozen encoder, we trained head sets (morphologizer and biaffine parser, `train-related.sh`, AdamW 0.002 on a half cosine over 12 epochs, dropout 0.2, 1,500 words per batch, 4 sentences per document, seed 1, encoder output cached, `Train` with `FreezeEncoder`):

| Head set | Training data | Size | sha256 (prefix) | Development (in-language) UPOS / UFeats / LAS |
|---|---|---:|---|---|
| `mr` | Marathi-UFAL + 2,000 CMUPAN sentences | 18,747,237 | `ffef65599cc3bd86` | 90.6 / 65.2 / 71.6 |
| `mrx` | Marathi-UFAL + all CMUPAN | 19,395,140 | `4e041df215316145` | 91.1 / 67.5 / 74.9 |
| `sd` | Sindhi-Isra, 2,000 sentences | 16,379,358 | `1778e87a8cdb4c0d` | 93.7 / 79.9 / 68.6 |
| `indo` | Marathi (UFAL, 3,000 CMUPAN), Sindhi (1,200), Punjabi (Rang, CS) | 22,375,291 | `43edc56ca9c3ffad` | 90.4 / 67.3 / 67.0 |
| `sa` (tried, not used) | Sanskrit-Vedic, 2,000 sentences | 23,794,898 | `d91ef0506c1c712c` | 65.6 / 54.9 / 12.8 |
| `mrlem` (lemma probe only) | Marathi, all, edit-tree lemmatizer | 9,927,761 | `6e209f69d7c18637` | |

Weights licence MIT; no hosted-LLM labels. Share-alike lineage: yes. The labeller is not redistributed by this package; only the student is.

## Recipe

- Labelling: 300-document chunks, 13 source head sets, one encoder pass per batch of 32 sentences, 8 threads, about 4 minutes per chunk (encoder served from its ONNX graph); 11 chunks.
- Ensemble: `--use de,fr,nl,tr,zh,mr,mrx,sd,indo --ner-use en,de,fr,es,pt,it,nl,tr,zh --pos-agree 0.6 --uas-agree 0.5` (README.md). Kept for syntax: 34,013 of 59,799 sentences (32,276 train, 1,737 dev; 520,362 tokens; mean plurality share 0.787, mean medoid agreement 0.617). Kept for entities: 53,098 (50,380 train, 2,718 dev) with 14,770 PER, 1,843 ORG and 8,284 LOC; 6,701 sentences had a contested span and were left out. The thresholds are the first round's.
- Tagging: one window layer and a bidirectional LSTM of 64 (`--plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64`), learning rate 0.003 on a half cosine over 12 epochs, 1,000 words per batch, dropout 0.1, seed 1. Best development step 2,495 of 5,988 (development agreement with the labeller 85.9).
- Parser: default fast encoder, biaffine head with a two-layer LSTM of 192, 10 epochs, 1,500 words per batch, learning rate 0.002, dropout 0.25, seed 7. Best step 2,443 of 3,488 (development LAS against the labeller 67.3).
- Entities: default fast encoder, tagging decoder, 8 epochs, learning rate 0.003, dropout 0.4, seed 7. Best step 4,466 of 7,145 (development F1 against the labeller 79.36).
- Machine: x64 host (AMD Ryzen 9 7950X3D); 4 threads for the student's training and 8 for the related heads and the labelling, resumable chunks of at most 25 minutes. Wall time of the student: tagging 18 min, parser 30 min (two runs), entities 20 min.
- PyTorch timing: no new head design (the related heads are the accurate tier's own), so not taken.
- CPU ledger (`cpu-hours.tsv`, reserved cores times wall time, an upper bound): the first round about 21 CPU-hours. This round: related heads and their scoring about 3.5, labelling 5.9, student 4.5, development and final evaluation 0.9, lemma probe 0.7: 15.5, plus about 0.6 on one core of the arm64 machine for the spaCy baseline.

## Black-box baseline

spaCy 3.8.18 (thinc 8.3.14) `spacy train` on the same labelled sentences (syntax: 32,276 train, 1,737 dev; entities: 50,380 and 2,718). Configs written by `spacy init config --lang hi --pipeline morphologizer,parser --optimize efficiency` (sha256 `a3124bb44d726fac590e2c94c67084fe1d419d91aa3feecf2822e48fa027e0bc`) and `--pipeline ner` (`c914dca7257cd7f0cb0a2039e07a5d25c103f67b71ddc6204f4c1f643b88459e`). One core on an arm64 Mac mini: 27.5 minutes for syntax, 7.0 minutes for entities.

## Results

In `docs/results-table.md`, section "Hindi".
