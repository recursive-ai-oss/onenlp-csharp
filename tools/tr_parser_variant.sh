#!/usr/bin/env bash
# Trains a Turkish parser variant on the BOUN training file and scores it on the development file in both modes.
# usage: tr_parser_variant.sh <work-dir> <data-root> <base-models-dir> <tag> <train-parser options...>
# Resumable: run it again with the same arguments and it continues from the checkpoint.
set -eu
W=$1; D=$2; BASE=$3; TAG=$4; shift 4
export ONENLP_ARTIFACTS=$W/art
HERE=$(cd "$(dirname "$0")" && pwd)
B=$(dirname "$(find -L "$W/art" -path '*/bin/*' -name 'RecursiveAI.OneNlp.Recipes.Turkish.dll' | head -1)")
OUT=$W/p-$TAG
mkdir -p "$OUT"
dotnet "$B/RecursiveAI.OneNlp.Recipes.Turkish.dll" train-parser --train "$D/train/ud/tr/tr_boun-ud-train.conllu" --dev "$D/train/ud/tr/tr_boun-ud-dev.conllu" --out "$OUT" --threads 4 --seconds 1450 "$@" | grep -E 'completed|chunk_wall|model='
if [ -f "$OUT/model.onenlp" ]; then
  M=$W/m-$TAG; mkdir -p "$M"
  cp "$BASE/splitter.onenlp" "$BASE/tagging.onenlp" "$M/"; cp "$OUT/model.onenlp" "$M/parser.onenlp"
  for mode in gold raw; do echo "$TAG $mode"; "$HERE/tr_dev_eval.sh" "$W" "$D" "$M" "$TAG" splitter,tagging,parser dev $mode; done
fi
