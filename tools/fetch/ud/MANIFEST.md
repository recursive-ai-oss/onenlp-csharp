# UD treebanks fetched for the accurate tier

Release r2.18 (15 May 2026), each repository at tag `r2.18`. Written by `tools/fetch/fetch_ud.py` from the catalogue `tools/fetch/ud/treebanks.tsv`; verdicts and quotes are in `docs/ud-licences.md`. Roles: `train` and `dev` feed training and model selection; `eval` (the test split) is fixed here and never read by training or tuning. Files are fetched on the worker and never committed. Treebanks of the nine languages with their own fetch scripts are not repeated here.

| Role | Treebank | File | Sentences | Tokens | Licence | SHA-256 | Notes |
|---|---|---|---|---|---|---|---|
| eval | Abaza-ATB | `abq_atb-ud-test.conllu` | 98 | 652 | CC BY-SA 4.0 | `d271dc50e15ace0be9c3db71eeef0611d15ad7835e33f1a249fbdf42a1e53002` |  |
| eval | Abkhaz-AbNC | `ab_abnc-ud-test.conllu` | 1,545 | 13,054 | CC BY-SA 4.0 | `0455d5d2e39544991666351e521e0ef0a2b8590d8fee18466d699f7c5891ea7d` |  |
| train | Afrikaans-AfriBooms | `af_afribooms-ud-train.conllu` | 1,315 | 33,878 | CC BY-SA 4.0 | `5dfd07a87750b4b535eca5a4b4254352d2d40fbdd3fc1a9d332e62cec0f0c05f` |  |
| dev | Afrikaans-AfriBooms | `af_afribooms-ud-dev.conllu` | 194 | 5,317 | CC BY-SA 4.0 | `9be338cd501863631e03c123be2399e32b3d845d91347e5d6aa10c76254fbb49` |  |
| eval | Afrikaans-AfriBooms | `af_afribooms-ud-test.conllu` | 425 | 10,063 | CC BY-SA 4.0 | `e41f9111795ea733f91a93787e76a3191a088c213375c50f8ec6a9597a94e967` |  |
| eval | Akkadian-PISANDUB | `akk_pisandub-ud-test.conllu` | 101 | 1,852 | CC BY-SA 4.0 | `2b8dbee244e3c8181c7176ea00e576fc7ee7c0a88f3a1ce37e1ef49ab27cfd6f` |  |
| eval | Akkadian-RIAO | `akk_riao-ud-test.conllu` | 1,874 | 23,165 | CC BY-SA 3.0 | `ba10dec363a15196298157fcb551dfe3853178b1bf9ff4e24c65809efae43737` |  |
| eval | Akuntsu-TuDeT | `aqz_tudet-ud-test.conllu` | 343 | 1,468 | CC BY-SA 4.0 | `f6943a1351c2ea99c15f84bf410e457d48118e339af7856312d7c9c13bda9da9` |  |
| train | Albanian-STAF | `sq_staf-ud-train.conllu` | 160 | 2,879 | CC BY-SA 4.0 | `afd5faa6893363a23143f37ceed28ff194a4dde9c300124d9ef6eb0465ced97a` |  |
| dev | Albanian-STAF | `sq_staf-ud-dev.conllu` | 20 | 312 | CC BY-SA 4.0 | `9cabb2937fea2870246cca249b1d4818502a7d382255fbf7377d8d7c9f0a26e7` |  |
| eval | Albanian-STAF | `sq_staf-ud-test.conllu` | 20 | 372 | CC BY-SA 4.0 | `c693a5370df5228445e45cbe72329800d0513cf7bdd71602e7b5f245405131f9` |  |
| eval | Albanian-TSA | `sq_tsa-ud-test.conllu` | 60 | 922 | CC BY-SA 4.0 | `7b610b073d90a11737bba4f46edcb36495dbbac5dd49c4eb6877112f949775c8` |  |
| eval | Alemannic-DIVITAL | `gsw_divital-ud-test.conllu` | 977 | 19,743 | CC BY-SA 4.0 | `9dceb05da1ecc987f33a5bda30559857a2ba5f28eac1e95aecc53acbd436964a` |  |
| eval | Alemannic-UZH | `gsw_uzh-ud-test.conllu` | 100 | 1,444 | CC BY-SA 4.0 | `a2626633bf5a43d5ea436057854efbb5d57b997f67b3262c01d1d58a7b91bb2f` |  |
| eval | Amharic-ATT | `am_att-ud-test.conllu` | 1,074 | 10,010 | CC BY-SA 4.0 | `7960fde84b71012ee0aac89fc6a23fb13856f095b0186d48f220ab09115bc0c3` |  |
| train | Ancient_Greek-PTNK | `grc_ptnk-ud-train.conllu` | 727 | 18,440 | CC BY-SA 4.0 | `236e4bce3fc13d0d54fa7ba7946ca963cccd4bae6606961cf8099660e7b4a5e0` |  |
| dev | Ancient_Greek-PTNK | `grc_ptnk-ud-dev.conllu` | 439 | 10,723 | CC BY-SA 4.0 | `86bde6ee1e43db1a9e1a1a74953749e01370a04665bb5cad4163e9bb6aeb2eff` |  |
| eval | Ancient_Greek-PTNK | `grc_ptnk-ud-test.conllu` | 410 | 10,346 | CC BY-SA 4.0 | `16fc6f69cf38c08604629120f481751980e823731b287133e38b71e1d33a354e` |  |
| eval | Apurina-UFPA | `apu_ufpa-ud-test.conllu` | 183 | 1,099 | CC BY-SA 4.0 | `f873e52f4a6f885fdeaa80d2913e88e25d3be87bc4be551d110ee70d4fbd0a3b` |  |
| eval | Arabic-PUD | `ar_pud-ud-test.conllu` | 1,000 | 20,747 | CC BY-SA 3.0 | `befc6dd18b5b8803644ae8208e2e5f52c0957a36437627c05110914ec42281a3` |  |
| train | Armenian-ArmTDP | `hy_armtdp-ud-train.conllu` | 4,352 | 82,871 | CC BY-SA 4.0 | `ff3a446b751d56668a9e40ce8490d297b62fc31164ccf94e8baea10f87697987` |  |
| dev | Armenian-ArmTDP | `hy_armtdp-ud-dev.conllu` | 575 | 10,755 | CC BY-SA 4.0 | `968d15cd466d4601dea675b135f4ba7b72cd94e30c67b408c26f69fb463f8788` |  |
| eval | Armenian-ArmTDP | `hy_armtdp-ud-test.conllu` | 600 | 10,667 | CC BY-SA 4.0 | `1d8e20312013ddf277e789ec961104fd3de981cd1fcb8087796f22b43ae354a6` | 4 test texts also in train |
| train | Armenian-BSUT | `hy_bsut-ud-train.conllu` | 1,451 | 25,644 | CC BY-SA 4.0 | `56a9ca8f8d86c586f1c5e8b266fc09c276a51fbae051ec9d141cb5b931ca40e2` |  |
| dev | Armenian-BSUT | `hy_bsut-ud-dev.conllu` | 479 | 10,312 | CC BY-SA 4.0 | `bcacd482998ddb8568967b6966495103dea91c09a3502313db7ac271cbf5e2e0` |  |
| eval | Armenian-BSUT | `hy_bsut-ud-test.conllu` | 593 | 10,212 | CC BY-SA 4.0 | `21f33b8e31c84001eb1c8e16dc963650db5f11c121f6370ccd25438f69714269` |  |
| train | Assamese-AiW | `as_aiw-ud-train.conllu` | 51 | 597 | CC BY-SA 4.0 | `aa566d257a0374bf840f13d59997df95a2d7c6df944e467e5556f6efee8fc4aa` |  |
| dev | Assamese-AiW | `as_aiw-ud-dev.conllu` | 14 | 189 | CC BY-SA 4.0 | `a5a19d479e6c250fdcdadfc6901f652f9eb943ece5dd152677e7bcc201e87e77` |  |
| eval | Assamese-AiW | `as_aiw-ud-test.conllu` | 8 | 85 | CC BY-SA 4.0 | `b758a12d52622e714b62b2778a854b1f1e32b63f70badbae1b076fc6136dcc4a` |  |
| eval | Assyrian-AS | `aii_as-ud-test.conllu` | 57 | 453 | CC BY-SA 4.0 | `3fc0829e9ae9a7314bb7c2d5f93a4c7f7daa899c5d47aaba71890e31fcdc6e0b` |  |
| eval | Azerbaijani-TueCL | `az_tuecl-ud-test.conllu` | 148 | 912 | CC BY-SA 4.0 | `8b315f8b065f094e38482465706ba44a9caf1b082fc8bbc94908f6fdeee8c3b5` |  |
| eval | Bambara-CRB | `bm_crb-ud-test.conllu` | 1,026 | 13,823 | CC BY-SA 4.0 | `b0c220c66fb6b04c715e26ad8e9a48b16a024e9814d3384b99121de77aff6fe9` |  |
| eval | Bavarian-MaiBaam | `bar_maibaam-ud-test.conllu` | 1,070 | 15,036 | CC BY-SA 4.0 | `95c892ce4a6a05abe1fd901ffe01b8f653e04d5986897355b6c5b1d238851a6c` |  |
| eval | Beja-Autogramm | `bej_autogramm-ud-test.conllu` | 763 | 11,948 | CC BY-SA 4.0 | `1828c4ca8093ce2eed8790a23e7c401372d764d9071fa8e046386d9c8a6354c8` |  |
| eval | Bengali-BRU | `bn_bru-ud-test.conllu` | 56 | 320 | CC BY-SA 4.0 | `8030755486fed8ec345138092b26cab8bae26744c9271037022f44774bdba9a3` |  |
| eval | Bhojpuri-BHTB | `bho_bhtb-ud-test.conllu` | 357 | 6,665 | CC BY-SA 4.0 | `586cdabb8474e4f58ac97a7ff1fb8646c6d1fd0a4d86994ead7a720f9524b393` |  |
| eval | Bokota-ChibErgIS | `sab_chibergis-ud-test.conllu` | 406 | 2,713 | CC BY-SA 4.0 | `24499104f0423ee828b21d2eeb68a9d42c83f08b7275182fe7e569f890524541` |  |
| train | Bororo-BDT | `bor_bdt-ud-train.conllu` | 17,107 | 128,418 | CC BY-SA 4.0 | `e985bea81b1d5d95235797839c16667093daf60e94968623b3acdfe84257a550` |  |
| dev | Bororo-BDT | `bor_bdt-ud-dev.conllu` | 2,138 | 15,678 | CC BY-SA 4.0 | `f19c563f951f4c8b7d644bf4d6cdfd580b6cadf5b45bd2cad1d5f74871ca0a84` |  |
| eval | Bororo-BDT | `bor_bdt-ud-test.conllu` | 2,139 | 16,260 | CC BY-SA 4.0 | `c102da808d9a76f227d899a66588fdb2b2441a23cbcaea0b72c890687a9482c6` | 151 test texts also in train |
| eval | Brahui-Kholum | `brh_kholum-ud-test.conllu` | 64 | 819 | CC BY-SA 4.0 | `92f895953a4662c01c27ad9f48cabefaeee79ed39fdd0b858a42c0d70ccaac16` |  |
| eval | Breton-KEB | `br_keb-ud-test.conllu` | 884 | 10,006 | CC BY-SA 4.0 | `f6eec6ee7f844a92df0023314202655c0a5a6341f9c1708d40c0d3beebcbeff9` |  |
| train | Buryat-BDT | `bxr_bdt-ud-train.conllu` | 20 | 158 | CC BY-SA 4.0 | `5358bb9f3ec980174ba8e5ca00e721f729e63cebeeadd65328ad9578acfd6e80` |  |
| eval | Buryat-BDT | `bxr_bdt-ud-test.conllu` | 907 | 10,029 | CC BY-SA 4.0 | `550a7722dd83f4c482602ed348ce5765df23b47d74f51eb3d5479533403c1d36` |  |
| eval | Cantonese-HK | `yue_hk-ud-test.conllu` | 1,004 | 13,918 | CC BY-SA 4.0 | `cbd843a195d0db4cdafbf6fcafb7b7b559afea750411006f4728311e70cc4e2a` |  |
| eval | Cappadocian-AMGiC | `cpg_amgic-ud-test.conllu` | 70 | 820 | CC BY-SA 4.0 | `c78e42f40b7b2ab6e4bae44021afe38c1bfd5b25c48a993b18d12fff1312d3a3` |  |
| eval | Cappadocian-TueCL | `cpg_tuecl-ud-test.conllu` | 350 | 4,118 | CC BY-SA 4.0 | `808c5f26a016bc9fc92eb34242ed248ec5b1529e17baf52a64d7cdff77d62482` |  |
| train | Catalan-AnCora | `ca_ancora-ud-train.conllu` | 13,123 | 429,579 | CC BY 4.0 | `edfeabcddf81e65ed3b647ed31117a7739df5e10336c3e707ff68217b0ef18c5` |  |
| dev | Catalan-AnCora | `ca_ancora-ud-dev.conllu` | 1,709 | 58,073 | CC BY 4.0 | `29fe512fdd86724d2c9f24604001f2ca021dd64b06c9fd0a9ad9ee7a7de05e01` |  |
| eval | Catalan-AnCora | `ca_ancora-ud-test.conllu` | 1,846 | 59,610 | CC BY 4.0 | `7cd23807c1f8b090959b82fcab566c6312fc048c29bc9db5ff0bf794c9b98633` |  |
| eval | Cebuano-GJA | `ceb_gja-ud-test.conllu` | 198 | 1,394 | CC BY-SA 4.0 | `b04bff0dffbe01145124741050b7ff972e3c2805075125d6e5ee13b8cead1b1d` |  |
| train | Central_Kurdish-Mukri | `ckb_mukri-ud-train.conllu` | 92 | 539 | CC BY-SA 4.0 | `51f782d3306584ed6611343e6027048ee4c071618a43f26fa6f0c028a5ec46ee` |  |
| dev | Central_Kurdish-Mukri | `ckb_mukri-ud-dev.conllu` | 23 | 104 | CC BY-SA 4.0 | `42c1b3cf910d10983906947d7f3c1efeeddc0f789c89275521445c0c0956b993` |  |
| eval | Central_Kurdish-Mukri | `ckb_mukri-ud-test.conllu` | 23 | 122 | CC BY-SA 4.0 | `614fb68983ec38598cdce713c223e5bdfcd83e6c81ca97c3f5382846ca8bb081` |  |
| eval | Chukchi-HSE | `ckt_hse-ud-test.conllu` | 1,004 | 6,124 | CC BY-SA 4.0 | `ab8ea2026d0e7347b68d0842c4a579405fd968ab17d63fb5a8c2577388040982` |  |
| train | Classical_Chinese-Kyoto | `lzh_kyoto-ud-train.conllu` | 74,609 | 374,560 | PD | `ce1202b74d176440a8a94d959e7ccd22496de49c80ca70bcda283bbc49191b68` |  |
| dev | Classical_Chinese-Kyoto | `lzh_kyoto-ud-dev.conllu` | 6,102 | 31,043 | PD | `b67614202e30006f9cada1abccbd8f04371f50bd9821791db4c3932a3fd6b3a7` |  |
| eval | Classical_Chinese-Kyoto | `lzh_kyoto-ud-test.conllu` | 5,528 | 27,566 | PD | `e492ba5f5054ee560c33197e1681a5c18c3f21adff7dca82be3ed4af09cbf1e5` | 275 test texts also in train |
| eval | Classical_Chinese-TueCL | `lzh_tuecl-ud-test.conllu` | 100 | 648 | CC BY-SA 4.0 | `596d6b22837e0e5bc72471dc6d10d80029da276c4e3298d89bfd4a1c1727fa2a` |  |
| train | Coptic-Bohairic | `cop_bohairic-ud-train.conllu` | 477 | 16,523 | CC BY 4.0 | `4ccf9fbf03ebe3bb0e430f4b3005379343006997840825d4582f54f0047f6695` |  |
| dev | Coptic-Bohairic | `cop_bohairic-ud-dev.conllu` | 166 | 5,139 | CC BY 4.0 | `858a4b744596d9e6518ac42b0f8da7094544a5abb8073387cbeb14338b85cb68` |  |
| eval | Coptic-Bohairic | `cop_bohairic-ud-test.conllu` | 358 | 11,061 | CC BY 4.0 | `fe42a33fa03c665bbb2dce47955921893405cf1fe969b07edfd47813ad01dbbc` |  |
| train | Coptic-Scriptorium | `cop_scriptorium-ud-train.conllu` | 1,467 | 37,639 | CC BY 4.0 | `aa6fa27ce7e25b9cbb8afaee4a3a9b2c4042d1190d11d54c3ca2483dd4078bac` |  |
| dev | Coptic-Scriptorium | `cop_scriptorium-ud-dev.conllu` | 380 | 10,963 | CC BY 4.0 | `ce55dd58495ac792f7aaf3b2fd32d6037a653d17818f8c6c94533eb7bb26b0ef` |  |
| eval | Coptic-Scriptorium | `cop_scriptorium-ud-test.conllu` | 405 | 10,372 | CC BY 4.0 | `13ad24c3726502b4e2699b185cda1df3295a5f4962da9f0dd5c6fc642d4ad222` |  |
| train | Croatian-SET | `hr_set-ud-train.conllu` | 6,914 | 152,857 | CC BY-SA 4.0 | `c52be3eedff5bc7b5e2175a8e57f64853f344a0c19870c52d914c7ad238ad8cb` |  |
| dev | Croatian-SET | `hr_set-ud-dev.conllu` | 960 | 22,292 | CC BY-SA 4.0 | `2e35eacb1790428860bb0ab4d7ff2b9537146d53093e72e4a4ce4e7f512ff2d4` |  |
| eval | Croatian-SET | `hr_set-ud-test.conllu` | 1,136 | 24,260 | CC BY-SA 4.0 | `9d766b171f4fa06c8e6da5d1d1712cf763d80d294f45b92ae96ef4c6e6c649e5` | 1 test texts also in train |
| train | Czech-CAC | `cs_cac-ud-train.conllu` | 23,478 | 472,609 | CC BY-SA 4.0 | `b089aeebc4a2a32b0cc07767e2704dc2ea61410970c45f0ff8b01e223c12d154` |  |
| dev | Czech-CAC | `cs_cac-ud-dev.conllu` | 603 | 10,912 | CC BY-SA 4.0 | `06ab3b41d0192641a063048c58f27ecc10640b61c0bae3112dd778ea1f4201f7` |  |
| eval | Czech-CAC | `cs_cac-ud-test.conllu` | 628 | 10,862 | CC BY-SA 4.0 | `71c03ef1ab14451e294bbc0d7896ea4f8a3faf26790fc2f04cc473e62e43162c` | 1 test texts also in train |
| train | Czech-CLTT | `cs_cltt-ud-train.conllu` | 467 | 13,894 | CC BY-SA 4.0 | `6302be79c2f79f03604612f5ec8cec2d912f5ce9134c212f818384a58f6f7ee7` |  |
| dev | Czech-CLTT | `cs_cltt-ud-dev.conllu` | 316 | 10,710 | CC BY-SA 4.0 | `ad067342247f90fa70b973a7f7c6e00f9bbd85ee1b68ddbbafb197bbc388c9ab` |  |
| eval | Czech-CLTT | `cs_cltt-ud-test.conllu` | 338 | 11,409 | CC BY-SA 4.0 | `22c4c7a972d685b9d2408b39ede626f23c39be6756c625462020dbf12223f258` | 26 test texts also in train |
| eval | Czech-PUD | `cs_pud-ud-test.conllu` | 1,000 | 18,609 | CC BY-SA 4.0 | `af365749e569cc9daeeebee916dd33161e5c23b278077553f6ffbf4f82c5b652` |  |
| eval | Czech-Poetry | `cs_poetry-ud-test.conllu` | 297 | 6,289 | CC BY-SA 4.0 | `6005155e5c2de75894e3cd055e7031f9dfa0b7ab23d73c740d5d8e67b3535d73` |  |
| train | Danish-DDT | `da_ddt-ud-train.conllu` | 4,383 | 80,378 | CC BY-SA 4.0 | `65730e35b07d1e944ee9ae1d344b5c84746cf0aeadf5faf0550059b4ded86fb6` |  |
| dev | Danish-DDT | `da_ddt-ud-dev.conllu` | 564 | 10,332 | CC BY-SA 4.0 | `d714b22776ad60fbdb6e4ffc15a5c3ffc162705617567d3cf2b09cc6b51e800a` |  |
| eval | Danish-DDT | `da_ddt-ud-test.conllu` | 565 | 10,023 | CC BY-SA 4.0 | `2232ee43dcd35dba32b476534d230edf1aa26d4bd830813f452fd829c276f737` | 1 test texts also in train |
| train | Egyptian-PC | `egy_pc-ud-train.conllu` | 1,619 | 19,486 | CC BY-SA 4.0 | `27b6be6a7160b94c867cc9680d5b173857581f6bfa30ba2bdb589abff9c9ba27` |  |
| dev | Egyptian-PC | `egy_pc-ud-dev.conllu` | 230 | 3,167 | CC BY-SA 4.0 | `5e207323fdb111c2b5badf135d562812de448e6b96962c9093dec4846fca9399` |  |
| eval | Egyptian-PC | `egy_pc-ud-test.conllu` | 1,240 | 11,581 | CC BY-SA 4.0 | `9b04cd3a15772b604e822961297ba2b60ac627d774530fcb9fefb0d151c464af` | 6 test texts also in train |
| train | Erzya-JR | `myv_jr-ud-train.conllu` | 1,217 | 10,294 | CC BY-SA 4.0 | `99f7244a06044b21ada930b1f3a923af0b5bfd24d4badee3ab400aaa77e201ec` |  |
| eval | Erzya-JR | `myv_jr-ud-test.conllu` | 935 | 10,515 | CC BY-SA 4.0 | `3757d7d7fade34e36551e22c047a490cf65799b6517a9c1da4f10effa89c4c75` | 1 test texts also in train |
| eval | Esperanto-Cairo | `eo_cairo-ud-test.conllu` | 20 | 177 | CC BY-SA 4.0 | `7591318b925ac2d78eb0678d5d6eef7466347c429c02634ef7db9836ef673ed7` |  |
| eval | Esperanto-Prago | `eo_prago-ud-test.conllu` | 131 | 3,166 | CC BY-SA 4.0 | `d91a9bbbce1fdd1ee4cfda66e90bbd3454b83180a653b195cde3613152110795` |  |
| train | Faroese-FarPaHC | `fo_farpahc-ud-train.conllu` | 1,020 | 23,094 | CC BY-SA 4.0 | `44994da63f8e94ff97663b1749ccaf9006422345d25fe976b0a155cacb407fa1` |  |
| dev | Faroese-FarPaHC | `fo_farpahc-ud-dev.conllu` | 300 | 8,746 | CC BY-SA 4.0 | `afc127c6e33a510d66b7d1ed49283e27b6822aa728073907bd50edbaccaffb5a` |  |
| eval | Faroese-FarPaHC | `fo_farpahc-ud-test.conllu` | 301 | 8,644 | CC BY-SA 4.0 | `ad601746b37d6940761af31dc0df42a929c106ea68504e2705fb3a04f7743cc0` |  |
| eval | Faroese-OFT | `fo_oft-ud-test.conllu` | 1,208 | 10,002 | CC BY-SA 4.0 | `3fa80518bc02b034231390467631e40389a721e5e9d9774cf84c692e15419dc1` |  |
| train | Finnish-FTB | `fi_ftb-ud-train.conllu` | 14,981 | 127,613 | CC BY 4.0 | `cc8c5290f26d16219c97f587a68ccfd86e2f3a4232998eb0d523642a316a1ce9` |  |
| dev | Finnish-FTB | `fi_ftb-ud-dev.conllu` | 1,875 | 15,726 | CC BY 4.0 | `676c3e0d09f2ee6c23ab8e8fef4dad86e56693beb742c1a9b2761e36bbac5c4c` |  |
| eval | Finnish-FTB | `fi_ftb-ud-test.conllu` | 1,867 | 16,286 | CC BY 4.0 | `f7b738e86bf79c4c2db18ad8141649b175846d9f57c452ac1ca8671cc34f954e` |  |
| eval | Finnish-PUD | `fi_pud-ud-test.conllu` | 1,000 | 15,813 | CC BY-SA 4.0 | `303c3d03795ca8ddb3340d4d7c2ac28d2aaececa8f9ba962f0440f5c2c2412a0` |  |
| train | Finnish-TDT | `fi_tdt-ud-train.conllu` | 12,217 | 162,815 | CC BY-SA 4.0 | `07217d128d3752e61f1249c1e57de402ca17b31d2fba479d4f857ff78e28b40d` |  |
| dev | Finnish-TDT | `fi_tdt-ud-dev.conllu` | 1,364 | 18,308 | CC BY-SA 4.0 | `e4c0f4d3afc4a66b71a64756d62f900d1e4c3b58d9bc548dea330f12f15d89b0` |  |
| eval | Finnish-TDT | `fi_tdt-ud-test.conllu` | 1,555 | 21,070 | CC BY-SA 4.0 | `3317528f16ab74333dce44051971e1ed0d115c8ffa1359a20f3bddb8e63498ee` | 18 test texts also in train |
| eval | Frisian_Dutch-Fame | `qfn_fame-ud-test.conllu` | 400 | 3,729 | CC BY-SA 4.0 | `f8bd770bb4d99866a9a563720ca8b6a9b03c27a98b714aed3106dc1fa9f55d9a` |  |
| eval | Galician-PUD | `gl_pud-ud-test.conllu` | 1,000 | 23,510 | CC BY-SA 4.0 | `4fc7406056b0445c2e0f368b0f9be507c3b096c85e2b5dfd108bb1e052bc7d41` |  |
| train | Galician-TreeGal | `gl_treegal-ud-train.conllu` | 600 | 15,436 | LGPL-LR | `2e469c3bd17a710bc1913ba325691a3a5ba9417f1003f9e3a42c91e15e603f5b` |  |
| eval | Galician-TreeGal | `gl_treegal-ud-test.conllu` | 400 | 10,112 | LGPL-LR | `685d6f2e8d4fc819247c9c1fafdcca310f9796522c22672733690fd751673817` |  |
| train | Georgian-GLC | `ka_glc-ud-train.conllu` | 2,214 | 40,530 | CC BY-SA 4.0 | `12a620fe47e4ca1b1af6606b26e2ddd2fb0fe7373c6e52e8fcaf1bf33badcccb` |  |
| dev | Georgian-GLC | `ka_glc-ud-dev.conllu` | 469 | 9,611 | CC BY-SA 4.0 | `09071606f369c8481b4cf2b50249b656026e7aa368c6b5365011ce5cc0780809` |  |
| eval | Georgian-GLC | `ka_glc-ud-test.conllu` | 481 | 10,041 | CC BY-SA 4.0 | `676354f37d64841380096ff89d424e22ab48876a59548ce808bc15aef1f9a7c3` | 5 test texts also in train |
| train | Georgian-GNC | `ka_gnc-ud-train.conllu` | 918 | 11,516 | CC BY-SA 4.0 | `1aef7b0b902a8c86fb67b73153b6571c8372dc976a29a8f7d5bc7420fbe6b92a` |  |
| dev | Georgian-GNC | `ka_gnc-ud-dev.conllu` | 130 | 1,483 | CC BY-SA 4.0 | `af06b26f8e504143b366a0c0457c47ff1b2f21829e1a0984edad9b66c8a4cdc6` |  |
| eval | Georgian-GNC | `ka_gnc-ud-test.conllu` | 830 | 10,426 | CC BY-SA 4.0 | `732d4efea4266ddff88b06ff8ddb0bbb7fe7badc5b58006611e3d691cfa57dea` |  |
| eval | Gheg-GPS | `aln_gps-ud-test.conllu` | 966 | 15,990 | CC BY-SA 4.0 | `080a83e775d2b1fe6f62339ae98e0550f3549181b633efbb26af93f470b8b285` |  |
| eval | Gorontalo-BungoLoLombi | `gor_bungololombi-ud-test.conllu` | 39 | 205 | CC BY-SA 4.0 | `ca670ff09e78f4964637defb5bc95b9f3ef87e3748b886e2a4889385ed45a06a` |  |
| train | Greek-Cretan | `el_cretan-ud-train.conllu` | 180 | 2,792 | CC BY-SA 4.0 | `7c2ddf0a20ac643f72999426b4591ea5856ccfc04f3ab4d6d7d28fc56497701d` |  |
| dev | Greek-Cretan | `el_cretan-ud-dev.conllu` | 60 | 1,066 | CC BY-SA 4.0 | `c6c8109731ba3c85c4813b02f7ee7e39231ccfac508138978d4b6cc2e3b4bbbe` |  |
| eval | Greek-Cretan | `el_cretan-ud-test.conllu` | 30 | 493 | CC BY-SA 4.0 | `b86298c7e0653e1e441043539ec7245c109b6475869619b30e0e6140a790959c` |  |
| train | Greek-GUD | `el_gud-ud-train.conllu` | 1,071 | 14,859 | CC BY-SA 4.0 | `5cc9ca2876872d87e8d5f6ee2bcd40151617f6aa92a406a94474fdfc341e243f` |  |
| eval | Greek-GUD | `el_gud-ud-test.conllu` | 736 | 10,634 | CC BY-SA 4.0 | `1810904eb8fb9711f5c12eb0da63dc440fc42e3d41bbd24be194b59e07d19a69` |  |
| eval | Greek-Lesbian | `el_lesbian-ud-test.conllu` | 625 | 6,840 | CC BY-SA 4.0 | `73f684206f15390e6ed94eaeb8d38d71c57eaf5cd5d967e46c5fc30b2a510974` |  |
| train | Greek-Messinian | `el_messinian-ud-train.conllu` | 60 | 501 | CC BY-SA 4.0 | `80fce9286d4a2f1d7ab9c74210913864a11686f5161288eeaaac5a71389c19b8` |  |
| dev | Greek-Messinian | `el_messinian-ud-dev.conllu` | 20 | 156 | CC BY-SA 4.0 | `249c8148920e4d8360da0715b3b2008893f2ffa198bd63841f8d717cb52bcc43` |  |
| eval | Greek-Messinian | `el_messinian-ud-test.conllu` | 30 | 242 | CC BY-SA 4.0 | `d29a102fedee985581e9eadda1f33b441267d56a7a7f1bc407dd6241a5d3ce36` |  |
| eval | Guajajara-TuDeT | `gub_tudet-ud-test.conllu` | 1,182 | 9,160 | CC BY-SA 4.0 | `199e18933f9cd0a5c9fcc64ef4ab7d6fa0aad3eadb87762859a3b3b11d47d1f1` |  |
| eval | Guarani-OldTuDeT | `gn_oldtudet-ud-test.conllu` | 59 | 217 | CC BY-SA 4.0 | `eb34d78210fd90e6a9365dd96e1a393192d333a005f5846613f01c58acb36345` |  |
| eval | Gujarati-GujTB | `gu_gujtb-ud-test.conllu` | 187 | 1,885 | CC BY-SA 4.0 | `7c66318f76301f4bfc3336386679fc909371a9e15f78002b6b793a6a789228a9` |  |
| eval | Gwichin-TueCL | `gwi_tuecl-ud-test.conllu` | 313 | 1,008 | CC BY-SA 4.0 | `ae583b3418f585b09e80b995e1552ec529442e6ab4c28d517bcbc6d944265a78` |  |
| train | Haitian_Creole-Adolphe | `ht_adolphe-ud-train.conllu` | 2,605 | 55,527 | CC BY-SA 4.0 | `4aa3b19d7d08ac78fcd0208d481daf18bcd019b15e04367d4b319f844f13a390` |  |
| dev | Haitian_Creole-Adolphe | `ht_adolphe-ud-dev.conllu` | 265 | 6,186 | CC BY-SA 4.0 | `22830babd73c3738f683c1b6bbfddbbf76f901e0ab6bfcf0f584ed420229126a` |  |
| eval | Haitian_Creole-Adolphe | `ht_adolphe-ud-test.conllu` | 444 | 10,021 | CC BY-SA 4.0 | `bade643a6f41417fce5e52101e1ecaa2090ad97d59e0eb88fbe276d2c4c333bd` | 6 test texts also in train |
| eval | Haitian_Creole-Autogramm | `ht_autogramm-ud-test.conllu` | 144 | 3,279 | CC BY-SA 4.0 | `c3ffe9169c10d22d1c29246c3a16ee1c20db7e912642e3d7a9271c198fd20921` |  |
| eval | Hausa-EasternAutogramm | `ha_easternautogramm-ud-test.conllu` | 335 | 9,485 | CC BY-SA 4.0 | `25c2ee4d7ddaa64b15e3f179ac81889bab9c65dd88684731db4cba9ae49cea9c` |  |
| eval | Hausa-NorthernAutogramm | `ha_northernautogramm-ud-test.conllu` | 1,305 | 15,424 | CC BY-SA 4.0 | `271c213d3728ba21988329f7d1fbc87be9522463cc3196a760b6a8751e6b9708` |  |
| eval | Hausa-SouthernAutogramm | `ha_southernautogramm-ud-test.conllu` | 1,927 | 14,398 | CC BY-SA 4.0 | `9a700e3f7ed889025543177f7080d18963dff7c6f41641d66e7d28a8771772f0` |  |
| eval | Hausa-WesternAutogramm | `ha_westernautogramm-ud-test.conllu` | 775 | 13,903 | CC BY-SA 4.0 | `46577042a34f8757b4898ce9a8a3b6d5fcbc3d53910eaf6ea2289b2427f0f877` |  |
| train | Hebrew-IAHLTknesset | `he_iahltknesset-ud-train.conllu` | 2,362 | 54,209 | CC BY-SA 4.0 | `3b0413a210f776c5838ff29f504d91afff89059675fbbe2153a1667f11fbe10f` |  |
| dev | Hebrew-IAHLTknesset | `he_iahltknesset-ud-dev.conllu` | 253 | 6,441 | CC BY-SA 4.0 | `bea61498172719a6562a1a93e17c6c379ca44538b9337b5edb44ad2e86a6ecc4` |  |
| eval | Hebrew-IAHLTknesset | `he_iahltknesset-ud-test.conllu` | 268 | 6,357 | CC BY-SA 4.0 | `9f646c1af22f58ca404be2be92481b3395683dfcb6d8cc31dbb4a530a6d3bd55` | 3 test texts also in train |
| train | Hebrew-IAHLTwiki | `he_iahltwiki-ud-train.conllu` | 4,298 | 120,832 | CC BY-SA 4.0 | `29b143ddbbad01d119c721868de534fbad0a56da3fa0dbbc40d55eac64c71b9e` |  |
| dev | Hebrew-IAHLTwiki | `he_iahltwiki-ud-dev.conllu` | 348 | 9,395 | CC BY-SA 4.0 | `88b1182a41a51bb34912782b1568c6f28d5290e41cab2dcf0cb001449c929ed8` |  |
| eval | Hebrew-IAHLTwiki | `he_iahltwiki-ud-test.conllu` | 393 | 10,734 | CC BY-SA 4.0 | `57af21391a7e031d1581256fa5dc3fd7b97de81356ff782e613705ae5ac52e07` |  |
| eval | Hebrew-PostRab | `he_postrab-ud-test.conllu` | 296 | 8,029 | CC BY-SA 4.0 | `d95be2095689d7f3e300fb2ae40fcba51bc83c02007a14697280bbb8d94f315e` |  |
| eval | Highland_Puebla_Nahuatl-ITML | `azz_itml-ud-test.conllu` | 1,260 | 10,088 | CC BY-SA 4.0 | `d9dbf6e05c885557b4303d7c470d343a7befd5bef0d9bd66aba4ae05dc88fc7f` |  |
| eval | Hittite-HitTB | `hit_hittb-ud-test.conllu` | 136 | 1,309 | CC BY-SA 4.0 | `a55968a8a694503431cfceebf06fad3d2de6a509cc32f92f53710b58fa5fd019` |  |
| train | Icelandic-GC | `is_gc-ud-train.conllu` | 3,960 | 78,568 | CC BY-SA 4.0 | `622205dfee16f16b3cc7cdd2e62f5c6d52c68ffcbe2733ec627c85cc56258fd9` |  |
| dev | Icelandic-GC | `is_gc-ud-dev.conllu` | 500 | 10,694 | CC BY-SA 4.0 | `82bc5d1acabd7bea13503675ae4b4c2b76aa2167573b3509889db00fd1ce984b` |  |
| eval | Icelandic-GC | `is_gc-ud-test.conllu` | 540 | 10,349 | CC BY-SA 4.0 | `d7593fba5a12600417e392aafdb81cbe1ff588f86f5ed1ad0cf0f17b4bd0e0da` |  |
| train | Icelandic-IcePaHC | `is_icepahc-ud-train.conllu` | 34,007 | 705,747 | CC BY-SA 4.0 | `8fd17bd56309bdd2d3ecb3945fb59fe1961ddc4826baebef2c24f1b81212911f` |  |
| dev | Icelandic-IcePaHC | `is_icepahc-ud-dev.conllu` | 4,865 | 139,604 | CC BY-SA 4.0 | `1ea62344c94791c91f974bc243fab0b08a2c20febafa0b2a5146c9ad342ef68d` |  |
| eval | Icelandic-IcePaHC | `is_icepahc-ud-test.conllu` | 5,157 | 139,695 | CC BY-SA 4.0 | `2b30b3d504f344ffc96b3e9433020b38b8305c301312a3aaf1710a5d88186b1f` | 6 test texts also in train |
| train | Icelandic-Modern | `is_modern-ud-train.conllu` | 2,703 | 61,816 | CC BY-SA 4.0 | `dd8f3f9aeaef70a46b392392739375a25ea1638550087c34081bfcca43a8484e` |  |
| dev | Icelandic-Modern | `is_modern-ud-dev.conllu` | 394 | 8,431 | CC BY-SA 4.0 | `69f65bfeec6467af1875ef2520d9b2b2749815dcc11e6ddadaa3eb3c31c4f51b` |  |
| eval | Icelandic-Modern | `is_modern-ud-test.conllu` | 438 | 10,147 | CC BY-SA 4.0 | `d746fdc1c4ea1aea83e9136293aca2b57028e8fbb43229d3d57ba5cb4fcbc412` | 9 test texts also in train |
| eval | Icelandic-PUD | `is_pud-ud-test.conllu` | 1,000 | 18,833 | CC BY-SA 4.0 | `a024997a765ba1c5350f999a421b7faef43f7ebc0412b1378305ab7b91a8df52` |  |
| eval | Ika-ChibErgIS | `arh_chibergis-ud-test.conllu` | 628 | 5,307 | CC BY-SA 4.0 | `689ad44d92f5dfd2e39872179c7aa370df1691135ba1c24d075e285c696cdd2e` |  |
| train | Indonesian-CSUI | `id_csui-ud-train.conllu` | 656 | 18,222 | CC BY-SA 4.0 | `0b91a404b4d96ba7f8b21c087a225798d1671b00b3d6126eec9e0b1895a7ca24` |  |
| eval | Indonesian-CSUI | `id_csui-ud-test.conllu` | 374 | 10,041 | CC BY-SA 4.0 | `93800d74e254437e0219240770cdc18f99003f2993b7dae717358b524c8f474d` | 1 test texts also in train |
| train | Indonesian-GSD | `id_gsd-ud-train.conllu` | 4,482 | 97,602 | CC BY-SA 4.0 | `0bbacd84937cde4ec14692d10481c316ef86f607f958900a27b1956da91a27f3` |  |
| dev | Indonesian-GSD | `id_gsd-ud-dev.conllu` | 559 | 12,661 | CC BY-SA 4.0 | `56d4ab12267317d33511326da9fcd5b7eff641435c3b6365401937a33ce05e4b` |  |
| eval | Indonesian-GSD | `id_gsd-ud-test.conllu` | 557 | 11,756 | CC BY-SA 4.0 | `155171c985bc04b12aa9dfa5704fa686a9fb8182cd2d1f42c3ede828e1502e70` | 3 test texts also in train |
| eval | Indonesian-PUD | `id_pud-ud-test.conllu` | 1,000 | 19,446 | CC BY-SA 3.0 | `07efad4d64863ab442aef820afdb34c269f80366c6efb583225f943e5baf88b2` |  |
| eval | Irish-Cadhan | `ga_cadhan-ud-test.conllu` | 193 | 4,783 | CC BY-SA 4.0 | `0acb2f14db45c47149aaaed41a719813871ca62f54b6dea66b839671b515c48c` |  |
| train | Irish-IDT | `ga_idt-ud-train.conllu` | 4,005 | 95,881 | CC BY-SA 3.0 | `4d3bc888ae5ca154ca13ce2605eb13e0460890f0dd194968042676291e0bcea8` |  |
| dev | Irish-IDT | `ga_idt-ud-dev.conllu` | 451 | 10,000 | CC BY-SA 3.0 | `82909526e8090c650074957b1237cf16c401e871c6ca70a9bfecda390a991617` |  |
| eval | Irish-IDT | `ga_idt-ud-test.conllu` | 454 | 10,109 | CC BY-SA 3.0 | `01aa878295cc5620e1843f27657112910d2ca402640c9afafe3ed65338610a98` | 2 test texts also in train |
| train | Japanese-GSD | `ja_gsd-ud-train.conllu` | 7,050 | 168,333 | CC BY-SA 4.0 | `99f67fd88257e7cfe81d81c4b8ee98aff85fc22bb525d907475a2856c8cfa9f3` |  |
| dev | Japanese-GSD | `ja_gsd-ud-dev.conllu` | 507 | 12,287 | CC BY-SA 4.0 | `18d266e29336ef619787a928d89d20f2e64188be70d5b37829e3aadc5bb6b841` |  |
| eval | Japanese-GSD | `ja_gsd-ud-test.conllu` | 543 | 13,034 | CC BY-SA 4.0 | `5f50ee6ed45c7ebda3787e593eaf5e9a225f25e53f6b4b32df778031862c843f` |  |
| train | Japanese-GSDLUW | `ja_gsdluw-ud-train.conllu` | 7,050 | 130,284 | CC BY-SA 4.0 | `1f62d53dfc31610c0f4d2b49a2990f9086eb9758d81f6d3e606dab086c901baa` |  |
| dev | Japanese-GSDLUW | `ja_gsdluw-ud-dev.conllu` | 507 | 9,531 | CC BY-SA 4.0 | `6bd2417cf8b716f0705bf9bd69e6e456d4dd507e3f6fb3e2388fcf64f6fabb0c` |  |
| eval | Japanese-GSDLUW | `ja_gsdluw-ud-test.conllu` | 543 | 10,428 | CC BY-SA 4.0 | `ece9979804365bac228003e859d976e48634f2f9b71dc771a2f07079e8e65659` |  |
| eval | Japanese-PUD | `ja_pud-ud-test.conllu` | 1,000 | 28,788 | CC BY-SA 3.0 | `7ac51383189bdf5513102f9f8ac7ee1b967d5449f8c2dc1a9108fa21a8f1a688` |  |
| eval | Japanese-PUDLUW | `ja_pudluw-ud-test.conllu` | 1,000 | 22,910 | CC BY-SA 3.0 | `f4cc4fa04184dba6e8c06263db2aa9db654abede243053adb63eb9faa8e4180c` |  |
| eval | Javanese-CSUI | `jv_csui-ud-test.conllu` | 1,000 | 14,343 | CC BY-SA 4.0 | `6dd63637c8a679de75524b98a95091f915e920a2149df678a6baa01fb6994967` |  |
| eval | Kaapor-TuDeT | `urb_tudet-ud-test.conllu` | 83 | 366 | CC BY-SA 4.0 | `cb169ddc3605a69b0bacdf907f45145da98f316278091f325cc5a5aa582638e7` |  |
| eval | Kangri-KDTB | `xnr_kdtb-ud-test.conllu` | 288 | 2,514 | CC BY-SA 4.0 | `f3a7657175560cf0c621811c9c7186731b15997ba19dc68d1a7d946f520faf6c` |  |
| eval | Karelian-KKPP | `krl_kkpp-ud-test.conllu` | 228 | 3,094 | CC BY-SA 4.0 | `df72612b74d93dad19590855f59017547c4a2faafce1f060c455f66213da0520` |  |
| eval | Karo-TuDeT | `arr_tudet-ud-test.conllu` | 674 | 2,319 | CC BY-SA 4.0 | `67af0c64fa401b41aa97ce7bb03caaedf5e3babdfa0dc8ba51a8602a1159db8b` |  |
| train | Kazakh-KTB | `kk_ktb-ud-train.conllu` | 31 | 529 | CC BY-SA 4.0 | `6297597914a3f319db573573f927a6577c702f5fa00dcd653162aab46026aeaa` |  |
| eval | Kazakh-KTB | `kk_ktb-ud-test.conllu` | 1,047 | 10,007 | CC BY-SA 4.0 | `1100846ee50c2f970562a21dca8415a381c470ea06bdb8496ae213d59c3a3c4e` |  |
| eval | Kiche-IU | `quc_iu-ud-test.conllu` | 1,435 | 10,013 | CC BY-SA 4.0 | `b95afdd239bcb05520b01784fc20d115682a6a9d2d8e9b99aecd1daebfe13d19` |  |
| train | Komi_Permyak-UH | `koi_uh-ud-train.conllu` | 34 | 248 | CC BY-SA 4.0 | `16a935495e7ec2726b3a104a849457ebd1bfea11a61c5937a76973697db1bcd1` |  |
| eval | Komi_Permyak-UH | `koi_uh-ud-test.conllu` | 106 | 903 | CC BY-SA 4.0 | `e712e8d722925e72b87c7e4b4ef1221296a36f285dc6801c4d404f0d97f75335` |  |
| eval | Komi_Zyrian-IKDP | `kpv_ikdp-ud-test.conllu` | 214 | 2,309 | CC BY-SA 4.0 | `7bbf4167697614b1089eb2ee4a086da276f5f0256cc7a3e012e95c53f869450e` |  |
| train | Komi_Zyrian-Lattice | `kpv_lattice-ud-train.conllu` | 20 | 158 | CC BY-SA 4.0 | `9f0e532f036d4ba418281cc0b7ab028999d71d533da0b032db78b34f9a1a583e` |  |
| eval | Komi_Zyrian-Lattice | `kpv_lattice-ud-test.conllu` | 653 | 8,038 | CC BY-SA 4.0 | `4ddb1105ad0c14e25ee253f74dc625cf67163f775cbce9ebba37317ed570c3c4` |  |
| train | Korean-GSD | `ko_gsd-ud-train.conllu` | 4,400 | 56,687 | CC BY-SA 4.0 | `0bd96ceae934d1089ece599b8904b43b0074df75e41a8e07e4b72264337b39f8` |  |
| dev | Korean-GSD | `ko_gsd-ud-dev.conllu` | 950 | 11,958 | CC BY-SA 4.0 | `8f7c4e24bbf6db2b0df8007068eb2d56d5ad8a5539360589d0f15a8c17034566` |  |
| eval | Korean-GSD | `ko_gsd-ud-test.conllu` | 989 | 11,677 | CC BY-SA 4.0 | `3d1df99bda4800235e14bcfd915baf706eafa1a3935a75ffd32420a51e57f5aa` | 113 test texts also in train |
| train | Korean-KSL | `ko_ksl-ud-train.conllu` | 14,076 | 125,950 | CC BY-SA 4.0 | `80659ebe423e6b39fdcd4a5eed3251de5f78b3ddede837f69cf98934e1f5c8bd` |  |
| dev | Korean-KSL | `ko_ksl-ud-dev.conllu` | 1,480 | 12,852 | CC BY-SA 4.0 | `b986979df5137ab1554e7c38a0d7257ce7182a7e76eb82b586610733a289e15b` |  |
| eval | Korean-KSL | `ko_ksl-ud-test.conllu` | 1,813 | 16,302 | CC BY-SA 4.0 | `62574d11b83f62217494a53fd2a7cbf75b7fc3fe5df74021a91e66df65149033` | 140 test texts also in train |
| train | Korean-Kaist | `ko_kaist-ud-train.conllu` | 23,010 | 296,446 | CC BY-SA 4.0 | `6f99de7e21b8119022831afda52643dd8ff60ea386f2679c938dd7f652725d94` |  |
| dev | Korean-Kaist | `ko_kaist-ud-dev.conllu` | 2,066 | 25,278 | CC BY-SA 4.0 | `a1ce2dceee65683c2df3b0cce96c83d01e5ff67756060b998fe01d8bc8ca4faa` |  |
| eval | Korean-Kaist | `ko_kaist-ud-test.conllu` | 2,287 | 28,366 | CC BY-SA 4.0 | `fd94dc89afb01d1f7f340a46d567e0f27ae6903a70d7b6f650b49f7427f83b97` | 1 test texts also in train |
| eval | Korean-LittlePrince | `ko_littleprince-ud-test.conllu` | 1,551 | 13,656 | CC BY-SA 4.0 | `3539a9002a74fd66759009bd9b5135fcc7b07ea821cea194fb25e31b5c73a81d` |  |
| eval | Korean-PUD | `ko_pud-ud-test.conllu` | 1,000 | 16,584 | CC BY-SA 3.0 | `55f9c8d465400e070664c5319091d31ce35a16ec220b0bebc5e3c9b12f530948` |  |
| train | Kyrgyz-KTMU | `ky_ktmu-ud-train.conllu` | 1,308 | 12,357 | CC BY-SA 4.0 | `7652f9b54ec96a778133cb1528769ceb2de83d0812076495f9943704b9672ea9` |  |
| eval | Kyrgyz-KTMU | `ky_ktmu-ud-test.conllu` | 1,222 | 11,771 | CC BY-SA 4.0 | `843061f0ad2204d5d65ee77edf2e576fa54887415ec8e1bb0cd928fd7b23877b` | 88 test texts also in train |
| eval | Kyrgyz-TueCL | `ky_tuecl-ud-test.conllu` | 173 | 1,250 | CC BY-SA 4.0 | `08224be6b74346f582234f54527412dd6265c8d8b42e7d6ef463aade85bdcd9b` |  |
| eval | Latgalian-Cairo | `ltg_cairo-ud-test.conllu` | 20 | 170 | CC BY-SA 4.0 | `43397b8f310b63e3fdb4a5be6a2952a0f726a17433aef9652a445dc337745952` |  |
| train | Latin-CIRCSE | `la_circse-ud-train.conllu` | 918 | 14,574 | CC BY-SA 4.0 | `09c44e9453745d9123739c33d12dbcd4fd0f775434d36ab238a6e94179ecf1e8` |  |
| dev | Latin-CIRCSE | `la_circse-ud-dev.conllu` | 161 | 2,978 | CC BY-SA 4.0 | `66ccb9cb359227474228cda4164bd99158452471051d91894afc3fdb53164bbc` |  |
| eval | Latin-CIRCSE | `la_circse-ud-test.conllu` | 893 | 11,503 | CC BY-SA 4.0 | `51be34d895fcc372c04a44bba20fbec9463c76fc1e3297b0a7acd4262cbfe4b5` |  |
| train | Latin-LLCT | `la_llct-ud-train.conllu` | 7,289 | 194,143 | CC BY-SA 4.0 | `8aabc8735c7a623a9e4820e13861a87459dd0fb5b2d238f0583854143526a643` |  |
| dev | Latin-LLCT | `la_llct-ud-dev.conllu` | 850 | 24,189 | CC BY-SA 4.0 | `040457f2e47265a5676c83ce236e1d625af41eda8f779812841e8ba84e28480c` |  |
| eval | Latin-LLCT | `la_llct-ud-test.conllu` | 884 | 24,079 | CC BY-SA 4.0 | `81d9f57de66d83303ad4df54a60cd445ac089b50070d369831a632b4cf948e64` | 27 test texts also in train |
| eval | Latvian-Cairo | `lv_cairo-ud-test.conllu` | 20 | 168 | CC BY-SA 4.0 | `3ea46f1ea2ee587913458fe8d90ef390367d48f2cc05adf5d861a8826b5b29fd` |  |
| train | Latvian-LVTB | `lv_lvtb-ud-train.conllu` | 15,059 | 258,266 | CC BY-SA 4.0 | `1fa6a60852b21aafca644c7929e2aaaf7b01899ccc71d7a2fa15f457caed49d2` |  |
| dev | Latvian-LVTB | `lv_lvtb-ud-dev.conllu` | 2,109 | 34,729 | CC BY-SA 4.0 | `d9dec7ed8809ab112f09945400fddf72d4731354624b1b637f5db5a5dabbb91d` |  |
| eval | Latvian-LVTB | `lv_lvtb-ud-test.conllu` | 2,412 | 37,323 | CC BY-SA 4.0 | `4237279f832058659ed8494af13239f7efe25058919bc0f5d84050ef47f1968d` | 6 test texts also in train |
| train | Ligurian-GLT | `lij_glt-ud-train.conllu` | 20 | 226 | C-UDA 1.0 | `f30f09fb772efe2c130e8860e7ab5be7d2d456f7beb4f529d444f909ec028c66` |  |
| eval | Ligurian-GLT | `lij_glt-ud-test.conllu` | 296 | 6,702 | C-UDA 1.0 | `54cb0ff01074cf8d74a753f5db237db970ed3eef39e7bd28deed09d702a01f73` |  |
| train | Lithuanian-ALKSNIS | `lt_alksnis-ud-train.conllu` | 2,341 | 47,641 | CC BY-SA 4.0 | `0bbfd5abf70d1cbeb22dfbd4150e3643e49f018e3e18e053cb319bfb1eed32d6` |  |
| dev | Lithuanian-ALKSNIS | `lt_alksnis-ud-dev.conllu` | 617 | 11,560 | CC BY-SA 4.0 | `638ff53f0e3d0ea3c19ce970086ada947aa9d00afe07c3b78637e04f8ea81492` |  |
| eval | Lithuanian-ALKSNIS | `lt_alksnis-ud-test.conllu` | 684 | 10,846 | CC BY-SA 4.0 | `5992220412e2aa5090551d77e5b6abee97cdae2653de098f73c630731efd10fa` | 2 test texts also in train |
| train | Lithuanian-HSE | `lt_hse-ud-train.conllu` | 153 | 3,210 | CC BY-SA 4.0 | `454fa0aff84aa88070cf53587634278ec4d6d02610c57e8111648bdc51f720de` |  |
| dev | Lithuanian-HSE | `lt_hse-ud-dev.conllu` | 55 | 1,086 | CC BY-SA 4.0 | `0c1e0ced25cc5fdfb1d8361aa9fbf96cd1148ab92ec28ad001339eb5b3c0da61` |  |
| eval | Lithuanian-HSE | `lt_hse-ud-test.conllu` | 55 | 1,060 | CC BY-SA 4.0 | `8d07e08164817c3eebc38864c5a482839c3e71e8fad5ed65faa7f730f42de82c` |  |
| train | Livvi-KKPP | `olo_kkpp-ud-train.conllu` | 19 | 144 | CC BY-SA 4.0 | `40857eed98d673ecb1a1afa2cbf53793edbd8c512893ca7f197cda9563a2c609` |  |
| eval | Livvi-KKPP | `olo_kkpp-ud-test.conllu` | 106 | 1,488 | CC BY-SA 4.0 | `5b63123ba5b1867a7c7d1475f7272313c09d0d4ecfe019f95019416294679cd5` |  |
| train | Low_Saxon-LSDC | `nds_lsdc-ud-train.conllu` | 500 | 11,666 | CC BY-SA 4.0 | `8bae0f102e95d754cf9d026247f35403e01b3438ccf81de5dcf41f5119cb2165` |  |
| eval | Low_Saxon-LSDC | `nds_lsdc-ud-test.conllu` | 500 | 10,973 | CC BY-SA 4.0 | `d70988ab7d03d77b7ce25bfc197758a2b965091299d7c17b3eff4df9301343ac` |  |
| eval | Luxembourgish-LuxBank | `lb_luxbank-ud-test.conllu` | 20 | 206 | CC BY-SA 4.0 | `75f0a411ce12907ea6cf408ae31c1b662311f56a95630f4290069eda866d7574` |  |
| eval | Macedonian-MTB | `mk_mtb-ud-test.conllu` | 155 | 1,360 | CC BY-SA 4.0 | `93219e9c20d85ad98fcdbb9df398d33489c865bed7388ac9e712446d8d671b05` |  |
| eval | Madi-Jarawara | `jaa_jarawara-ud-test.conllu` | 20 | 115 | CC BY-SA 4.0 | `c34240d0298baba9e7eb0bd668d78b178cda9b1c46c15dc37ddc458f890eb0f4` |  |
| eval | Makurap-TuDeT | `mpu_tudet-ud-test.conllu` | 37 | 178 | CC BY-SA 4.0 | `7f1eb647644fa18732311336b645bafacc52dfdd86de5fee69dba36e796696d8` |  |
| eval | Malayalam-UFAL | `ml_ufal-ud-test.conllu` | 218 | 2,403 | CC BY-SA 4.0 | `cf7d726146e0fa3910436462e776c0ddc1fb562a49deb9a447d3b199fb682a9e` |  |
| train | Maltese-MUDT | `mt_mudt-ud-train.conllu` | 1,123 | 22,880 | CC BY-SA 4.0 | `345cbf5342c1dccc527f9836f0ecaad4e919474a53980d80be58c766f5d07f70` |  |
| dev | Maltese-MUDT | `mt_mudt-ud-dev.conllu` | 433 | 10,209 | CC BY-SA 4.0 | `98617bff25a17b5b18e5d932d0f3f4074ad47b21632169471ba9512cc6683878` |  |
| eval | Maltese-MUDT | `mt_mudt-ud-test.conllu` | 518 | 11,073 | CC BY-SA 4.0 | `2edccdc01bca344e33de4c49845b21df328c3bf0b188508f724cddce50847b08` |  |
| train | Marathi-CMUPAN | `mr_cmupan-ud-train.conllu` | 7,616 | 94,144 | CC BY-SA 4.0 | `e8bcaebd7a0ba277777dcb30e2e8fc9470a93bcf57ef4b87d0aebe9fda6da6fa` |  |
| dev | Marathi-CMUPAN | `mr_cmupan-ud-dev.conllu` | 987 | 11,648 | CC BY-SA 4.0 | `fd03a75728467ce2eb4e14e09e2edadd893ebc0b37b02c172cdbcaf5f4c4492e` |  |
| eval | Marathi-CMUPAN | `mr_cmupan-ud-test.conllu` | 1,003 | 12,406 | CC BY-SA 4.0 | `57f1bac6c2b07da3f8b7366dcbd98e66a8935546257444477c9c9f13a82bd010` | 5 test texts also in train |
| train | Marathi-UFAL | `mr_ufal-ud-train.conllu` | 373 | 2,997 | CC BY-SA 4.0 | `bfd0e45a4a065a27adc621c7b30c3d14095ceef03db63cae5f58a8d759d7a430` |  |
| dev | Marathi-UFAL | `mr_ufal-ud-dev.conllu` | 46 | 438 | CC BY-SA 4.0 | `daca927607183525e6b443e7a54550fb4b49fe252e5551941eb2530f2767f4aa` |  |
| eval | Marathi-UFAL | `mr_ufal-ud-test.conllu` | 47 | 412 | CC BY-SA 4.0 | `fa70d27491912b156a26908a885e6120fd478b8a08bd189e05b3ab205d0bad25` |  |
| eval | Middle_Armenian-ArmTDP | `axm_armtdp-ud-test.conllu` | 27 | 1,093 | CC BY-SA 4.0 | `4eaefe59fd9f8ba68151bf7321850bac0ee37a95e771bfd5526e9012bb936f39` |  |
| eval | Middle_French-ALTM | `frm_altm-ud-test.conllu` | 217 | 7,084 | CC BY-SA 4.0 | `9e0a1b48726dfe8ec15cbf98068bbcb1cd3610fcfe14f07b2eb1fc1aa4397fe1` |  |
| train | Moksha-JR | `mdf_jr-ud-train.conllu` | 22 | 226 | CC BY-SA 4.0 | `849fff74b8f051981faaadd9c705af8925d8f9cdf0119eee20ed56ab4498ac89` |  |
| eval | Moksha-JR | `mdf_jr-ud-test.conllu` | 468 | 4,353 | CC BY-SA 4.0 | `55e7fb77447ddd77494f45a988ce24c925681e973e95658cbdef80b3f1976a30` |  |
| eval | Munduruku-TuDeT | `myu_tudet-ud-test.conllu` | 158 | 1,022 | CC BY-SA 4.0 | `be065df80a7c073145300309458d758b1a25f69dbf25cc50553b559c942c6ad7` |  |
| train | Naija-NSC | `pcm_nsc-ud-train.conllu` | 7,279 | 111,916 | CC BY-SA 4.0 | `2ecbbbd970575573da99c82897a08ce8c32ddc4f88d65bdc213e30e772ee4804` |  |
| dev | Naija-NSC | `pcm_nsc-ud-dev.conllu` | 990 | 14,571 | CC BY-SA 4.0 | `3733a8a80aa5d7f2b61ae26570d84a0609ebbf79639f0c0a1dda68d37388c92e` |  |
| eval | Naija-NSC | `pcm_nsc-ud-test.conllu` | 972 | 14,350 | CC BY-SA 4.0 | `59bb428a3435ed09f4e9cf9738d47875ba89f16f7fcd4f48d33b264c3e74bad9` | 24 test texts also in train |
| eval | Neapolitan-RB | `nap_rb-ud-test.conllu` | 20 | 201 | CC BY-SA 4.0 | `f842471d68bcdfc793f4a614427b16b072d725b782e1506bc1ea3a261d11ac1b` |  |
| eval | Nenets-Tundra | `yrk_tundra-ud-test.conllu` | 170 | 1,272 | CC BY-SA 4.0 | `4eeaed90a6162b75ef7405254fb2d000b6ffa42061b9f12842d473f24b558720` |  |
| eval | Nepali-BK | `ne_bk-ud-test.conllu` | 69 | 801 | CC BY-SA 4.0 | `118d6bf0213f2dbca84557e4d213e9b20ddbf45b4d8a5ae74eedbc54779387e2` |  |
| train | North_Sami-Giella | `sme_giella-ud-train.conllu` | 2,257 | 16,835 | CC BY-SA 4.0 | `2da7620d329d6974f4fa4c313bde62780544ed1a0d0351b02d8999543a97dbf3` |  |
| eval | North_Sami-Giella | `sme_giella-ud-test.conllu` | 865 | 10,010 | CC BY-SA 4.0 | `b276ad5df5cad1b11412cbab1d89a8bf6e3983cddcf3c62140912c0a0bde0ecc` | 5 test texts also in train |
| train | Northern_Kurdish-Kurmanji | `kmr_kurmanji-ud-train.conllu` | 20 | 242 | CC BY-SA 4.0 | `12b4ba91cca2748fb4bcdca03d6fb31c8792ee5e649b17f3209c68abd9ad17db` |  |
| eval | Northern_Kurdish-Kurmanji | `kmr_kurmanji-ud-test.conllu` | 734 | 10,017 | CC BY-SA 4.0 | `db9501d607afa1480f958558823e733568ce6b10696de80b7c6cbc408827467a` |  |
| eval | Northwest_Gbaya-Autogramm | `gya_autogramm-ud-test.conllu` | 403 | 2,692 | CC BY-SA 4.0 | `a7635c03ab95314b69eeff4aac4e7148e442823ffd49a204eb3fb5b4a61e62f0` |  |
| train | Norwegian-Bokmaal | `no_bokmaal-ud-train.conllu` | 15,696 | 243,886 | CC BY-SA 4.0 | `caa990b444d11030df842d39f2b4bda271beb90a93deda0a56406fa061535b03` |  |
| dev | Norwegian-Bokmaal | `no_bokmaal-ud-dev.conllu` | 2,409 | 36,369 | CC BY-SA 4.0 | `1c803c3f90e510ce995c5f9108e240f283c34025333a61281cb90fdf2e82c83a` |  |
| eval | Norwegian-Bokmaal | `no_bokmaal-ud-test.conllu` | 1,939 | 29,966 | CC BY-SA 4.0 | `b45e4809d38ea560f0b164023b0f4c6d8f0d7731f66c4df129917bb06d1676f8` | 7 test texts also in train |
| train | Norwegian-Nynorsk | `no_nynorsk-ud-train.conllu` | 14,174 | 245,330 | CC BY-SA 4.0 | `35bd1b52ce31749324268dba7b1e866309a2e82828e73305c99026b82357b79a` |  |
| dev | Norwegian-Nynorsk | `no_nynorsk-ud-dev.conllu` | 1,890 | 31,250 | CC BY-SA 4.0 | `c7c497ddab208c3b53ab3849241845c3eacc1dc6045dc3a9631f166e6e541984` |  |
| eval | Norwegian-Nynorsk | `no_nynorsk-ud-test.conllu` | 1,511 | 24,773 | CC BY-SA 4.0 | `d005008ca04a764e77791b0aa8da982e9a41ceef4303b71e30c304f5ffc7342f` | 12 test texts also in train |
| eval | Odia-ODTB | `or_odtb-ud-test.conllu` | 456 | 5,818 | CC BY-SA 4.0 | `05bfe6833a3d1ba0edae8ca99611b216c7227234c84c06337f5da3947c6d95f4` |  |
| train | Old_East_Slavic-Birchbark | `orv_birchbark-ud-train.conllu` | 1,113 | 7,879 | CC BY-SA 4.0 | `a7445b102e3d9adb01b098dfdcdb4d76e71f3ee54ef353622e78a0106ac5ef01` |  |
| dev | Old_East_Slavic-Birchbark | `orv_birchbark-ud-dev.conllu` | 1,062 | 10,009 | CC BY-SA 4.0 | `cf9b279451e4377d5067975ce96446fc3fb0a65aaf4898b4452720f356ce3c38` |  |
| eval | Old_East_Slavic-Birchbark | `orv_birchbark-ud-test.conllu` | 1,009 | 10,019 | CC BY-SA 4.0 | `6c35cd8763c93eb940315eb65b233d1b10fb11d50dd19da3467d5947b439d259` |  |
| train | Old_East_Slavic-RNC | `orv_rnc-ud-train.conllu` | 3,778 | 101,125 | CC BY-SA 4.0 | `cf63ba8422500ad82a859fcfc1894913fe9a36a283f488cfc293267eb6a59184` |  |
| dev | Old_East_Slavic-RNC | `orv_rnc-ud-dev.conllu` | 1,363 | 38,736 | CC BY-SA 4.0 | `f19c3da5b388d73b0acd0098bf51986c39ebc21711874fec8eb6e02dac977fd7` |  |
| eval | Old_East_Slavic-RNC | `orv_rnc-ud-test.conllu` | 906 | 28,203 | CC BY-SA 4.0 | `a7bd85424134f2e7dd5cf608adcce9ef1501ba9ee38d73a56bcc803b8b716041` | 13 test texts also in train |
| train | Old_East_Slavic-Ruthenian | `orv_ruthenian-ud-train.conllu` | 4,120 | 116,486 | CC BY-SA 4.0 | `fc13f73dc71e5fac95938eba424b3d5236bfa8edd8ce7bd0e28db2d28a2f9680` |  |
| dev | Old_East_Slavic-Ruthenian | `orv_ruthenian-ud-dev.conllu` | 855 | 10,836 | CC BY-SA 4.0 | `a2047c4c734dab0a5b68f888979a3ca14670d217d7dcc11bb12b775f2dbbfd1d` |  |
| eval | Old_East_Slavic-Ruthenian | `orv_ruthenian-ud-test.conllu` | 390 | 10,011 | CC BY-SA 4.0 | `f34964cee7d6a38b3dd39ceb4dbde38a4fd9aacd8512272c19318027e044ea0d` |  |
| eval | Old_English-Cairo | `ang_cairo-ud-test.conllu` | 20 | 171 | CC BY-SA 4.0 | `ab8f9d54587e9bf3a0e08df54c0b0cb0751e2ef42591e288cdcdc9dc45ec1d7e` |  |
| eval | Old_French-ALTM | `fro_altm-ud-test.conllu` | 553 | 15,285 | CC BY-SA 4.0 | `d628cf9e584693fd20f00c2d265060bc77ef32c44282d4ba8009841f9cb8dfaf` |  |
| eval | Old_Georgian-GLC | `oge_glc-ud-test.conllu` | 151 | 6,884 | CC BY-SA 4.0 | `29a8e5c34f83460d021f417853983187b864c9cd3c83a12db7bec391e5fd73eb` |  |
| eval | Old_Irish-DipWBG | `sga_dipwbg-ud-test.conllu` | 34 | 438 | CC BY-SA 4.0 | `f04fd8e44c60be16b68f8ced77cbcd34a304f5986671c48f10e92ad6b9ac161a` |  |
| train | Old_Occitan-CorAG | `pro_corag-ud-train.conllu` | 1,092 | 37,926 | CC BY-SA 4.0 | `dc245b73f81ad2d38c21f2f951084b7077972dd01dc12ee81d28a99e3005a4be` |  |
| dev | Old_Occitan-CorAG | `pro_corag-ud-dev.conllu` | 118 | 4,405 | CC BY-SA 4.0 | `d3d32eb1b5863df1552dd7d30368cf5d5d0a9edbd6cb9b7a6f0f4e93dfb527f2` |  |
| eval | Old_Occitan-CorAG | `pro_corag-ud-test.conllu` | 276 | 10,208 | CC BY-SA 4.0 | `8dcefd909fce660b618e8b93fa43aeba4bb4f1e1b0d9140a3d7c45eb09e8d414` |  |
| eval | Old_Turkish-Clausal | `otk_clausal-ud-test.conllu` | 20 | 158 | CC0 1.0 | `5e3faa1555cdbeece7ea52948101825b2972760d1fad3b8eee2dda238d953724` |  |
| train | Ottoman_Turkish-BOUN | `ota_boun-ud-train.conllu` | 114 | 1,997 | CC BY-SA 4.0 | `f500562db6aea177c2ba95cdbad0ae77c2fe2765cf2845b2f5929fd41efe021d` |  |
| eval | Ottoman_Turkish-BOUN | `ota_boun-ud-test.conllu` | 400 | 6,854 | CC BY-SA 4.0 | `361a24fcf6103e4e7cddd02aea78ac79920dd0da857c1fbbe54f50326a9be36c` |  |
| train | Ottoman_Turkish-DUDU | `ota_dudu-ud-train.conllu` | 1,117 | 10,845 | CC BY-SA 4.0 | `37f77df5e2cd87ee2d5aae4c9658acbdba0fb834cdffb9ba3616bdc22805b4b4` |  |
| dev | Ottoman_Turkish-DUDU | `ota_dudu-ud-dev.conllu` | 121 | 1,177 | CC BY-SA 4.0 | `7ae73a87a240711d5e82ba0be29429dfc44638be2a271fd8243618ed065859ec` |  |
| eval | Ottoman_Turkish-DUDU | `ota_dudu-ud-test.conllu` | 1,008 | 10,005 | CC BY-SA 4.0 | `34464bb054f04133f8e4c955e4924a6d6fbba8951582cc8275e38d8d021008e4` |  |
| eval | Ottoman_Turkish-TueCL | `ota_tuecl-ud-test.conllu` | 148 | 929 | CC BY-SA 4.0 | `1b9ef35117edd9b93dfad7a3db761ef952a216b9c2952f22f8b25a292b40549c` |  |
| eval | Pashto-Prince | `ps_prince-ud-test.conllu` | 64 | 1,180 | CC BY-SA 4.0 | `c0e96933fe3fdda0dee9a1bdf827bf0e92420e2308fcf0f1680601bfd166c570` |  |
| eval | Pashto-Sikaram | `ps_sikaram-ud-test.conllu` | 200 | 5,467 | CC BY-SA 4.0 | `d449b3e64337f33882db5d9fdfa50e38e61cf0aec7b5d55354cf195c1a1b357f` |  |
| eval | Paumari-TueCL | `pad_tuecl-ud-test.conllu` | 101 | 504 | CC BY-SA 4.0 | `ada6142432cc4b7181075ceb89df24ac3d2e6593e99cf90c4b5cdf28fe8a0fe5` |  |
| train | Persian-PerDT | `fa_perdt-ud-train.conllu` | 26,196 | 452,496 | CC BY-SA 4.0 | `bf6823e7a9010afe69367389116f5c1eaafd616c8fc6a302a4e0c779cc64b632` |  |
| dev | Persian-PerDT | `fa_perdt-ud-dev.conllu` | 1,456 | 25,147 | CC BY-SA 4.0 | `541904074688af3335da317a0673049c3856eab995d3b1bc03ca9ff843615094` |  |
| eval | Persian-PerDT | `fa_perdt-ud-test.conllu` | 1,455 | 24,133 | CC BY-SA 4.0 | `4daf1fbf1bf9e0e36ac15c8ce1fe050740c7dc016cd0fdeb67b785699c1d599d` |  |
| train | Persian-Seraji | `fa_seraji-ud-train.conllu` | 4,798 | 121,067 | CC BY-SA 4.0 | `fce94c19557f6cdc188068fab4ff36783a03816d161fa596cfa621bb83e32e7b` |  |
| dev | Persian-Seraji | `fa_seraji-ud-dev.conllu` | 599 | 15,832 | CC BY-SA 4.0 | `acc241631c278f96b084285946db28b87fe9c97d81ac58119cdaad0be05799b0` |  |
| eval | Persian-Seraji | `fa_seraji-ud-test.conllu` | 600 | 16,024 | CC BY-SA 4.0 | `6d2311596634c739eaf4c4ef4d4125d4ff7562ac56ea4da0b3db6501ecb25710` | 3 test texts also in train |
| eval | Pesh-ChibErgIS | `pay_chibergis-ud-test.conllu` | 524 | 4,275 | CC BY-SA 4.0 | `f8b4f7c9a26b955ab36f68c082624fdaf143bcba020ab4cda1fd67fb6e633578` |  |
| eval | Phrygian-KUL | `xpg_kul-ud-test.conllu` | 203 | 1,921 | CC BY-SA 4.0 | `efdea547cb551da56901b2933563f961ce5788a1d6e34a6c28557a3a30f092bc` |  |
| train | Polish-MPDT | `pl_mpdt-ud-train.conllu` | 1,433 | 33,520 | CC BY-SA 4.0 | `82ecf3fcc83a1ccd2f0ed0d6c06639bafb6d1b11b4481b24b5ed87321ab9f697` |  |
| dev | Polish-MPDT | `pl_mpdt-ud-dev.conllu` | 162 | 3,748 | CC BY-SA 4.0 | `2d9cb5ab6a85d798b5959871ee2afa1875de3975b536749752f5ff786c3a60e3` |  |
| eval | Polish-MPDT | `pl_mpdt-ud-test.conllu` | 423 | 10,005 | CC BY-SA 4.0 | `490d3c845736997bc29341428723a0ea31574ed17373529fb685651311a0dffe` |  |
| eval | Polish-PUD | `pl_pud-ud-test.conllu` | 1,000 | 18,384 | CC BY-SA 4.0 | `1af733133fa641380144c65a0292f5e8b46a077e90085f21cedbf416f2907287` |  |
| train | Punjabi-CS | `pa_cs-ud-train.conllu` | 73 | 1,351 | CC BY-SA 4.0 | `e6dc75ffcb013071445f1b1de33b08ca7c4bf75c99249ad26e367b337a07fe10` |  |
| dev | Punjabi-CS | `pa_cs-ud-dev.conllu` | 21 | 289 | CC BY-SA 4.0 | `e5104af01ef6652b95d0c93587c190dd0da9046ce17c304019efa64f8b189421` |  |
| eval | Punjabi-CS | `pa_cs-ud-test.conllu` | 19 | 263 | CC BY-SA 4.0 | `04d389457a64b13f82063986db1818d93ca3d4e4e644b10364f2235d15651436` |  |
| train | Punjabi-Rang | `pa_rang-ud-train.conllu` | 81 | 1,212 | CC BY-SA 4.0 | `afe84964785f36189ca8d1ec727b8d975c5d69946ecf94f01dab56e23a443c72` |  |
| dev | Punjabi-Rang | `pa_rang-ud-dev.conllu` | 30 | 395 | CC BY-SA 4.0 | `704fa20ddb8a0b8b8ef6ec66ee454c9b414c7d6de5ca3b0b0d0d2beee862910f` |  |
| eval | Punjabi-Rang | `pa_rang-ud-test.conllu` | 26 | 300 | CC BY-SA 4.0 | `101104b21bba7e4a7425da07e12f0d65bb1dcc2464dbb44e724f5f537294dfb3` |  |
| eval | Romanian-ArT | `ro_art-ud-test.conllu` | 50 | 573 | CC BY-SA 4.0 | `71cefb0314745ebda17f59a198c1aaaef90a8d369f8becfb45b0d7e282995c86` |  |
| eval | Romanian-MolDoRo | `ro_moldoro-ud-test.conllu` | 30 | 241 | CC BY-SA 4.0 | `ab69621780bf2681d4121ff591f241cfa7bd55264a297ae54baa754f7607787a` |  |
| train | Romanian-RRT | `ro_rrt-ud-train.conllu` | 8,043 | 185,106 | CC BY-SA 4.0 | `15c7a90a45f8a7ae809c09f6ec9cf2250bf4cf323cf0cbfcda7cfc4dbac4ea19` |  |
| dev | Romanian-RRT | `ro_rrt-ud-dev.conllu` | 752 | 17,047 | CC BY-SA 4.0 | `b81e3848e9e9fb06afd32867a8795208edc3305df981aedc361c8273dc84a3bf` |  |
| eval | Romanian-RRT | `ro_rrt-ud-test.conllu` | 729 | 16,311 | CC BY-SA 4.0 | `32a2f7b9fc82c1e82d9959ce8c5ec7b7840f725177aa06b497b51f6834801989` |  |
| train | Romanian-SiMoNERo | `ro_simonero-ud-train.conllu` | 3,747 | 116,857 | CC BY-SA 4.0 | `63f20bb5b27e35414e1ee9d45aa144a69911d1b9b6246b1d6f65989134216a37` |  |
| dev | Romanian-SiMoNERo | `ro_simonero-ud-dev.conllu` | 443 | 14,611 | CC BY-SA 4.0 | `5f1e22f302597408d5f2aae355571e0cef9897f639983c7f140aa7afc8ce4276` |  |
| eval | Romanian-SiMoNERo | `ro_simonero-ud-test.conllu` | 491 | 14,552 | CC BY-SA 4.0 | `dfe5c3f752ef3aa6eecfc8013555c6776e6f360d3d341777c7f6e1abd87984f4` |  |
| train | Russian-GSD | `ru_gsd-ud-train.conllu` | 3,850 | 74,900 | CC BY-SA 4.0 | `0ae33a4bbda12697e2eb0a32af4971bda73af5852c8521744a974a316c02bab5` |  |
| dev | Russian-GSD | `ru_gsd-ud-dev.conllu` | 579 | 11,709 | CC BY-SA 4.0 | `e3e3d4e8d1d7544e531b2b81ae64257efacc97f93276ce6d2ce52fd1de052fa2` |  |
| eval | Russian-GSD | `ru_gsd-ud-test.conllu` | 601 | 11,385 | CC BY-SA 4.0 | `f26e022329162a1c6306f76644d06f770f1572501755421165387137fe63138d` | 1 test texts also in train |
| eval | Russian-PUD | `ru_pud-ud-test.conllu` | 1,000 | 19,355 | CC BY-SA 3.0 | `91a755eb4296451472cc9dd54fcb3b77e33d91378a50a2fd29360b8c94bc4c33` |  |
| train | Russian-Poetry | `ru_poetry-ud-train.conllu` | 3,568 | 43,972 | CC BY-SA 4.0 | `55830162ef3dd32fc4a8f6c57a9c45ef17db1e33b7f0bcf2cedbbd776e953ad1` |  |
| dev | Russian-Poetry | `ru_poetry-ud-dev.conllu` | 789 | 10,102 | CC BY-SA 4.0 | `f6056b7c0ba4bf24bac2616a576e8db7e161ab1ebb4e47832e5cd1831aa2e5b6` |  |
| eval | Russian-Poetry | `ru_poetry-ud-test.conllu` | 728 | 10,038 | CC BY-SA 4.0 | `26a3b470836e31f40efe84141fd70a4110f3e3d0b4c1309db76e4afaa9c1274f` | 8 test texts also in train |
| eval | Sanskrit-UFAL | `sa_ufal-ud-test.conllu` | 230 | 1,843 | CC BY-SA 4.0 | `8e956d19e67e96d8fce8807a37f6d5b995db35df12a8b7ba233dbbaf74a4aa6f` |  |
| train | Sanskrit-Vedic | `sa_vedic-ud-train.conllu` | 21,477 | 161,959 | CC BY-SA 4.0 | `05829c606047956b25aa6ebbd1fd2b11255ed04840fb90c3661f102b556c791d` |  |
| dev | Sanskrit-Vedic | `sa_vedic-ud-dev.conllu` | 2,996 | 23,862 | CC BY-SA 4.0 | `f103e93cea236173afe93652c1ed251bc1dd2716c230ab95eab31a8090527871` |  |
| eval | Sanskrit-Vedic | `sa_vedic-ud-test.conllu` | 2,709 | 20,619 | CC BY-SA 4.0 | `242546c3d6709c7c30d9c443039eb1483bc6d8f45b4873f821b60c19756cce43` | 95 test texts also in train |
| train | Scottish_Gaelic-ARCOSG | `gd_arcosg-ud-train.conllu` | 3,544 | 68,775 | CC BY-SA 4.0 | `f3f5e6db533cda5accb0b49e9c0d7df0e51f6b43eeeeeff74d6a3f531ecb6e40` |  |
| dev | Scottish_Gaelic-ARCOSG | `gd_arcosg-ud-dev.conllu` | 656 | 10,644 | CC BY-SA 4.0 | `dab5dd95554277c14b8e21f1f7616c90053cece0b8473fa88f2774fc0a1e9267` |  |
| eval | Scottish_Gaelic-ARCOSG | `gd_arcosg-ud-test.conllu` | 548 | 10,597 | CC BY-SA 4.0 | `3181b5ba5d701f56e4f7e9f06018e25051eb2b66b2a63e66bb354a22876b73bd` | 11 test texts also in train |
| train | Serbian-SET | `sr_set-ud-train.conllu` | 3,328 | 74,259 | CC BY-SA 4.0 | `bdbf4bd58e672d1b93c781723195765bd699322663194c842ebe5741ef2143ac` |  |
| dev | Serbian-SET | `sr_set-ud-dev.conllu` | 536 | 11,993 | CC BY-SA 4.0 | `5c38174a5c6e41d52673d80dbf01cd4516dc48c4cce12505a63f67d82cee9f5b` |  |
| eval | Serbian-SET | `sr_set-ud-test.conllu` | 520 | 11,421 | CC BY-SA 4.0 | `ad8b8be1446a9ca31b2a5465d7e74608440d896ad029f247b9e15b006b547448` |  |
| eval | Sicilian-STB | `scn_stb-ud-test.conllu` | 505 | 11,204 | CC BY-SA 4.0 | `3a95c9e187e0241d1107e051abd2188e92c9cb406969e4e7a1b184e2099b8543` |  |
| train | Sindhi-Isra | `sd_isra-ud-train.conllu` | 4,096 | 74,589 | CC BY-SA 4.0 | `a321069b0337fc12078f5c44df7166068d7fa82aeed3a3c0b55b416d00dfa49c` |  |
| dev | Sindhi-Isra | `sd_isra-ud-dev.conllu` | 773 | 9,579 | CC BY-SA 4.0 | `8f24e20b04c7595ac00b529eebb1c3af9fe0663bba910fc1cddf4695bcc3c16e` |  |
| eval | Sindhi-Isra | `sd_isra-ud-test.conllu` | 887 | 11,059 | CC BY-SA 4.0 | `ea1128ea255365a449cae5f27ef88ac6fcde392df52bc005213aa7c680edc4c6` |  |
| eval | Sinhala-Appuwa | `si_appuwa-ud-test.conllu` | 69 | 669 | CC BY-SA 4.0 | `a9da717514c50331aa01039a4fe926c8dc93b14203bee450a4749429120b21bb` |  |
| eval | Sinhala-STB | `si_stb-ud-test.conllu` | 100 | 880 | CC BY-SA 4.0 | `7847eb43b4199aab75d84b4f63d2009e7d8ca47600e90bc5a2df60071798e3b6` |  |
| train | Skolt_Sami-Giellagas | `sms_giellagas-ud-train.conllu` | 20 | 153 | CC BY-SA 4.0 | `a0373fabf8dd981fed93d1acfa1071b516e8c02c1f985bf60bd9970400762026` |  |
| eval | Skolt_Sami-Giellagas | `sms_giellagas-ud-test.conllu` | 241 | 2,896 | CC BY-SA 4.0 | `8a12273f20d6834b12993a7a9a1aa7fafe4f57b1b3317d702385c5b46407cdd4` |  |
| train | Slovak-SNK | `sk_snk-ud-train.conllu` | 8,483 | 80,874 | CC BY-SA 4.0 | `086b3ec1b48bc5942ac42ae274d9a26a75633152b1d444c649369aa4faa5d825` |  |
| dev | Slovak-SNK | `sk_snk-ud-dev.conllu` | 1,060 | 12,754 | CC BY-SA 4.0 | `5660577846d500db86ab139618518d106e0191a100155ee4e7d37254d6036819` |  |
| eval | Slovak-SNK | `sk_snk-ud-test.conllu` | 1,061 | 12,744 | CC BY-SA 4.0 | `3cdd11080a868685cf1fd5d20274603754aea00930847130c5d9e1b90ff5136b` | 4 test texts also in train |
| train | Slovenian-SSJ | `sl_ssj-ud-train.conllu` | 10,903 | 215,155 | CC BY-SA 4.0 | `c6991732aaecf8d6346bb10e154898093337ad93c6f029c0bd7c55ba94957243` |  |
| dev | Slovenian-SSJ | `sl_ssj-ud-dev.conllu` | 1,250 | 26,500 | CC BY-SA 4.0 | `2999a1ac8261df77e71c4e824c8e036e4845f84748f36d04bc7b730d7daf9d9a` |  |
| eval | Slovenian-SSJ | `sl_ssj-ud-test.conllu` | 1,282 | 25,442 | CC BY-SA 4.0 | `c14d5d2f4f20a7ad43e0f598a2e18c5e41f08364ab36be1c87d6d9eae7f5c8b0` |  |
| train | Slovenian-SST | `sl_sst-ud-train.conllu` | 5,094 | 76,964 | CC BY-SA 4.0 | `596ceeb89b1f21bfc8299562fe31c9bbaaab18ba7dae553e441cf113184b553e` |  |
| dev | Slovenian-SST | `sl_sst-ud-dev.conllu` | 595 | 9,986 | CC BY-SA 4.0 | `05e07d9d041eeb68feacfafd6d97a71e5b4b86c5688b9f4b072435aae1e5f5d8` |  |
| eval | Slovenian-SST | `sl_sst-ud-test.conllu` | 432 | 11,443 | CC BY-SA 4.0 | `7192fa2003550eb739d7aa3f38fc35fff5c1834829473fd66f9965b72b1e0354` | 7 test texts also in train |
| train | Southern_Kurdish-Garrusi | `sdh_garrusi-ud-train.conllu` | 152 | 1,177 | CC BY-SA 4.0 | `18a654a493307343a33d82f6076f9983a4b240204678b2b6a289460128fcfc57` |  |
| dev | Southern_Kurdish-Garrusi | `sdh_garrusi-ud-dev.conllu` | 49 | 372 | CC BY-SA 4.0 | `d21bfb6378c10ff321539e6849e8b671d254fed219188a7f903991ace2179a4f` |  |
| eval | Southern_Kurdish-Garrusi | `sdh_garrusi-ud-test.conllu` | 20 | 136 | CC BY-SA 4.0 | `1af8b35eef608b240ea24030ca86977b3c9ebad2fba681dff9f1a5df722b1bc4` |  |
| eval | Spanish_Sign_Language-LSE | `ssp_lse-ud-test.conllu` | 488 | 1,393 | CC BY-SA 4.0 | `7717cee81bae6100d168d6d6375c7c58b54fdf2f798bcc8a0003eb77857bd613` |  |
| eval | Swedish-Old | `sv_old-ud-test.conllu` | 40 | 507 | CC BY-SA 4.0 | `827ff5409b769c649b95cfa7687fee40af06182c17b9e81a6d2d214378a812e3` |  |
| eval | Swedish-PUD | `sv_pud-ud-test.conllu` | 1,000 | 19,076 | CC BY-SA 4.0 | `4f978396f363c85a9c7ece193327529e4cdbe9ce0f6ad807eb8846e865043fff` |  |
| eval | Swedish-SweLL | `sv_swell-ud-test.conllu` | 643 | 10,895 | CC BY-SA 4.0 | `7946c25f353bb771112dcde9c01ee28bf332fd5c885f3359476cc27f769ec21e` |  |
| train | Swedish-Talbanken | `sv_talbanken-ud-train.conllu` | 4,315 | 66,646 | CC BY-SA 4.0 | `5e3c41f35999453c94a0f66155f695e6699950c44741abe7d98a2305f7f3f63d` |  |
| dev | Swedish-Talbanken | `sv_talbanken-ud-dev.conllu` | 504 | 9,797 | CC BY-SA 4.0 | `e1c14ae088f575d9f5f2d870d456b9e3911a04725f141752bb8c972a81cb6c6c` |  |
| eval | Swedish-Talbanken | `sv_talbanken-ud-test.conllu` | 1,219 | 20,377 | CC BY-SA 4.0 | `f7bc84ce37cd6a71e95b8d8684801da0bb6d2be4e419ba2e865eb109d6cd1bc6` |  |
| eval | Swedish_Sign_Language-SSLC | `swl_sslc-ud-test.conllu` | 203 | 1,610 | CC BY-SA 4.0 | `aedbbaafc4aa3366c2b9c8e258256077206cc8d9f97684be581cf04e25b5d87d` |  |
| eval | Tagalog-TRG | `tl_trg-ud-test.conllu` | 128 | 734 | CC BY-SA 4.0 | `d21eb1072f6da530de2e0e3a9065a76ae36559efd43afc7c6d48264024afa8c1` |  |
| eval | Tamil-MWTT | `ta_mwtt-ud-test.conllu` | 534 | 2,584 | CC BY-SA 4.0 | `a1d1c312ce05d5a9de2f4272651f470920575cd59691e2e36085b61cbfad9318` |  |
| eval | Tatar-NMCTT | `tt_nmctt-ud-test.conllu` | 148 | 2,280 | CC BY-SA 4.0 | `94ed206be1ef7f91580598947e1d05542a694ab09a8cecad500142abb27c6bb8` |  |
| eval | Teko-TuDeT | `eme_tudet-ud-test.conllu` | 913 | 2,896 | CC BY-SA 4.0 | `383e2be7f7cd3a6bafb65dc0a4307f961f0a1cfe9473082b9d00c4b82a895892` |  |
| train | Telugu-MTG | `te_mtg-ud-train.conllu` | 1,051 | 5,082 | CC BY-SA 4.0 | `efc8e26e34962c5b46d8a13ad386adf2b9cc7c39b61ef3c8a8e6c37f7e536f6b` |  |
| dev | Telugu-MTG | `te_mtg-ud-dev.conllu` | 131 | 662 | CC BY-SA 4.0 | `81c5bd38ca55625fea9992add2a0588f639fec0ab8d492d4e2192df28f9be1bb` |  |
| eval | Telugu-MTG | `te_mtg-ud-test.conllu` | 146 | 721 | CC BY-SA 4.0 | `a25cae618253cc36081a3ccb83bf6fea44044c997e49e0b8d5e57f9e266ff9a8` | 4 test texts also in train |
| train | Telugu_English-TECT | `qte_tect-ud-train.conllu` | 61 | 290 | CC BY-SA 4.0 | `6b9500e18d250b10c805d81868599ad7656f2fe9a6763821636e3e487f52cf71` |  |
| eval | Telugu_English-TECT | `qte_tect-ud-test.conllu` | 36 | 166 | CC BY-SA 4.0 | `59785ac108d13fb7e3b4acce184c4d08d4d31affcdad02821ef2206ad81b342f` | 3 test texts also in train |
| eval | Thai-PUD | `th_pud-ud-test.conllu` | 1,000 | 22,330 | CC BY-SA 3.0 | `0eeeaf5cf4352df2fe0b7f114e29d5ab5c1f54476faf64e99313de14d588a31b` |  |
| train | Thai-TUD | `th_tud-ud-train.conllu` | 2,902 | 62,011 | CC BY-SA 4.0 | `e0d7d4aef175e4c1427585d66425dfc5ec60114b47b42a2aa0f3b9b17a82382e` |  |
| dev | Thai-TUD | `th_tud-ud-dev.conllu` | 362 | 7,521 | CC BY-SA 4.0 | `ad6a334d5cebb96e86b1d1928f30515765724e6041543833ad78393e3106dc93` |  |
| eval | Thai-TUD | `th_tud-ud-test.conllu` | 363 | 7,683 | CC BY-SA 4.0 | `294547eb580d2d44aee73f131355f7a9cb552a93515bef24b855d7366a24e2f7` |  |
| eval | Tswana-Popapolelo | `tn_popapolelo-ud-test.conllu` | 20 | 214 | CC BY-SA 4.0 | `3bac49736bc465f8692d40f170cd335d1e0a92597de3f522c9a92453c4e99e31` |  |
| eval | Tupinamba-TuDeT | `tpn_tudet-ud-test.conllu` | 581 | 4,508 | CC BY-SA 4.0 | `9357cf53c4dee3bb384225eeea46e0f8d4ea54f579d9dbccd4c84375ad13c692` |  |
| eval | Turkish_English-BUTR | `qti_butr-ud-test.conllu` | 58 | 441 | CC BY-SA 4.0 | `5f92c95d197348453a7f61b225b4ad4341f168025da81d7dbc69ca1ec867b1af` |  |
| train | Ukrainian-ParlaMint | `uk_parlamint-ud-train.conllu` | 5,611 | 87,369 | CC BY-SA 4.0 | `f470a0d5dfa842bce5b00e5fa0b1136e6243d404bb1968f133c7a3005d0105e9` |  |
| dev | Ukrainian-ParlaMint | `uk_parlamint-ud-dev.conllu` | 739 | 10,842 | CC BY-SA 4.0 | `ad62eac62bf08e977d82f9607a513d57c74e1e4216cd7bb2c6a4926bcbbe583d` |  |
| eval | Ukrainian-ParlaMint | `uk_parlamint-ud-test.conllu` | 792 | 10,955 | CC BY-SA 4.0 | `92e8aeeeec90d45b45f83d6c975f0ad6da0941cc9a73b17dd3588dd9e02e75ad` | 36 test texts also in train |
| eval | Umbrian-IKUVINA | `xum_ikuvina-ud-test.conllu` | 202 | 1,162 | CC BY-SA 4.0 | `6ba6697e5f2dda57795d4ff0061b8c1c20371f63da70de73a673181e3b4863bc` |  |
| eval | Upper_Sorbian-UFAL | `hsb_ufal-ud-test.conllu` | 644 | 11,167 | CC BY-SA 4.0 | `eeb14b07ff7af360f8afefc2907fd2c53cb64fa8ebd3dde07438c953c67a0063` |  |
| train | Uyghur-UDT | `ug_udt-ud-train.conllu` | 1,656 | 19,262 | CC BY-SA 4.0 | `8b41b86261138009342b6c4b5f6a51fa45746079712e37a74afeab841600d285` |  |
| dev | Uyghur-UDT | `ug_udt-ud-dev.conllu` | 900 | 10,644 | CC BY-SA 4.0 | `ae2ea20117e509bc9af19b80df58a911aba5d0e61f332c22b9aab2a47934dc90` |  |
| eval | Uyghur-UDT | `ug_udt-ud-test.conllu` | 900 | 10,330 | CC BY-SA 4.0 | `e0884a71a6e01ca24e175fce25f2e47a4ae750d734cae24fe974598b3aafd293` |  |
| eval | Uzbek-TueCL | `uz_tuecl-ud-test.conllu` | 148 | 939 | CC BY-SA 4.0 | `23257711196df0bbfaf5b25397d7c6102fbc8800ed83b15eb77342929ee0e469` |  |
| eval | Uzbek-UT | `uz_ut-ud-test.conllu` | 500 | 5,930 | CC BY-SA 4.0 | `6297430a3ac34275eefeab74d3b074618894704082e73e9d048ade561271a03a` |  |
| train | Uzbek-UzUDT | `uz_uzudt-ud-train.conllu` | 483 | 5,442 | CC BY-SA 4.0 | `c1fb9548cbba34e3dddd5ca2af7d5eb3e37f1e8a6e640e29061ff5b9e581aa12` |  |
| eval | Uzbek-UzUDT | `uz_uzudt-ud-test.conllu` | 201 | 2,140 | CC BY-SA 4.0 | `6a191e59b6cbaade250463109581e95350702eb85fff765f407eca00ed1937dc` | 1 test texts also in train |
| eval | Veps-VWT | `vep_vwt-ud-test.conllu` | 103 | 1,303 | CC BY-SA 4.0 | `82d5e7a1f25545ab434a49bd75bddce945079213a59f7f3ab0996a97600e8a3a` |  |
| eval | Vietnamese-TueCL | `vi_tuecl-ud-test.conllu` | 100 | 1,888 | CC BY-SA 4.0 | `e637df46377d8dee0f1dc4968bc323c2f82f2736343d1ae51820ef49f89ba0f2` |  |
| train | Vietnamese-VTB | `vi_vtb-ud-train.conllu` | 1,400 | 20,215 | CC BY-SA 4.0 | `1df180722db99b724f7368aca50597133bee28606de3394cd8d12a57da89978d` |  |
| dev | Vietnamese-VTB | `vi_vtb-ud-dev.conllu` | 1,123 | 26,162 | CC BY-SA 4.0 | `0b779257b9225575b3b30e15d64cc4ef9945a77f8cceb9f92d794b483a2752dd` |  |
| eval | Vietnamese-VTB | `vi_vtb-ud-test.conllu` | 800 | 11,692 | CC BY-SA 4.0 | `abc1b0ea98661bbb494a8c0da123ba2fe1a3ef71322c380fe66468884d9b4285` |  |
| eval | Warlpiri-UFAL | `wbp_ufal-ud-test.conllu` | 55 | 315 | CC BY-SA 4.0 | `4a890f5dca1e25850503da5cfb4ca43146a5c23241c4751487387337bc4ae4c9` |  |
| train | Welsh-CCG | `cy_ccg-ud-train.conllu` | 1,355 | 28,563 | CC BY-SA 4.0 | `9da8036f144dc6e74c5f52be4bade6d1b34494b649bc65935911b12fa21417c4` |  |
| dev | Welsh-CCG | `cy_ccg-ud-dev.conllu` | 409 | 9,123 | CC BY-SA 4.0 | `268f8d60acf9224d8d0c611fb9ba3236f1f1a97242b4ba13eb029a2c912a5ec5` |  |
| eval | Welsh-CCG | `cy_ccg-ud-test.conllu` | 953 | 17,026 | CC BY-SA 4.0 | `2a6cc2c9fffb01523436c91203fc5d656b2bf5a0b91868195527ee72a9a9f950` |  |
| train | Western_Armenian-ArmTDP | `hyw_armtdp-ud-train.conllu` | 5,274 | 95,823 | CC BY-SA 4.0 | `dc62b71b963f2b03efbf8ea203bfa0095f38b1941369517046bb64d2cfb1d10f` |  |
| dev | Western_Armenian-ArmTDP | `hyw_armtdp-ud-dev.conllu` | 693 | 13,430 | CC BY-SA 4.0 | `d263d8580c66c012c5a12fdb25dcb8533b5961d8ba34e580a6b60d659b32884e` |  |
| eval | Western_Armenian-ArmTDP | `hyw_armtdp-ud-test.conllu` | 677 | 13,499 | CC BY-SA 4.0 | `cf74cf3bd1728e8ea940a00bfd3cb03d1cd68a14c2d3a60ac4e59dd05c9e7e8f` | 3 test texts also in train |
| eval | Western_Sierra_Puebla_Nahuatl-MesoTree | `nhi_mesotree-ud-test.conllu` | 3,024 | 19,535 | CC BY-SA 4.0 | `db6c6986f9dfa3ae8c3fb08f2f529fbc95b030bd5a1a3a41b2d48b6fbaca047c` |  |
| train | Wolof-WTB | `wo_wtb-ud-train.conllu` | 1,188 | 23,561 | CC BY-SA 4.0 | `206e6579debb4f6d1c333edf6cce00d74da298390f98046cc04ba5a9210d832d` |  |
| dev | Wolof-WTB | `wo_wtb-ud-dev.conllu` | 449 | 10,294 | CC BY-SA 4.0 | `fb4c74f866805d3ba5c42616e5643c1021e23fa44b35971526355122107b8c06` |  |
| eval | Wolof-WTB | `wo_wtb-ud-test.conllu` | 470 | 10,403 | CC BY-SA 4.0 | `4e9dd202909447c914cbb38ced7d890482d0ac2794b8789f61440c6818f3bdb7` |  |
| eval | Xavante-XDT | `xav_xdt-ud-test.conllu` | 248 | 2,239 | CC BY-SA 4.0 | `164d32abd625b66dd12f3a3bc6b00e143f955facc6b5077fa7dac8238d88b39e` |  |
| eval | Yakut-YKTDT | `sah_yktdt-ud-test.conllu` | 299 | 1,460 | CC BY-SA 4.0 | `ecb202e2c652e5ddcdb84413a5a2a23b32f2e5a824ccb6dd67bbc8ea7db92bf0` |  |
| train | Yiddish-YiTB | `yi_yitb-ud-train.conllu` | 1,877 | 16,458 | CC BY-SA 4.0 | `19f9461e4edec9ba96e2f2ba618f08849ada24d711caed09910969d31a9caed1` |  |
| dev | Yiddish-YiTB | `yi_yitb-ud-dev.conllu` | 157 | 1,873 | CC BY-SA 4.0 | `1a6fda4b0a63aa2a0d6c46e939f9e91b90a29ea505a914a464185f3d5d529a68` |  |
| eval | Yiddish-YiTB | `yi_yitb-ud-test.conllu` | 1,079 | 10,017 | CC BY-SA 4.0 | `48631739ff20143774e2f4df952738d919444f92757c5b0102cd9624515da152` | 3 test texts also in train |
| eval | Yoruba-YTB | `yo_ytb-ud-test.conllu` | 317 | 8,243 | CC BY-SA 4.0 | `b6c475516f4d9c9cdb56ef4285ea8dad215fc21f245ecdf0f95b285ce6309896` |  |
| eval | Yupik-SLI | `ess_sli-ud-test.conllu` | 309 | 2,568 | CC BY-SA 4.0 | `7fa6d43981be1d2eb0875dd15c3163a2588182343ba304d3339a4565d21f38f0` |  |
| train | Zaar-Autogramm | `say_autogramm-ud-train.conllu` | 978 | 10,712 | CC BY-SA 4.0 | `8e20de1794e63618188d62e7a482d01a1d5617f7c82430defa09006e30195d3f` |  |
| eval | Zaar-Autogramm | `say_autogramm-ud-test.conllu` | 1,127 | 10,240 | CC BY-SA 4.0 | `2d5522629a3432d853ca506b7742dccea6fcb86db527001fe328656a3e645885` | 13 test texts also in train |
| train | Zazaki-ZSD | `zza_zsd-ud-train.conllu` | 138 | 978 | CC BY-SA 4.0 | `4b1bf756562bc9b1a8e9de15ad5010ebcdafc0bc185b6a981b7773637850aa01` |  |
| dev | Zazaki-ZSD | `zza_zsd-ud-dev.conllu` | 30 | 213 | CC BY-SA 4.0 | `68dbc77b063a9e2121a56c611608020b3abe578bb5f71f72a5a12bb7e1d17aaa` |  |
| eval | Zazaki-ZSD | `zza_zsd-ud-test.conllu` | 32 | 250 | CC BY-SA 4.0 | `0e8df3978de4eaeaaaab10c81bac4c89d848abb41e7a4f5417603164b00d5b33` |  |

URL of every file: `https://raw.githubusercontent.com/UniversalDependencies/UD_<Language>-<Treebank>/r2.18/<file>`.
