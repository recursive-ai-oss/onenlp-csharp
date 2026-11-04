# Tools

Scripts that support the tests and measurements, none of which ship in a package:

- drivers for black-box comparisons (Python, run in pinned virtual environments),
- scripts that fetch and pin datasets, models and reference tools by version and SHA-256.

Datasets, models and reference outputs are never committed, except small outputs on text we wrote.

- `oracles/spacy_matcher/` : inputs and driver for the rule-matcher comparison with spaCy; see its README.
- `recognizers-gold/` : authoring source and builder of the English recognizer gold set; see `docs/recognizer-gold.md`.
- `oracles/Recognizers/`, `oracles/duckling/` and `oracles/score_recognizers.sh` : the Microsoft.Recognizers.Text and Duckling comparators for the recognizer gold set.
