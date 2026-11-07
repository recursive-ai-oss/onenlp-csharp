# Published reference results

Numbers for the accurate tier to be judged against, copied from the sources named in each row, nothing re-run. Every number is an F1 (or accuracy where the source says so) × 100.

How to read the rows:

- **Metric.** UD columns are the CoNLL 2018 shared-task definitions, produced by the official `conll18_ud_eval.py`. *Tokens* and *Words* score the system's tokenization against the gold; *Sents* is sentence-boundary F1; UPOS, XPOS, UFeats, AllTags, Lemmas, UAS and LAS are F1 over aligned words, so a tokenization error costs the later columns too.
- **Raw** means the system received only the text (predicted tokenization and sentence splitting). **Gold** means gold tokens and sentences were given, which removes the first columns' errors from the rest. Only compare like with like.
- **Release.** UD treebanks change between releases (EWT's annotations are corrected at most releases), so numbers from different releases differ slightly even for the same system. Our own test set is EWT r2.18.
- Retrieved 2026-11-04.

## UD English-EWT, test split

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.17 | 89.16 | 98.92 | 96.20 | 95.75 | 96.39 | 94.64 | 96.63 | 88.90 | 86.77 |
| UDPipe 2, `english-ewt-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 88.42 | 99.07 | 96.75 | 96.27 | 97.21 | 95.51 | 97.13 | 91.59 | 89.91 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 97.61 | 97.13 | 98.13 | 96.34 | 97.96 | 94.09 | 92.36 |
| Trankit large, XLM-R large ([3]) | 2.5 | raw | 98.67 | 90.49 | 98.67 | 96.65 | 96.47 | 96.75 | 95.49 | 97.25 | 91.29 | 89.40 |
| Trankit base, XLM-R base ([3]) | 2.5 | raw | 98.48 | 88.35 | 98.48 | 95.95 | 95.71 | 96.26 | 94.62 | 96.84 | 90.14 | 87.96 |
| Stanza 1.1.1 ([3], same table) | 2.5 | raw | 99.01 | 81.13 | 99.01 | 95.40 | 95.12 | 96.11 | 93.90 | 97.21 | 86.22 | 83.59 |
| UDPipe 1.2 ([3], same table) | 2.5 | raw | 98.90 | 77.40 | 98.90 | 93.26 | 92.75 | 94.23 | n/a | 95.45 | 80.22 | 77.03 |
| spaCy 2.3 pretrained ([3], same table) | 2.5 | raw | 97.30 | 61.19 | 97.30 | 86.72 | 90.83 | n/a | n/a | 87.05 | n/a | n/a |

Notes:

- UDPipe 2 reports the column "Words" (not Tokens) and does not report Tokens for raw text on the page; its gold-tokenization row has no Tokens/Sents/Words by definition. Its UD 2.17 models are distributed under CC BY-NC-SA ([2]); we cite the published numbers only and do not use or redistribute the models.
- The Trankit page's "spaCy" row is spaCy 2.3's public English model, trained on a different corpus; it is information, not a target.
- Stanza's page states its scores are end-to-end from raw text on the official test sets with the CoNLL 2018 script, and are for models with charlm and without transformers.
- Trankit's page lists the Stanza and UDPipe numbers as obtained by running their public pretrained models on UD 2.5.
- Gates for the accurate tier use the row whose input mode and release match what we measure; where our run differs (release, mode), the comparison is information and says so.

## UNER v1, English-EWT, test split

| System (source) | Setting | PER | ORG | LOC | Overall (micro F1) |
|---|---|---|---|---|---|
| XLM-R large fine-tuned on en_ewt ([4], Table 6) | gold tokens, entity-level micro F1 | .950 | .709 | .871 | .858 |

Notes: [4] reports one fine-tuning configuration (XLM-R large, hyperparameters in its §5.1) and one number per tag; no confidence interval is published. Types are PER, ORG and LOC.

## The nine 0.5 languages: UD test sets

The same reading rules as above. The treebank per language is the one chosen in `tools/fetch/<lang>/MANIFEST.md` (r2.18). Retrieved 2026-11-05. Columns are Tokens, Sents, Words, UPOS, XPOS, UFeats, AllTags, Lemmas, UAS, LAS (CLAS, MLAS and BLEX are on the sources' pages). **Version mismatch applies to every row**: Stanza's numbers are on UD 2.12, UDPipe 2's on 2.17, Trankit's on 2.5, ours on 2.18; treebanks are corrected between releases, and a row from another release is information, not a gate. Where a source's table has no row for our treebank, the section says so.

### Chinese: UD Chinese-GSDSimp, test (r2.18 is our release)

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 92.14 | 99.10 | 92.14 | 88.82 | 88.61 | 91.47 | 87.95 | 92.07 | 73.41 | 70.65 |
| UDPipe 2, `chinese-gsdsimp-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 98.80 | 90.13 | 86.67 | 86.83 | 89.52 | 86.16 | 90.09 | 71.74 | 69.52 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 95.73 | 95.97 | 99.28 | 95.20 | 99.90 | 86.76 | 83.60 |
| Trankit large, XLM-R large ([3]) | 2.5 | raw | 97.66 | 98.20 | 97.66 | 94.99 | 94.78 | 97.24 | 94.32 | 97.66 | 87.01 | 84.63 |
| Trankit base, XLM-R base ([3]) | 2.5 | raw | 96.94 | 99.70 | 96.94 | 94.17 | 93.98 | 96.51 | 93.52 | 96.94 | 84.64 | 81.96 |

- Stanza names this model `zh-hans`. Raw-text rows depend on word segmentation (Tokens 92 for Stanza, 90 for UDPipe 2 on Words), so the gold-tokenization UDPipe row is the cleaner target for the heads. Trankit's Chinese-GSDSimp figures use an older annotation of the same treebank (r2.5).

### French: UD French-GSD, test

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.71 | 95.03 | 99.50 | 97.63 | 99.50 | 97.74 | 97.05 | 98.20 | 93.27 | 91.28 |
| UDPipe 2, `french-gsd-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 93.82 | 98.88 | 97.43 | — | 97.15 | 96.70 | 97.73 | 93.26 | 91.21 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 98.55 | — | 98.30 | 97.83 | 98.85 | 94.99 | 93.11 |
| Trankit large, XLM-R large ([3]) | 2.5 | raw | 99.77 | 98.80 | 99.72 | 97.89 | 99.72 | 97.34 | 96.66 | 97.90 | 94.97 | 93.37 |
| Trankit base, XLM-R base ([3]) | 2.5 | raw | 99.70 | 96.63 | 99.66 | 97.85 | 99.66 | 97.16 | 96.60 | 97.80 | 94.00 | 92.34 |

- French-GSD carries no XPOS: UDPipe 2 reports a dash, and the XPOS scores of Stanza and Trankit equal their Words scores, as they do when every XPOS is `_`.

### Spanish: UD Spanish-AnCora, test

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.97 | 98.49 | 99.96 | 98.96 | 96.10 | 98.54 | 95.65 | 99.24 | 93.09 | 91.30 |
| UDPipe 2, `spanish-ancora-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 98.69 | 99.95 | 99.05 | 96.08 | 98.74 | 95.61 | 99.37 | 93.81 | 92.07 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 99.09 | 96.12 | 98.79 | 95.64 | 99.41 | 94.01 | 92.26 |
| Trankit large, XLM-R large ([3]) | 2.5 | raw | 99.91 | 99.39 | 99.91 | 99.06 | 99.00 | 98.85 | 98.36 | 99.15 | 94.77 | 93.29 |
| Trankit base, XLM-R base ([3]) | 2.5 | raw | 99.94 | 99.13 | 99.93 | 99.02 | 98.94 | 98.80 | 98.27 | 99.17 | 94.11 | 92.41 |


### Portuguese: UD Portuguese-Bosque, test

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.85 | 93.10 | 99.76 | 97.45 | 99.76 | 96.44 | 95.33 | 98.04 | 90.59 | 87.75 |
| UDPipe 2, `portuguese-bosque-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 89.73 | 99.68 | 97.77 | — | 96.89 | 95.82 | 98.33 | 92.16 | 89.84 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 98.09 | — | 97.15 | 96.07 | 98.63 | 93.31 | 90.93 |
| Trankit large, XLM-R large ([3]) | 2.5 | raw | 99.75 | 97.18 | 99.67 | 97.52 | 99.67 | 96.50 | 95.17 | 97.97 | 93.31 | 90.91 |
| Trankit base, XLM-R base ([3]) | 2.5 | raw | 99.70 | 97.48 | 99.65 | 97.27 | 99.65 | 96.50 | 94.95 | 97.89 | 92.76 | 90.25 |

- Bosque carries no XPOS; same reading as for French-GSD.

### German: UD German-GSD, test

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.62 | 83.84 | 99.60 | 95.61 | 97.36 | 89.78 | 87.19 | 97.23 | 85.80 | 81.80 |
| UDPipe 2, `german-gsd-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 83.32 | 99.79 | 96.87 | 97.71 | 91.17 | 88.82 | 97.19 | 87.44 | 84.36 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 97.11 | 97.93 | 91.53 | 89.21 | 97.37 | 89.24 | 86.16 |
| Trankit large, XLM-R large ([3]) | 2.5 | raw | 99.71 | 89.72 | 99.72 | 95.23 | 97.68 | 91.68 | 87.21 | 96.58 | 89.01 | 85.20 |
| Trankit base, XLM-R base ([3]) | 2.5 | raw | 99.75 | 92.72 | 99.75 | 95.04 | 97.57 | 91.51 | 86.86 | 96.60 | 88.73 | 84.77 |


### Italian: UD Italian-ParlaMint and MarkIT, test (the standard Italian treebanks are NC)

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1 ParlaMint, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.59 | 95.83 | 99.01 | 96.83 | 95.96 | 95.28 | 93.74 | 95.64 | 85.19 | 81.45 |
| UDPipe 2, `italian-parlamint-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 94.12 | 99.42 | 98.58 | 97.99 | 97.95 | 97.09 | 98.69 | 91.79 | 89.72 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 99.19 | 98.55 | 98.49 | 97.61 | 99.20 | 93.24 | 91.13 |
| Stanza 1.5.1 MarkIT, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 100.00 | 99.12 | 99.67 | 96.29 | 96.34 | 94.28 | 92.34 | 87.08 | 85.58 | 80.63 |
| UDPipe 2 MarkIT, `italian-markit-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 98.24 | 99.62 | 97.02 | 97.03 | 94.02 | 92.37 | 88.31 | 88.68 | 84.79 |
| UDPipe 2 MarkIT, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 97.36 | 97.36 | 94.28 | 92.60 | 88.62 | 89.41 | 85.54 |

- The rows are labelled by treebank where the table mixes two. Each system was trained on that treebank's own training split of its release; ParlaMint and MarkIT are young treebanks whose splits may differ between releases 2.12, 2.17 and 2.18, which is not checked sentence by sentence. Our fast tier trains on 897 sentences (`tools/fetch/it/MANIFEST.md`).
- For information only (ISDT is CC BY-NC-SA and not usable by us): Stanza 1.5.1 on Italian-ISDT, raw, UD 2.12: 99.93 99.07 99.79 98.41 98.32 98.14 97.69 98.23 93.29 91.61 (Tokens, Sents, Words, UPOS, XPOS, UFeats, AllTags, Lemmas, UAS, LAS); UDPipe 2.17 Italian-ISDT raw: UPOS 98.49, Lemma 98.67, LAS 93.10.

### Turkish: UD Turkish-BOUN, test (IMST is NC)

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.58 | 90.08 | 96.32 | 89.92 | 85.74 | 81.66 | 73.03 | 90.37 | 71.53 | 65.17 |
| UDPipe 2, `turkish-boun-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 86.25 | 96.57 | 90.01 | 86.01 | 81.08 | 71.65 | 90.59 | 73.16 | 67.27 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 93.14 | 89.02 | 83.03 | 73.41 | 93.62 | 80.54 | 74.00 |

- Trankit (UD 2.5) predates BOUN; its Turkish model is IMST (NC), information only: Trankit large raw UPOS 96.20, Lemmas 96.21, UAS 78.94, LAS 73.14 (IMST, not our test set).
- BOUN's "Words" differ from "Tokens" (96.32 vs 99.58 in Stanza's run) because the treebank splits agglutinated forms into syntactic words in multiword tokens.

### Hindi: no allowed training treebank; Hindi-PUD is eval-only

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

- Our own Hindi model is trained on self-labelled web text (labelled with the help of related-language heads) and scored on Hindi-PUD (`results-table.md`, "Hindi"); the black-box reference for it is spaCy 3.8.18 trained on the same labels. Every published system is trained and scored on Hindi-HDTB, which is CC BY-NC-SA 4.0 and not usable by us. No published number is on Hindi-PUD, so there is no reference row for the test set we can fetch. For information only, on HDTB: Stanza 1.5.1 raw, UD 2.12: 100.00 99.52 100.00 97.95 97.53 94.41 92.74 96.88 95.26 92.46 (Tokens, Sents, Words, UPOS, XPOS, UFeats, AllTags, Lemmas, UAS, LAS); UDPipe 2.17 raw: UPOS 97.61, Lemma 98.86, LAS 92.30; Trankit large raw, UD 2.5: UPOS 98.01, Lemmas 96.54, UAS 95.95, LAS 92.96.

### Dutch: UD Dutch-Alpino, test

| System (source) | UD release | Input | Tokens | Sents | Words | UPOS | XPOS | UFeats | AllTags | Lemmas | UAS | LAS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stanza 1.5.1, pretrained, charlm, no transformer ([1]) | 2.12 | raw | 99.87 | 90.08 | 99.87 | 96.75 | 95.50 | 97.00 | 94.97 | 95.33 | 91.42 | 88.67 |
| UDPipe 2, `dutch-alpino-ud-2.17-251125`, mBERT embeddings ([2]) | 2.17 | raw | n/a | 91.16 | 99.78 | 97.97 | 96.89 | 97.52 | 96.56 | 95.99 | 94.12 | 92.25 |
| UDPipe 2, same model ([2]) | 2.17 | gold | n/a | n/a | n/a | 98.17 | 97.12 | 97.75 | 96.73 | 96.20 | 95.30 | 93.37 |
| Trankit large, XLM-R large ([3]) | 2.5 | raw | 99.43 | 90.65 | 99.43 | 96.67 | 95.01 | 96.55 | 94.75 | 96.39 | 94.41 | 92.49 |
| Trankit base, XLM-R base ([3]) | 2.5 | raw | 99.22 | 89.88 | 99.22 | 96.55 | 94.92 | 96.22 | 94.56 | 96.23 | 93.28 | 91.28 |

- Alpino was reworked between UD 2.5 and 2.17 (lemmas and relations), which accounts for part of the gap between Trankit's and the later systems' rows.

## The nine 0.5 languages: named entities, gold tokens, entity-level micro F1

Only rows on our evaluation corpus, or on the corpus the nearest published row used, are listed; every row says which.

| Language | Our corpus (test) | System (source) | F1 | Comparable? |
|---|---|---|---|---|
| zh | UNER Chinese-GSDSimp | XLM-R large fine-tuned on the same corpus ([4], Table 6) | .894 (LOC .906, ORG .802, PER .925) | same corpus, same scheme (UNER v1 commit differs from the r2.18 tokens in 5 sentences) |
| zh | UNER Chinese-PUD | XLM-R large fine-tuned on English EWT, zero-shot ([4], Table 6) | .410 | information: cross-lingual, not a target |
| pt | UNER Portuguese-Bosque | XLM-R large fine-tuned on the same corpus ([4], Table 6) | .904 (LOC .882, ORG .861, PER .966) | same corpus, same scheme |
| pt | UNER Portuguese-PUD | XLM-R large fine-tuned on English EWT, zero-shot ([4], Table 6) | .848 | information |
| de | GermEval 2014 | Stanza NER model, Stanza 1.x ([6]) | 85.2 | same corpus; the page lists four classes, as our baseline uses; it does not state the split |
| de | GermEval 2014 | Trankit large / base ([7]) | 89.4 / 86.9 | same corpus; the page does not state the split or the classes |
| de | UNER German-PUD | XLM-R large fine-tuned on English EWT, zero-shot ([4], Table 6) | .814 | information |
| fr | WikiNER French | Stanza ([6]) / Trankit large / base ([7]) | 92.9 / 92.9 / 92.3 | same corpus, **different split**: ours cuts by article at fixed positions, theirs is their own; information, not a gate |
| nl | WikiNER Dutch | Stanza ([6]) / Trankit large / base ([7]) | 94.8 / 95.0 / 94.8 | same corpus, different split, as for French |
| es | WikiNER Spanish | none published on this corpus | | Stanza reports CoNLL-2002 88.1 and AnCora 88.6, Trankit CoNLL-2002 89.2 / 88.9: other corpora, not comparable |
| it | WikiNER Italian | none published on this corpus | | Stanza reports FBK 87.92: another corpus |
| tr | Turkish-WikiNER | none read | | Stanza reports Starlang 81.65: another corpus |
| hi | Naamapadam Hindi | none read | | Stanza reports IL-NER 84.16: another corpus |

So the reference for the NER rows of es, it, tr and hi is the black-box spaCy 3.8.18 baseline on the same data (`tools/baselines/spacy/lang/PROVENANCE.md`), as for English.

## Sources

1. Stanza: Qi, Zhang, Zhang, Bolton, Manning (2020), *Stanza: A Python Natural Language Processing Toolkit for Many Human Languages*, ACL System Demonstrations (arXiv 2003.07082). Performance page, "System Performance on UD Treebanks", Stanza 1.5.1 on UD 2.12: <https://stanfordnlp.github.io/stanza/performance.html>.
2. UDPipe 2: Straka (2018), *UDPipe 2.0 Prototype at CoNLL 2018 UD Shared Task*, CoNLL 2018; Straka, Straková, Hajič (2019), *Evaluating Contextualized Embeddings on 54 Languages in POS Tagging, Lemmatization and Dependency Parsing* (arXiv 1904.02099). Published model documentation with per-treebank results, "Universal Dependencies 2.17 Models", version 251125: <https://ufal.mff.cuni.cz/udpipe/2/models>.
3. Trankit: Nguyen, Lai, Veyseh, Nguyen (2021), *Trankit: A Light-Weight Transformer-based Toolkit for Multilingual Natural Language Processing*, EACL System Demonstrations (arXiv 2101.03289). Results page, "Universal Dependencies v2.5": <https://trankit.readthedocs.io/en/latest/performance.html>.
4. Mayhew, Blevins, Liu, Šuppa, Gonen, Imperial, Karlsson, Lin, Ljubešić, Miranda, Plank, Riab, Pinter (2024), *Universal NER: A Gold-Standard Multilingual Named Entity Recognition Benchmark*, NAACL 2024, pp. 4322–4337, <https://aclanthology.org/2024.naacl-long.243/> (Table 6 in the appendix).
5. CoNLL 2018 shared-task evaluation: <https://universaldependencies.org/conll18/evaluation.html>.
6. Stanza NER models page, F1 per corpus: <https://stanfordnlp.github.io/stanza/ner_models.html> (retrieved 2026-11-05).
7. Trankit results page, "Named Entity Recognition" (Trankit large, Trankit base, Stanza v1.1.1; micro F1 on the test sets of 11 datasets) and the 90-treebank table (UD 2.5; columns Tokens, Sents, Words, UPOS, XPOS, UFeats, AllTags, Lemmas, UAS, LAS, CLAS, MLAS, BLEX): <https://trankit.readthedocs.io/en/latest/performance.html> (retrieved 2026-11-05).
8. Nothman, Ringland, Radford, Murphy, Curran (2013), *Learning multilingual named entity recognition from Wikipedia*, Artificial Intelligence 194; data: <https://figshare.com/articles/dataset/5462500> (CC BY 4.0 per the record).
9. Benikova, Biemann, Reznicek (2014), *NoSta-D Named Entity Annotation for German: Guidelines and Dataset*, LREC 2014; GermEval 2014 NER shared task.
10. Mhaske, Kedia, Doddapaneni, Khapra, Kumar, Murthy, Kunchukuttan (2023), *Naamapadam: A Large-Scale Named Entity Annotated Data for Indic Languages*, ACL 2023 (arXiv 2212.10168).
11. Turkish-WikiNER (turkish-nlp-suite): data card <https://huggingface.co/datasets/turkish-nlp-suite/turkish-wikiNER> (CC BY-SA 4.0 per the card); no paper was read.

<!-- BEGIN generated: all-languages (tools/fetch/ud_docs.py) -->

## Every gating treebank: Stanza, UDPipe 2 and Trankit

`docs/references.tsv` holds every number as published, one row per system, treebank and input mode, for all treebanks the three sources list (655 rows). Columns: `system`, `system_version`, `model`, `treebank` (repository name), `ud_release`, `input` (`raw` or `gold` tokenization), `tokens`, `sents`, `words`, `upos`, `xpos`, `ufeats`, `alltags`, `lemmas`, `uas`, `las`, `clas`, `mlas`, `blex` (F1 × 100, empty when the source does not report it), `source`, then the comparison with r2.18: `in_r2_18`, `test_sents_ref`/`test_tokens_ref` (the test split at the source's release), `test_sents_r2_18`/`test_tokens_r2_18`, and `test_changed`.

Sources (read 2026-11-06, copied, nothing re-run):

- **Stanza 1.5.1**, pretrained models with charlm and no transformer, UD 2.12, end to end from raw text, CoNLL 2018 script: `stanfordnlp.github.io/stanza/performance.html` (137 treebanks).
- **UDPipe 2**, models `*-ud-2.17-251125` on UD 2.17 (mBERT embeddings), raw text and gold tokenization: `ufal.mff.cuni.cz/udpipe/2/models`. The models are CC BY-NC-SA; only the published numbers are used. The page reports Words, not Tokens.
- **Trankit** (large = XLM-R large, base = XLM-R base), UD 2.5, raw text: `trankit.readthedocs.io/en/latest/performance.html` (EACL 2021, Nguyen et al.). The page repeats runs of Stanza 1.1.1, UDPipe 1.2 and spaCy 2.3 on UD 2.5; those are not used.

**Version caveat.** `test_changed` compares the number of sentences and tokens in the test split at the source's release tag with r2.18 (`stats.xml` of each repository at that tag): `same size` means the test set has the same size (annotations may still have been corrected: UD fixes labels in most releases, so scores move by a few tenths even then), `changed` means the test set itself differs in size and the comparison is information, not a gate. Rows whose treebank has no allowed data are marked `not checked`. Compare like with like: Stanza and Trankit are raw-text end-to-end; UDPipe 2 also gives gold-tokenization rows.

The table lists UPOS / UFeats / Lemmas / UAS / LAS for each gating treebank (the treebank per language is in `docs/ud-licences.md`). Stanza: UD 2.12, raw. UDPipe 2: UD 2.17, raw and gold. Trankit large: UD 2.5, raw. `=` means the test split has the same size in r2.18 as in the source's release, `≠` that it differs, `-` that the source does not list the treebank.

| Language | Treebank | Stanza 2.12 | UDPipe 2 raw (2.17) | UDPipe 2 gold (2.17) | Trankit large (2.5) |
|---|---|---|---|---|---|
| Afrikaans | Afrikaans-AfriBooms | 98.60 / 98.41 / 98.29 / 89.54 / 86.14 = | 98.64 / 98.23 / 98.29 / 90.57 / 87.56 = | 98.73 / 98.32 / 98.36 / 90.76 / 87.76 = | 98.87 / 98.68 / 97.47 / 91.61 / 89.35 ≠ |
| Albanian | Albanian-STAF | - | 87.90 / 70.16 / 80.65 / 77.96 / 65.86 = | 88.71 / 70.16 / 81.99 / 77.15 / 64.52 = | - |
| Ancient Greek | Ancient_Greek-PTNK | - | 98.57 / 91.75 / 94.84 / 88.44 / 84.30 = | 98.55 / 91.80 / 94.85 / 92.12 / 87.85 = | - |
| Armenian | Armenian-ArmTDP | 83.05 / 66.22 / 91.35 / 74.04 / 61.80 ≠ | 97.67 / 94.17 / 96.76 / 89.91 / 86.17 = | 98.06 / 94.57 / 97.13 / 90.79 / 87.01 = | 95.03 / 90.42 / 93.45 / 86.47 / 83.01 ≠ |
| Assamese | Assamese-AiW | - | - | - | - |
| Bororo | Bororo-BDT | - | 91.68 / 89.76 / 91.56 / 78.80 / 72.57 = | 91.73 / 89.82 / 91.56 / 79.11 / 72.85 = | - |
| Buryat | Buryat-BDT | 75.16 / 74.51 / 77.78 / 56.21 / 40.52 ≠ | - | - | - |
| Catalan | Catalan-AnCora | 98.70 / 98.27 / 99.06 / 93.55 / 91.66 ≠ | 99.07 / 98.67 / 99.43 / 94.84 / 93.32 = | 99.15 / 98.75 / 99.49 / 94.98 / 93.46 = | 99.11 / 98.66 / 98.49 / 95.15 / 93.83 ≠ |
| Central Kurdish | Central_Kurdish-Mukri | - | - | - | - |
| Chinese | Chinese-GSDSimp | 88.82 / 91.47 / 92.07 / 73.41 / 70.65 ≠ | 86.67 / 89.52 / 90.09 / 71.74 / 69.52 = | 95.73 / 99.28 / 99.90 / 86.76 / 83.60 = | 94.99 / 97.24 / 97.66 / 87.01 / 84.63 ≠ |
| Classical Chinese | Classical_Chinese-Kyoto | 89.16 / 91.38 / 97.63 / 71.32 / 65.80 = | 89.58 / 91.65 / 97.58 / 71.49 / 66.32 = | 93.21 / 94.56 / 99.53 / 84.45 / 79.13 = | 92.89 / 94.43 / 99.67 / 82.03 / 76.82 ≠ |
| Coptic | Coptic-Scriptorium | 85.77 / 86.37 / 85.95 / 66.37 / 64.43 ≠ | 73.46 / 72.83 / 74.21 / 52.08 / 50.03 ≠ | 97.42 / 97.94 / 97.67 / 91.07 / 88.27 ≠ | - |
| Croatian | Croatian-SET | 98.21 / 95.27 / 96.60 / 90.29 / 86.95 = | 98.46 / 96.36 / 97.64 / 92.36 / 89.46 = | 98.53 / 96.44 / 97.71 / 92.88 / 89.97 = | 98.58 / 96.85 / 96.71 / 93.86 / 89.74 = |
| Czech | Czech-CAC | 98.52 / 93.64 / 97.46 / 91.09 / 88.71 = | 99.58 / 97.85 / 99.27 / 96.25 / 94.95 = | 99.59 / 97.85 / 99.28 / 96.25 / 94.95 = | 99.64 / 98.01 / 98.30 / 95.48 / 93.97 = |
| Danish | Danish-DDT | 98.30 / 97.87 / 97.88 / 88.61 / 86.61 = | 97.99 / 97.44 / 97.52 / 88.59 / 86.60 = | 98.20 / 97.63 / 97.65 / 89.84 / 87.87 = | 98.71 / 98.33 / 97.35 / 91.75 / 90.33 = |
| Dutch | Dutch-Alpino | 96.75 / 97.00 / 95.33 / 91.42 / 88.67 = | 97.97 / 97.52 / 95.99 / 94.12 / 92.25 = | 98.17 / 97.75 / 96.20 / 95.30 / 93.37 = | 96.67 / 96.55 / 96.39 / 94.41 / 92.49 = |
| Egyptian | Egyptian-PC | - | - | - | - |
| English | English-EWT | 96.20 / 96.39 / 96.63 / 88.90 / 86.77 ≠ | 96.75 / 97.21 / 97.13 / 91.59 / 89.91 = | 97.61 / 98.13 / 97.96 / 94.09 / 92.36 = | 96.65 / 96.75 / 97.25 / 91.29 / 89.40 ≠ |
| Erzya | Erzya-JR | 84.84 / 75.88 / 84.72 / 70.08 / 57.35 ≠ | 88.50 / 79.36 / 84.27 / 73.33 / 63.92 ≠ | 89.25 / 79.93 / 84.87 / 74.25 / 64.70 ≠ | - |
| Faroese | Faroese-FarPaHC | 96.51 / 92.92 / 99.64 / 84.39 / 80.07 = | 97.48 / 94.37 / 99.74 / 86.48 / 82.71 = | 97.65 / 94.59 / 100.00 / 87.41 / 83.58 = | - |
| Finnish | Finnish-FTB | 96.88 / 97.12 / 97.10 / 90.99 / 88.63 = | 96.73 / 96.69 / 95.66 / 90.21 / 87.31 = | 97.05 / 96.85 / 95.81 / 92.38 / 89.42 = | 98.32 / 98.09 / 96.94 / 95.84 / 94.53 = |
| French | French-GSD | 97.63 / 97.74 / 98.20 / 93.27 / 91.28 ≠ | 97.43 / 97.15 / 97.73 / 93.26 / 91.21 = | 98.55 / 98.30 / 98.85 / 94.99 / 93.11 = | 97.89 / 97.34 / 97.90 / 94.97 / 93.37 ≠ |
| Galician | Galician-TreeGal | 93.89 / 93.11 / 94.52 / 77.27 / 71.86 = | 96.10 / 95.22 / 97.31 / 83.47 / 79.23 = | 97.35 / 96.30 / 98.56 / 86.85 / 82.33 = | 97.62 / 96.50 / 91.08 / 87.17 / 83.90 = |
| Georgian | Georgian-GLC | - | 96.37 / 91.80 / 94.16 / 83.58 / 79.76 = | 97.19 / 92.52 / 94.86 / 85.33 / 81.35 = | - |
| German | German-GSD | 95.61 / 89.78 / 97.23 / 85.80 / 81.80 ≠ | 96.87 / 91.17 / 97.19 / 87.44 / 84.36 = | 97.11 / 91.53 / 97.37 / 89.24 / 86.16 = | 95.23 / 91.68 / 96.58 / 89.01 / 85.20 ≠ |
| Greek | Greek-GUD | 96.52 / 92.50 / 92.63 / 89.81 / 85.33 ≠ | 98.08 / 94.36 / 95.93 / 93.61 / 90.37 ≠ | 98.13 / 94.41 / 95.97 / 94.05 / 90.79 ≠ | - |
| Haitian Creole | Haitian_Creole-Adolphe | - | 95.53 / 97.98 / 99.02 / 64.82 / 60.63 = | 95.55 / 98.02 / 99.05 / 64.83 / 60.63 = | - |
| Hebrew | Hebrew-IAHLTwiki | 91.69 / 86.88 / 92.51 / 83.23 / 81.19 ≠ | 86.16 / 81.75 / 87.24 / 75.73 / 74.01 = | 97.12 / 92.55 / 98.30 / 93.63 / 91.23 = | - |
| Icelandic | Icelandic-IcePaHC | 96.36 / 90.42 / 95.78 / 86.79 / 82.85 ≠ | 96.92 / 92.10 / 96.33 / 87.27 / 83.41 = | 97.08 / 92.24 / 96.47 / 87.73 / 83.82 = | - |
| Indonesian | Indonesian-GSD | 94.16 / 95.58 / 98.28 / 87.05 / 80.83 = | 94.35 / 95.55 / 98.14 / 87.65 / 81.91 = | 94.79 / 95.99 / 98.59 / 88.53 / 82.72 = | 93.39 / 96.11 / 99.53 / 86.33 / 79.81 ≠ |
| Irish | Irish-IDT | 96.01 / 89.41 / 95.32 / 86.81 / 81.51 = | 96.35 / 91.32 / 95.77 / 87.31 / 81.93 = | 96.46 / 91.43 / 95.88 / 87.61 / 82.20 = | 94.72 / 80.90 / 92.64 / 83.47 / 76.86 ≠ |
| Italian | Italian-ParlaMint | 96.83 / 95.28 / 95.64 / 85.19 / 81.45 = | 98.58 / 97.95 / 98.69 / 91.79 / 89.72 = | 99.19 / 98.49 / 99.20 / 93.24 / 91.13 = | - |
| Japanese | Japanese-GSD | 96.38 / 97.36 / 96.02 / 90.01 / 89.29 = | 95.07 / 96.16 / 95.10 / 88.04 / 87.28 = | 98.61 / 99.99 / 98.53 / 94.94 / 94.03 = | 93.66 / 95.23 / 94.68 / 86.67 / 85.56 ≠ |
| Kazakh | Kazakh-KTB | 85.58 / 78.70 / 89.21 / 60.55 / 51.38 = | - | - | 77.94 / 63.01 / 50.60 / 47.46 / 37.98 ≠ |
| Komi Permyak | Komi_Permyak-UH | - | - | - | - |
| Komi Zyrian | Komi_Zyrian-Lattice | - | - | - | - |
| Korean | Korean-Kaist | 95.74 / 100.00 / 94.10 / 87.63 / 85.50 = | 96.21 / - / 94.39 / 88.95 / 86.97 = | 96.21 / - / 94.39 / 88.95 / 86.97 = | 95.13 / 98.70 / 92.36 / 90.00 / 88.22 = |
| Kyrgyz | Kyrgyz-KTMU | 87.65 / 60.22 / 88.09 / 74.95 / 58.77 ≠ | 90.96 / 77.29 / 88.71 / 84.01 / 73.36 = | 91.66 / 77.98 / 89.39 / 84.78 / 74.02 = | - |
| Latin | Latin-LLCT | 99.57 / 96.79 / 98.09 / 96.13 / 94.88 ≠ | 99.77 / 97.12 / 97.80 / 95.03 / 94.06 = | 99.78 / 97.12 / 97.80 / 95.05 / 94.07 = | - |
| Latvian | Latvian-LVTB | 96.70 / 94.73 / 96.12 / 88.91 / 85.77 ≠ | 97.43 / 95.28 / 96.81 / 89.35 / 86.48 = | 98.09 / 95.97 / 97.48 / 90.37 / 87.47 = | 97.61 / 95.18 / 95.83 / 93.63 / 91.25 ≠ |
| Ligurian | Ligurian-GLT | 89.14 / 88.25 / 92.68 / 77.61 / 69.18 = | - | - | - |
| Lithuanian | Lithuanian-ALKSNIS | 93.71 / 88.16 / 92.95 / 77.82 / 72.75 = | 95.96 / 91.02 / 93.40 / 82.45 / 78.63 = | 96.09 / 91.19 / 93.47 / 83.76 / 79.92 = | 97.45 / 94.46 / 94.30 / 90.48 / 87.67 = |
| Livvi | Livvi-KKPP | - | - | - | - |
| Low Saxon | Low_Saxon-LSDC | - | 90.25 / 71.74 / 83.84 / 73.91 / 64.88 = | 90.80 / 72.23 / 84.33 / 75.13 / 65.93 = | - |
| Maltese | Maltese-MUDT | 95.72 / 99.85 / 99.85 / 83.62 / 78.22 = | 95.78 / 99.84 / - / 84.86 / 80.04 = | 95.88 / 100.00 / - / 85.36 / 80.47 = | - |
| Marathi | Marathi-UFAL | 85.78 / 73.53 / 82.84 / 68.87 / 59.56 = | 83.94 / 74.45 / 84.67 / 67.15 / 59.85 = | 88.59 / 76.94 / 87.86 / 73.06 / 64.81 = | 87.79 / 70.62 / 81.50 / 72.79 / 63.36 = |
| Moksha | Moksha-JR | - | - | - | - |
| Naija | Naija-NSC | 97.55 / 98.84 / 99.14 / 92.39 / 89.56 = | 98.00 / 98.90 / 99.28 / 92.62 / 89.97 = | 98.02 / 98.93 / 99.31 / 92.68 / 90.03 = | - |
| North Sami | North_Sami-Giella | 90.73 / 88.08 / 88.54 / 72.26 / 66.63 = | 91.83 / 89.47 / 86.94 / 76.20 / 71.26 = | 91.94 / 89.62 / 87.05 / 76.44 / 71.52 = | - |
| Northern Kurdish | Northern_Kurdish-Kurmanji | - | - | - | 75.07 / 57.15 / 57.63 / 37.12 / 29.89 = |
| Norwegian | Norwegian-Bokmaal | 98.38 / 97.09 / 98.61 / 93.26 / 91.85 = | 98.38 / 97.38 / 98.67 / 94.05 / 92.84 = | 98.57 / 97.59 / 98.87 / 94.69 / 93.47 = | 98.85 / 98.07 / 98.40 / 95.54 / 94.33 = |
| Old East Slavic | Old_East_Slavic-RNC | 91.04 / 72.39 / 81.04 / 68.10 / 60.81 ≠ | 97.77 / 90.13 / 90.60 / 80.58 / 77.10 = | 98.02 / 90.38 / 90.81 / 82.12 / 78.58 = | - |
| Old Occitan | Old_Occitan-CorAG | - | 95.25 / 95.37 / - / 77.79 / 74.80 ≠ | 96.83 / 97.02 / - / 83.90 / 80.85 ≠ | - |
| Ottoman Turkish | Ottoman_Turkish-BOUN | - | 85.77 / 80.51 / 81.99 / 63.60 / 52.37 = | 86.18 / 80.96 / 82.36 / 64.41 / 53.06 = | - |
| Persian | Persian-Seraji | 97.69 / 97.70 / 98.18 / 90.98 / 87.96 = | 97.97 / 97.95 / 98.57 / 92.00 / 89.17 = | 98.30 / 98.27 / 98.85 / 92.62 / 89.76 = | 97.78 / 97.70 / 97.35 / 92.24 / 89.58 = |
| Polish | Polish-MPDT | - | 94.59 / 89.44 / 94.70 / 78.97 / 75.06 = | 96.29 / 91.02 / 96.35 / 82.66 / 78.54 = | - |
| Portuguese | Portuguese-Bosque | 97.45 / 96.44 / 98.04 / 90.59 / 87.75 = | 97.77 / 96.89 / 98.33 / 92.16 / 89.84 = | 98.09 / 97.15 / 98.63 / 93.31 / 90.93 = | 97.52 / 96.50 / 97.97 / 93.31 / 90.91 ≠ |
| Punjabi | Punjabi-Rang | - | - | - | - |
| Romanian | Romanian-RRT | 97.50 / 97.10 / 97.42 / 90.70 / 86.86 ≠ | 97.86 / 97.38 / 98.10 / 92.01 / 88.84 ≠ | 98.14 / 97.64 / 98.36 / 92.77 / 89.53 ≠ | 97.90 / 97.43 / 97.98 / 93.60 / 89.50 ≠ |
| Russian | Russian-GSD | 97.63 / 93.63 / 95.51 / 89.56 / 85.89 = | 98.06 / 93.77 / 97.20 / 92.04 / 89.11 = | 98.54 / 94.18 / 97.58 / 93.25 / 90.19 = | 98.25 / 95.78 / 95.75 / 92.70 / 89.75 = |
| Sanskrit | Sanskrit-Vedic | 89.19 / 80.70 / 88.92 / 62.52 / 50.64 ≠ | 93.56 / 89.24 / 93.45 / 65.47 / 56.91 = | 93.83 / 90.36 / 93.50 / 77.99 / 68.85 = | - |
| Scottish Gaelic | Scottish_Gaelic-ARCOSG | 94.62 / 91.91 / 95.81 / 82.73 / 77.81 ≠ | 93.87 / 90.71 / 95.19 / 81.04 / 76.86 = | 96.65 / 93.63 / 97.70 / 87.26 / 83.15 = | 94.70 / 90.71 / 95.48 / 81.60 / 76.46 ≠ |
| Serbian | Serbian-SET | 98.44 / 94.31 / 96.30 / 91.44 / 88.36 = | 99.06 / 96.16 / 97.96 / 93.40 / 91.07 = | 99.06 / 96.19 / 97.95 / 94.12 / 91.77 = | 99.06 / 96.40 / 96.90 / 95.57 / 93.13 = |
| Sindhi | Sindhi-Isra | - | 93.27 / 71.01 / 94.79 / 83.73 / 74.44 = | 93.41 / 71.23 / 94.88 / 85.51 / 76.42 = | - |
| Skolt Sami | Skolt_Sami-Giellagas | - | - | - | - |
| Slovak | Slovak-SNK | 96.34 / 90.95 / 94.08 / 89.13 / 86.58 = | 97.65 / 93.14 / 96.59 / 91.41 / 89.66 = | 97.83 / 93.31 / 96.66 / 93.75 / 91.91 = | 97.90 / 95.22 / 94.79 / 96.97 / 95.60 ≠ |
| Slovenian | Slovenian-SSJ | 98.41 / 96.09 / 97.69 / 92.45 / 90.80 = | 98.85 / 97.10 / 98.69 / 94.47 / 92.92 = | 98.91 / 97.16 / 98.73 / 94.65 / 93.10 = | 99.24 / 97.99 / 97.55 / 96.91 / 96.06 ≠ |
| Southern Kurdish | Southern_Kurdish-Garrusi | - | 77.26 / 72.92 / 67.15 / 68.59 / 62.09 = | 83.82 / 78.68 / 75.00 / 76.47 / 68.38 = | - |
| Spanish | Spanish-AnCora | 98.96 / 98.54 / 99.24 / 93.09 / 91.30 ≠ | 99.05 / 98.74 / 99.37 / 93.81 / 92.07 = | 99.09 / 98.79 / 99.41 / 94.01 / 92.26 = | 99.06 / 98.85 / 99.15 / 94.77 / 93.29 ≠ |
| Swedish | Swedish-Talbanken | 98.28 / 97.29 / 97.92 / 90.32 / 87.85 = | 98.67 / 97.24 / 98.53 / 92.89 / 90.52 = | 98.79 / 97.36 / 98.66 / 93.39 / 91.02 = | 99.06 / 98.04 / 97.87 / 94.41 / 92.97 = |
| Telugu | Telugu-MTG | 93.97 / 99.10 / 99.79 / 90.09 / 81.64 = | 93.77 / 98.48 / - / 90.44 / 84.35 = | 94.17 / 98.89 / - / 91.26 / 85.16 = | 94.31 / 98.33 / 98.89 / 93.19 / 87.08 = |
| Telugu English | Telugu_English-TECT | - | - | - | - |
| Thai | Thai-TUD | - | 77.29 / 87.14 / - / 58.97 / 50.87 = | 89.39 / 100.00 / - / 81.27 / 71.00 = | - |
| Turkish | Turkish-BOUN | 89.92 / 81.66 / 90.37 / 71.53 / 65.17 = | 90.01 / 81.08 / 90.59 / 73.16 / 67.27 = | 93.14 / 83.03 / 93.62 / 80.54 / 74.00 = | - |
| Ukrainian | Ukrainian-ParlaMint | - | 98.56 / 95.01 / 99.07 / 93.95 / 91.91 = | 98.60 / 95.02 / 99.12 / 93.96 / 91.92 = | - |
| Uyghur | Uyghur-UDT | 89.26 / 87.58 / 95.30 / 74.80 / 63.42 = | 89.87 / 88.03 / 94.64 / 75.68 / 64.94 = | 90.26 / 88.51 / 95.15 / 77.54 / 66.62 = | 88.50 / 86.56 / 94.45 / 80.19 / 70.14 = |
| Uzbek | Uzbek-UzUDT | - | 86.15 / 70.68 / 29.27 / 71.16 / 55.78 ≠ | 86.24 / 70.49 / 29.22 / 73.63 / 57.97 ≠ | - |
| Vietnamese | Vietnamese-VTB | 81.48 / 88.02 / 81.66 / 58.82 / 51.49 = | 78.40 / - / 85.77 / 56.34 / 49.57 = | 89.92 / - / 99.47 / 75.84 / 65.51 = | 89.70 / 94.64 / 94.88 / 71.07 / 65.37 ≠ |
| Welsh | Welsh-CCG | 95.86 / 90.11 / 94.85 / 86.32 / 80.25 = | 95.50 / 89.93 / 94.89 / 87.52 / 81.76 = | 95.89 / 90.30 / 95.32 / 88.45 / 82.61 = | - |
| Western Armenian | Western_Armenian-ArmTDP | 96.42 / 92.70 / 96.70 / 87.73 / 82.83 ≠ | 96.81 / 92.37 / 96.89 / 89.35 / 84.85 = | 96.90 / 92.47 / 97.01 / 89.65 / 85.14 = | - |
| Wolof | Wolof-WTB | 94.36 / 93.19 / 94.63 / 83.28 / 77.32 = | 94.32 / 93.47 / 95.18 / 83.49 / 78.02 = | 95.29 / 94.33 / 95.94 / 85.75 / 80.16 = | - |
| Yiddish | Yiddish-YiTB | - | 94.50 / 99.61 / 93.53 / 89.38 / 85.56 = | 94.85 / 100.00 / 93.87 / 90.34 / 86.48 = | - |
| Zaar | Zaar-Autogramm | - | 84.94 / 85.78 / 82.47 / 70.99 / 62.81 ≠ | 90.67 / 91.37 / 87.42 / 83.04 / 73.34 ≠ | - |
| Zazaki | Zazaki-ZSD | - | - | - | - |

73 of the 84 gating treebanks have published numbers from at least one source. No source lists: Assamese-AiW, Central_Kurdish-Mukri, Egyptian-PC, Komi_Permyak-UH, Komi_Zyrian-Lattice, Livvi-KKPP, Moksha-JR, Punjabi-Rang, Skolt_Sami-Giellagas, Telugu_English-TECT, Zazaki-ZSD. For these the accurate tier reports its own numbers with the bootstrap interval and no reference row (small treebanks, mostly under 100 training sentences, which the three systems do not cover).

Of the rows on gating treebanks, 75 are on a test split whose size differs from r2.18 (marked ≠); most are Trankit's UD 2.5 and Stanza's UD 2.12 rows on treebanks that grew or were resplit since.

<!-- END generated: all-languages -->
