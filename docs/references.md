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

## Sources

1. Stanza: Qi, Zhang, Zhang, Bolton, Manning (2020), *Stanza: A Python Natural Language Processing Toolkit for Many Human Languages*, ACL System Demonstrations (arXiv 2003.07082). Performance page, "System Performance on UD Treebanks", Stanza 1.5.1 on UD 2.12: <https://stanfordnlp.github.io/stanza/performance.html>.
2. UDPipe 2: Straka (2018), *UDPipe 2.0 Prototype at CoNLL 2018 UD Shared Task*, CoNLL 2018; Straka, Straková, Hajič (2019), *Evaluating Contextualized Embeddings on 54 Languages in POS Tagging, Lemmatization and Dependency Parsing* (arXiv 1904.02099). Published model documentation with per-treebank results, "Universal Dependencies 2.17 Models", version 251125: <https://ufal.mff.cuni.cz/udpipe/2/models>.
3. Trankit: Nguyen, Lai, Veyseh, Nguyen (2021), *Trankit: A Light-Weight Transformer-based Toolkit for Multilingual Natural Language Processing*, EACL System Demonstrations (arXiv 2101.03289). Results page, "Universal Dependencies v2.5": <https://trankit.readthedocs.io/en/latest/performance.html>.
4. Mayhew, Blevins, Liu, Šuppa, Gonen, Imperial, Karlsson, Lin, Ljubešić, Miranda, Plank, Riab, Pinter (2024), *Universal NER: A Gold-Standard Multilingual Named Entity Recognition Benchmark*, NAACL 2024, pp. 4322–4337, <https://aclanthology.org/2024.naacl-long.243/> (Table 6 in the appendix).
5. CoNLL 2018 shared-task evaluation: <https://universaldependencies.org/conll18/evaluation.html>.
