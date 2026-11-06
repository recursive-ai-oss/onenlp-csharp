#!/usr/bin/env bash
# Frozen-encoder head probes with the same recipe for any exported encoder.
#   probe.sh TAG ONNX TOKENIZER TASK...      TASK is one of upos-en, upos-fr, upos-nl, upos-tr, upos-zh, ner-en
# env: PROBE_DLL (the AccurateProbe recipe), DATA (data root), OUT_ROOT, THREADS (default 8)
# Each task trains until done or early-stopped and resumes from its checkpoint when started again.
set -eu
TAG=$1 ONNX=$2 TOK=$3; shift 3
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10
for TASK in "$@"; do
  OUT=$OUT_ROOT/$TAG-$TASK; mkdir -p "$OUT"
  case $TASK in
    upos-en) ARGS="upos --lang en --train $DATA/train/ud-en-ewt-r2.18/en_ewt-ud-train.conllu --dev $DATA/train/ud-en-ewt-r2.18/en_ewt-ud-dev.conllu" ;;
    upos-fr|upos-nl|upos-tr|upos-zh)
      L=${TASK#upos-}; B=$(case $L in fr) echo fr_gsd;; nl) echo nl_alpino;; tr) echo tr_boun;; zh) echo zh_gsdsimp;; esac)
      ARGS="upos --lang $L --train $DATA/train/ud/$L/$B-ud-train.conllu --dev $DATA/train/ud/$L/$B-ud-dev.conllu" ;;
    ner-en)
      for s in train dev; do
        [ -s "$OUT_ROOT/uner-en-$s.jsonl" ] || dotnet "$PROBE_DLL" convert --in "$DATA/train/uner-en-ewt-8ed072de/en_ewt-ud-$s.iob2" --out "$OUT_ROOT/uner-en-$s.jsonl"
      done
      ARGS="ner --lang en --train $OUT_ROOT/uner-en-train.jsonl --dev $OUT_ROOT/uner-en-dev.jsonl" ;;
    *) echo "unknown task $TASK" >&2; exit 2 ;;
  esac
  echo "== $TAG $TASK"
  dotnet "$PROBE_DLL" $ARGS --onnx "$ONNX" --tokenizer "$TOK" --out "$OUT" --threads "${THREADS:-8}" | tail -n 12
done
