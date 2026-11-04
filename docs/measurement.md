# Measurement

How OneNLP scores its own output, how it puts confidence intervals on scores, and how a results row is judged against a reference. The scorers live in `src/OneNlp/Scoring/`; the command-line tool that turns predictions into results rows is `tools/OneNlp.Eval/`.

## Universal Dependencies metrics

`UdScorer` computes the metrics of the CoNLL 2018 shared task on parsing from raw text to Universal Dependencies (Zeman et al., 2018, "CoNLL 2018 Shared Task: Multilingual Parsing from Raw Text to Universal Dependencies", §5; evaluation description at <https://universaldependencies.org/conll18/evaluation.html>).

**Character stream.** A corpus's text is the concatenation of its token forms (a multiword token counts once, as its own form) with spaces removed. Gold and predicted corpora must have the same stream, otherwise scoring throws. Every token, word and sentence is a span over this stream. The words of a multiword token share the token's span. Empty nodes (IDs like `8.1`) are ignored.

| Metric | Defined as |
|---|---|
| Tokens, Sentences | F1 of the spans: a predicted span is correct when a gold span has the same start and end. |
| Words | F1 of the aligned words (below). |
| UPOS, XPOS, UFeats, AllTags, Lemmas | Precision and recall over words; a gold word and its aligned predicted word agree on the column (AllTags: UPOS, XPOS and UFeats together). |
| UAS | Aligned words whose head is the aligned counterpart of the gold head (both roots also count). |
| LAS | UAS and the same relation. |
| CLAS | LAS restricted to content words (below). |
| MLAS | CLAS, and the same UPOS and UFeats, and the functional children of the word (below) agree one for one in attachment, relation, UPOS and UFeats. |
| BLEX | CLAS, and the same lemma. |

All of them report precision (correct / predicted words), recall (correct / gold words) and F1 = 2·correct / (gold + predicted). Where a metric restricts to content words, each side counts its own content words. `AlignedAccuracy` is correct over aligned gold words. The figure usually quoted, and the one the results table carries, is F1.

**Word alignment.** Words with the same span align. Where a multiword token is involved on either side, the words of the group of overlapping tokens are aligned by the longest common subsequence of their lower-cased forms. A plain word that ends before a multiword token starts aligns with nothing. A group larger than 64 million word pairs is refused.

**Relations.** Language-specific subtypes are dropped (`nmod:poss` is `nmod`). Content relations: `nsubj obj iobj csubj ccomp xcomp obl vocative expl dislocated advcl advmod discourse nmod appos nummod acl amod conj fixed flat compound list parataxis orphan goeswith reparandum root dep`. Functional relations: `aux cop mark det clf case cc`.

**Lemmas.** A gold lemma of `_` accepts any predicted lemma.

**Features.** UFeats compares only the 21 universal features of the UD guidelines (`Abbr Animacy Aspect Case Definite Degree Evident Foreign Gender Mood NumType Number Person Polarity Polite Poss PronType Reflex Tense VerbForm Voice`), in any order. Everything else (`Typo`, `NumForm`, `Style`, `ExtPos`, layered features such as `Number[psor]`) is ignored; a word with no universal feature has `_`. The shared-task description does not spell this out; it was settled by running the reference script, as a program, on crafted pairs (script SHA-256 `1072e02af00b1a56205b5e8216d51dee9b8944a104d80744afaccc78859fcb16`).

**Heads.** A head out of range, or a cycle among the heads of a sentence, is an error for gold and predicted data alike.

Inputs are CoNLL-U files (`UdCorpus.ReadFile`) or `Doc`s (`UdCorpus.FromDocs`; UPOS is `Token.Pos`, XPOS `Token.Tag`, features `Token.Morph`, relation `Token.Dep`; a token that is its own head is a root). `UdScorer.Score(predicted, gold, "LAS")` returns a `NamedScore` in points for use as a training loop's development score.

## Span F1

`SpanScorer` scores entities and other labelled spans by exact match: a predicted span is correct when its start, end and label equal a gold span's (Tjong Kim Sang and De Meulder, 2003, the CoNLL-2003 span-level definition as `conlleval` and `seqeval` compute it). Precision = correct / predicted, recall = correct / gold, F1 their harmonic mean, 0 where undefined. It reports micro counts, counts per label, and macro F1 (the mean of the per-label F1 over labels in the gold or the prediction).

Tag sequences are read as strict IOB2: a span is `B-X` followed by any number of `I-X`. An `I-X` that does not continue such a span, and an `I-Y` after `B-X`, starts nothing and is ignored. This equals seqeval's `mode="strict", scheme=IOB2`.

## Text classification

`ClassificationScorer`: accuracy; per-label counts, macro F1 (unweighted mean over labels in the gold or the prediction) and micro F1 (equal to accuracy for single-label tasks); for multi-label tasks, macro and micro F1 at a threshold (0.5 by default) and ROC-AUC. ROC-AUC per label is the Mann-Whitney statistic with ties counting half (Hanley and McNeil, 1982); the multi-label figure is the mean over labels, skipping labels with only positives or only negatives.

## Bootstrap confidence intervals

`Bootstrap` implements the percentile bootstrap (Efron and Tibshirani, 1993, §13.3).

- **Resampling unit.** UD: the gold sentence. Each gold item and its aligned predicted item count for the gold sentence in which the gold item starts; a predicted item without a gold counterpart counts for the gold sentence containing its first character. Span F1: the sentence or document of the input. Classification: the document.
- **Statistic.** Counts add up across units, so a resample sums its units' counts and takes F1 (`Bootstrap.F1Points`); nothing is re-scored.
- **Draws.** 1,000 resamples by default (at least 100), each drawing as many units as the test set has, with replacement. The generator is SplitMix64 from the caller's seed, so a seed gives the same draws on every platform; the default seed is recorded in each row.
- **Interval.** The resampled values are sorted; the interval runs from the entry at `floor(0.025 · B)` to the entry at `ceil(0.975 · B) − 1` (0-based), so for 1,000 resamples entries 25 and 974. The estimate is the statistic on the full test set.
- **Paired.** To compare two systems on the same test set, `Bootstrap.Paired` applies the same draw to both on each resample and reports each system's interval and the interval of the difference. The systems differ when the difference's interval excludes 0.

## The within-noise rule

A quality row passes when our score is within noise of its reference. Scores are in points (0 to 100).

- **The reference has an interval** (we ran it ourselves on the same test set): within noise when our interval and the reference's overlap.
- **The reference is a published number** with no interval: within noise when it lies inside our interval, or is within 0.5 points of our score.

`NoiseRule.WithinNoise` implements both.

## Producing a results row

```
dotnet OneNlp.Eval.dll ud --gold gold.conllu --pred ours.conllu \
    --component "fast tagger" --language en --dataset "UD English-EWT test" \
    --reference UPOS=96.4,LAS=88.1 --reference-source "published, <citation>" \
    --metrics UPOS,LAS --resamples 1000 --seed 20260101 --json rows.json --markdown rows.md
```

With `--ref-pred reference.conllu` (a reference system's output on the same test set) the reference gets its own interval and the row carries the paired difference. `span --gold gold.iob2 --pred ours.iob2` does the same for entity F1 (micro F1, precision, recall; files have one token per line, the tag in the last column, blank lines between sentences). The Markdown row is

`| Component | Language | Metric | Score | 95% CI | Reference | Within noise |`

and the JSON row carries the same fields plus the dataset, the reference source, the resample count and the seed.

## Cross-check against the reference scorers

`tools/eval/crosscheck.py` checks the scorers against the reference programs, which run as black boxes:

- **UD:** the shared-task evaluation script (`conll18_ud_eval.py`, from the shared-task site; run through its public functions at full precision and once as a program for its two-decimal table) on UD English-EWT test (release r2.15) paired with predictions that `tools/eval/perturb_ud.py` makes by seeded errors: wrong tags, features, lemmas, relations and heads; split and merged words; multiword tokens added, removed and re-spelled; merged and split sentences; blanked columns.
- **Span F1:** seqeval in strict IOB2 mode against `tools/eval/perturb_span.py`'s synthetic tag sets, including malformed predictions and gold.

Every precision, recall, F1 and aligned accuracy must agree to four decimal places (in points). `tools/fetch/conll18_crosscheck.sh` fetches the data and tools and checks them against pinned SHA-256 values.
