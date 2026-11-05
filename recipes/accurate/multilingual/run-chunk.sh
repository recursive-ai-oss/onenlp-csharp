#!/usr/bin/env bash
# One resumable chunk of the multilingual fine-tuning recipe. Run it again with the same environment until the log says completed=True.
#   env: RECIPE_DLL DATA ONNX TOKENIZER LANGS OUT  [BUDGET=1560] [THREADS=8] [SEED=1] [STEPS=500] [LR=1e-4] [BATCH_WORDS] [EXTRA=...]
set -euo pipefail
mkdir -p "$OUT"
dotnet "$RECIPE_DLL" train --langs "$LANGS" --data "$DATA" --onnx "$ONNX" --tokenizer "$TOKENIZER" --out "$OUT" \
  --seed "${SEED:-1}" --threads "${THREADS:-8}" --budget-seconds "${BUDGET:-1560}" \
  --steps "${STEPS:-500}" --lr "${LR:-1e-4}" ${BATCH_WORDS:+--batch-words $BATCH_WORDS} ${EXTRA:-}
