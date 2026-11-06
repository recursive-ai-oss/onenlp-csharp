#!/usr/bin/env bash
# Splits an exported sentence-transformers model into the files the multilingual embeddings packages embed:
# model.onnx in three byte ranges (model.onnx.part1..3) plus the small configuration files, and prints the manifest rows.
#   make_shards.sh <model-dir> <out-dir>
set -euo pipefail
src=$(realpath "$1"); out=$(realpath -m "$2")
mkdir -p "$out"; rm -f "$out"/*
total=$(stat -c %s "$src/model.onnx")
part=$(( (total + 2) / 3 ))
split -b "$part" -d -a1 --numeric-suffixes=1 "$src/model.onnx" "$out/model.onnx.part"
cp "$src/tokenizer.json" "$src/modules.json" "$src/sentence_bert_config.json" "$out/"
cp "$src/1_Pooling/config.json" "$out/pooling.json"
for f in "$out"/*; do
  printf '%s %s %s\n' "$(basename "$f")" "$(stat -c %s "$f")" "$(sha256sum "$f" | cut -d' ' -f1)"
done
echo "whole model.onnx: $total $(sha256sum "$src/model.onnx" | cut -d' ' -f1)"
