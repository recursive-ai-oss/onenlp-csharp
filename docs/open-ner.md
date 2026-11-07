# Open-type entities

Entity types named at run time: `Nlp.Entities(text, "contract party", "governing law")`, or `OpenEntityRecognizer.Recognize(doc, types)` on a document you tokenized.

## Model

- The encoder reads the text once. Candidate spans (up to 12 tokens, flat in the output) get a vector from the span's first, last and mean token vectors and its width; every type name is run through the same encoder and mean-pooled and passed through a feed-forward layer to one vector. A span scores a type with a dot product of the two (scaled by 1/sqrt(width), plus a learned offset) in a shared 128-wide space, and a sigmoid turns it into a probability (Zaratiana et al. 2023, sections 3.2 to 3.4; the paper only).
- Training uses binary cross-entropy per token-type cell. A type an example lists with no spans is a negative; a type it does not list teaches nothing, since one data set may label "person" and not "actor". Up to `negativeTypes` negatives are drawn per example.
- Prediction keeps spans at or above the threshold (0.5 by default, per call with `threshold`), and where two overlap the more probable wins. The label is the type string as you gave it. Type vectors are cached, so a fixed list of types costs one encoder pass per type, once.
- Types: 1 to 256 distinct strings of at most 200 characters, in any language the encoder reads (the shipped head is trained on English).

## Happy path

`Nlp.Entities(string text, params string[] types)` finds the registered open-type pipeline (`ModelPackages.RegisterOpenEntities`, called by the model package) and throws, naming `RecursiveAI.OneNlp.Models.Multilingual`, when there is none. Nothing is downloaded. A pipeline's `Recognize` is thread-safe.

## Data and limits

The recipe (`recipes/openner`) trains on Few-NERD and UNER English (CC BY-SA 4.0) and on entity labels written by Qwen2.5-7B-Instruct-AWQ (Apache-2.0) for FineWeb and Wikipedia passages, after a precision filter. The model therefore has share-alike lineage. CrossNER (MIT) only measures. Results, hand-check numbers and every hash are in `recipes/openner/PROVENANCE.md` and `results.md`.

The model is English only and well below the best published open-type recognizers on zero-shot sets (CrossNER 21 to 41 F1 on our run; recall is the weak side). When the types are fixed and you have labels, train the ordinary `EntityRecognizer`.

A saved head names its encoder, as every head does; load it with the encoder's directory available.
