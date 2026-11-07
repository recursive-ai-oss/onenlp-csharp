#!/usr/bin/env bash
# Usage: regress_check.sh <other tree> <out dir> <lang...>: runs the recognizers of this tree and of another tree over each language's test set and
# compares the raw predictions byte for byte. Both trees must have OneNlp.Eval buildable (Release).
set -euo pipefail
OTHER=$1; OUT=$2; shift 2
mkdir -p "$OUT"
for T in "$(pwd)" "$OTHER"; do
  ( cd "$T" && dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj > /dev/null )
done
dll() { local A=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$1")}; echo "${A:-$1/artifacts}/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"; }
for L in "$@"; do
  dotnet "$(dll "$(pwd)")" recognize --lang "$L" --in testdata/recognizers/$L/test.jsonl --out "$OUT/$L.new.jsonl" 2> /dev/null
  dotnet "$(dll "$OTHER")" recognize --lang "$L" --in testdata/recognizers/$L/test.jsonl --out "$OUT/$L.base.jsonl" 2> /dev/null
  if cmp -s "$OUT/$L.new.jsonl" "$OUT/$L.base.jsonl"; then echo "$L identical ($(wc -l < "$OUT/$L.new.jsonl") lines)"; else echo "$L DIFFERS"; fi
done
