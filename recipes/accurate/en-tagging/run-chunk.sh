#!/usr/bin/env bash
# One resumable chunk of the recipe. Run it again until DIR/model.onenlp exists.
#   env: RECIPE_DLL ONNX TOKENIZER TRAIN DEV OUT  [POOLING=mean] [BUDGET=1500] [THREADS=8] [SEED=1]
set -euo pipefail
mkdir -p "$OUT"
dotnet "$RECIPE_DLL" train --onnx "$ONNX" --tokenizer "$TOKENIZER" --train "$TRAIN" --dev "$DEV" --out "$OUT" \
  --pooling "${POOLING:-mean}" --seed "${SEED:-1}" --threads "${THREADS:-8}" --budget-seconds "${BUDGET:-1500}" --epochs "${EPOCHS:-20}"
