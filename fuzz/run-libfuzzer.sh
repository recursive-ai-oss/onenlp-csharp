#!/usr/bin/env bash
# Runs one fuzz target under libFuzzer with SharpFuzz coverage.
#
#   fuzz/run-libfuzzer.sh <target> <seconds> <work-dir>
#
# Needs the .NET SDK on PATH and two tools, given by environment variables:
#   SHARPFUZZ         the SharpFuzz.CommandLine executable (instruments an assembly in place)
#   LIBFUZZER_DOTNET  the libfuzzer-dotnet host executable
# The seed corpus is fuzz/corpus/<target>/. Findings land in <work-dir>/findings/. Output goes to stdout.
set -euo pipefail
target=${1:?target name}
seconds=${2:?seconds}
work=${3:?work directory}
root=$(cd "$(dirname "$0")/.." && pwd)

mkdir -p "$work/findings" "$work/corpus"
dotnet publish "$root/fuzz/OneNlp.Fuzz" -c Release -o "$work/bin" -v quiet -clp:NoSummary
"$SHARPFUZZ" "$work/bin/RecursiveAI.OneNlp.dll"
cp -n "$root/fuzz/corpus/$target/"* "$work/corpus/" 2>/dev/null || true

ONENLP_FUZZ_TARGET="$target" "$LIBFUZZER_DOTNET" --target_path="$work/bin/RecursiveAI.OneNlp.Fuzz" \
  -max_total_time="$seconds" -rss_limit_mb=2048 -timeout=20 -artifact_prefix="$work/findings/" "$work/corpus"
