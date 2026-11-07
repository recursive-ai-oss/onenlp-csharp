# Provenance: RecursiveAI.OneNlp.Models.Hindi

Three trained pipeline files, `tagging.onenlp`, `parser.onenlp` and `ner.onenlp` (`weights.manifest.props` lists their sizes and sha256). They are trained on Hindi web text that the accurate tier labelled; no human Hindi annotation was used. The recipe, data, label generator, seeds, settings, machine and results are in `recipes/hi/fast/PROVENANCE.md` and `recipes/hi/fast/README.md` of the source repository; the essentials:

- **Text:** FineWeb-2 `hin_Deva`, revision af9c13333eb981300149d5ca60a8e9d659b276b9 (ODC-By 1.0, Common Crawl terms of use): 61,268 sentences from 3,200 documents.
- **Labels:** the heads of nine other languages (English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish, Chinese) of the multilingual accurate-tier model, applied to Hindi through its shared mmBERT-base encoder (MIT) and combined by agreement. The labeller's own training data are Universal Dependencies r2.18 treebanks and named-entity corpora of those nine languages (CC BY-SA 4.0, CC BY 4.0), so the weights have share-alike lineage and are credited in `THIRD-PARTY-NOTICES.md`.
- **Evaluation data** (UD Hindi-PUD, CC BY-SA 3.0) was not used for training or for choosing settings.
- **Not in the lineage:** UD Hindi-HDTB and every other non-commercial Hindi resource.
- **Seeds and settings:** tagging seed 1, parser and entities seed 7; see the recipe provenance.
- **Limits:** no lemmas and no XPOS tags; the labels are transferred, not annotated, so the scores on Hindi-PUD (gold tokens: UPOS 70.9, LAS 31.9) are much lower than those of the languages with a treebank.
