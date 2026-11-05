#!/usr/bin/env bash
# Inference parity of an exported encoder against PyTorch on 200 multilingual probe texts.
#   parity.sh NAME REPO TOKENIZER_JSON  (env: PY, MODELS, WORK, PARITY_RECORDS, ACCURATE_DLL, REVISION)
# Steps: pick texts -> our tokenizer writes ids -> PyTorch hidden states for those ids -> OneModel vs PyTorch.
set -euo pipefail
NAME=$1 REPO=$2 TOK=$3
HERE=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$WORK"
[ -f "$WORK/texts.jsonl" ] || "$PY" "$HERE/select_texts.py" --corpus "$PARITY_RECORDS" --out "$WORK/texts.jsonl"
dotnet "$ACCURATE_DLL" ids --tokenizer "$TOK" --texts "$WORK/texts.jsonl" --out "$WORK/$NAME.ids.json"
"$PY" "$HERE/reference_hidden.py" --repo "$REPO" ${REVISION:+--revision "$REVISION"} --ids "$WORK/$NAME.ids.json" --out "$WORK/$NAME.ref.bin"
dotnet "$ACCURATE_DLL" parity --model "$MODELS/$NAME.onnx" --ids "$WORK/$NAME.ids.json" --ref "$WORK/$NAME.ref.bin"
