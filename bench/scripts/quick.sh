#!/usr/bin/env bash
# Development aid: builds the harness and prints a short table for the families matching FILTER (budget 400 ms per row).
#   bench/scripts/quick.sh MODELS TATOEBA FILTER
set -euo pipefail
REPO=$(cd "$(dirname "$0")/../.." && pwd)
dotnet build -m:4 -c Release "$REPO/bench/OneNlp.Bench.Tokenizers" -v q -clp:NoSummary | grep -E 'error|warn' | head -20 || true
DLL=$(find "${ONENLP_ARTIFACTS:-$REPO/artifacts}" -path '*bin/OneNlp.Bench.Tokenizers/release/RecursiveAI.OneNlp.Bench.Tokenizers.dll' | head -1)
dotnet "$DLL" --models "$1" --tatoeba "$2" --filter "$3" --budget-ms 400 | grep -E '^   |^\| ' | grep -vE '^\|---|^\| family' | cut -c1-200
