# Comparator scores on the English recognizer gold set

Scores of two black-box comparators, produced by `tools/oracles/score_recognizers.sh` and described in `docs/recognizer-gold.md`. `*.test.*` is the held-out split, `*.gold.*` the development split. Each `.json` holds every row with its interval; each `.md` is the same table.

- Microsoft.Recognizers.Text 1.8.13 (`Microsoft.Recognizers.Text`, `.Number`, `.NumberWithUnit`, `.DateTime`, `.Sequence`), English culture, run on .NET 10.0.403.
- Duckling from the container image `rasa/duckling:0.2.0.2-r4` (digest `sha256:55e7adfb6d63eb0d1c1bdb8a28080a74d4d281871258c2ca2d7cd0eaf29b2d11`), `en_US`, `tz=UTC`.
- Reference time per sentence: the `reference` field of the gold line (UTC). Bootstrap: 1000 resamples over sentences, seed 20260101.
- Run on an AMD Ryzen 9 7950X3D (32 threads), Linux x64. Nothing here is timed.
