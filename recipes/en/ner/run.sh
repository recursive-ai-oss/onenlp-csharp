#!/usr/bin/env bash
# Trains and evaluates the English NER recipe on UNER English-EWT.
#
#   run.sh <data-root> <work-dir> <decoding: tagging|spans> [extra train options...]
#
# <data-root> holds train/uner-en-ewt-*/ and eval/uner-en-ewt-*/ as fetched by tools/fetch/en/fetch.sh. Each call to `train` resumes
# from the last checkpoint in <work-dir>/<decoding>, so a killed run can be started again with the same command.
set -eu
data=$1; work=$2; decoding=$3; shift 3
dll=${RECIPE_DLL:?set RECIPE_DLL to RecursiveAI.OneNlp.Recipes.EnNer.dll}
mkdir -p "$work/data" "$work/$decoding"
for split in train dev test; do
  src=$(ls "$data"/*/uner-en-ewt-*/en_ewt-ud-$split.iob2 2>/dev/null | head -1 || true)
  [ -n "$src" ] || src=$(find "$data" -name "en_ewt-ud-$split.iob2" | head -1)
  [ -s "$work/data/$split.jsonl" ] || dotnet "$dll" convert "$src" "$work/data/$split.jsonl"
done
dotnet "$dll" train "$work/data/train.jsonl" "$work/data/dev.jsonl" "$work/$decoding" --decoding "$decoding" "$@"
