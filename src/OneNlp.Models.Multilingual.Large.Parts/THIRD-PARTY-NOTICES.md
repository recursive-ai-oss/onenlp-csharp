# Third-party notices: the weights of RecursiveAI.OneNlp.Models.Multilingual.Large

The code of this package is licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. The weights are a fine-tuned copy of a third-party model, trained on the data below.

## Base model

- **mmBERT-base** (Marone et al., "mmBERT: A Modern Multilingual Encoder with Annealed Language Learning", 2025, arXiv:2509.06888), Johns Hopkins University CLSP, <https://huggingface.co/jhu-clsp/mmBERT-base>, revision `c5955035435e2bf121cde7f3c8863ef52ff35d82`, licensed MIT. The token embedding table and `tokenizer.json` are the model's own, unchanged; the transformer layers were fine-tuned.

## Training data (the fine-tuned weights are derived from it)

- **Universal Dependencies r2.18** (<https://universaldependencies.org>): English-EWT, German-GSD, French-GSD, Portuguese-Bosque, Dutch-Alpino, Turkish-BOUN and Chinese-GSDSimp (Creative Commons Attribution-ShareAlike 4.0), Spanish-AnCora (Creative Commons Attribution 4.0), Italian-ParlaMint and Italian-MarkIT (Creative Commons Attribution-ShareAlike 4.0 and Attribution 4.0 respectively; see the treebank READMEs). Tags, morphology, lemmas and dependency parses.
- **Universal NER v1** (Mayhew et al., NAACL 2024, <https://www.universalner.org>): English-EWT, Portuguese-Bosque and Chinese-GSDSimp, Creative Commons Attribution-ShareAlike 4.0.
- **GermEval 2014 NER** (Benikova, Biemann and Reznicek, LREC 2014), Creative Commons Attribution 4.0.
- **WikiNER** (Nothman et al., "Learning multilingual named entity recognition from Wikipedia", Artificial Intelligence 2013), Spanish, French, Italian and Dutch, Creative Commons Attribution 4.0 (the text is Wikipedia, Creative Commons Attribution-ShareAlike).
- **Turkish-WikiNER** (Altinok 2023), Creative Commons Attribution-ShareAlike 4.0.

The model has ShareAlike training-data lineage; the source datasets retain their respective terms and are not redistributed with the weights. The additional training corpora of the expanded model are credited below. No label generator was used. The official test splits and every other evaluation set were not used for training or for choosing settings.

## Expanded universal-dependencies training corpus

Universal Dependencies r2.18 contributors, <https://universaldependencies.org>. Every treebank used for training is listed below, with its source and licence; the original nine-language sources listed above also remain in the lineage of the warm start. The Italian combined training set includes MarkIT (CC BY 4.0) as well as ParlaMint. SHA-256 per split and roles are recorded in `tools/fetch/ud/MANIFEST.md`; only train and dev splits contribute to training or selection. Test files do not train or select the model.

| Treebank | Licence | Source / contributors |
|---|---|---|
| Afrikaans-AfriBooms | CC BY-SA 4.0 | [UD Afrikaans-AfriBooms contributors](https://github.com/UniversalDependencies/UD_Afrikaans-AfriBooms/tree/r2.18) |
| Assamese-AiW | CC BY-SA 4.0 | [UD Assamese-AiW contributors](https://github.com/UniversalDependencies/UD_Assamese-AiW/tree/r2.18) |
| Bororo-BDT | CC BY-SA 4.0 | [UD Bororo-BDT contributors](https://github.com/UniversalDependencies/UD_Bororo-BDT/tree/r2.18) |
| Buryat-BDT | CC BY-SA 4.0 | [UD Buryat-BDT contributors](https://github.com/UniversalDependencies/UD_Buryat-BDT/tree/r2.18) |
| Catalan-AnCora | CC BY 4.0 | [UD Catalan-AnCora contributors](https://github.com/UniversalDependencies/UD_Catalan-AnCora/tree/r2.18) |
| Central_Kurdish-Mukri | CC BY-SA 4.0 | [UD Central_Kurdish-Mukri contributors](https://github.com/UniversalDependencies/UD_Central_Kurdish-Mukri/tree/r2.18) |
| Coptic-Scriptorium | CC BY 4.0 | [UD Coptic-Scriptorium contributors](https://github.com/UniversalDependencies/UD_Coptic-Scriptorium/tree/r2.18) |
| Coptic-Bohairic | CC BY 4.0 | [UD Coptic-Bohairic contributors](https://github.com/UniversalDependencies/UD_Coptic-Bohairic/tree/r2.18) |
| Czech-CAC | CC BY-SA 4.0 | [UD Czech-CAC contributors](https://github.com/UniversalDependencies/UD_Czech-CAC/tree/r2.18) |
| Czech-CLTT | CC BY-SA 4.0 | [UD Czech-CLTT contributors](https://github.com/UniversalDependencies/UD_Czech-CLTT/tree/r2.18) |
| Welsh-CCG | CC BY-SA 4.0 | [UD Welsh-CCG contributors](https://github.com/UniversalDependencies/UD_Welsh-CCG/tree/r2.18) |
| Danish-DDT | CC BY-SA 4.0 | [UD Danish-DDT contributors](https://github.com/UniversalDependencies/UD_Danish-DDT/tree/r2.18) |
| German-GSD | CC BY-SA 4.0 | [UD German-GSD contributors](https://github.com/UniversalDependencies/UD_German-GSD/tree/r2.18) |
| Egyptian-PC | CC BY-SA 4.0 | [UD Egyptian-PC contributors](https://github.com/UniversalDependencies/UD_Egyptian-PC/tree/r2.18) |
| Greek-GUD | CC BY-SA 4.0 | [UD Greek-GUD contributors](https://github.com/UniversalDependencies/UD_Greek-GUD/tree/r2.18) |
| Greek-Cretan | CC BY-SA 4.0 | [UD Greek-Cretan contributors](https://github.com/UniversalDependencies/UD_Greek-Cretan/tree/r2.18) |
| Greek-Messinian | CC BY-SA 4.0 | [UD Greek-Messinian contributors](https://github.com/UniversalDependencies/UD_Greek-Messinian/tree/r2.18) |
| English-EWT | CC BY-SA 4.0 | [UD English-EWT contributors](https://github.com/UniversalDependencies/UD_English-EWT/tree/r2.18) |
| Spanish-AnCora | CC BY 4.0 | [UD Spanish-AnCora contributors](https://github.com/UniversalDependencies/UD_Spanish-AnCora/tree/r2.18) |
| Persian-Seraji | CC BY-SA 4.0 | [UD Persian-Seraji contributors](https://github.com/UniversalDependencies/UD_Persian-Seraji/tree/r2.18) |
| Persian-PerDT | CC BY-SA 4.0 | [UD Persian-PerDT contributors](https://github.com/UniversalDependencies/UD_Persian-PerDT/tree/r2.18) |
| Finnish-FTB | CC BY 4.0 | [UD Finnish-FTB contributors](https://github.com/UniversalDependencies/UD_Finnish-FTB/tree/r2.18) |
| Finnish-TDT | CC BY-SA 4.0 | [UD Finnish-TDT contributors](https://github.com/UniversalDependencies/UD_Finnish-TDT/tree/r2.18) |
| Faroese-FarPaHC | CC BY-SA 4.0 | [UD Faroese-FarPaHC contributors](https://github.com/UniversalDependencies/UD_Faroese-FarPaHC/tree/r2.18) |
| French-GSD | CC BY-SA 4.0 | [UD French-GSD contributors](https://github.com/UniversalDependencies/UD_French-GSD/tree/r2.18) |
| Irish-IDT | CC BY-SA 3.0 | [UD Irish-IDT contributors](https://github.com/UniversalDependencies/UD_Irish-IDT/tree/r2.18) |
| Scottish_Gaelic-ARCOSG | CC BY-SA 4.0 | [UD Scottish_Gaelic-ARCOSG contributors](https://github.com/UniversalDependencies/UD_Scottish_Gaelic-ARCOSG/tree/r2.18) |
| Galician-TreeGal | LGPL-LR | [UD Galician-TreeGal contributors](https://github.com/UniversalDependencies/UD_Galician-TreeGal/tree/r2.18) |
| Ancient_Greek-PTNK | CC BY-SA 4.0 | [UD Ancient_Greek-PTNK contributors](https://github.com/UniversalDependencies/UD_Ancient_Greek-PTNK/tree/r2.18) |
| Hebrew-IAHLTwiki | CC BY-SA 4.0 | [UD Hebrew-IAHLTwiki contributors](https://github.com/UniversalDependencies/UD_Hebrew-IAHLTwiki/tree/r2.18) |
| Hebrew-IAHLTknesset | CC BY-SA 4.0 | [UD Hebrew-IAHLTknesset contributors](https://github.com/UniversalDependencies/UD_Hebrew-IAHLTknesset/tree/r2.18) |
| Croatian-SET | CC BY-SA 4.0 | [UD Croatian-SET contributors](https://github.com/UniversalDependencies/UD_Croatian-SET/tree/r2.18) |
| Haitian_Creole-Adolphe | CC BY-SA 4.0 | [UD Haitian_Creole-Adolphe contributors](https://github.com/UniversalDependencies/UD_Haitian_Creole-Adolphe/tree/r2.18) |
| Armenian-ArmTDP | CC BY-SA 4.0 | [UD Armenian-ArmTDP contributors](https://github.com/UniversalDependencies/UD_Armenian-ArmTDP/tree/r2.18) |
| Armenian-BSUT | CC BY-SA 4.0 | [UD Armenian-BSUT contributors](https://github.com/UniversalDependencies/UD_Armenian-BSUT/tree/r2.18) |
| Western_Armenian-ArmTDP | CC BY-SA 4.0 | [UD Western_Armenian-ArmTDP contributors](https://github.com/UniversalDependencies/UD_Western_Armenian-ArmTDP/tree/r2.18) |
| Indonesian-GSD | CC BY-SA 4.0 | [UD Indonesian-GSD contributors](https://github.com/UniversalDependencies/UD_Indonesian-GSD/tree/r2.18) |
| Indonesian-CSUI | CC BY-SA 4.0 | [UD Indonesian-CSUI contributors](https://github.com/UniversalDependencies/UD_Indonesian-CSUI/tree/r2.18) |
| Icelandic-IcePaHC | CC BY-SA 4.0 | [UD Icelandic-IcePaHC contributors](https://github.com/UniversalDependencies/UD_Icelandic-IcePaHC/tree/r2.18) |
| Icelandic-GC | CC BY-SA 4.0 | [UD Icelandic-GC contributors](https://github.com/UniversalDependencies/UD_Icelandic-GC/tree/r2.18) |
| Icelandic-Modern | CC BY-SA 4.0 | [UD Icelandic-Modern contributors](https://github.com/UniversalDependencies/UD_Icelandic-Modern/tree/r2.18) |
| Italian-ParlaMint | CC BY-SA 4.0 | [UD Italian-ParlaMint contributors](https://github.com/UniversalDependencies/UD_Italian-ParlaMint/tree/r2.18) |
| Japanese-GSD | CC BY-SA 4.0 | [UD Japanese-GSD contributors](https://github.com/UniversalDependencies/UD_Japanese-GSD/tree/r2.18) |
| Georgian-GLC | CC BY-SA 4.0 | [UD Georgian-GLC contributors](https://github.com/UniversalDependencies/UD_Georgian-GLC/tree/r2.18) |
| Georgian-GNC | CC BY-SA 4.0 | [UD Georgian-GNC contributors](https://github.com/UniversalDependencies/UD_Georgian-GNC/tree/r2.18) |
| Kazakh-KTB | CC BY-SA 4.0 | [UD Kazakh-KTB contributors](https://github.com/UniversalDependencies/UD_Kazakh-KTB/tree/r2.18) |
| Northern_Kurdish-Kurmanji | CC BY-SA 4.0 | [UD Northern_Kurdish-Kurmanji contributors](https://github.com/UniversalDependencies/UD_Northern_Kurdish-Kurmanji/tree/r2.18) |
| Korean-Kaist | CC BY-SA 4.0 | [UD Korean-Kaist contributors](https://github.com/UniversalDependencies/UD_Korean-Kaist/tree/r2.18) |
| Korean-GSD | CC BY-SA 4.0 | [UD Korean-GSD contributors](https://github.com/UniversalDependencies/UD_Korean-GSD/tree/r2.18) |
| Korean-KSL | CC BY-SA 4.0 | [UD Korean-KSL contributors](https://github.com/UniversalDependencies/UD_Korean-KSL/tree/r2.18) |
| Komi_Permyak-UH | CC BY-SA 4.0 | [UD Komi_Permyak-UH contributors](https://github.com/UniversalDependencies/UD_Komi_Permyak-UH/tree/r2.18) |
| Komi_Zyrian-Lattice | CC BY-SA 4.0 | [UD Komi_Zyrian-Lattice contributors](https://github.com/UniversalDependencies/UD_Komi_Zyrian-Lattice/tree/r2.18) |
| Kyrgyz-KTMU | CC BY-SA 4.0 | [UD Kyrgyz-KTMU contributors](https://github.com/UniversalDependencies/UD_Kyrgyz-KTMU/tree/r2.18) |
| Latin-LLCT | CC BY-SA 4.0 | [UD Latin-LLCT contributors](https://github.com/UniversalDependencies/UD_Latin-LLCT/tree/r2.18) |
| Latin-CIRCSE | CC BY-SA 4.0 | [UD Latin-CIRCSE contributors](https://github.com/UniversalDependencies/UD_Latin-CIRCSE/tree/r2.18) |
| Ligurian-GLT | C-UDA 1.0 | [UD Ligurian-GLT contributors](https://github.com/UniversalDependencies/UD_Ligurian-GLT/tree/r2.18) |
| Lithuanian-ALKSNIS | CC BY-SA 4.0 | [UD Lithuanian-ALKSNIS contributors](https://github.com/UniversalDependencies/UD_Lithuanian-ALKSNIS/tree/r2.18) |
| Lithuanian-HSE | CC BY-SA 4.0 | [UD Lithuanian-HSE contributors](https://github.com/UniversalDependencies/UD_Lithuanian-HSE/tree/r2.18) |
| Latvian-LVTB | CC BY-SA 4.0 | [UD Latvian-LVTB contributors](https://github.com/UniversalDependencies/UD_Latvian-LVTB/tree/r2.18) |
| Classical_Chinese-Kyoto | PD | [UD Classical_Chinese-Kyoto contributors](https://github.com/UniversalDependencies/UD_Classical_Chinese-Kyoto/tree/r2.18) |
| Moksha-JR | CC BY-SA 4.0 | [UD Moksha-JR contributors](https://github.com/UniversalDependencies/UD_Moksha-JR/tree/r2.18) |
| Marathi-UFAL | CC BY-SA 4.0 | [UD Marathi-UFAL contributors](https://github.com/UniversalDependencies/UD_Marathi-UFAL/tree/r2.18) |
| Marathi-CMUPAN | CC BY-SA 4.0 | [UD Marathi-CMUPAN contributors](https://github.com/UniversalDependencies/UD_Marathi-CMUPAN/tree/r2.18) |
| Maltese-MUDT | CC BY-SA 4.0 | [UD Maltese-MUDT contributors](https://github.com/UniversalDependencies/UD_Maltese-MUDT/tree/r2.18) |
| Erzya-JR | CC BY-SA 4.0 | [UD Erzya-JR contributors](https://github.com/UniversalDependencies/UD_Erzya-JR/tree/r2.18) |
| Low_Saxon-LSDC | CC BY-SA 4.0 | [UD Low_Saxon-LSDC contributors](https://github.com/UniversalDependencies/UD_Low_Saxon-LSDC/tree/r2.18) |
| Dutch-Alpino | CC BY-SA 4.0 | [UD Dutch-Alpino contributors](https://github.com/UniversalDependencies/UD_Dutch-Alpino/tree/r2.18) |
| Norwegian-Bokmaal | CC BY-SA 4.0 | [UD Norwegian-Bokmaal contributors](https://github.com/UniversalDependencies/UD_Norwegian-Bokmaal/tree/r2.18) |
| Norwegian-Nynorsk | CC BY-SA 4.0 | [UD Norwegian-Nynorsk contributors](https://github.com/UniversalDependencies/UD_Norwegian-Nynorsk/tree/r2.18) |
| Livvi-KKPP | CC BY-SA 4.0 | [UD Livvi-KKPP contributors](https://github.com/UniversalDependencies/UD_Livvi-KKPP/tree/r2.18) |
| Old_East_Slavic-RNC | CC BY-SA 4.0 | [UD Old_East_Slavic-RNC contributors](https://github.com/UniversalDependencies/UD_Old_East_Slavic-RNC/tree/r2.18) |
| Old_East_Slavic-Birchbark | CC BY-SA 4.0 | [UD Old_East_Slavic-Birchbark contributors](https://github.com/UniversalDependencies/UD_Old_East_Slavic-Birchbark/tree/r2.18) |
| Old_East_Slavic-Ruthenian | CC BY-SA 4.0 | [UD Old_East_Slavic-Ruthenian contributors](https://github.com/UniversalDependencies/UD_Old_East_Slavic-Ruthenian/tree/r2.18) |
| Ottoman_Turkish-BOUN | CC BY-SA 4.0 | [UD Ottoman_Turkish-BOUN contributors](https://github.com/UniversalDependencies/UD_Ottoman_Turkish-BOUN/tree/r2.18) |
| Ottoman_Turkish-DUDU | CC BY-SA 4.0 | [UD Ottoman_Turkish-DUDU contributors](https://github.com/UniversalDependencies/UD_Ottoman_Turkish-DUDU/tree/r2.18) |
| Punjabi-Rang | CC BY-SA 4.0 | [UD Punjabi-Rang contributors](https://github.com/UniversalDependencies/UD_Punjabi-Rang/tree/r2.18) |
| Punjabi-CS | CC BY-SA 4.0 | [UD Punjabi-CS contributors](https://github.com/UniversalDependencies/UD_Punjabi-CS/tree/r2.18) |
| Naija-NSC | CC BY-SA 4.0 | [UD Naija-NSC contributors](https://github.com/UniversalDependencies/UD_Naija-NSC/tree/r2.18) |
| Polish-MPDT | CC BY-SA 4.0 | [UD Polish-MPDT contributors](https://github.com/UniversalDependencies/UD_Polish-MPDT/tree/r2.18) |
| Old_Occitan-CorAG | CC BY-SA 4.0 | [UD Old_Occitan-CorAG contributors](https://github.com/UniversalDependencies/UD_Old_Occitan-CorAG/tree/r2.18) |
| Portuguese-Bosque | CC BY-SA 4.0 | [UD Portuguese-Bosque contributors](https://github.com/UniversalDependencies/UD_Portuguese-Bosque/tree/r2.18) |
| Telugu_English-TECT | CC BY-SA 4.0 | [UD Telugu_English-TECT contributors](https://github.com/UniversalDependencies/UD_Telugu_English-TECT/tree/r2.18) |
| Romanian-RRT | CC BY-SA 4.0 | [UD Romanian-RRT contributors](https://github.com/UniversalDependencies/UD_Romanian-RRT/tree/r2.18) |
| Romanian-SiMoNERo | CC BY-SA 4.0 | [UD Romanian-SiMoNERo contributors](https://github.com/UniversalDependencies/UD_Romanian-SiMoNERo/tree/r2.18) |
| Russian-GSD | CC BY-SA 4.0 | [UD Russian-GSD contributors](https://github.com/UniversalDependencies/UD_Russian-GSD/tree/r2.18) |
| Russian-Poetry | CC BY-SA 4.0 | [UD Russian-Poetry contributors](https://github.com/UniversalDependencies/UD_Russian-Poetry/tree/r2.18) |
| Sanskrit-Vedic | CC BY-SA 4.0 | [UD Sanskrit-Vedic contributors](https://github.com/UniversalDependencies/UD_Sanskrit-Vedic/tree/r2.18) |
| Zaar-Autogramm | CC BY-SA 4.0 | [UD Zaar-Autogramm contributors](https://github.com/UniversalDependencies/UD_Zaar-Autogramm/tree/r2.18) |
| Sindhi-Isra | CC BY-SA 4.0 | [UD Sindhi-Isra contributors](https://github.com/UniversalDependencies/UD_Sindhi-Isra/tree/r2.18) |
| Southern_Kurdish-Garrusi | CC BY-SA 4.0 | [UD Southern_Kurdish-Garrusi contributors](https://github.com/UniversalDependencies/UD_Southern_Kurdish-Garrusi/tree/r2.18) |
| Slovak-SNK | CC BY-SA 4.0 | [UD Slovak-SNK contributors](https://github.com/UniversalDependencies/UD_Slovak-SNK/tree/r2.18) |
| Slovenian-SSJ | CC BY-SA 4.0 | [UD Slovenian-SSJ contributors](https://github.com/UniversalDependencies/UD_Slovenian-SSJ/tree/r2.18) |
| Slovenian-SST | CC BY-SA 4.0 | [UD Slovenian-SST contributors](https://github.com/UniversalDependencies/UD_Slovenian-SST/tree/r2.18) |
| North_Sami-Giella | CC BY-SA 4.0 | [UD North_Sami-Giella contributors](https://github.com/UniversalDependencies/UD_North_Sami-Giella/tree/r2.18) |
| Skolt_Sami-Giellagas | CC BY-SA 4.0 | [UD Skolt_Sami-Giellagas contributors](https://github.com/UniversalDependencies/UD_Skolt_Sami-Giellagas/tree/r2.18) |
| Albanian-STAF | CC BY-SA 4.0 | [UD Albanian-STAF contributors](https://github.com/UniversalDependencies/UD_Albanian-STAF/tree/r2.18) |
| Serbian-SET | CC BY-SA 4.0 | [UD Serbian-SET contributors](https://github.com/UniversalDependencies/UD_Serbian-SET/tree/r2.18) |
| Swedish-Talbanken | CC BY-SA 4.0 | [UD Swedish-Talbanken contributors](https://github.com/UniversalDependencies/UD_Swedish-Talbanken/tree/r2.18) |
| Telugu-MTG | CC BY-SA 4.0 | [UD Telugu-MTG contributors](https://github.com/UniversalDependencies/UD_Telugu-MTG/tree/r2.18) |
| Thai-TUD | CC BY-SA 4.0 | [UD Thai-TUD contributors](https://github.com/UniversalDependencies/UD_Thai-TUD/tree/r2.18) |
| Turkish-BOUN | CC BY-SA 4.0 | [UD Turkish-BOUN contributors](https://github.com/UniversalDependencies/UD_Turkish-BOUN/tree/r2.18) |
| Uyghur-UDT | CC BY-SA 4.0 | [UD Uyghur-UDT contributors](https://github.com/UniversalDependencies/UD_Uyghur-UDT/tree/r2.18) |
| Ukrainian-ParlaMint | CC BY-SA 4.0 | [UD Ukrainian-ParlaMint contributors](https://github.com/UniversalDependencies/UD_Ukrainian-ParlaMint/tree/r2.18) |
| Uzbek-UzUDT | CC BY-SA 4.0 | [UD Uzbek-UzUDT contributors](https://github.com/UniversalDependencies/UD_Uzbek-UzUDT/tree/r2.18) |
| Vietnamese-VTB | CC BY-SA 4.0 | [UD Vietnamese-VTB contributors](https://github.com/UniversalDependencies/UD_Vietnamese-VTB/tree/r2.18) |
| Wolof-WTB | CC BY-SA 4.0 | [UD Wolof-WTB contributors](https://github.com/UniversalDependencies/UD_Wolof-WTB/tree/r2.18) |
| Yiddish-YiTB | CC BY-SA 4.0 | [UD Yiddish-YiTB contributors](https://github.com/UniversalDependencies/UD_Yiddish-YiTB/tree/r2.18) |
| Chinese-GSDSimp | CC BY-SA 4.0 | [UD Chinese-GSDSimp contributors](https://github.com/UniversalDependencies/UD_Chinese-GSDSimp/tree/r2.18) |
| Zazaki-ZSD | CC BY-SA 4.0 | [UD Zazaki-ZSD contributors](https://github.com/UniversalDependencies/UD_Zazaki-ZSD/tree/r2.18) |

## Ligurian Genoese Ligurian Treebank (GLT)

The base-size multilingual model was trained on the 20-sentence training split of **UD Ligurian-GLT r2.18**, contributed by **Stefano Lusito and Jean Maillard**, under the Computational Use of Data Agreement 1.0 (C-UDA). Source and acknowledgments: <https://github.com/UniversalDependencies/UD_Ligurian-GLT/tree/r2.18>. The provider acknowledges De Ferrari Editore and the newspaper O Stafî. Only computational use was made; no treebank text is redistributed with the model. C-UDA §2.2: “The C-UDA does not impose any restriction with respect to the use, modification, or distribution of Results.” The training data retains its own terms.

## Additional entity training corpora

- **Universal NER v1**, Mayhew et al. 2024, <https://www.universalner.org>: Croatian-SET, Danish-DDT, Indonesian-GSD/CSUI, Norwegian-Bokmål/Nynorsk, Serbian-SET, Slovak-SNK, Slovenian-SSJ and Swedish-Talbanken, CC BY-SA 4.0. The model also retains the nine-language entity sources listed above.
- **KPWr-NER n82**, CLARIN-PL contributors, <https://huggingface.co/datasets/clarin-pl/kpwr-ner>, revision `a20247f8`, CC BY 3.0.
- **RONEC 2.0**, Romanian named-entity corpus contributors, <https://huggingface.co/datasets/community-datasets/ronec>, revision `2a0856f6`, MIT.

Dataset revision pins, source links, split roles and hashes for these entity corpora are in `tools/fetch/ud-ner/` and `recipes/accurate/common/corpus.json`. The word segmenters for Japanese, Classical Chinese, Thai and Vietnamese train on the same UD training treebanks listed above (Japanese-GSD, Classical Chinese-Kyoto, Thai-TUD, Vietnamese-VTB); the Chinese segmenter comes from Chinese-GSDSimp. No additional corpus was used.
