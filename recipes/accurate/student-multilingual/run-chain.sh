#!/usr/bin/env bash
# Runs run-chunk.sh through a job queue, one chunk at a time, until the recipe reports completed=True.
#   env: as run-chunk.sh, plus  NAME (log name)  LOGS (log directory)  QUEUE (the command that runs a chunk, for example "wq run --class heavy --cores 4 --max 30m --")
#        [MAX_CHUNKS=40]
set -uo pipefail
: "${NAME:?}" "${LOGS:?}"
mkdir -p "$LOGS"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for ((i = 1; i <= ${MAX_CHUNKS:-40}; i++)); do
  if grep -q '^completed=True' "$LOGS/$NAME.log" 2>/dev/null; then exit 0; fi
  ${QUEUE:-} bash "$here/run-chunk.sh" >>"$LOGS/$NAME.log.tmp" 2>&1
  grep -v '^wq:' "$LOGS/$NAME.log.tmp" >>"$LOGS/$NAME.log"; : >"$LOGS/$NAME.log.tmp"
done
