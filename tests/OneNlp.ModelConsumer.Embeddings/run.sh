#!/usr/bin/env bash
# Packs the core, neural, Embeddings and Embeddings.Multilingual (with its shards) packages into a throwaway folder feed, then builds, runs and NativeAOT-publishes the consumer
# app against that feed, with none, the English, the multilingual, and both model packages (MODES="..." selects).
#   Needs E5_WEIGHTS (the make_shards.sh output directory) besides the English weights directory argument.
#   run.sh <weights-dir> <work-dir> [extra dotnet args, e.g. -p:CppCompilerAndLinker=gcc]
set -euo pipefail
weights=$(realpath "$1"); work=$(realpath -m "$2"); shift 2
here=$(cd "$(dirname "$0")" && pwd); repo=$(cd "$here/../.." && pwd)
feed=$work/feed; mkdir -p "$feed"; rm -f "$feed"/*.nupkg
export NUGET_PACKAGES=$work/nuget; rm -rf "$NUGET_PACKAGES/recursiveai.onenlp" "$NUGET_PACKAGES"/recursiveai.onenlp.*
export ONENLP_CONSUMER_FEED=$feed ONENLP_EMBEDDINGS_WEIGHTS=$weights ONENLP_EMBEDDINGS_E5_WEIGHTS=${E5_WEIGHTS:?set E5_WEIGHTS to the multilingual weights directory} ONENLP_ARTIFACTS=$work/artifacts
for p in src/OneNlp/OneNlp.csproj src/OneNlp.Neural/OneNlp.Neural.csproj src/OneNlp.Models.Embeddings/OneNlp.Models.Embeddings.csproj src/OneNlp.Models.Embeddings.Multilingual.Shard1/OneNlp.Models.Embeddings.Multilingual.Shard1.csproj src/OneNlp.Models.Embeddings.Multilingual.Shard2/OneNlp.Models.Embeddings.Multilingual.Shard2.csproj src/OneNlp.Models.Embeddings.Multilingual.Shard3/OneNlp.Models.Embeddings.Multilingual.Shard3.csproj src/OneNlp.Models.Embeddings.Multilingual/OneNlp.Models.Embeddings.Multilingual.csproj; do
  dotnet pack "$repo/$p" -c Release -o "$feed" -v quiet -clp:NoSummary -nologo
done
ls -l "$feed"
for mode in ${MODES:-none en multi both}; do
  echo "=== Models=$mode: JIT run"
  dotnet run --project "$here" -c Release -p:Models=$mode -v quiet -clp:NoSummary -nologo
  echo "=== Models=$mode: NativeAOT publish"
  out=$work/aot-$mode
  rm -rf "$out"
  dotnet publish "$here" -c Release -p:Models=$mode -o "$out" -v minimal -clp:NoSummary -nologo "$@" 2>&1 | tee "$work/aot-$mode.log" | tail -20
  grep -ciE 'warning (IL|NETSDK|CS|CA)' "$work/aot-$mode.log" || true
  ls -l "$out/RecursiveAI.OneNlp.ModelConsumer.Embeddings"
  "$out/RecursiveAI.OneNlp.ModelConsumer.Embeddings"
done
