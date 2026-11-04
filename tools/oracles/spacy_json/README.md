# spaCy `Doc.to_json` / `Doc.from_json` interop

`spacy_json.py` checks that our JSON document format (`docs/json-format.md`) and spaCy's agree, using spaCy only through its documented public API (`spacy.blank`, `Doc(vocab, words, spaces, ...)`, `Doc.to_json`, `Doc.from_json`). Tested against spaCy 3.8.18 (MIT).

    python spacy_json.py produce <dir>   # spaCy writes <case>.spacy.json for hand-annotated documents
    python spacy_json.py consume <dir>   # spaCy reads every <case>.ours.json and serializes it again; the shared fields must match

The fixtures in `testdata/serialization/spacy/` are the output of `produce` (text and annotations we wrote) and of our writer. `OneNlp.Tests` reads the first with our reader and compares our output to it; `consume` closes the loop in the other direction. Regenerate our files with `ONENLP_WRITE_FIXTURES=1` when the writer changes on purpose.
