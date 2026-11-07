#!/usr/bin/env bash
# Black-box spaCy baseline for Hindi on the self-labelled data (recipes/hi/fast): spaCy's released command-line tool with the configuration
# `spacy init config` writes by default, trained on the same labelled sentences as our student. Only spaCy's documented CLI and public Python
# API are used.
#
#   hi_selflabelled.sh <stage> <labelled-dir> <work-dir>
#
# stages: init | convert-ud | train-ud | convert-ner | train-ner
#   labelled-dir  holds hi-ud-{train,dev}.conllu and hi-ner-{train,dev}.iob2 (the files `recipes/hi/fast ensemble` wrote)
#   env: ONENLP_ORACLES  directory with the venvs (default $HOME/onenlp/oracles; needs spacy/), THREADS  BLAS threads (default 1)
#
# The pipeline is `morphologizer,parser`: the labelled data has UPOS, UFeats and the parse, but no XPOS and no lemmas, so the tagger and
# the trainable lemmatizer of the default English pipeline have nothing to learn from. The ner pipeline is `ner`, default as for the other
# languages. Prediction and scoring use predict_ud.py / predict_ner.py of this directory and tools/OneNlp.Eval.
set -euo pipefail
STAGE=${1:?stage}; DATA=${2:?labelled directory}; WORK=${3:?work directory}
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ORACLES=${ONENLP_ORACLES:-$HOME/onenlp/oracles}
PY=$ORACLES/spacy/bin/python
SPACY=$ORACLES/spacy/bin/spacy
export OMP_NUM_THREADS=${THREADS:-1} OPENBLAS_NUM_THREADS=${THREADS:-1} MKL_NUM_THREADS=${THREADS:-1}
mkdir -p "$WORK/ud" "$WORK/ner" "$WORK/cfg"
case $STAGE in
init)
  $SPACY init config "$WORK/cfg/morph_parser.cfg" --lang hi --pipeline morphologizer,parser --optimize efficiency
  $SPACY init config "$WORK/cfg/ner.cfg" --lang hi --pipeline ner --optimize efficiency
  shasum -a 256 "$WORK"/cfg/*.cfg 2>/dev/null || sha256sum "$WORK"/cfg/*.cfg
  ;;
convert-ud)
  # Ten sentences per document, so the parser sees sentence boundaries inside running text (as for the other languages).
  for s in train dev; do
    $SPACY convert "$DATA/hi-ud-$s.conllu" "$WORK/ud" --converter conllu --n-sents 10
  done
  ;;
train-ud)
  /usr/bin/time -p $SPACY train "$WORK/cfg/morph_parser.cfg" --output "$WORK/model-ud" \
    --paths.train "$WORK/ud/hi-ud-train.spacy" --paths.dev "$WORK/ud/hi-ud-dev.spacy"
  ;;
convert-ner)
  for s in train dev; do
    $PY "$HERE/prepare_ner.py" "$DATA/hi-ner-$s.iob2" "$WORK/ner/$s.spacy" hi
  done
  ;;
train-ner)
  /usr/bin/time -p $SPACY train "$WORK/cfg/ner.cfg" --output "$WORK/model-ner" \
    --paths.train "$WORK/ner/train.spacy" --paths.dev "$WORK/ner/dev.spacy"
  ;;
*) echo "unknown stage $STAGE" >&2; exit 2 ;;
esac
