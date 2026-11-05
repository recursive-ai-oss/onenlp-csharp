# English fast tier, one shared encoder

Trains every English head on one fast encoder with the public `Train` API: tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, biaffine dependency parser with sentence starts, and entity recognizer. This is the recipe behind `RecursiveAI.OneNlp.Models.English`.

The two datasets are not the same text: Universal Dependencies English-EWT carries tags, lemmas and trees, UNER English-EWT carries entities. Every training example is one or the other, and a head skips the examples that hold no gold for it, so each head learns from its own data while all of them shape the shared encoder. The development score is the mean of the tagging mean (UPOS, XPOS, UFeats, Lemmas), LAS and entity F1, so no head is given up for another.

    run.sh <data-root> <work-dir> [options]
    dotnet RecursiveAI.OneNlp.Recipe.EnJoint.dll predict --model M.onenlp --gold test.conllu --mode raw|gold [--mwt] --out P.conllu

`<data-root>` holds `train/ud-en-ewt-r2.18/` and `train/uner-en-ewt-8ed072de/` as fetched by `tools/fetch/en/fetch.sh`. Set `JOINT_DLL` and `NER_DLL` to the built recipe assemblies (`recipes/en/joint` and `recipes/en/ner`). Each call of `run.sh` trains for at most `CHUNK_SECONDS` (default 1500) and resumes from the last checkpoint, so a long run is the same command repeated until it prints `model=...`; the result is the same as one uninterrupted run on the same machine and thread count.

Defaults are the shipped configuration: encoder 128 wide with 64-wide embedding rows, one window layer, ReLU, 16,384-row tables for the neighbouring words and the wider context; parser with one bidirectional LSTM layer of 192 and no label biaffine; entity head reading two tokens either side. Every number can be changed from the command line (see the top of `Program.cs`). Training uses one thread; the full run is about five hours of one core.

The fast encoder keeps a second, inference-only form of itself (the joining layer folded into the embedding tables, one product per window layer), so a trained model costs a table lookup per attribute and one small product per token; training is unchanged.
