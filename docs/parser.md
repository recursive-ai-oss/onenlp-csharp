# Dependency parser and noun chunks

Normative for the `parser` head (`RecursiveAI.OneNlp.Neural.Components.Parser`) and `Doc.NounChunks`.

## Parser

A graph-based parser: Dozat and Manning 2017, "Deep Biaffine Attention for Neural Dependency Parsing" (§2), with maximum-spanning-tree
decoding (McDonald et al. 2005, using the Chu-Liu/Edmonds algorithm of Chu and Liu 1965 and Edmonds 1967). Chosen over a
transition-based parser (Goldberg and Nivre 2012, with pseudo-projective lifting, Nivre and Nilsson 2005) because the loss is a plain
differentiable function of the whole sentence (no oracle runs inside the training step), the batch is a few large matrix products,
and the decoder returns non-projective trees without a lifting step. The transition-based design was not implemented, so there is no
measured comparison.

- **Input.** One vector per token from the encoder. Optionally `ContextLayers` residual window layers (`x <- x + ReLU(W [x(i-1); x(i); x(i+1)])`, the ends of a document read zeros), then optionally a bidirectional LSTM (`RecurrentSize` hidden units per direction, `RecurrentLayers` layers) run over each document. The documents of a batch form one padded LSTM batch with their lengths (packed-sequence semantics), so a document's parse does not depend on which others share the batch. The recipe uses no window layers and a 2-layer LSTM of 192 per direction: on the same encoder, window layers alone reached 74.9 dev LAS and the LSTM 79.7 (see the recipe's results).
- **Arcs.** Two ReLU projections (of the LSTM output) to `ArcSize` (default 256). The score of token *j* as the head of token *i* is `d(i)' U h(j) + b . h(j)`; column 0 is an artificial root with a learned vector; a token cannot head itself (a fixed -10000 on the diagonal). Training uses the softmax cross entropy over the heads of each token.
- **Labels.** Two ReLU projections to `LabelSize` (default 128). The score of relation *c* for the arc (*i*, *j*) is `d(i)' U_c h(j) + W [d(i); h(j)] + b`, trained on the gold arcs with cross entropy. Two options trade accuracy for speed. `labelBilinear: false` drops the matrix `U_c` per relation, which costs `LabelSize` squared times the number of relations multiply-adds per token and was most of the label work. `labelProduct: true` then adds the element-wise product to the linear map, `W [d(i); h(j); d(i) * h(j)] + b`; the product is the diagonal of the bilinear form, so the scorer keeps a multiplicative interaction at three times the width instead of a full matrix per relation. Both are written to the model file only when set, so older files load unchanged.
- **Sentences.** A tree is built per sentence. If the document already has sentence boundaries they are used. Otherwise a two-class classifier on the same vectors marks sentence starts and the boundaries are written into the document. Training examples that hold several sentences are what teach it (the recipe groups ten). A sentence longer than 400 tokens is parsed in pieces of 400.
- **Decoding.** The maximum spanning arborescence over the arc scores with exactly one token attached to the root: if the free solution has several, each candidate root is forced in turn and the best total score wins. Labels: only the token attached to the root can take the relation `root`, and it takes no other (when the label set holds `root`).
- **Labels set.** Every dependency relation in the training examples' reference documents, in ordinal order, subtypes included.
- **Use in training.** An example is used only when predicted and reference tokens match one to one. The loss is the sum of arc, label and boundary cross entropies over the batch, divided by the number of tokens.

### Speed configuration

The first English parser (two LSTM layers of 192, arc 256, label 128 with a matrix per relation) took 137 microseconds per token on one thread, of which the LSTM was about 80 and the label scorer about 50; the arc scorer, the projections and the tree decoder together were under 20. The shipped parsers use `labelBilinear: false`, `labelProduct: true` and one LSTM layer:

| Languages | LSTM per direction | Arc, label size | Why |
|---|---|---|---|
| English, German, Dutch, Portuguese, Spanish, French | 128 | 128, 64 | the cheapest setting whose dev LAS stayed within about 1 point of the larger parser |
| Italian, Chinese | 192 | 192, 96 | the smaller setting lost 2.4 (Italian) and 3.4 (Chinese) dev LAS on these small treebanks; this one lost 0.8 and 1.4 |

English dev LAS (UD English-EWT r2.18, gold tokens, selection by dev only), same encoder, seed 7 unless noted: 2 x 192 with a bilinear label scorer 79.66; 1 x 128, arc 128, label 64, product 79.40 (seed 8: 79.06); 1 x 192, arc 192, label 96, product 79.74; 2 x 128 78.74; 2 x 96 78.73; 1 x 160 78.89; 1 x 128 with one window layer 78.49. Seed-to-seed spread on dev is about 0.5, so the differences among the one-layer settings are not resolved by the dev set.

The LSTM step is memory-bound at 192 units (a step reads 590 KB of weights per direction) and compute-bound at 128; prediction code is in `LstmInference` and `RowKernels`, unchanged by this configuration. Speed numbers and test rows are in `docs/results.md`.

## Noun chunks

`Doc.NounChunks` returns base noun phrases from heads, relations and parts of speech: a NOUN, PROPN or PRON token that is a subject, object, indirect object, oblique, nominal modifier, apposition, nominal predicate (`xcomp`) or the root, or is `conj` in a chain that ends in one of those, plus the words before it in its phrase. Relation subtypes `tmod`, `npmod` and `poss` do not make a phrase head. Phrases never overlap; the next is skipped when its left edge falls inside the previous.

The rule was written from spaCy's published description (a flat phrase with a noun as its head, with no nested noun phrase) and its
exact conditions settled by black-box comparison with spaCy 3.8.18 on trees built with `Doc(vocab, words, pos, heads, deps)`:
- spaCy's English rule answers to a fixed set of relation names (`nsubj`, `nsubjpass`, `dobj`, `dative`, `pobj`, `appos`, `attr`, `pcomp`, `oprd`, `ROOT`, plus `conj`), not to UD names, so the core rule is checked in a profile that uses those names (`NounChunkProfile.ClearStyle`) and the UD profile maps onto it;
- a phrase runs from its left edge to the head token; the left edge is the smallest index in the head's subtree;
- the second and later `conj` of a chain qualify when the first conjunct's relation does.

The Universal Dependencies profile (the one `NounChunks` uses) differs from spaCy's English rule in two ways: a case marker, conjunction, subordinator, copula, auxiliary or clause (`case`, `cc`, `mark`, `cop`, `aux`, `advcl`, `acl`, `ccomp`, `csubj`, `parataxis`, ...) does not extend a phrase to the left, so *of the staff* gives *the staff*; and the phrase extends to the right over `flat`, `goeswith` and `fixed` dependents, so a name written head-first (*Ada Lovelace*) is one phrase.

Differential result: `tests/OneNlp.Tests/NounChunkTests.cs` compares with spaCy on 4,000 random trees (crossing arcs included, seed recorded in the fixture) and the 2,001 tree shapes of the EWT r2.18 dev set with relations mapped. Known residue (not in the committed fixture, found in larger runs on other seeds: 3 in 90,000 random trees): spaCy under-reports the left edge of a phrase when arcs cross several times in a row; this occurs in about 1 in 30,000 of the random trees and in none of the treebank trees. Regenerate the fixture with `tools/oracles/spacy_chunks/make_cases.py`.
