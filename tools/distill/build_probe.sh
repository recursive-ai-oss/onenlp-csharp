#!/usr/bin/env bash
# Builds a project (default: the probe recipe) into an output directory at low priority. If BUSY_FILE names a file, the build waits
# while that file exists and stops if it appears (for a machine shared with timing runs).
set -u
PROJECT=${1:-recipes/accurate/probe/AccurateProbe.csproj}; OUT=${2:-probe-bin}
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10 DOTNET_PROCESSOR_COUNT=4 DOTNET_CLI_TELEMETRY_OPTOUT=1 ONENLP_ARTIFACTS=${ONENLP_ARTIFACTS:-artifacts}
WATCH=
if [ -n "${BUSY_FILE:-}" ]; then
  while [ -e "$BUSY_FILE" ]; do sleep 20; done
  ( while [ ! -e "$BUSY_FILE" ]; do sleep 20; done; kill -- -$$ 2>/dev/null ) &
  WATCH=$!
fi
nice dotnet build -c Release -m:4 -v quiet -clp:NoSummary -p:UseSharedCompilation=false -o "$OUT" "$PROJECT" 2>&1 | tail -8
[ -n "$WATCH" ] && kill $WATCH 2>/dev/null
ls "$OUT" | grep -c dll
