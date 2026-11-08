# Accurate-tier corpus

The languages the accurate tier is trained on, as `recipes/accurate/common/corpus.json` describes them (written by `tools/accurate/make_corpus_spec.py` from the fetched files; licence verdicts in `ud-licences.md`). Release r2.18 of Universal Dependencies.

- **84 languages** with allowed training data; 9 of them keep the data of the earlier nine-language run, so their rows stay comparable.
- **605,763 training sentences, 9,882,945 words** (before the dev hold-out).
- **19 languages with an entity corpus** (21 sets).
- **5 languages need a trained word segmenter** in front for raw text: Japanese (ja), Classical Chinese (lzh), Thai (th), Vietnamese (vi), Chinese (zh).
- **17 small languages** (under 500 training sentences in all; the sampler caps their repetition).
- **Dev split:** 67 languages have one in the release; 7 hold out every 10th training sentence; 10 (fewer than 200 sentences, no dev split) train on all of it and are not seen by model selection.

## Languages

Words are syntactic words of the training files. Extra treebanks are trained on and never scored; the language's test set is its gating treebank's test split. *Leaks* are extra-treebank training sentences that repeat a gating test sentence; the reader drops them.

| Code | Language | Gating treebank (train sentences / words) | Extra treebanks (train sentences) | Dev | Entities (train sentences) | Segmenter | Small | Lemma table (dev) |
|---|---|---|---|---|---|---|---|---|
| af | Afrikaans | Afrikaans-AfriBooms (1,315 / 33,878) |  | release |  |  |  | 1:0.6 (86.7% at 99.9%) |
| as | Assamese | Assamese-AiW (51 / 597) |  | release |  |  | yes | 1:0.6 (45.5% at 97.7%) |
| bor | Bororo | Bororo-BDT (17,107 / 128,418) |  | release |  |  |  | none |
| bxr | Buryat | Buryat-BDT (20 / 158) |  | none |  |  | yes | none |
| ca | Catalan | Catalan-AnCora (13,123 / 429,579) |  | release |  |  |  | 1:0.6 (94.9% at 99.6%) |
| ckb | Central Kurdish | Central_Kurdish-Mukri (92 / 539) |  | release |  |  | yes | 2:1.0 (42.3% at 97.7%) |
| cop | Coptic | Coptic-Scriptorium (1,467 / 37,639) | Coptic-Bohairic (477) | release |  |  |  | 1:0.8 (79.0% at 98.5%) |
| cs | Czech | Czech-CAC (23,478 / 472,609) | Czech-CLTT (467) | release |  |  |  | 1:0.6 (91.0% at 99.9%) |
| cy | Welsh | Welsh-CCG (1,355 / 28,563) |  | release |  |  |  | 1:0.6 (87.0% at 99.0%) |
| da | Danish | Danish-DDT (4,383 / 80,378) |  | release | uner-da-ddt (4,383) |  |  | 1:0.6 (86.3% at 99.8%) |
| de | German | German-GSD (13,813 / 263,777) |  | release | germeval-2014 (24,000) |  |  | 1:0.6 (89.1% at 99.2%) |
| egy | Egyptian | Egyptian-PC (1,619 / 19,486) |  | release |  |  |  | 1:0.6 (89.2% at 99.5%) |
| el | Greek | Greek-GUD (1,071 / 14,859) | Greek-Cretan (180), Greek-Messinian (60) | release |  |  |  | 1:0.6 (76.1% at 98.1%) |
| en | English | English-EWT (12,544 / 204,578) |  | release | uner-en-ewt (12,543) |  |  | 1:0.6 (91.5% at 99.4%) |
| es | Spanish | Spanish-AnCora (14,287 / 453,039) |  | release | wikiner-es-wp3 (113,959) |  |  | 1:0.6 (94.5% at 99.8%) |
| fa | Persian | Persian-Seraji (4,798 / 121,067) | Persian-PerDT (26,196, 1 leaks) | release |  |  |  | 1:0.6 (95.3% at 98.7%) |
| fi | Finnish | Finnish-FTB (14,981 / 127,613) | Finnish-TDT (12,217) | release |  |  |  | 1:0.6 (79.9% at 98.4%) |
| fo | Faroese | Faroese-FarPaHC (1,020 / 23,094) |  | release |  |  |  | 1:0.6 (12.9% at 100.0%) |
| fr | French | French-GSD (14,450 / 354,647) |  | release | wikiner-fr-wp3 (119,460) |  |  | 1:0.6 (92.2% at 99.8%) |
| ga | Irish | Irish-IDT (4,005 / 95,881) |  | release |  |  |  | 1:0.6 (87.2% at 99.2%) |
| gd | Scottish Gaelic | Scottish_Gaelic-ARCOSG (3,544 / 68,775) |  | release |  |  |  | 1:0.6 (90.0% at 99.6%) |
| gl | Galician | Galician-TreeGal (600 / 15,436) |  | held out |  |  |  | 1:0.6 (81.9% at 99.7%) |
| grc | Ancient Greek | Ancient_Greek-PTNK (727 / 18,440) |  | release |  |  |  | 1:0.6 (81.1% at 100.0%) |
| he | Hebrew | Hebrew-IAHLTwiki (4,298 / 120,832) | Hebrew-IAHLTknesset (2,362) | release |  |  |  | 1:0.6 (91.6% at 99.5%) |
| hr | Croatian | Croatian-SET (6,914 / 152,857) |  | release | uner-hr-set (6,914) |  |  | 1:0.6 (85.5% at 99.5%) |
| ht | Haitian Creole | Haitian_Creole-Adolphe (2,605 / 55,527) |  | release |  |  |  | 1:0.6 (97.5% at 99.3%) |
| hy | Armenian | Armenian-ArmTDP (4,352 / 82,871) | Armenian-BSUT (1,451, 1 leaks) | release |  |  |  | 1:0.6 (82.6% at 99.7%) |
| hyw | Western Armenian | Western_Armenian-ArmTDP (5,274 / 95,823) |  | release |  |  |  | 1:0.6 (79.3% at 99.6%) |
| id | Indonesian | Indonesian-GSD (4,482 / 97,602) | Indonesian-CSUI (656) | release | uner-id-gsd (4,482), uner-id-csui (656) |  |  | 1:0.6 (86.8% at 99.4%) |
| is | Icelandic | Icelandic-IcePaHC (34,007 / 705,747) | Icelandic-GC (3,960), Icelandic-Modern (2,703, 1 leaks) | release |  |  |  | 1:0.6 (93.7% at 98.3%) |
| it | Italian | Italian-ParlaMint (897 / 28,881) |  | release | wikiner-it-wp3 (115,965) |  |  | 1:0.6 (88.1% at 98.2%) |
| ja | Japanese | Japanese-GSD (7,050 / 168,333) |  | release |  | character-tagger |  | 1:0.6 (92.0% at 99.7%) |
| ka | Georgian | Georgian-GLC (2,214 / 40,530) | Georgian-GNC (918) | release |  |  |  | 1:0.6 (75.1% at 97.7%) |
| kk | Kazakh | Kazakh-KTB (31 / 529) |  | none |  |  | yes | none |
| kmr | Northern Kurdish | Northern_Kurdish-Kurmanji (20 / 242) |  | none |  |  | yes | none |
| ko | Korean | Korean-Kaist (23,010 / 296,446) | Korean-GSD (4,400), Korean-KSL (14,076) | release |  |  |  | 1:0.6 (75.2% at 98.0%) |
| koi | Komi Permyak | Komi_Permyak-UH (34 / 248) |  | none |  |  | yes | none |
| kpv | Komi Zyrian | Komi_Zyrian-Lattice (20 / 158) |  | none |  |  | yes | none |
| ky | Kyrgyz | Kyrgyz-KTMU (1,308 / 12,357) |  | held out |  |  |  | 1:0.8 (71.1% at 97.5%) |
| la | Latin | Latin-LLCT (7,289 / 194,143) | Latin-CIRCSE (918) | release |  |  |  | 1:0.6 (94.4% at 99.7%) |
| lij | Ligurian | Ligurian-GLT (20 / 226) |  | none |  |  | yes | none |
| lt | Lithuanian | Lithuanian-ALKSNIS (2,341 / 47,641) | Lithuanian-HSE (153) | release |  |  |  | 1:0.6 (72.5% at 99.4%) |
| lv | Latvian | Latvian-LVTB (15,059 / 258,266) |  | release |  |  |  | 1:0.6 (85.4% at 99.0%) |
| lzh | Classical Chinese | Classical_Chinese-Kyoto (74,609 / 374,560) |  | release |  | character-tagger |  | 1:0.6 (95.5% at 100.0%) |
| mdf | Moksha | Moksha-JR (22 / 226) |  | none |  |  | yes | none |
| mr | Marathi | Marathi-UFAL (373 / 2,997) | Marathi-CMUPAN (7,616) | release |  |  |  | 1:0.9 (68.0% at 97.2%) |
| mt | Maltese | Maltese-MUDT (1,123 / 22,880) |  | release |  |  |  | none |
| myv | Erzya | Erzya-JR (1,217 / 10,294) |  | held out |  |  |  | 1:0.6 (73.9% at 98.8%) |
| nds | Low Saxon | Low_Saxon-LSDC (500 / 11,666) |  | held out |  |  |  | 1:0.6 (79.3% at 99.5%) |
| nl | Dutch | Dutch-Alpino (12,289 / 186,027) |  | release | wikiner-nl-wp3 (163,725) |  |  | 1:0.6 (86.4% at 99.0%) |
| no | Norwegian | Norwegian-Bokmaal (15,696 / 243,886) | Norwegian-Nynorsk (14,174, 4 leaks) | release | uner-no-bokmaal (15,696), uner-no-nynorsk (14,174) |  |  | 1:0.6 (85.7% at 99.6%) |
| olo | Livvi | Livvi-KKPP (19 / 144) |  | none |  |  | yes | none |
| orv | Old East Slavic | Old_East_Slavic-RNC (3,778 / 101,125) | Old_East_Slavic-Birchbark (1,113), Old_East_Slavic-Ruthenian (4,120) | release |  |  |  | 1:0.8 (72.1% at 97.1%) |
| ota | Ottoman Turkish | Ottoman_Turkish-BOUN (114 / 1,997) | Ottoman_Turkish-DUDU (1,117) | release |  |  |  | 1:0.6 (63.2% at 99.3%) |
| pa | Punjabi | Punjabi-Rang (81 / 1,212) | Punjabi-CS (73) | release |  |  | yes | 2:0.9 (50.6% at 97.1%) |
| pcm | Naija | Naija-NSC (7,279 / 111,916) |  | release |  |  |  | 1:0.6 (96.9% at 99.7%) |
| pl | Polish | Polish-MPDT (1,433 / 33,520) |  | release | kpwr-ner-n82 (12,570) |  |  | 1:0.6 (74.8% at 99.6%) |
| pro | Old Occitan | Old_Occitan-CorAG (1,092 / 37,926) |  | release |  |  |  | none |
| pt | Portuguese | Portuguese-Bosque (7,018 / 171,776) |  | release | uner-pt-bosque (7,018) |  |  | 1:0.6 (90.3% at 99.6%) |
| qte | Telugu English | Telugu_English-TECT (61 / 290) |  | none |  |  | yes | none |
| ro | Romanian | Romanian-RRT (8,043 / 185,106) | Romanian-SiMoNERo (3,747) | release | ronec-2.0 (9,000) |  |  | 1:0.6 (91.6% at 99.7%) |
| ru | Russian | Russian-GSD (3,850 / 74,900) | Russian-Poetry (3,568) | release |  |  |  | 1:0.6 (77.0% at 99.7%) |
| sa | Sanskrit | Sanskrit-Vedic (21,477 / 161,959) |  | release |  |  |  | 1:0.6 (85.0% at 99.2%) |
| say | Zaar | Zaar-Autogramm (978 / 10,712) |  | held out |  |  |  | 1:0.8 (85.4% at 97.6%) |
| sd | Sindhi | Sindhi-Isra (4,096 / 74,589) |  | release |  |  |  | 1:0.6 (89.7% at 100.0%) |
| sdh | Southern Kurdish | Southern_Kurdish-Garrusi (152 / 1,177) |  | release |  |  | yes | none |
| sk | Slovak | Slovak-SNK (8,483 / 80,874) |  | release | uner-sk-snk (8,483) |  |  | 1:0.6 (65.5% at 99.5%) |
| sl | Slovenian | Slovenian-SSJ (10,903 / 215,155) | Slovenian-SST (5,094) | release | uner-sl-ssj (10,903) |  |  | 1:0.6 (88.6% at 99.8%) |
| sme | North Sami | North_Sami-Giella (2,257 / 16,835) |  | held out |  |  |  | 1:0.6 (75.9% at 99.6%) |
| sms | Skolt Sami | Skolt_Sami-Giellagas (20 / 153) |  | none |  |  | yes | none |
| sq | Albanian | Albanian-STAF (160 / 2,879) |  | release |  |  | yes | 2:0.6 (53.2% at 97.6%) |
| sr | Serbian | Serbian-SET (3,328 / 74,259) |  | release | uner-sr-set (3,328) |  |  | 1:0.6 (83.9% at 99.5%) |
| sv | Swedish | Swedish-Talbanken (4,315 / 66,646) |  | release | uner-sv-talbanken (4,303) |  |  | 1:0.6 (87.6% at 99.8%) |
| te | Telugu | Telugu-MTG (1,051 / 5,082) |  | release |  |  |  | none |
| th | Thai | Thai-TUD (2,902 / 62,011) |  | release |  | character-tagger |  | none |
| tr | Turkish | Turkish-BOUN (7,803 / 100,713) |  | release | turkish-wikiner (17,967) |  |  | 1:0.6 (75.9% at 97.8%) |
| ug | Uyghur | Uyghur-UDT (1,656 / 19,262) |  | release |  |  |  | 1:0.6 (57.2% at 99.8%) |
| uk | Ukrainian | Ukrainian-ParlaMint (5,611 / 87,369) |  | release |  |  |  | 1:0.6 (87.0% at 99.9%) |
| uz | Uzbek | Uzbek-UzUDT (483 / 5,442) |  | held out |  |  | yes | 1:0.6 (62.6% at 97.4%) |
| vi | Vietnamese | Vietnamese-VTB (1,400 / 20,215) |  | release |  | character-tagger |  | 1:0.6 (80.8% at 99.5%) |
| wo | Wolof | Wolof-WTB (1,188 / 23,561) |  | release |  |  |  | 1:0.6 (84.5% at 97.9%) |
| yi | Yiddish | Yiddish-YiTB (1,877 / 16,458) |  | release |  |  |  | 1:0.6 (82.7% at 98.8%) |
| zh | Chinese | Chinese-GSDSimp (3,997 / 98,614) |  | release | uner-zh-gsdsimp (3,997) | character-tagger |  | 1:0.6 (86.4% at 100.0%) |
| zza | Zazaki | Zazaki-ZSD (138 / 978) |  | release |  |  | yes | 1:0.6 (71.8% at 100.0%) |

Left out of training: Japanese: Japanese-GSDLUW (the same sentences as Japanese-GSD, cut into other word units).

## Languages without an accurate-tier model

Every treebank of these languages is ruled out (`ud-licences.md`, "Languages without an accurate-tier model"), so there is no allowed training data: Ancient Hebrew, Basque, Belarusian, Bulgarian, Chintang, Classical Armenian, Estonian, Gothic, Hungarian, Kadiweu, Khoekhoe, Maghrebi Arabic French, Manx, Mbya Guarani, Naga, Nheengatu, Occitan, Old Church Slavonic, Pomak, Ruuli, Shanghainese, South Levantine Arabic, Turkish German, Urdu, Xibe. Hindi has only the test-only PUD treebank.

## Evaluation-only data

Test-only treebanks of the trained languages are scored as extra rows and never trained on: Czech-PUD, Czech-Poetry, Greek-Lesbian, Finnish-PUD, Faroese-OFT, Irish-Cadhan, Galician-PUD, Hebrew-PostRab, Haitian_Creole-Autogramm, Indonesian-PUD, Icelandic-PUD, Japanese-PUD, Japanese-PUDLUW, Korean-LittlePrince, Korean-PUD, Komi_Zyrian-IKDP, Kyrgyz-TueCL, Latvian-Cairo, Classical_Chinese-TueCL, Ottoman_Turkish-TueCL, Polish-PUD, Romanian-ArT, Romanian-MolDoRo, Russian-PUD, Sanskrit-UFAL, Albanian-TSA, Swedish-Old, Swedish-PUD, Swedish-SweLL, Thai-PUD, Uzbek-TueCL, Uzbek-UT, Vietnamese-TueCL.
