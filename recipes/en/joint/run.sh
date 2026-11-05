#!/usr/bin/env bash
# Trains the English joint recipe (one encoder, all heads) on UD English-EWT and UNER English-EWT.
#
#   run.sh <data-root> <work-dir> [train options...]
#
# <data-root> holds train/ud-en-ewt-r2.18/, train/uner-en-ewt-8ed072de/ as fetched by tools/fetch/en/fetch.sh. Set JOINT_DLL to the built
# RecursiveAI.OneNlp.Recipe.EnJoint.dll and NER_DLL to the built RecursiveAI.OneNlp.Recipes.EnNer.dll (it converts the UNER files to JSONL).
# Each call continues from the last checkpoint in <work-dir>/run and stops after CHUNK_SECONDS (default 1500), so a long run is the same
# command repeated until it prints model=...; the result equals one uninterrupted run on the same machine and thread count.
set -eu
data=$1; work=$2; shift 2
joint=${JOINT_DLL:?set JOINT_DLL}
ner=${NER_DLL:?set NER_DLL}
mkdir -p "$work/data" "$work/run"
for split in train dev; do
  src=$(find "$data" -name "en_ewt-ud-$split.iob2" | head -1)
  [ -s "$work/data/ner-$split.jsonl" ] || dotnet "$ner" convert "$src" "$work/data/ner-$split.jsonl"
done
ud=$(dirname "$(find "$data" -name en_ewt-ud-train.conllu | head -1)")
exec dotnet "$joint" train --ud-train "$ud/en_ewt-ud-train.conllu" --ud-dev "$ud/en_ewt-ud-dev.conllu" \
  --ner-train "$work/data/ner-train.jsonl" --ner-dev "$work/data/ner-dev.jsonl" --out "$work/run" \
  --budget-seconds "${CHUNK_SECONDS:-1500}" "$@"
