# Catalyst (black-box comparator)

Catalyst is the managed .NET NLP pipeline our English fast tier is compared against on speed, counted only at matched or better accuracy. This tool runs its English pipeline on the English-EWT test text and writes files the shared scorers read. Information only: it gates nothing by itself.

- **Packages:** `Catalyst` 26.10.6514 and `Catalyst.Models.English` 26.9.5316, from nuget.org (pinned in `CatalystOracle.csproj`).
- **Licence:** both packages declare `MIT` in their NuGet metadata (`license` expression, checked 2026-11-04). The English models ship inside `Catalyst.Models.English` (a 36 MB assembly); nothing is fetched at run time. Observed: the storage directory the tool hands to Catalyst stays empty after a run, and the run works with the model package alone. The model package's copyright line also credits HoldOffHunger (2019) for a British-to-American lookup table.
- **Use:** the usage follows the package's published README (register the English models, build a `Pipeline`, process a `Document`). The package contents were not unpacked or read.

```
dotnet run -- ud  <ewt-test.conllu> <out.conllu>   # tokens, sentences, UPOS, lemmas
dotnet run -- ner <uner-test.iob2>  <out.tsv>        # PER/ORG/LOC entity tokens
python3 score_spans.py <uner-test.iob2> <out.tsv>   # character-span entity F1
python3 <conll18_ud_eval.py> -v <ewt-test.conllu> <out.conllu>
```

## What it can and cannot be compared on

- **Raw text only.** Catalyst tokenizes and splits sentences itself, so there is no gold-tokenization row. The raw text of each test document is rebuilt from the gold forms and `SpaceAfter`.
- **No dependency parser, no XPOS, no morphology.** The CoNLL-U file carries UPOS and lemmas only; every word is attached to the first word so the scorer sees a valid tree. The XPOS, UFeats, AllTags, UAS, LAS and related columns of its score file are meaningless and are not reported.
- **NER.** The default English pipeline has no person/organisation/location model. The README's separate entity model, tag `WikiNER`, is added for the `ner` mode (it comes with the English model package). It tokenizes differently from UNER, so the score is on character spans (exact begin, end and label), not tokens, and is therefore not directly comparable with token-based NER F1. Its other entity type (e-mail or URL) is dropped.
- The tool prints a NuGet advisory warning for `MessagePack` 3.1.8 (a dependency of Catalyst); it is shown, not suppressed as an error, because the tool only reads our own test text.

## Results (UD English-EWT r2.18 test, UNER English-EWT test; 2026-11-04)

AMD Ryzen 9 7950X3D, .NET 10.0.403; accuracy only, no timing.

| Row | Catalyst |
|---|---|
| Tokens (raw) | 94.47 |
| Sentences (raw) | 64.57 |
| Words (raw) | 94.75 |
| UPOS (raw) | 80.70 (aligned accuracy 85.17) |
| Lemmas (raw) | 83.84 (aligned accuracy 88.48) |
| NER micro F1, character spans, PER/ORG/LOC | 57.77 (PER 64.47, ORG 45.92, LOC 59.85; P 67.07, R 50.74) |

Output hashes: `pred-ud-raw.conllu` `a638f28f1568d051fb0d792eac2ccfc82f5130c4cf61e4873b20927333dbec6c`, `pred-ner.tsv` `c08f7e14b7639fb780ddad8d45a699da7ba59a3b06a564a8c8ef663f1a440456`.
