#!/usr/bin/env bash
# Packs core, .Neural, the English and German models and the embedder into a throwaway folder feed, then runs the design's happy-path
# sample as an application (JIT, then NativeAOT), and runs the program without any model package to show the errors.
#   run.sh <work-dir> <tokenizer.json> [extra dotnet args, e.g. -p:CppCompilerAndLinker=gcc]
# The weights are read through ONENLP_LANGID_WEIGHTS, ONENLP_ENGLISH_WEIGHTS, ONENLP_GERMAN_WEIGHTS and ONENLP_EMBEDDINGS_WEIGHTS.
set -euo pipefail
work=$(realpath -m "$1"); tokenizer=$(realpath "$2"); shift 2
here=$(cd "$(dirname "$0")" && pwd); repo=$(cd "$here/../.." && pwd)
feed=$work/feed; mkdir -p "$feed"; rm -f "$feed"/*.nupkg
export NUGET_PACKAGES=$work/nuget; rm -rf "$NUGET_PACKAGES/recursiveai.onenlp" "$NUGET_PACKAGES"/recursiveai.onenlp.*
export ONENLP_SAMPLE_TOKENIZER=$tokenizer ONENLP_CONSUMER_FEED=$feed ONENLP_ARTIFACTS=$work/artifacts
for p in OneNlp OneNlp.Neural OneNlp.Models.English OneNlp.Models.German OneNlp.Models.Embeddings; do
  dotnet pack "$repo/src/$p/$p.csproj" -c Release -o "$feed" -v quiet -clp:NoSummary -nologo
done
for mode in true false; do
  echo "=== WithModels=$mode: JIT run"
  dotnet run --project "$here" -c Release -p:WithModels=$mode -v quiet -clp:NoSummary -nologo
  echo "=== WithModels=$mode: NativeAOT publish"
  out=$work/aot-$mode
  rm -rf "$out"
  dotnet publish "$here" -c Release -p:WithModels=$mode -o "$out" -v minimal -clp:NoSummary -nologo "$@" 2>&1 | tee "$work/aot-$mode.log" | tail -20
  grep -ciE 'warning (IL|NETSDK|CS|CA)' "$work/aot-$mode.log" || true
  "$out/RecursiveAI.OneNlp.SampleConsumer"
done
