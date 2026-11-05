#!/usr/bin/env bash
# Black-box spaCy baseline for one of zh fr es pt de it tr hi nl, trained on the data tools/fetch/<lang>/fetch.sh
# fetched. Same recipe as run.sh (English): spaCy's released CLI, the configs `spacy init config` wrote
# (tools/baselines/spacy/lang/<lang>/), only the data paths overridden.
#
#   run_lang.sh <lang> <stage> <data-root> <work-dir>
#
# stages: convert | train-ud | train-ner | score-ud | score-ner
# env:    GOLD_PREPROC=1  train and score on the gold tokenization (corpora.*.gold_preproc = true, a documented option
#                         of the training config), writing model-ud-gp / model-ner-gp and *-gp files; used for Chinese, whose
#                         default pipeline segments into single characters and so cannot be given gold words by the default run
#         ONENLP_ORACLES  directory with the venvs (default $HOME/onenlp/oracles; needs spacy/, seqeval/, ud-tools/)
#         THREADS         BLAS/OpenMP threads (default 1; spaCy's training loop uses one core)
#
# The score stages write predictions and the reference scorers' output; the intervals come from tools/OneNlp.Eval
# (see docs/measurement.md) run over the prediction files.
set -euo pipefail
LANG_=${1:?lang}; STAGE=${2:?stage}; DATA=${3:?data-root}; WORK=${4:?work-dir}
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ORACLES=${ONENLP_ORACLES:-$HOME/onenlp/oracles}
PY=$ORACLES/spacy/bin/python
SPACY=$ORACLES/spacy/bin/spacy
THREADS=${THREADS:-1}
export OMP_NUM_THREADS=$THREADS OPENBLAS_NUM_THREADS=$THREADS MKL_NUM_THREADS=$THREADS
CFG=$HERE/lang/$LANG_

# --- per-language data ---------------------------------------------------------------------------
# UD_PREFIX: train/dev files are train/ud/<lang>/<UD_PREFIX>-ud-{train,dev}.conllu
# UD_TESTS:  evaluation treebanks (first = gating), eval/ud/<lang>/<name>-ud-test.conllu
# NER_*:     directory under train|eval and the file prefix; NER_TESTS lists evaluation files (first = gating)
# NER_MAP:   entity type renaming (types not listed become O), empty = keep the file's types
UD_PREFIX=""; UD_TESTS=""; NER_KIND=""; NER_DIR=""; NER_PREFIX=""; NER_TESTS=""; NER_MAP=""
case $LANG_ in
  zh) UD_PREFIX=zh_gsdsimp; UD_TESTS="zh_gsdsimp zh_pud"
      NER_KIND=uner; NER_DIR=zh; NER_PREFIX=zh_gsdsimp-ud; NER_TESTS="zh_gsdsimp-ud zh_pud-ud" ;;
  fr) UD_PREFIX=fr_gsd; UD_TESTS="fr_gsd fr_pud"
      NER_KIND=ner; NER_DIR=fr/wikiner-fr-wp3; NER_PREFIX=wikiner-fr-wp3; NER_TESTS="wikiner-fr-wp3" ;;
  es) UD_PREFIX=es_ancora; UD_TESTS="es_ancora es_pud"
      NER_KIND=ner; NER_DIR=es/wikiner-es-wp3; NER_PREFIX=wikiner-es-wp3; NER_TESTS="wikiner-es-wp3" ;;
  pt) UD_PREFIX=pt_bosque; UD_TESTS="pt_bosque pt_pud"
      NER_KIND=uner; NER_DIR=pt; NER_PREFIX=pt_bosque-ud; NER_TESTS="pt_bosque-ud pt_pud-ud" ;;
  de) UD_PREFIX=de_gsd; UD_TESTS="de_gsd de_pud"
      NER_KIND=ner; NER_DIR=de/germeval-2014; NER_PREFIX=germeval-2014; NER_TESTS="germeval-2014"
      NER_MAP="PER=PER,LOC=LOC,ORG=ORG,OTH=MISC" ;;
  it) UD_PREFIX=it_gating; UD_TESTS="it_parlamint it_markit it_pud"
      NER_KIND=ner; NER_DIR=it/wikiner-it-wp3; NER_PREFIX=wikiner-it-wp3; NER_TESTS="wikiner-it-wp3" ;;
  tr) UD_PREFIX=tr_boun; UD_TESTS="tr_boun tr_pud"
      NER_KIND=ner; NER_DIR=tr/turkish-wikiner; NER_PREFIX=turkish-wikiner; NER_TESTS="turkish-wikiner"
      NER_MAP="PERSON=PER,ORG=ORG,GPE=LOC,LOC=LOC" ;;
  hi) UD_PREFIX=""; UD_TESTS="hi_pud"
      NER_KIND=ner; NER_DIR=hi/naamapadam-hi; NER_PREFIX=naamapadam-hi; NER_TESTS="naamapadam-hi" ;;
  nl) UD_PREFIX=nl_alpino; UD_TESTS="nl_alpino"
      NER_KIND=ner; NER_DIR=nl/wikiner-nl-wp3; NER_PREFIX=wikiner-nl-wp3; NER_TESTS="wikiner-nl-wp3" ;;
  *) echo "unknown language $LANG_" >&2; exit 2 ;;
esac
ner_train_file() { # split
  if [ "$NER_KIND" = uner ]; then echo "$DATA/train/uner/$NER_DIR/$NER_PREFIX-$1.iob2"; else echo "$DATA/train/ner/$NER_DIR/$NER_PREFIX-$1.iob2"; fi
}
ner_test_file() { # prefix
  if [ "$NER_KIND" = uner ]; then echo "$DATA/eval/uner/$NER_DIR/$1-test.iob2"; else echo "$DATA/eval/ner/$NER_DIR/$1-test.iob2"; fi
}
GP=""; GPARGS=()
if [ "${GOLD_PREPROC:-}" = 1 ]; then GP=-gp; GPARGS=(--corpora.train.gold_preproc true --corpora.dev.gold_preproc true); fi
if [ "$(uname)" = Darwin ]; then TIMEV="/usr/bin/time -l"; else TIMEV="/usr/bin/time -v"; fi
mkdir -p "$WORK/ud" "$WORK/ner"

case $STAGE in
convert)
  if [ -n "$UD_PREFIX" ]; then
    # --n-sents 10: several sentences per document, so the parser sees sentence boundaries inside a document.
    for s in train dev; do
      $SPACY convert "$DATA/train/ud/$LANG_/$UD_PREFIX-ud-$s.conllu" "$WORK/ud" --converter conllu --n-sents 10
    done
  fi
  MAP_ARG=(); [ -n "$NER_MAP" ] && MAP_ARG=("$NER_MAP")
  for s in train dev; do
    $PY "$HERE/prepare_ner.py" "$(ner_train_file $s)" "$WORK/ner/$s.spacy" "$LANG_" ${MAP_ARG[@]+"${MAP_ARG[@]}"}
  done
  shasum -a 256 "$WORK"/ud/*.spacy "$WORK"/ner/*.spacy 2>/dev/null || sha256sum "$WORK"/ud/*.spacy "$WORK"/ner/*.spacy
  ;;
train-ud)
  [ -n "$UD_PREFIX" ] || { echo "no allowed UD training data for $LANG_" >&2; exit 3; }
  $TIMEV $SPACY train "$CFG/tagger_parser.cfg" --output "$WORK/model-ud$GP" ${GPARGS[@]+"${GPARGS[@]}"} \
    --paths.train "$WORK/ud/$UD_PREFIX-ud-train.spacy" --paths.dev "$WORK/ud/$UD_PREFIX-ud-dev.spacy"
  ;;
train-ner)
  $TIMEV $SPACY train "$CFG/ner.cfg" --output "$WORK/model-ner$GP" ${GPARGS[@]+"${GPARGS[@]}"} \
    --paths.train "$WORK/ner/train.spacy" --paths.dev "$WORK/ner/dev.spacy"
  ;;
score-ud)
  EV=$ORACLES/ud-tools/conll18_ud_eval.py
  for t in $UD_TESTS; do
    GOLD=$DATA/eval/ud/$LANG_/$t-ud-test.conllu
    for mode in raw gold; do
      [ -n "$GP" ] && [ $mode = raw ] && continue   # a gold-preprocessed model is only meaningful on gold tokens
      $PY "$HERE/predict_ud.py" "$WORK/model-ud$GP/model-best" "$GOLD" $mode "$WORK/pred-ud-$t-$mode$GP.conllu"
      echo "== CoNLL 2018 ($t, $mode tokenization$GP)"
      $PY "$EV" -v "$GOLD" "$WORK/pred-ud-$t-$mode$GP.conllu" | tee "$WORK/scores-ud-$t-$mode$GP.txt"
    done
  done
  ;;
score-ner)
  for t in $NER_TESTS; do
    GOLD=$(ner_test_file $t)
    $PY "$HERE/predict_ner.py" "$WORK/model-ner$GP/model-best" "$GOLD" "$WORK/pred-ner-$t$GP.tsv" ${NER_MAP:+"$NER_MAP"}
    echo "== seqeval ($t, gold tokens)"
    $ORACLES/seqeval/bin/python "$HERE/score_ner.py" "$WORK/pred-ner-$t$GP.tsv" | tee "$WORK/scores-ner-$t$GP.txt"
    # Prediction files in the shape OneNlp.Eval 'span' reads: token, tag (last column).
    cut -f1,2 "$WORK/pred-ner-$t$GP.tsv" > "$WORK/gold-ner-$t.tsv"
    cut -f1,3 "$WORK/pred-ner-$t$GP.tsv" > "$WORK/pred-ner-$t$GP.span.tsv"
  done
  ;;
*) echo "unknown stage $STAGE" >&2; exit 2;;
esac
