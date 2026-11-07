#!/usr/bin/env bash
# Scores every source language's heads and the ensemble on the hand-annotated development sentences (testdata/hi/selfdev.conllu), gold tokens.
#
#   score-sources.sh <accurate-weights-dir> <work-dir>
#
# Needs the repository built in Release. The development sentences rank the source languages; Hindi-PUD is never used for choosing.
set -euo pipefail
WEIGHTS=${1:?weights directory of the accurate tier}
WORK=${2:?work directory}
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
GOLD=$ROOT/testdata/hi/selfdev.conllu
mkdir -p "$WORK"
RECIPE="dotnet run -c Release --no-build --project $ROOT/recipes/hi/fast --"
EVAL="dotnet run -c Release --no-build --project $ROOT/tools/OneNlp.Eval --"
$RECIPE label --weights "$WEIGHTS" --conllu "$GOLD" --out "$WORK/selfdev.jsonl.gz" --langs "${LANGS:-en,de,fr,es,pt,it,nl,tr,zh}" --threads "${THREADS:-4}"
$RECIPE ensemble --in "$WORK/selfdev.jsonl.gz" --out "$WORK/selfdev-ens" --per-source --pos-agree 0 --uas-agree 0 ${ENSEMBLE_ARGS:-} > /dev/null
for P in "$WORK"/selfdev-ens/src-*.conllu; do
  printf '%s ' "$(basename "$P" .conllu)"
  $EVAL ud --gold "$GOLD" --pred "$P" --metrics UPOS,UFeats,UAS,LAS --resamples 200 2>&1 | grep -E 'UPOS|UFeats|UAS|LAS' | tr '\n' ' '
  echo
done
