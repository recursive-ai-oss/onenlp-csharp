# Licences of the Universal Dependencies treebanks for ZH, FR, ES, PT, DE, IT, TR, HI and NL

Every UD treebank of these nine languages in release 2.18 (15 May 2026; each repository at tag `r2.18`), with the licence as the treebank states it and the verdict for training and evaluating OneNLP models. Read on 2026-11-05 from each treebank's `README` (the `License:` metadata line and its licence section) and `LICENSE.txt`.

Rule applied: CC BY-SA, CC BY and CC0 data may train models, with credit; non-commercial (NC), research-only, paid and platform-restricted text may not. A treebank inside a CC BY-SA release that is itself NC is out.

- **go**: train and evaluate, with credit. Models trained on CC BY-SA data carry "SA lineage" in their provenance.
- **eval-only**: test-only treebanks (they ship no training split) and treebanks kept for evaluation.
- **hold**: unresolved terms or restricted source text; not used until the terms are resolved.
- **no-go**: non-commercial or otherwise outside the rule; never fetched.
- **chosen**: the treebank that trains (and gates) the language's fast-tier model.

The release-wide notice in each README that applies to the Google-originated treebanks (GSD for German, Spanish, French and Portuguese) and to the Parallel UD (PUD) treebanks reads: the underlying sentences are ones "Google asserts no ownership over and no copyright over", provided "AS IS"; the annotations are CC BY-SA 4.0 (GSD) or CC BY-SA 3.0 (PUD).

## Chinese

| Treebank | Licence line (README) | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| GSDSimp | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **go, chosen** (Simplified script) |
| GSD | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go; the Traditional-script twin of GSDSimp (same sentences): never train on one and test on the other. Not fetched |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched as a second test set; Traditional script) |
| CFL | CC BY-SA 4.0 | CC BY-SA 4.0 | test | eval-only (learner essays); not fetched |
| HK | CC BY-SA 4.0 | CC BY-SA 4.0 | test | eval-only (subtitles, legislative proceedings, Traditional script); not fetched |
| Beginner | CC BY-NC-SA 3.0 | CC BY-NC-SA 3.0 | test | **no-go** (NC) |
| PatentChar | CC BY-NC-SA 3.0 | CC BY-SA 4.0 (contradicts the README) | test | **no-go** until the maintainers resolve the contradiction (the stricter line is taken) |

Classical Chinese (`lzh`) is a different language and is not covered.

## French

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| GSD | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **go, chosen** |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched) |
| Rhapsodie | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (spoken French, small); not used |
| ParisStories | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (oral French); not used |
| Sequoia | LGPL-LR | none | train, dev, test | **go**: LGPL-LR is allowed with credit and treated like CC BY-SA (SA lineage). Not used for the baselines (not retrained)  |
| FQB | LGPL-LR | none | test | **go**, same ruling (test-only: eval)  |
| ParTUT | CC BY-NC-SA 4.0 | CC BY-NC-SA 4.0 | train, dev, test | **no-go** (NC) |
| FTB | none in release 2.18 | none | not in the release | not available (last tagged r2.11) |

## Spanish

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| AnCora | CC BY 4.0 | CC BY 4.0 | train, dev, test | **go, chosen**. The README still carries an older sentence, "The GNU license is inherited from the original dataset", while its changelog says "The license changed to CC BY 4.0" in release 2.8 and `LICENSE.txt` and the metadata agree on CC BY 4.0. Recorded as a stale sentence, low risk |
| GSD | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go; not used |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched) |
| COSER | CC BY-SA 4.0 | CC BY-SA 4.0 | test | eval-only (spoken rural Spanish); not fetched |
| SVarT, Spanish-English Miami | not in release 2.18 | none | none | not available |

Spanish Sign Language (`LSE`) is not Spanish text and is not covered.

## Portuguese

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| Bosque | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **go, chosen** (Universal NER annotates the same sentences) |
| GSD | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (the changelog: Google dropped the "NC" restriction from the annotations); not used |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched) |
| PetroGold | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (oil and gas academic texts); not used |
| Porttinari | CC BY 4.0 | CC BY 4.0 | train, dev, test | go on the authors' statement (newspaper text); not used |
| DANTEStocks | CC BY 4.0 | CC BY 4.0 | train, dev, test | **no-go** (ruled): tweets, platform terms apply to the text |
| CINTIL | CC BY-NC-ND 4.0 | CC BY-NC-ND 4.0 | train, dev, test | **no-go** (NC, ND) |
| DHBB | not in release 2.18 | none | none | not available |

## German

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| GSD | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **go, chosen** |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched) |
| HDT | CC BY-SA 4.0 (annotation) | "The annotation of the treebank is licensed under the Creative Commons License Attribution-ShareAlike 4.0" | train (in parts), dev, test | **no-go** (ruled): the README's licence section says "Heise gave permission to distribute the text for academic use", so the text is not cleared for commercial use; not used until resolved |
| LIT | CC BY-NC-SA 4.0 | CC BY-NC-SA 4.0 | test | **no-go** (NC) |

## Italian

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| ISDT | CC BY-NC-SA 3.0 | "released by the authors under the Creative Commons" NC-SA 3.0 | train, dev, test | **no-go** (NC). The standard Italian treebank is not usable |
| VIT | CC BY-NC-SA 3.0 | NC-SA 3.0 | train, dev, test | **no-go** (NC) |
| ParTUT | CC BY-NC-SA 4.0 | NC-SA 4.0 | train, dev, test | **no-go** (NC) |
| PoSTWITA | CC BY-NC-SA 4.0 | NC-SA 4.0 | train, dev, test | **no-go** (NC; also tweets) |
| KIParlaForest | CC BY-NC-SA 4.0 | NC-SA 4.0 | test | **no-go** (NC) |
| ParlaMint | CC BY-SA 4.0 | CC BY-SA 4.0 | train (326 sentences), test (375) | **go, chosen** by default: modern text, but tiny and with no dev split |
| MarkIT | CC BY 4.0 | CC BY 4.0 | train, dev, test | go (611 grammar-example sentences), used with ParlaMint |
| Old | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go; Dante's Comedy (Old Italian), not used: not modern text |
| TWITTIRO | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **no-go** (ruled): ironic tweets, platform terms apply to the text |
| Valico | CC BY-SA 4.0 | CC BY-SA 4.0 | test | eval-only (learner essays); not fetched |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched) |

## Turkish

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| BOUN | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **go, chosen** |
| Kenet | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (dictionary examples); not used |
| Tourism | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (customer reviews); not used |
| FrameNet | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (grammar examples); not used |
| Atis | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go on the maintainers' licence; the sentences translate the English ATIS corpus; not used |
| Penn | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **no-go** (ruled): a Turkish version of the Penn Treebank, whose source sentences are LDC-licensed; not used |
| GB | CC BY-SA 4.0 | CC BY-SA 4.0 | test | eval-only (grammar-book examples); not fetched |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched) |
| IMST | CC BY-NC-SA 4.0 | CC BY-NC-SA 4.0 | train, dev, test | **no-go** (NC). The standard Turkish treebank is not usable |
| ULU | not in release 2.18 | none | none | not available |

Turkish-German `SAGT` is a code-switching treebank under CC BY-NC-SA 4.0 and is out of scope.

## Hindi

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| HDTB | CC BY-NC-SA 4.0 | "licensed under the Creative Commons License Attribution-NonCommercial-ShareAlike 4.0" | train, dev, test | **no-go** (NC). It is Hindi's only sizeable treebank |
| PUD | CC BY-SA 3.0 | CC BY-SA 3.0 | test | eval-only (fetched) |
| Convers | not in release 2.18 | none | none | not available |
| Hindi-English `HIENCS` | not in release 2.18 | none | none | not available (code-mixed) |

**There is no allowed Hindi training treebank.** Hindi-PUD has 1,000 test sentences and no training split, so no tagger, morphologizer, lemmatizer or parser baseline can be trained for Hindi from UD data, and our Hindi models would have to learn from other data. Our Hindi models need data this rule does not allow, or new data of our own.

## Dutch

| Treebank | Licence line | LICENSE.txt | Splits | Verdict |
|---|---|---|---|---|
| Alpino | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | **go, chosen** (news) |
| LassySmall | CC BY-SA 4.0 | CC BY-SA 4.0 | train, dev, test | go (Wikipedia; larger than Alpino); not used |

## Universal NER (v1)

UNER v1 (Mayhew et al., NAACL 2024) is released under CC BY-SA 4.0 as `UniversalNER/UNER_<Language>-<Treebank>` repositories; the PUD-based ones keep PUD's CC BY-SA 3.0. Listing of the organisation's repositories, 2026-11-05:

| Language | UNER datasets | Verdict |
|---|---|---|
| Chinese | GSD, GSDSIMP (train, dev, test), PUD (test) | go; PUD eval-only |
| Portuguese | Bosque (train, dev, test), PUD (test) | go; PUD eval-only |
| German | PUD (test) | eval-only; no training data |
| French, Spanish, Italian, Turkish, Hindi, Dutch | none | gap: other sources below |

Each UNER dataset is a separate corpus from its treebank: it was cut from an older UD commit. Checked against r2.18 sentence by sentence: Portuguese-Bosque has identical tokens in every sentence of every split (7,018 / 1,172 / 1,167), so its entity labels sit on r2.18's tokens; Chinese-GSDSimp has identical texts, with different tokens in 2 train, 1 dev and 2 test sentences. Scheme: PER, ORG, LOC.

## Named-entity corpora for the languages without UNER

Terms read 2026-11-05 from the dataset's own page or card. "Chosen" means fetched by `tools/fetch/<lang>/fetch.sh` and used for the baseline.

| Corpus | Languages | Terms (as stated) | Verdict |
|---|---|---|---|
| WikiNER (Nothman et al. 2013), wp3 files, figshare 5462500 | fr, es, it, nl (also de, en, pt, pl, ru) | figshare record licence: CC BY 4.0. Silver labels projected from Wikipedia links; the underlying text is Wikipedia (CC BY-SA) | **go, chosen** for fr, es, it, nl; SA lineage. No official split: we split by article (see the manifests) |
| GermEval 2014 NER (NoSta-D, Benikova et al. 2014) | de | "The data is released under the permissive CC-BY license" (LREC 2014 abstract); the shared-task page links the CC BY 4.0 text; HF card: cc-by-4.0 | **go, chosen** for de; gold labels, official split; 12 fine-grained types with embedded and derived spans |
| Turkish-WikiNER (Altinok 2023), `turkish-nlp-suite/turkish-wikiNER` | tr | HF card: `license: cc-by-sa-4.0` | **go, chosen** for tr; SA lineage; 18 OntoNotes-style types |
| Naamapadam (Mhaske et al. 2023), `ai4bharat/naamapadam` | hi | Card: "We license the actual packaging of the mined data under the Creative Commons CC0 license"; "We do not own any of the text from which this data has been extracted." Source data: "Samanantar dataset", whose card says "Creative Commons Attribution-NonCommercial 4.0 International" (`license: cc-by-nc-4.0`) | **no-go** the text comes from Samanantar, which is non-commercial, so the text sources are not permissive. Hindi NER is a logged gap |
| WikiANN / PAN-X (Pan et al. 2017), `unimelb-nlp/wikiann` | all nine | HF card: `license: unknown`, "[More Information Needed]" | **no-go**: out |
| WikiNEuRal, MultiNERD (Babelscape) | fr, es, it, de, nl, zh (MultiNERD) | HF cards: cc-by-nc-sa-4.0 | **no-go** (NC) |
| MultiCoNER v1/v2 | hi, zh, tr, nl, de, es, fr, it, pt | HF card: cc-by-4.0 | not examined: a fine-grained scheme that does not map onto PER/ORG/LOC; candidate for a later look |
| CoNLL-2002 (es, nl) | es, nl | HF card: `license: unknown`; newspaper text, no open terms stated | not used (at most evaluation, through published baselines) |
| `ctoraman/atis-ner-turkish` | tr | HF: cc-by-nc-sa-4.0 | **no-go** (NC) |
| Smaller Turkish NER sets on the HF hub (e-commerce, clinical, privacy, synthetic) | tr | assorted (mit, cc-by-4.0, cc0-1.0, cc-by-sa-4.0) | not used: single narrow domains, not a general-purpose gold set |

NER labels follow each corpus: the baselines map them to PER, ORG, LOC (and MISC where the corpus has it) as the manifests state; Turkish maps PERSON, ORG, GPE and LOC and drops the other types to O.

## Rulings and data gaps (recorded 2026-11-05)

Rulings of the project; the owner may revisit them.

- **No-go by rule:** German-HDT (text licensed for academic use only, i.e. research-only); Italian-TWITTIRO and Portuguese-DANTEStocks (tweet text, platform-restricted); Turkish-Penn (translated from an LDC corpus).
- **Ruled 2026-11-05:** French Sequoia and FQB (LGPL-LR) are allowed with credit, treated like CC BY-SA (go; unused, French baselines not retrained). WikiANN is out. Naamapadam is out: its text comes from Samanantar (CC BY-NC 4.0), so the Hindi NER baseline trained on it is withdrawn and Hindi NER is a logged gap beside Hindi UD. AnCora: the `LICENSE.txt` (CC BY 4.0) governs; the README still carries a stale sentence ("The GNU license is inherited ..."), which is recorded and not relied on.
- **Hindi has no UD baseline and no UD-trained model.** Its only sizeable treebank (HDTB) is non-commercial and Hindi-PUD is test-only. A Hindi tagger, parser or NER model waits for allowed data (Naamapadam is out, see the NER table).
- **Italian trains on 897 sentences** (ParlaMint train plus MarkIT), because ISDT, VIT and ParTUT are non-commercial. Italian rows are reported as they come and compared with spaCy trained on the same small set; they are not hidden or compared with systems trained on ISDT.
