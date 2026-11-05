#!/usr/bin/env bash
# Parity for one model: reference run (sentence-transformers) then our run, then the comparison.
# Usage: parity.sh NAME REPO REVISION [PREFIX_TEXT] [MODEL_FILE]
# Environment: ST_VENV (python venv with sentence-transformers), MODELS (dir with NAME/), CORPUS (parity corpus dir),
#              OUT (work dir), TOOL (EmbedTool.dll)
set -euo pipefail
NAME=$1; REPO=$2; REV=$3; PFX=${4:-}; MF=${5:-model.onnx}
HERE=$(cd "$(dirname "$0")" && pwd)
P=$OUT/parity-$NAME; mkdir -p "$OUT"
[ -e "$P.ref.json" ] || "$ST_VENV/bin/python" "$HERE/parity_reference.py" --corpus "$CORPUS" --repo "$REPO" --revision "$REV" --out "$P" --prefix "$PFX" > "$P.ref.log"
TAG=ours; [ "$MF" = model.onnx ] || TAG=ours-${MF%.onnx}
dotnet "$TOOL" run "$MODELS/$NAME" "$P.texts.jsonl" "$P.$TAG" --model "$MF" --prefix "$PFX" --time
"$ST_VENV/bin/python" "$HERE/parity_compare.py" "$P" --ours "$TAG" | tee "$P.$TAG.compare.json"
