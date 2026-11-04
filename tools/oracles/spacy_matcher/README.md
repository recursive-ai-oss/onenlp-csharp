# spaCy matcher oracle

Black-box comparison inputs for `Matcher`, `PhraseMatcher` and `EntityRuler`.

- `generate_cases.py OUT.json` writes the cases: documents with hand-set annotation, token patterns (every attribute, operator and predicate), phrase lists, entity-rule sets, strings for the lexical flags, and patterns that must be rejected. Pure Python, fixed seed.
- `run_oracle.py CASES.json OUT.json` runs spaCy over the cases through its documented API (`Doc(vocab, words=..., spaces=...)`, `Matcher`, `PhraseMatcher`, the `entity_ruler` pipe, token attributes) and records the answers, the spaCy version and a sweep of single-code-point properties.

Run both in a virtual environment with spaCy installed, then compress the two outputs into `tests/OneNlp.Tests/Matching/cases.json.gz` and `expected.json.gz` (`gzip -9n`). The recorded version is checked by the tests.
