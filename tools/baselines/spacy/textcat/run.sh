#!/usr/bin/env bash
# Black-box spaCy baseline for text classification and span categorization (spaCy's released CLI with the configs `spacy init config`
# wrote, unchanged except for the data paths).
#
#   run.sh <mode> <stage> <train-jsonl> <dev-jsonl> <test-jsonl> <work-dir>
#
# mode:  textcat | textcat_multilabel | spancat
# stage: prepare | train | predict
# env:   ONENLP_ORACLES  directory with the spacy/ virtual environment (default $HOME/onenlp/oracles)
#
# The configs were generated with
#   spacy init config <mode>.cfg --lang en --pipeline <mode> --optimize efficiency
# (spaCy 3.8.18, thinc 8.3.14); their SHA-256 are in README.md. Training settings are spaCy's: seed 0, dropout 0.1, Adam, batches by
# words, evaluation every 200 steps, stopping after 1600 steps without improvement or 20000 steps.
set -euo pipefail
MODE=${1:?mode}; STAGE=${2:?stage}; TRAIN=${3:?train.jsonl}; DEV=${4:?dev.jsonl}; TEST=${5:?test.jsonl}; WORK=${6:?work-dir}
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ORACLES=${ONENLP_ORACLES:-$HOME/onenlp/oracles}
PY=$ORACLES/spacy/bin/python
SPACY=$ORACLES/spacy/bin/spacy
export OMP_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 MKL_NUM_THREADS=1
mkdir -p "$WORK"
if [ "$(uname)" = Darwin ]; then TIMEV="/usr/bin/time -l"; else TIMEV="/usr/bin/time -v"; fi
case $STAGE in
prepare)
  $PY "$HERE/textcat_data.py" prepare "$MODE" "$TRAIN" "$WORK/train.spacy"
  $PY "$HERE/textcat_data.py" prepare "$MODE" "$DEV" "$WORK/dev.spacy"
  shasum -a 256 "$WORK"/train.spacy "$WORK"/dev.spacy
  ;;
train)
  $TIMEV $SPACY train "$HERE/$MODE.cfg" --output "$WORK/model" --paths.train "$WORK/train.spacy" --paths.dev "$WORK/dev.spacy"
  ;;
predict)
  $PY "$HERE/textcat_data.py" predict "$MODE" "$WORK/model/model-best" "$TEST" "$WORK/pred-test.jsonl"
  ;;
*) echo "unknown stage $STAGE" >&2; exit 2;;
esac
