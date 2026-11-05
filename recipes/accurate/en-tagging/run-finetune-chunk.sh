#!/usr/bin/env bash
# One resumable chunk of the fine-tuning recipe: the encoder and the UPOS head train together. Run it again until DIR/model.onenlp exists.
#   env: RECIPE_DLL ONNX TOKENIZER TRAIN DEV OUT  [BUDGET=1560] [THREADS=8] [SEED=1] [STEPS=800] [BATCH_WORDS=500] [LR=5e-5]
set -euo pipefail
mkdir -p "$OUT"
dotnet "$RECIPE_DLL" train --finetune --onnx "$ONNX" --tokenizer "$TOKENIZER" --train "$TRAIN" --dev "$DEV" --out "$OUT" \
  --seed "${SEED:-1}" --threads "${THREADS:-8}" --budget-seconds "${BUDGET:-1560}" \
  --batch-words "${BATCH_WORDS:-500}" --lr "${LR:-5e-5}" --warmup 40 --total-steps "${STEPS:-800}" --max-steps "${STEPS:-800}" \
  --epochs 100 --patience 100 --eval-every 200 --checkpoint-every 15 --log-every 5
