# Recognizer gold authoring

`en.src.txt` is the source of the English recognizer gold set; `build.py` validates it and writes `testdata/recognizers/en/gold.jsonl` (development) and `test.jsonl` (held-out). The schema, labelling rules, split and the rule about who may read the test file are in `docs/recognizer-gold.md`.

```
python3 tools/recognizers-gold/build.py            # writes testdata/recognizers/en/
python3 tools/recognizers-gold/build.py --out DIR  # writes elsewhere
```
