#!/usr/bin/env bash
# Black-box spaCy baseline on English-EWT (UD) and UNER English-EWT. Uses only spaCy's released CLI
# and public Python API, with the generated configs in this directory unchanged except for data paths.
#
#   run.sh <stage> <data-root> <work-dir>
#
# stages: setup | convert | train-ud | train-ner | score-ud | score-ner
# env:    ONENLP_ORACLES  directory holding the venvs (default: $HOME/onenlp/oracles; needs spacy/)
#         THREADS         BLAS/OpenMP threads for training (default 8)
set -euo pipefail
STAGE=${1:?stage}; DATA=${2:?data-root}; WORK=${3:?work-dir}
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ORACLES=${ONENLP_ORACLES:-$HOME/onenlp/oracles}
PY=$ORACLES/spacy/bin/python
SPACY=$ORACLES/spacy/bin/spacy
UD=ud-en-ewt-r2.18
NE=uner-en-ewt-8ed072de
THREADS=${THREADS:-8}
export OMP_NUM_THREADS=$THREADS OPENBLAS_NUM_THREADS=$THREADS MKL_NUM_THREADS=$THREADS
mkdir -p "$WORK/ud" "$WORK/ner"
# Resource report of the training run: GNU time on Linux, BSD time on macOS.
if [ "$(uname)" = Darwin ]; then TIMEV="/usr/bin/time -l"; else TIMEV="/usr/bin/time -v"; fi

case $STAGE in
setup)
  # seqeval 1.2.2 (MIT) and the UD CoNLL 2018 evaluation script (Mozilla Public License 2.0), both run as black boxes.
  uv venv --python 3.13 "$ORACLES/seqeval"
  uv pip install --python "$ORACLES/seqeval/bin/python" seqeval==1.2.2
  mkdir -p "$ORACLES/ud-tools"
  curl -fsSL -o "$ORACLES/ud-tools/conll18_ud_eval.py" https://universaldependencies.org/conll18/conll18_ud_eval.py
  sha256sum "$ORACLES/ud-tools/conll18_ud_eval.py"
  ;;
convert)
  # --n-sents 10: several sentences per document, so the parser sees sentence boundaries inside a document
  # (with one sentence per document it can never learn to split running text).
  for s in train dev; do
    $SPACY convert "$DATA/train/$UD/en_ewt-ud-$s.conllu" "$WORK/ud" --converter conllu --n-sents 10
  done
  $PY "$HERE/prepare_ner.py" "$DATA/train/$NE/en_ewt-ud-train.iob2" "$WORK/ner/train.spacy"
  $PY "$HERE/prepare_ner.py" "$DATA/train/$NE/en_ewt-ud-dev.iob2" "$WORK/ner/dev.spacy"
  sha256sum "$WORK"/ud/*.spacy "$WORK"/ner/*.spacy
  ;;
train-ud)
  $TIMEV $SPACY train "$HERE/tagger_parser.cfg" --output "$WORK/model-ud" \
    --paths.train "$WORK/ud/en_ewt-ud-train.spacy" --paths.dev "$WORK/ud/en_ewt-ud-dev.spacy"
  ;;
train-ner)
  $TIMEV $SPACY train "$HERE/ner.cfg" --output "$WORK/model-ner" \
    --paths.train "$WORK/ner/train.spacy" --paths.dev "$WORK/ner/dev.spacy"
  ;;
score-ud)
  EV=$ORACLES/ud-tools/conll18_ud_eval.py
  GOLD=$DATA/eval/$UD/en_ewt-ud-test.conllu
  for mode in raw gold; do
    $PY "$HERE/predict_ud.py" "$WORK/model-ud/model-best" "$GOLD" $mode "$WORK/pred-ud-$mode.conllu"
    echo "== CoNLL 2018 ($mode tokenization)"
    $PY "$EV" -v "$GOLD" "$WORK/pred-ud-$mode.conllu" | tee "$WORK/scores-ud-$mode.txt"
  done
  ;;
score-ner)
  $PY "$HERE/predict_ner.py" "$WORK/model-ner/model-best" "$DATA/eval/$NE/en_ewt-ud-test.iob2" "$WORK/pred-ner.tsv"
  $ORACLES/seqeval/bin/python "$HERE/score_ner.py" "$WORK/pred-ner.tsv" | tee "$WORK/scores-ner.txt"
  ;;
*) echo "unknown stage $STAGE" >&2; exit 2;;
esac
