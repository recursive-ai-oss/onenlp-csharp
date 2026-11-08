#!/usr/bin/env bash
# Runs consumer apps against a folder feed of packed packages: once with the JIT, once published with NativeAOT (the published app
# runs too), counting trim and AOT warnings. This is how the shipped bytes are exercised the way a user's project would use them.
#
#   consumers.sh <feed-dir> <work-dir> <project-dir-under-tests>... [-- extra dotnet publish args, e.g. -p:CppCompilerAndLinker=gcc]
#
# Project directories are names such as OneNlp.ModelConsumer.German or OneNlp.SampleConsumer. ONENLP_SAMPLE_TOKENIZER names the
# tokenizer.json the sample consumer reads. Each project is also run with WithModel=false / WithModels=false when NO_MODEL=1.
set -uo pipefail
feed=$(realpath "$1"); work=$(realpath -m "$2"); shift 2
repo=$(cd "$(dirname "$0")/../.." && pwd)
projects=()
while [[ $# -gt 0 && "$1" != "--" ]]; do projects+=("$1"); shift; done
[[ "${1:-}" == "--" ]] && shift
export NUGET_PACKAGES=$work/nuget ONENLP_CONSUMER_FEED=$feed ONENLP_ARTIFACTS=$work/artifacts
rm -rf "$NUGET_PACKAGES/recursiveai.onenlp" "$NUGET_PACKAGES"/recursiveai.onenlp.*
mkdir -p "$work/logs"
status=0
for p in "${projects[@]}"; do
  dir=$repo/tests/$p
  modes=(true)
  [[ "${NO_MODEL:-0}" == 1 ]] && modes+=(false)
  for m in "${modes[@]}"; do
    tag=$p-$m
    echo "=== $p WithModel=$m: JIT"
    dotnet run --project "$dir" -c Release -p:WithModel=$m -p:WithModels=$m -v quiet -clp:NoSummary -nologo > "$work/logs/$tag.jit.log" 2>&1
    rc=$?; tail -2 "$work/logs/$tag.jit.log"; echo "jit exit $rc"; [[ $rc == 0 ]] || status=1
    echo "=== $p WithModel=$m: NativeAOT"
    out=$work/aot/$tag; rm -rf "$out"
    dotnet publish "$dir" -c Release -p:WithModel=$m -p:WithModels=$m -o "$out" -v minimal -clp:NoSummary -nologo "$@" > "$work/logs/$tag.aot.log" 2>&1
    echo "publish exit $?; trim/AOT warnings: $(grep -ciE 'warning (IL|NETSDK|CS|CA)[0-9]*' "$work/logs/$tag.aot.log")"
    exe=$(ls "$out"/RecursiveAI.OneNlp.* 2>/dev/null | grep -v '\.dbg$\|\.pdb$' | head -1)
    ls -l "$exe" | awk '{print "size", $5}'
    "$exe" > "$work/logs/$tag.aot.run.log" 2>&1; rc=$?; tail -2 "$work/logs/$tag.aot.run.log"; echo "aot run exit $rc"; [[ $rc == 0 ]] || status=1
  done
done
exit $status
