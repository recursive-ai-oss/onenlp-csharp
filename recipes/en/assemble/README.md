# Assemble English models into one pipeline file

Puts separately trained pipelines (each with its own encoder) into one pipeline file, so a model package can carry a single file.

    dotnet RecursiveAI.OneNlp.Recipe.EnAssemble.dll <out.onenlp> tagging.onenlp parser.onenlp ner.onenlp

The tokenizer and string table come from the first part. Components are added in order; a rule `sentencizer` is dropped, since the parser sets the sentence boundaries. No weights change, so each component predicts what it predicted in its own file. The inputs of the packaged English model are listed in `src/OneNlp.Models.English/PROVENANCE.md`.
