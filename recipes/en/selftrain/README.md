# English fast tier, self-trained narrow encoders

The packaged English model keeps the architecture of `recipes/en/fast-tagging`, `parser` and `ner` (hash embeddings plus a small encoder per task) but its tagging and entity encoders are narrower and have no window layer, so they run about twenty times faster at prediction. A narrow encoder alone fits the gold annotation worse than a wide one, so it is trained on more text: the earlier (wide) models label running web text, and the narrow model trains on the treebank plus those labels. This is self-training in the sense of Scudder 1965 and Yarowsky 1995; here the teacher is larger than the student.

Steps (all with the public API, through `recipes/en/joint`):

1. `label --model teacher-tagging.onenlp --text text.jsonl --out labelled.conllu` labels each sentence of the text with the teacher's tagger, morphologizer and lemmatizer. `label-ner` does the same for entities.
2. `tools/selftrain/check_overlap.py KEYS.npy --clean-to DIR labelled.conllu ...` drops every labelled sentence whose normalised key (letters and digits only, case-folded, NFKC) occurs in the evaluation key set, which covers the train, development and test splits of every treebank and entity set used in this repository. Counts are in `results/dedup-report.jsonl`.
3. `train --heads tagging --width 96 --depth 0 --extra-train labelled.conllu --epochs 8` for the tagging encoder; `train --heads ner --width 128 --depth 0 --ner-context 1 --ner-hidden 128 --extra-ner labelled.jsonl --extra-docs 120000 --epochs 12` for entities. The development split of the treebank (or of UNER) selects the checkpoint; the test split is read once for the reported rows.
4. `recipes/en/assemble` puts the tagging model, the parser (unchanged, from `recipes/en/parser`) and the entity model into one file.

The parser was tried with the same recipe (an LSTM of 160 or 192 over a 128-wide encoder with no window layer) and stayed 1.2 points of development LAS behind the wide one, so the packaged parser is the earlier model.
