#!/usr/bin/env bash
# Packs the core, neural and frozen-encoder packages into a throwaway folder feed, then builds, runs and NativeAOT-publishes the consumer app against it.
#   run.sh <weights-dir> <work-dir> [extra dotnet args, e.g. -p:CppCompilerAndLinker=gcc]
# <weights-dir> holds student-v1.onnx and tokenizer.json (the files listed in the part's weights.manifest.props).
set -euo pipefail
weights=$(realpath "$1"); work=$(realpath -m "$2"); shift 2
here=$(cd "$(dirname "$0")" && pwd); repo=$(cd "$here/../.." && pwd)
feed=$work/feed; mkdir -p "$feed"; rm -f "$feed"/*.nupkg
export NUGET_PACKAGES=$work/nuget; rm -rf "$NUGET_PACKAGES/recursiveai.onenlp" "$NUGET_PACKAGES"/recursiveai.onenlp.*
export ONENLP_CONSUMER_FEED=$feed ONENLP_MULTILINGUAL_WEIGHTS=$weights ONENLP_ARTIFACTS=$work/artifacts
for p in src/OneNlp/OneNlp.csproj src/OneNlp.Neural/OneNlp.Neural.csproj src/OneNlp.Models.Multilingual.Parts/FrozenEncoder/OneNlp.Models.Multilingual.FrozenEncoder.csproj; do
  dotnet pack "$repo/$p" -c Release -o "$feed" -v quiet -clp:NoSummary -nologo
done
ls -l "$feed"
echo "=== JIT run"
dotnet run --project "$here" -c Release -v quiet -clp:NoSummary -nologo
echo "=== NativeAOT publish"
out=$work/aot
rm -rf "$out"
dotnet publish "$here" -c Release -o "$out" -v minimal -clp:NoSummary -nologo "$@" 2>&1 | tee "$work/aot.log" | tail -20
grep -ciE 'warning (IL|NETSDK|CS|CA)' "$work/aot.log" || true
ls -l "$out/RecursiveAI.OneNlp.ModelConsumer.FrozenEncoder"
"$out/RecursiveAI.OneNlp.ModelConsumer.FrozenEncoder"
