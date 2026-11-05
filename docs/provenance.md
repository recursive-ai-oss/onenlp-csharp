# Model provenance logs

Every trained model, ours or a baseline we train for comparison, ships with a provenance log. The log is a Markdown file named `PROVENANCE.md` next to the recipe that produced the model; it is packed into the model package's notices. It answers four questions: what data went in, who produced the labels, how to reproduce it, and why the evaluation can be trusted.

## Rules the log records

- **Allowed training data:** permissive data (public domain, CC0, CC BY, MIT, Apache-2.0, BSD), CC BY-SA and ODbL data and crawled web text (with credit), our own labels, and synthetic labels from open-weight models. Out: non-commercial, research-only, paid or LDC data, and data under terms that forbid the use.
- **Share-alike lineage.** Mark every model whose training data includes a CC BY-SA or ODbL source (`SA lineage: yes`), with the sources, so the credit and any later licensing question can be answered from the log alone.
- **Evaluation data never trains.** Every data file has a role (`train`, `dev`, `eval`) in the data manifest. Training code reads `train` and `dev` files only. The log records the manifest hash and the result of the train/test separation check.

## Template

Copy this block, fill every field, and write `none` or `n/a` where a field does not apply. Do not leave a field out.

````markdown
# Provenance: <model id>

- **Model:** <id, version>; <one line on what it is: components, language, tier>
- **Recipe:** <path to the recipe directory>; config hash <sha256 of the config file>
- **Seed(s):** <training seed, data-order seed, split seed>
- **Produced:** <date>; machine <CPU, cores>; wall time <h:mm>; software <SDK/runtime or Python and library versions, library version of the trainer>
- **Weights:** <file name(s)>, <size> bytes, sha256 <hash>
- **SA lineage:** <yes | no>; <if yes, which sources>
- **Licence of the weights:** <licence>

## Training data

| Dataset | Release | Role | Files | Licence (quoted from the dataset's own licence file) | URL | sha256 |
|---|---|---|---|---|---|---|

## Label generator

<none: labels are the datasets' gold annotations | the model, its version, its weights licence and where it was run, the prompts or settings, the number of examples labelled>

## Pretrained components

<encoder, embeddings or teacher models used as a starting point, with licence, source URL, sha256, and the notice that ships with it; `none` if trained from scratch>

## Evaluation data

| Dataset | Release | Role | Files | sha256 | Used for |
|---|---|---|---|---|---|

## Train/test separation

- Manifest: <path or name> sha256 <hash>
- Text-level overlap of each evaluation file with the training and dev files: <exact and case/whitespace-folded counts; how the overlap was handled>
- Evaluation files were not used for early stopping, model selection or hyperparameter choice: <yes | no; if no, say what and why>

## Results

<where the scores and the prediction files are, and the command that regenerates them>

## Notes

<anything that affects reuse: known data problems, deviations from the recipe, hold items>
````

## Where the pieces live

- The data manifest for a dataset family is written by its fetch script (for English, `tools/fetch/en/fetch.sh` writes `manifest-en.json` under the data root, and `tools/fetch/en/MANIFEST.md` keeps the hashes and licences in the repository).
- Recipes and their logs: `recipes/<model-id>/PROVENANCE.md`. Baselines trained with other tools for comparison: next to the baseline scripts, for example `tools/baselines/spacy/PROVENANCE.md`.

## Data entries for the 0.5 languages

One entry per language for the data our fast-tier models and their spaCy baselines train and are judged on. Each lists the datasets with release, role and licence; file-level hashes, split sizes and the train/test text overlap are in `tools/fetch/<lang>/MANIFEST.md`; the baselines' own log is `tools/baselines/spacy/lang/PROVENANCE.md`. The licence verdict for every UD treebank of the language is in `docs/ud-licences.md`. **All of these are SA lineage** (CC BY-SA sources, or Wikipedia-derived text) except where a line says otherwise. Evaluation files never train: `fetch_lang.py` places them under `eval/`, and the roles in each manifest are `train`, `dev`, `eval`.

| Language | Training and dev data (release, licence) | Evaluation data | Notes |
|---|---|---|---|
| Chinese | UD Chinese-GSDSimp r2.18 (CC BY-SA 4.0); UNER Chinese-GSDSIMP (CC BY-SA 4.0) | GSDSimp test; UNER GSDSIMP test; UD Chinese-PUD and UNER Chinese-PUD (CC BY-SA 3.0, test-only, Traditional script) | Never train on the Traditional twin GSD and test on GSDSimp, or the reverse |
| French | UD French-GSD r2.18 (CC BY-SA 4.0); WikiNER French wp3 (CC BY 4.0, silver labels, Wikipedia text) | GSD test (416 sentences); UD French-PUD (CC BY-SA 3.0, test-only); WikiNER test (our article split) | Sequoia and FQB held (LGPL-LR) |
| Spanish | UD Spanish-AnCora r2.18 (CC BY 4.0, not SA); WikiNER Spanish wp3 (CC BY 4.0) | AnCora test; UD Spanish-PUD; WikiNER test | The AnCora README still carries a stale GNU-licence sentence; `LICENSE.txt` and metadata say CC BY 4.0 |
| Portuguese | UD Portuguese-Bosque r2.18 (CC BY-SA 4.0); UNER Portuguese-Bosque (CC BY-SA 4.0, same tokens) | Bosque test; UNER Bosque test; UD and UNER Portuguese-PUD (CC BY-SA 3.0, test-only) | |
| German | UD German-GSD r2.18 (CC BY-SA 4.0); GermEval 2014 NER (CC BY 4.0, not SA) | GSD test; UD German-PUD; GermEval test; UNER German-PUD (test-only) | HDT held (text licensed for academic use) |
| Italian | UD Italian-ParlaMint r2.18 train (CC BY-SA 4.0) and MarkIT (CC BY 4.0), derived into `it_gating`; WikiNER Italian wp3 (CC BY 4.0) | ParlaMint test (375 sentences); MarkIT test; UD Italian-PUD; WikiNER test | 897 training sentences: ISDT, VIT, ParTUT, PoSTWITA, KIParlaForest are NC; TWITTIRO held |
| Turkish | UD Turkish-BOUN r2.18 (CC BY-SA 4.0); Turkish-WikiNER (CC BY-SA 4.0) | BOUN test; UD Turkish-PUD; Turkish-WikiNER test | IMST is NC; Penn held (translated Penn Treebank) |
| Hindi | none for UD. Naamapadam Hindi (CC0 packaging; silver labels over parallel crawled text) | UD Hindi-PUD (test-only); Naamapadam test (gold) | HDTB is NC. No allowed Hindi treebank exists |
| Dutch | UD Dutch-Alpino r2.18 (CC BY-SA 4.0); WikiNER Dutch wp3 (CC BY 4.0) | Alpino test; WikiNER test | LassySmall (CC BY-SA 4.0) allowed, unused |

The corpora with no official split (WikiNER, Naamapadam train and validation) are cut by the fixed rules in each `HEADER.md`/`MANIFEST.md`; the Italian training set is derived by `derive_italian` in `tools/fetch/fetch_lang.py`.
