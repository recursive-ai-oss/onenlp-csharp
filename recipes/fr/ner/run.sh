#!/usr/bin/env bash
# Trains and evaluates the French NER recipe on WikiNER French (wp3).
#
#   run.sh <data-root> <work-dir> <decoding: tagging|spans> [extra train options...]
#
# <data-root> holds wikiner-fr-wp3-{train,dev,test}.iob2 as fetched by tools/fetch/fr/fetch.sh. Each call to `train` resumes
# from the last checkpoint in <work-dir>/<decoding>, so a killed run can be started again with the same command.
set -eu
data=$1; work=$2; decoding=$3; shift 3
dll=${RECIPE_DLL:?set RECIPE_DLL to RecursiveAI.OneNlp.Recipes.FrNer.dll}
mkdir -p "$work/data" "$work/$decoding"
for split in train dev test; do
  src=$(find "$data" -name "wikiner-fr-wp3-$split.iob2" | head -1)
  [ -s "$work/data/$split.jsonl" ] || dotnet "$dll" convert "$src" "$work/data/$split.jsonl"
done
dotnet "$dll" train "$work/data/train.jsonl" "$work/data/dev.jsonl" "$work/$decoding" --decoding "$decoding" "$@"
