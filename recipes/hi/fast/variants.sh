#!/usr/bin/env bash
# Ensemble variants on the hand-annotated development sentences: which source languages to pool.
#   variants.sh <work-dir containing selfdev.jsonl.gz>
set -euo pipefail
WORK=${1:?work directory}
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
R="dotnet run -c Release --no-build --project $ROOT/recipes/hi/fast --"
E="dotnet run -c Release --no-build --project $ROOT/tools/OneNlp.Eval --"
for U in ${VARIANTS:-en,de,fr,es,pt,it,nl,tr,zh de,fr,es,pt,it,nl,tr,zh de,tr,nl,fr,zh de,tr,nl,fr,zh,pt de,tr,nl,zh de,tr,zh de,tr,nl,fr,zh,pt,es}; do
  $R ensemble --in "$WORK/selfdev.jsonl.gz" --out "$WORK/v" --per-source --pos-agree 0 --uas-agree 0 --use "$U" > /dev/null
  printf '%s ' "$U"
  $E ud --gold "$ROOT/testdata/hi/selfdev.conllu" --pred "$WORK/v/src-ensemble.conllu" --metrics UPOS,UAS,LAS --resamples 100 2>&1 | grep -E 'UPOS|UAS|LAS' | awk -F'|' '{printf "%s%s ", $5, $6}'
  echo
done
