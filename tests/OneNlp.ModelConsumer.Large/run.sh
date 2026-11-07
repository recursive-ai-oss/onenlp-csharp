#!/usr/bin/env bash
# Packs the core, neural and base-size accurate-tier model packagess into a throwaway folder feed, then builds, runs and NativeAOT-publishes the consumer
# app against that feed, with and without the model package.
#   run.sh <weights-dir> <work-dir> [extra dotnet args, e.g. -p:CppCompilerAndLinker=gcc]
set -euo pipefail
weights=$(realpath "$1"); work=$(realpath -m "$2"); shift 2
here=$(cd "$(dirname "$0")" && pwd); repo=$(cd "$here/../.." && pwd)
feed=$work/feed; mkdir -p "$feed"; rm -f "$feed"/*.nupkg
export NUGET_PACKAGES=$work/nuget; rm -rf "$NUGET_PACKAGES/recursiveai.onenlp" "$NUGET_PACKAGES"/recursiveai.onenlp.*
export ONENLP_CONSUMER_FEED=$feed ONENLP_MLLARGE_WEIGHTS=$weights ONENLP_ARTIFACTS=$work/artifacts
for p in src/OneNlp/OneNlp.csproj src/OneNlp.Neural/OneNlp.Neural.csproj src/OneNlp.Models.Multilingual.Large.Parts/{Encoder1,Encoder2,TokenTable1,TokenTable2,TokenTable3,TokenTable4}/OneNlp.Models.Multilingual.Large.*.csproj src/OneNlp.Models.Multilingual.Large/OneNlp.Models.Multilingual.Large.csproj; do
  dotnet pack "$repo/$p" -c Release -o "$feed" -v quiet -clp:NoSummary -nologo
done
ls -l "$feed"
for mode in true false; do
  echo "=== WithModel=$mode: JIT run"
  dotnet run --project "$here" -c Release -p:WithModel=$mode -v quiet -clp:NoSummary -nologo
  echo "=== WithModel=$mode: NativeAOT publish"
  out=$work/aot-$mode
  rm -rf "$out"
  dotnet publish "$here" -c Release -p:WithModel=$mode -o "$out" -v minimal -clp:NoSummary -nologo "$@" 2>&1 | tee "$work/aot-$mode.log" | tail -20
  grep -ciE 'warning (IL|NETSDK|CS|CA)' "$work/aot-$mode.log" || true
  ls -l "$out/RecursiveAI.OneNlp.ModelConsumer.Large"
  /usr/bin/time -v "$out/RecursiveAI.OneNlp.ModelConsumer.Large" 2>&1 | grep -E "^(FAIL|OK|FAILED|first|peak|.*(Maximum resident|Elapsed))|/" | cut -c1-300
done
