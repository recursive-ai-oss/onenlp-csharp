#!/usr/bin/env bash
# Trains one head set for a related language (or a pool of them) on the frozen encoder of the accurate tier. Resumable: run it again until it
# prints completed=True.
#
#   train-related.sh <name> <accurate-weights-dir> <out-root> <train-specs> <dev-specs> [seconds-per-run] [threads]
#
# Specs are comma-separated CoNLL-U files with an optional :N sentence limit, e.g. mr_ufal-ud-train.conllu,sd_isra-ud-train.conllu:1500.
# The result is <out-root>/<name>/model.onenlp, copied to <out-root>/heads/<name>.onenlp for `label --extra-heads <out-root>/heads`.
# Extra settings go in EXTRA (for example "--heads morphologizer,parser,lemmatizer --epochs 10").
set -euo pipefail
NAME=${1:?name}; WEIGHTS=${2:?weights}; OUT=${3:?out root}; TRAIN=${4:?train specs}; DEV=${5:?dev specs}
SECONDS_PER_RUN=${6:-1500}; THREADS=${7:-8}
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
mkdir -p "$OUT/$NAME" "$OUT/heads"
if [ -s "$OUT/heads/$NAME.onenlp" ]; then echo "$NAME already trained"; exit 0; fi
dotnet run -c Release --no-build --project "$ROOT/recipes/hi/fast" -- train-related --weights "$WEIGHTS" --name "$NAME" \
  --train "$TRAIN" --dev "$DEV" --out "$OUT/$NAME" --threads "$THREADS" --seconds "$SECONDS_PER_RUN" ${EXTRA:-}
[ -s "$OUT/$NAME/model.onenlp" ] && cp "$OUT/$NAME/model.onenlp" "$OUT/heads/$NAME.onenlp"
exit 0
