#!/usr/bin/env bash
# The 84-language base-size run: continues the nine-language model with heads for every language of the corpus spec.
#   env: RECIPE_DLL SPEC DATA ONNX TOKENIZER WARM (the earlier run's best.ckpt) OUT NAME LOGS LEDGER SEGMENTERS ZH_SEGMENTER [STEPS=1200] [SEED=3]
# Resumable 1100 s training budgets inside 30-minute queue jobs; the remaining time covers load, dev evaluation and checkpoint I/O.
set -euo pipefail
NINE=en,de,fr,es,pt,it,nl,tr,zh
REST=$(python3 -c "import json,sys; print(','.join(sorted(l['code'] for l in json.load(open('$SPEC'))['languages'] if l['code'] not in '$NINE'.split(','))))")
export LANGS=$NINE,$REST STEPS=${STEPS:-1200} BUDGET=${BUDGET:-1100} LR=${LR:-6e-5} SEED=${SEED:-3} BATCH_WORDS=${BATCH_WORDS:-3000} MAX_CHUNKS=${MAX_CHUNKS:-30}
export EXTRA="--spec $SPEC --plan 0.3 --parser-weight 1.5 --warmup 40 --eval-every 150 --dev-docs 10 --ner-train 2500 --ner-dev 100 --ner-use 700 --checkpoint-every 0 --log-every 5 --warm-from $WARM --warm-langs $NINE --segmenter-dir $SEGMENTERS --zh-segmenter $ZH_SEGMENTER --export $OUT/export"
exec bash "$(dirname "${BASH_SOURCE[0]}")/run-chain.sh"
