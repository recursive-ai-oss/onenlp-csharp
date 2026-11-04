# Third-party notices

OneNLP's own code is licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC.

Packages depend on other Recursive AI libraries (`RecursiveAI.OneFont`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training`), which carry the same licence and their own notices. This preview ships no third-party data, models or weights; each such addition will be listed here with its licence and provenance before it is packed.

## Test data (not packed)

`tests/testdata/unicode/WordBreakTest.txt` and `SentenceBreakTest.txt` are the Unicode 17.0.0 UAX #29 conformance files, © 2025 Unicode, Inc., redistributed under the Unicode License v3 (https://www.unicode.org/license.txt). They are used only by the test project and are not part of any package.

## Test fixtures

`tests/OneNlp.Tests/Matching/expected.json.gz` records the output of spaCy 3.8.18 (MIT licence, Copyright Explosion) on documents and patterns written for this repository. It is used only by tests.
