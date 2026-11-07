#!/usr/bin/env bash
# Trains one part of the Hindi pipeline on the files `ensemble` wrote. Safe to run again: it resumes from its checkpoint, and does nothing
# when the part is finished.
#
#   train-part.sh tagging|parser|ner <ensemble-dir> <out-dir> [seconds-per-run] [threads]
#
# The settings are the ones of the other fast-tier recipes (recipes/it/fast): one window layer and a bidirectional LSTM for the tagging
# head, the default encoder for the parser and the entity recognizer. None was tuned on Hindi gold: there is none that may be used for it.
set -euo pipefail
PART=${1:?tagging|parser|ner}
DATA=${2:?ensemble directory}
OUT=${3:?output directory}
SECONDS_PER_RUN=${4:-1500}
THREADS=${5:-4}
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
R="dotnet run -c Release --no-build --project $ROOT/recipes/hi/fast --"
mkdir -p "$OUT/$PART"
if [ -s "$OUT/$PART/model.onenlp" ]; then echo "$PART already trained"; exit 0; fi
case $PART in
  tagging)
    $R train-tagging --train "$DATA/hi-ud-train.conllu" --dev "$DATA/hi-ud-dev.conllu" --out "$OUT/tagging" \
      --plain 1 --relu 1 --depth 1 --recurrent 1 --recurrent-size 64 --lr 0.003 --cosine-epochs "${EPOCHS:-12}" --patience 100 \
      --threads "$THREADS" --seconds "$SECONDS_PER_RUN" ;;
  parser)
    $R train-parser --train "$DATA/hi-ud-train.conllu" --dev "$DATA/hi-ud-dev.conllu" --out "$OUT/parser" \
      --epochs "${EPOCHS:-10}" --patience 100 --threads "$THREADS" --seconds "$SECONDS_PER_RUN" ;;
  ner)
    for s in train dev; do
      [ -s "$OUT/ner-$s.jsonl" ] || $R ner-convert "$DATA/hi-ner-$s.iob2" "$OUT/ner-$s.jsonl"
    done
    timeout "$SECONDS_PER_RUN" $R ner-train "$OUT/ner-train.jsonl" "$OUT/ner-dev.jsonl" "$OUT/ner" --epochs "${EPOCHS:-8}" --patience 100 \
      --threads "$THREADS" --checkpoint-every 200 ;;
  *) echo "unknown part $PART" >&2; exit 2 ;;
esac
