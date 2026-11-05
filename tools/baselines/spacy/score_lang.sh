#!/usr/bin/env bash
# Scores the predictions of one language's spaCy baseline with OneNlp.Eval (bootstrap 95% intervals, 1000
# resamples over sentences) and writes one Markdown and one JSON file per test set and mode.
#
#   score_lang.sh <lang> <predictions-dir> <data-root> <OneNlp.Eval.dll> <out-dir>
#
# <predictions-dir> holds what run_lang.sh's score stages wrote: pred-ud-<test>-<raw|gold>.conllu and
# gold-ner-<test>.tsv / pred-ner-<test>.span.tsv. Uses the same test lists as run_lang.sh.
set -euo pipefail
LANG_=${1:?lang}; PRED=${2:?predictions-dir}; DATA=${3:?data-root}; DLL=${4:?OneNlp.Eval.dll}; OUT=${5:?out-dir}
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
SEED=20261105
mkdir -p "$OUT"
# Pull the test lists out of run_lang.sh so the two scripts cannot disagree: the case entry of this language runs from its
# label to the next label; grep -m1 stops at the first match without breaking the pipe.
ENTRY=$(sed -n "/^  $LANG_) /,/^  [a-z][a-z]) \|^  \*)/p" "$HERE/run_lang.sh")
UD_TESTS=$(grep -o -m1 'UD_TESTS="[^"]*"' <<< "$ENTRY" | cut -d'"' -f2)
NER_TESTS=$(grep -o -m1 'NER_TESTS="[^"]*"' <<< "$ENTRY" | cut -d'"' -f2)
for t in $UD_TESTS; do
  GOLD=$DATA/eval/ud/$LANG_/$t-ud-test.conllu
  for mode in raw gold gold-gp; do   # gold-gp: gold_preproc training (Chinese only), see run_lang.sh
    P=$PRED/pred-ud-$t-${mode/-gp/}$([ $mode = gold-gp ] && echo -gp || true).conllu
    [ -s "$P" ] || { echo "skip $P"; continue; }
    COMP="spaCy 3.8.18 default pipeline"; [ $mode = gold-gp ] && COMP="$COMP (gold_preproc)"
    # A scorer error is reported and the other files still run (Portuguese-Bosque is rejected by the CoNLL-U reader, see PROVENANCE.md).
    dotnet "$DLL" ud --gold "$GOLD" --pred "$P" --component "$COMP" --language "$LANG_" \
      --dataset "UD r2.18 $t test ($mode)" --resamples 1000 --seed $SEED \
      --json "$OUT/ud-$t-$mode.json" --markdown "$OUT/ud-$t-$mode.md" > /dev/null || echo "scorer failed: $P" >&2
  done
done
for t in $NER_TESTS; do
  for v in "" -gp; do
    G=$PRED/gold-ner-$t.tsv; P=$PRED/pred-ner-$t$v.span.tsv
    [ -s "$P" ] || { echo "skip $P"; continue; }
    dotnet "$DLL" span --gold "$G" --pred "$P" --component "spaCy 3.8.18 default ner$v" --language "$LANG_" \
      --dataset "$t test (gold tokens)" --resamples 1000 --seed $SEED \
      --json "$OUT/ner-$t$v.json" --markdown "$OUT/ner-$t$v.md" > /dev/null
  done
done
ls "$OUT"
