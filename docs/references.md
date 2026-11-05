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

- Every published system is trained and scored on Hindi-HDTB, which is CC BY-NC-SA 4.0 and not usable by us. No published number is on Hindi-PUD, so there is no reference row for the test set we can fetch. For information only, on HDTB: Stanza 1.5.1 raw, UD 2.12: 100.00 99.52 100.00 97.95 97.53 94.41 92.74 96.88 95.26 92.46 (Tokens, Sents, Words, UPOS, XPOS, UFeats, AllTags, Lemmas, UAS, LAS); UDPipe 2.17 raw: UPOS 97.61, Lemma 98.86, LAS 92.30; Trankit large raw, UD 2.5: UPOS 98.01, Lemmas 96.54, UAS 95.95, LAS 92.96.

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
