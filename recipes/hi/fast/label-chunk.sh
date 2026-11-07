#!/usr/bin/env bash
# Labels one chunk of Hindi web documents with the source heads of the accurate tier.
#
#   label-chunk.sh <accurate-weights-dir> <text.jsonl> <evaluation.conllu> <out-dir> <chunk> [docs-per-chunk]
#
# Chunk k covers documents k*N up to (k+1)*N of the text file. Re-running a finished chunk does nothing, so a chain of chunks can be
# restarted from the top after an interruption (a chunk is written only when complete).
set -euo pipefail
WEIGHTS=${1:?weights directory}
TEXT=${2:?text file}
PUD=${3:?evaluation CoNLL-U file whose text is excluded}
OUT=${4:?output directory}
K=${5:?chunk number}
N=${6:-400}
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
mkdir -p "$OUT"
F=$(printf '%s/chunk-%04d.jsonl.gz' "$OUT" "$K")
if [ -s "$F" ]; then echo "chunk $K already done"; exit 0; fi
dotnet run -c Release --no-build --project "$ROOT/recipes/hi/fast" -- label --weights "$WEIGHTS" --text "$TEXT" --pud "$PUD" \
  --out "$F" --first-doc $((K * N)) --docs "$N" --langs "${LANGS:-en,de,fr,es,pt,it,nl,tr,zh}" --threads "${THREADS:-8}"
