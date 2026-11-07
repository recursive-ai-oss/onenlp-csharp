#!/usr/bin/env bash
# One resumable chunk of the recipe. Run it again until DIR/model.onenlp exists.
#   env: RECIPE_DLL ONNX TOKENIZER TRAIN DEV OUT  [BUDGET=1500] [THREADS=8] [SEED=1] [EPOCHS=8] [EXTRA="--lr 0.002"]
set -euo pipefail
mkdir -p "$OUT"
# shellcheck disable=SC2086
dotnet "$RECIPE_DLL" train --onnx "$ONNX" --tokenizer "$TOKENIZER" --train "$TRAIN" --dev "$DEV" --out "$OUT" \
  --seed "${SEED:-1}" --threads "${THREADS:-8}" --budget-seconds "${BUDGET:-1500}" --epochs "${EPOCHS:-8}" ${EXTRA:-}
