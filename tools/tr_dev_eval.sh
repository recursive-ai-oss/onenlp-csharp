#!/usr/bin/env bash
# Builds the Turkish recipe and scores a model directory on the BOUN development (or test) file.
# usage: tr_dev_eval.sh <work-dir> <data-root> <models-dir> <tag> <parts> [dev|test] [raw|gold] [extra predict options...]
set -eu
W=$1; D=$2; M=$3; TAG=$4; PARTS=$5; S=${6:-dev}; MODE=${7:-raw}; shift 7 || true
export ONENLP_ARTIFACTS=$W/art
ROOT=$(cd "$(dirname "$0")/.." && pwd)
[ -n "${NOBUILD:-}" ] || dotnet build -c Release -v quiet -clp:NoSummary "$ROOT/recipes/tr/fast" >/dev/null
[ -n "${NOBUILD:-}" ] || dotnet build -c Release -v quiet -clp:NoSummary "$ROOT/tools/OneNlp.Eval" >/dev/null
B=$(dirname "$(find -L "$W/art" -path '*/bin/*' -name 'RecursiveAI.OneNlp.Recipes.Turkish.dll' | head -1)")
E=$(find -L "$W/art" -path '*/bin/*' -name 'RecursiveAI.OneNlp.Eval.dll' | head -1)
G=$D/train/ud/tr/tr_boun-ud-dev.conllu
[ "$S" = test ] && G=$D/eval/ud/tr/tr_boun-ud-test.conllu
dotnet "$B/RecursiveAI.OneNlp.Recipes.Turkish.dll" predict --models "$M" --parts "$PARTS" --gold "$G" --mode "$MODE" --out "$W/pred-$TAG-$S-$MODE.conllu" --threads 4 "$@" | tail -1
dotnet "$E" ud --gold "$G" --pred "$W/pred-$TAG-$S-$MODE.conllu" 2>&1 | grep -E '\| (Tokens|Sentences|Words|UPOS|UFeats|Lemmas|UAS|LAS|CLAS|MLAS) ' | awk -F'|' '{printf "%s %s %s; ", $4,$5,$6} END{print ""}'
