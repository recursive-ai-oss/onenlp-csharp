#!/usr/bin/env bash
# Builds a project (default: the probe recipe) into an output directory once no bench holds the machine; stops if a bench starts meanwhile.
set -u
PROJECT=${1:-recipes/accurate/probe/AccurateProbe.csproj}; OUT=${2:-$HOME/onenlp/u21/probe}
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10 DOTNET_PROCESSOR_COUNT=4 DOTNET_CLI_TELEMETRY_OPTOUT=1 ONENLP_ARTIFACTS=$HOME/onenlp/u21/artifacts
while [ -e "$HOME/WORKER-BENCH.lock" ]; do sleep 20; done
( while [ ! -e "$HOME/WORKER-BENCH.lock" ]; do sleep 20; done; kill -- -$$ 2>/dev/null ) &
WATCH=$!
nice dotnet build -c Release -m:4 -v quiet -clp:NoSummary -p:UseSharedCompilation=false -o "$OUT" "$PROJECT" 2>&1 | tail -8
kill $WATCH 2>/dev/null
ls "$OUT" | grep -c dll
