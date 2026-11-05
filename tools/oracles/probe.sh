#!/usr/bin/env bash
# Usage: probe.sh <lang> <file of text lines> [reference]: runs our recognizers over free text and prints what they found (needs OneNlp.Eval built in Release).
set -euo pipefail
ROOT=$(pwd)
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj > /dev/null
TMP=$(mktemp)
dotnet "$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll" recognize --lang "$1" --in "$2" --out "$TMP" --reference "${3:-2026-03-18T10:30:00+00:00}" 2>/dev/null
python3 tools/oracles/probe.py "$2" "$TMP"
rm -f "$TMP"
