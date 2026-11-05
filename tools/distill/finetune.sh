#!/usr/bin/env bash
# One resumable chunk of a head or fine-tuning run with the AccurateProbe recipe. Run it again with the same arguments until
# DIR/model.onenlp exists.
#   finetune.sh TAG TASK [recipe options...]       TASK is upos-en, upos-fr or ner-en
# env: PROBE_DLL (the AccurateProbe recipe), DATA (data root), OUT_ROOT, ONNX, TOKENIZER, THREADS (default 8), BUDGET (seconds, default 1500)
# Recipe options after TASK (for example --finetune --lr 3e-4 --total-steps 1500 --batch-words 1000) are passed through.
set -eu
TAG=$1 TASK=$2; shift 2
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10
OUT=$OUT_ROOT/$TAG; mkdir -p "$OUT"
case $TASK in
  upos-en) ARGS="upos --lang en --train $DATA/train/ud-en-ewt-r2.18/en_ewt-ud-train.conllu --dev $DATA/train/ud-en-ewt-r2.18/en_ewt-ud-dev.conllu" ;;
  upos-fr) ARGS="upos --lang fr --train $DATA/train/ud/fr/fr_gsd-ud-train.conllu --dev $DATA/train/ud/fr/fr_gsd-ud-dev.conllu" ;;
  ner-en)
    for s in train dev; do
      [ -s "$OUT_ROOT/uner-en-$s.jsonl" ] || dotnet "$PROBE_DLL" convert --in "$DATA/train/uner-en-ewt-8ed072de/en_ewt-ud-$s.iob2" --out "$OUT_ROOT/uner-en-$s.jsonl"
    done
    ARGS="ner --lang en --train $OUT_ROOT/uner-en-train.jsonl --dev $OUT_ROOT/uner-en-dev.jsonl" ;;
  *) echo "unknown task $TASK" >&2; exit 2 ;;
esac
echo "== $TAG $TASK $*"
dotnet "$PROBE_DLL" $ARGS --onnx "$ONNX" --tokenizer "$TOKENIZER" --out "$OUT" --threads "${THREADS:-8}" \
  --budget-seconds "${BUDGET:-1500}" --checkpoint-every 50 "$@"
