#!/usr/bin/env bash
# Runs run-chunk.sh through the job queue, one chunk at a time, until the recipe reports completed=True.
#   env: as run-chunk.sh, plus  NAME (log name)  [WQ_CORES=8] [LOGS=$HOME/onenlp/u35/logs]  [MAX_CHUNKS=40]
set -uo pipefail
LOGS="${LOGS:-$HOME/onenlp/u35/logs}"
mkdir -p "$LOGS"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for ((i = 1; i <= ${MAX_CHUNKS:-40}; i++)); do
  if grep -q '^completed=True' "$LOGS/$NAME.log" 2>/dev/null; then exit 0; fi
  wq run --class heavy --cores "${WQ_CORES:-8}" --max 30m --label "onenlp u35 $NAME chunk $i" -- bash "$here/run-chunk.sh" >>"$LOGS/$NAME.log.tmp" 2>&1
  grep -v '^wq:' "$LOGS/$NAME.log.tmp" >>"$LOGS/$NAME.log"; : >"$LOGS/$NAME.log.tmp"
done
