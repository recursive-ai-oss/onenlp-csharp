# Provenance: langid-75

- **Model:** langid-75 v1; the language-identification model of `RecursiveAI.OneNlp` (75 languages, linear softmax over hashed character n-grams, one table per script group). Embedded in the core assembly at build time from the weights directory (`ONENLP_LANGID_WEIGHTS`); listed in `src/OneNlp/LanguageId/weights.manifest.props`.
- **Recipe:** `recipes/langid`; config hash `7581539548222c131b5aa2c95e765a85a5967532eb5ce954474b8fe6b71e7368` (`config.txt`).
- **Seed(s):** training seed 1 (sample order and views); split seed: none, the split is by SHA-256 of each sentence's letter key (see `tools/fetch/langid/fetch.py`); evaluation draw seeds `main-<lang>` and `tatoeba-<lang>` (Python `random.Random` string seeds).
- **Produced:** 2026-11-04; AMD Ryzen 9 7950X3D (32 threads), 4 cores used; wall time about 0:04 for training; .NET SDK 10.0.403, OneNLP trainer at the commit that carries this file.
- **Weights:** `langid.bin`, 3,588,959 bytes, sha256 `9d06cc77632083274ae5ca5fbb0f3e676abfc63aa6a2d702cbdf9d558cba9d6d`.
- **SA lineage:** yes. Wikipedia-derived text: the Leipzig `wikipedia` corpora listed in `tools/fetch/langid/sources.tsv`, and the Wikipedia dumps of `st`, `ts` and `tn`.
- **Licence of the weights:** `MIT OR Apache-2.0`, with the credits in `THIRD-PARTY-NOTICES.md`.

## Training data

| Dataset | Release | Role | Files | Licence | URL | sha256 |
|---|---|---|---|---|---|---|
| Leipzig Corpora Collection, news, newscrawl, web and mixed corpora | per `sources.tsv` (years 2010–2024, up to 100,000 sentences each) | train (and eval for the first corpus of each language, see below) | `tools/fetch/langid/SHA256SUMS` lists every archive | "The text corpora offered for download are made available under the Creative Commons licence CC BY" (Terms of Usage page, checked 2026-11-04) | https://downloads.wortschatz-leipzig.de/corpora/ | per archive in `SHA256SUMS` |
| Leipzig Corpora Collection, Wikipedia corpora | per `sources.tsv` | train (eval for languages whose first corpus is Wikipedia) | same | same; the text is from Wikipedia (CC BY-SA 4.0) | same | same |
| Wikipedia dumps `stwiki`, `tswiki`, `tnwiki`, latest at fetch time | fetched 2026-11-04 | train and eval slice | `SHA256SUMS` | CC BY-SA 4.0 (Wikimedia terms of use) | https://dumps.wikimedia.org/ | `stwiki` 361a92b6…, `tnwiki` c1dc56e5…, `tswiki` 44d96bc5… |
| Tatoeba sentence export | weekly export, tarball sha256 7821ce66… | train (at most 20,000 per language), and eval slice | `sentences.csv` from the tarball | "default Creative Commons Attribution 2.0 France" (Tatoeba terms of use §6.2); licences of single sentences vary | https://tatoeba.org/en/downloads | see DESIGN Sources |

**Evaluation data never trains.** The evaluation files are `eval/langid/main/{words,pairs,sentences}.tsv` (the held-out pool of the first corpus of each language, 68 languages entirely excluded from training) and `eval/langid/tatoeba/*` (5% hash slice of Tatoeba). The manifest with SHA-256 of each is `MANIFEST.sha256` (itself `8eafe43b…`). For 7 languages (mi, sn, st, tn, ts, xh, yo) the held-out texts are a hash slice of the only corpus available (`same-corpus` in `dedup-report.tsv`). `dedup-report.tsv` lists, per language, how many training sentences were removed because their letters-only identity matched an evaluation sentence (4,906 in all). The training code loads only `train/langid/<lang>.txt`; the last 400 lines of each file are a development set, used for the progress report and for fitting the temperature, never trained on.

## Label generator

None: the label of a sentence is the language of the corpus it comes from.

## Pretrained components

None; trained from scratch.

## Evaluation data

See above and `docs/language-id.md`. References: `lingua-language-detector` 2.2.0 (PyPI, Apache-2.0) and `SearchPioneer.Lingua` 2.0.0 (NuGet, Apache-2.0), run as black boxes by `tools/oracles/Lingua`. Results: `results/`.
