#!/usr/bin/env bash
# Runs run-chunk.sh through the job queue, one chunk at a time, until the recipe reports completed=True.
#   env: as run-chunk.sh, plus  NAME (log name)  [WQ_CORES=8] [LOGS=$HOME/onenlp/logs]  [MAX_CHUNKS=40]
set -uo pipefail
LOGS="${LOGS:-$HOME/onenlp/logs}"
mkdir -p "$LOGS"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for ((i = 1; i <= ${MAX_CHUNKS:-40}; i++)); do
  if grep -q '^completed=True' "$LOGS/$NAME.log" 2>/dev/null; then exit 0; fi
  wq run --class heavy --cores "${WQ_CORES:-8}" --max 30m --label "onenlp mllarge $NAME chunk $i" -- bash "$here/run-chunk.sh" >>"$LOGS/$NAME.log.tmp" 2>&1
  grep -v '^wq:' "$LOGS/$NAME.log.tmp" >>"$LOGS/$NAME.log"
  if [ -n "${LEDGER:-}" ]; then
    # CPU-hours of the chunk: its cores times the wall time the recipe reports (queue waiting is not counted).
    wall=$(grep -o 'chunk_wall_s=[0-9.]*' "$LOGS/$NAME.log.tmp" | tail -1 | cut -d= -f2)
    [ -n "$wall" ] && awk -v n="$NAME chunk $i" -v c="${WQ_CORES:-8}" -v w="$wall" 'BEGIN{printf "%s\t%d\t%.3f\t%.3f\t\n", n, c, w/3600, c*w/3600}' >>"$LEDGER"
  fi
  : >"$LOGS/$NAME.log.tmp"
done
