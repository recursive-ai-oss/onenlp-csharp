# Hindi fast-tier recipe

Hindi has no treebank or entity corpus that we may train on (Hindi-HDTB and the Samanantar-sourced entity sets are non-commercial; `docs/ud-licences.md`), and Hindi-PUD is a test set. So the Hindi student is trained on **labels that the accurate tier makes on Hindi web text**, in four steps that this program runs:

1. `train-related`: heads for languages related to Hindi, trained on the **frozen** encoder of the accurate tier (`Related.cs`). Marathi (UD Marathi-UFAL and Marathi-CMUPAN) shares Hindi's script and much of its grammar; Sindhi (Sindhi-Isra) and Punjabi (Punjabi-Rang, Punjabi-CS) are Indo-Aryan too. A head set is a morphologizer (UPOS, UFeats) and a biaffine parser; the encoder output is cached per document, so the epochs after the first only train the small heads (a 100,000-token head set trains in about ten minutes on eight cores). Transfer works better from related languages than from distant ones (McDonald, Petrov and Hall 2011; Rosa and Zabokrtsky 2015; Lin et al. 2019), and a head trained on the encoder joins the existing source languages as one more voter.
2. `label`: the shared multilingual encoder reads each Hindi sentence once, and every source head writes its analysis of it: the accurate tier's heads for nine languages (English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish, Chinese; each trained on its own treebank and entity data) and the related-language heads. This is zero-shot cross-lingual transfer through a multilingual encoder with several source languages (McDonald et al. 2011; Conneau et al. 2020; Wu and Dredze 2019). Explicit adult content is filtered out of the text first (below).
3. `ensemble`: the analyses are combined by agreement (Zhou and Li 2005, tri-training: independently trained models agreeing is the confidence).
   - UPOS is the plurality label of the **voting sources** (`--use`: German, French, Dutch, Turkish, Chinese and the four related-language head sets); a UFeats value is kept when more than half of them give it.
   - The tree is the medoid of their trees (highest mean unlabelled agreement with the others), with each relation voted among the sources that share its head.
   - A sentence is kept for syntax when the mean plurality share of its UPOS is at least 0.6, the medoid's mean agreement is at least 0.5, and it has one root.
   - Entities are spans (type and boundaries) that at least half of the nine large languages' heads produce (`--ner-use`: the related languages have no entity data); a sentence with a span that two or more but fewer than half produce is left out of the entity files.
   - The thresholds were chosen once, before any student was trained, to keep about half of the sentences, and kept when the voters changed (57% of the sentences are kept for syntax). They were not tuned.
4. The student trains on the kept sentences with `train-tagging` (a morphologizer: UPOS and UFeats), `train-parser` and `ner-train`: the same fast-tier heads and `Train` API as the other languages. There is **no lemmatizer and no XPOS head**. Nothing transfers for XPOS (the sets are language-specific). For lemmas, an edit-tree lemmatizer trained on Marathi was scored on 315 nouns, verbs and adjectives that we lemmatized by hand (`testdata/hi/selfdev.lemmas.tsv`): 62.5% against 61.0% for copying the form, which is no real gain (Marathi lemmas are stems, Hindi's are infinitives in -ना), so the package fills `Token.Lemma` with the token's text and says so.

## Adult-content filter

FineWeb-2 is crawled web text and contains explicit adult pages. `AdultFilter.cs` is a plain rule that prefers losing a harmless document to keeping an explicit one: a document is dropped whole when it holds a word from a list we wrote (Devanagari, romanised Hindi and English explicit terms) or a host name in its text that carries an adult keyword (`...porn...com`); a sentence is dropped when it holds a word from a second, weaker list (words that are also used in news about courts, health and sport). Words are whole tokens, so `लंडन` (London) is not hit by the entry for a vulgar word that it begins with. The sample has no page addresses, so the address rule only sees addresses that occur in the text. The list is in the source and is easy to extend. It flags 352 of the 12,449 documents of the Hindi sample (2.83%); applied to the labels of the first round it would have removed 3,061 of 61,270 labelled sentences (5.0%, explicit pages are long). The labelling run applies it before the encoder reads anything (`label`: 89 of the 3,300 labelled documents and 125 further sentences), and `ensemble --text` applies it again to any labels made without it.

## Data

- **Text:** FineWeb-2 `hin_Deva` (ODC-By 1.0, Common Crawl terms), the Hindi sample of the distillation text (`tools/distill/fetch_data.py`), of which the first 3,300 documents are labelled. Sentences are cut with the Hindi tokenizer and sentencizer (the danda `।` and the full stop). They are kept when they have 5 to 50 tokens, nearly all letters Devanagari and mostly words, and dropped when they share an 8-word run with a Hindi-PUD sentence or repeat. Hits against Hindi-PUD: 0 (the distillation text had already been filtered against the evaluation keys).
- **Related-language treebanks** (UD r2.18, CC BY-SA 4.0, fetched by `tools/fetch/fetch_ud.py`): Marathi-UFAL (373 training sentences), Marathi-CMUPAN (7,616; 2,000 or all used), Sindhi-Isra (4,096; 1,200 to 2,000 used), Punjabi-Rang (81) and Punjabi-CS (73). Sanskrit-Vedic (2,000 sentences) was tried and is not used: its parse scores 12 LAS on its own development set and 7 on ours.
- **Evaluation:** Hindi-PUD is read once, for the final model (`evaluate.sh final`). Selection and checks use 5% of the labelled documents held out (agreement with the labeller only), 100 sentences we annotated ourselves (`testdata/hi/selfdev.conllu`: 17 held-out web sentences and 83 that we wrote, UPOS, heads and relations in the UD Hindi style, no 8-word run shared with Hindi-PUD) and 91 sentences with hand-labelled entities (`testdata/hi/nerspot.iob2`), from held-out web documents. The first ranked the source languages and the labelling model, and nothing else.

## Commands

```
R="dotnet run -c Release --project recipes/hi/fast --"
recipes/hi/fast/train-related.sh mr WEIGHTS OUT/rel "TRAIN.conllu[:N],..." "DEV.conllu[:N],..."   # resumable; also mrx, sd, indo (see PROVENANCE.md)
LANGS=en,de,...,indo EXTRA_HEADS=OUT/rel/heads recipes/hi/fast/label-chunk.sh WEIGHTS TEXT.jsonl PUD.conllu OUT/label K 300   # chunk K of 300 documents; 11 chunks
$R ensemble --in OUT/label/chunk-0000.jsonl.gz,... --out ENS --pos-agree 0.6 --uas-agree 0.5 \
    --use de,fr,nl,tr,zh,mr,mrx,sd,indo --ner-use en,de,fr,es,pt,it,nl,tr,zh --text TEXT.jsonl
recipes/hi/fast/train-part.sh tagging|parser|ner ENS STUDENT              # resumable
LABEL_LANGS=... EXTRA_HEADS=... LABEL_USE=... NER_USE=... recipes/hi/fast/evaluate.sh dev STUDENT-MODELS WEIGHTS OUT   # hand-annotated sentences
LABEL_LANGS=... EXTRA_HEADS=... LABEL_USE=... NER_USE=... recipes/hi/fast/evaluate.sh final STUDENT-MODELS WEIGHTS OUT PUD.conllu  # Hindi-PUD, once
$R lemma-probe --weights WEIGHTS --heads OUT/rel/heads/mrlem.onenlp --gold testdata/hi/selfdev.conllu --lemmas testdata/hi/selfdev.lemmas.tsv
tools/baselines/spacy/hi_selflabelled.sh ...                              # the same-labels baseline
```

`WEIGHTS` is the directory of the accurate tier's exported files (encoder and `heads/<language>.onenlp`). `label` reads both layouts (trainable parts, or the ONNX graph in pieces) and changes nothing in them. Put the three `model.onenlp` files in one directory as `tagging.onenlp`, `parser.onenlp` and `ner.onenlp` for `predict`, `speed` and the package. `score-sources.sh` and `variants.sh` score the source languages and the pooling variants on the hand-annotated sentences.

## Choices that the evidence settled

Development sentences, gold tokens, 100 sentences (931 tokens), UPOS / UAS / LAS:

| Labeller | UPOS | UAS | LAS |
|---|---|---|---|
| nine large languages, earlier encoder, first 17 sentences (208 tokens) | 79.33 | 63.94 | 50.48 |
| nine large languages, shipped encoder | 83.03 | 71.64 | 59.51 |
| Marathi head alone (Marathi-UFAL + 2,000 CMUPAN) | 80.02 | 80.02 | 68.85 |
| Marathi head, all CMUPAN (`mrx`) | 79.27 | 77.23 | 64.88 |
| Sindhi head alone | 86.68 | 82.17 | 66.49 |
| Indo-Aryan pool (`indo`: Marathi, Sindhi, Punjabi) | 87.97 | 75.30 | 63.69 |
| Sanskrit-Vedic head alone (not used) | 25.99 | 26.21 | 7.09 |
| the best single large language (German / Turkish / Turkish) | 77.23 | 63.91 | 47.26 |
| de, fr, nl, tr, zh + mr, mrx, sd, indo (**used**) | 92.16 | 82.38 | 74.44 |
| mr, sd, indo, de, tr, zh | 92.59 | 82.60 | 73.47 |
| de, fr, nl, tr, zh, mr, mrx, sd, indo, sa | 90.01 | | |
| all nine + the four related | 90.33 | 77.34 | 68.85 |
| mr, mrx, sd, indo only | 89.47 | 81.85 | 71.64 |
| mr, sd, indo | 89.04 | 81.95 | 71.43 |
| mr, sd | 87.76 | 80.02 | 68.85 |

The 95% intervals are about +-2 points for UPOS and +-4 for LAS (bootstrap, 200 resamples); the top variants are within noise of each other, so the set was picked once on these numbers and kept.

- **Labelling model.** The encoder is the one of the shipped base-size accurate tier (continued training, lemma tables, served from its ONNX graph); the first round used the earlier weights. With the related-language heads the labeller's gold-token UPOS on Hindi-PUD rose from 72.77 to 85.55 and its LAS from 26.43 to 49.04.
- **Related languages.** Marathi alone has a better parse than all nine large languages together (LAS 68.9 against 59.5), as the related-language literature suggests. Sindhi, in another script, still gives the best single UPOS among them. Sanskrit-Vedic does not transfer (Rigvedic verse). Marathi-heavy variants (`mrx` with all CMUPAN, `indo` with Punjabi and Sindhi) were pooled with the plain Marathi head as separate voters.
- **Voters.** English, Spanish, Portuguese and Italian are left out of the vote for UPOS and the tree (the set with them is 5.6 LAS lower on the development sentences), but they still vote on entities, where the related languages have no head.
- **Segmentation.** UAX #29 plus the sentencizer need no Hindi data: the danda is its own token and ends a sentence. No `SegmentationData.Hindi` exists.
