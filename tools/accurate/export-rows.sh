#!/usr/bin/env bash
# Equivalence and speed of a fine-tuned encoder written back to ONNX (TrainableTransformerEncoder.ExportOnnx / PipelineFile.Save).
#   export-rows.sh NAME INLINE_PIPELINE [TASK_PIPELINE_FOR_ROWS]
# env: ACCURATE_DLL PROBE_DLL EVAL_DLL PY (python with onnxruntime), MODEL_DIR (folder with the original encoder and tokenizer files the pipeline names),
#      IDS (probe token ids), WORK, and for the UPOS rows GOLD_UD and OLD_PRED (the predictions of the inline pipeline, CoNLL-U).
# INLINE_PIPELINE holds the encoder weights inside the file (a trainable encoder).
set -euo pipefail
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10
NAME=$1 INLINE=$2
HERE=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$WORK/$NAME"
W=$WORK/$NAME
# 1. Saving the pipeline again writes the encoder graph beside it.
dotnet "$PROBE_DLL" rewrite --model "$INLINE" --model-dir "$MODEL_DIR" --out "$W/model.onenlp"
ls -l "$W"
# 2. Trainable modules against the written graph in OneModel, and the modules' states dumped for ONNX Runtime.
dotnet "$ACCURATE_DLL" export-parity --pipeline "$INLINE" --model-dir "$MODEL_DIR" --graph "$W/model.encoder0.onnx" --ids "$IDS" --dump "$W/hidden.bin" --threads 8
"$PY" "$HERE/ort_hidden_compare.py" --model "$W/model.encoder0.onnx" --ids "$IDS" --ref "$W/hidden.bin"
# 3. Predictions of the saved pipeline against the inline one's.
if [ -n "${GOLD_UD:-}" ]; then
  dotnet "$PROBE_DLL" predict-upos --model "$W/model.onenlp" --model-dir "$W" --gold "$GOLD_UD" --out "$W/upos.conllu" --threads 8
  dotnet "$EVAL_DLL" ud --gold "$GOLD_UD" --pred "$W/upos.conllu" --metrics UPOS --component written-graph --language en --dataset test --resamples 1000 --seed 1 --json "$W/upos.json"
  if [ -n "${OLD_PRED:-}" ]; then
    echo "UPOS differences against the inline pipeline's predictions:"
    diff <(grep -v '^#' "$OLD_PRED" | cut -f1-4) <(grep -v '^#' "$W/upos.conllu" | cut -f1-4) | grep '^[<>]' | head -40 || true
    echo "differing tokens: $(diff <(grep -v '^#' "$OLD_PRED" | cut -f1-4) <(grep -v '^#' "$W/upos.conllu" | cut -f1-4) | grep -c '^<' || true)"
  fi
fi
