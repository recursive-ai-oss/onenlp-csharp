# Third-party notices

OneNLP's own code is licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC.

Packages depend on other Recursive AI libraries (`RecursiveAI.OneFont`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training`), which carry the same licence and their own notices. This preview ships no third-party models or weights; each addition will be listed here with its licence and provenance before it is packed. The data it does ship is listed under "Data packed with the library".

## Data packed with the library

- `src/OneNlp/Recognizers/Data/en/sequence.rules` lists the domain endings (top-level domains) of the IANA Root Zone Database, `tlds-alpha-by-domain.txt` version 2026110304 (https://data.iana.org/TLD/tlds-alpha-by-domain.txt). IANA dedicates its registry data to the public domain under CC0 1.0 (https://www.iana.org/help/licensing-terms).
- `src/OneNlp/Recognizers/Data/en/currency.rules` lists the alphabetic currency codes of ISO 4217. The codes are facts of the published standard; the file carries no text of the standard.

## Test data (not packed)

`tests/testdata/unicode/WordBreakTest.txt` and `SentenceBreakTest.txt` are the Unicode 17.0.0 UAX #29 conformance files, © 2025 Unicode, Inc., redistributed under the Unicode License v3 (https://www.unicode.org/license.txt). They are used only by the test project and are not part of any package.

## Test fixtures

`tests/OneNlp.Tests/Matching/expected.json.gz` records the output of spaCy 3.8.18 (MIT licence, Copyright Explosion) on documents and patterns written for this repository. It is used only by tests.
