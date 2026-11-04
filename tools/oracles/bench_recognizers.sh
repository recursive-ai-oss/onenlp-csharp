#!/usr/bin/env bash
# Indicative throughput of our recognizers against Microsoft.Recognizers.Text on the same English text.
# Usage: bench_recognizers.sh <work dir>      (run from the repository root; run it under the machine's heavy-job queue, not beside other work)
# Builds a ~10 MB corpus from the development split, then times 3 runs of each system over it. Ours runs every rule it has; the
# comparator runs the number, unit, currency and sequence models, and once more with its date/time model: that last row is the one that does the same work as ours.
set -euo pipefail
W=$(mkdir -p "$1" && cd "$1" && pwd)
ROOT=$(pwd)
dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj
dotnet build -c Release -v quiet -clp:NoSummary tools/oracles/Recognizers/RecognizersOracle.csproj
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
EVAL="$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"
ORACLE="$ART/bin/RecognizersOracle/release/RecognizersOracle.dll"
python3 tools/recognizers-gold/make_bench_text.py testdata/recognizers/en/gold.jsonl "$W/text.txt" 10
ls -l "$W/text.txt"
{
  echo "load before: $(cut -d' ' -f1-3 /proc/loadavg)"
  for i in 1 2 3; do
    echo "ours run $i: $(/usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$EVAL" recognize --in "$W/text.txt" --out "$W/ours.out.jsonl" --reference 2026-03-18T10:30:00+00:00 2>&1 | tr '\n' ' ')"
  done
  for i in 1 2 3; do
    echo "Recognizers.Text 1.8.13 (no date/time) run $i: $(/usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$ORACLE" bench "$W/text.txt" 2026-03-18T10:30:00 2>&1 | tr '\n' ' ')"
  done
  for i in 1 2 3; do
    echo "Recognizers.Text 1.8.13 with date/time run $i: $(/usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$ORACLE" bench "$W/text.txt" 2026-03-18T10:30:00 with-datetime 2>&1 | tr '\n' ' ')"
  done
  echo "load after: $(cut -d' ' -f1-3 /proc/loadavg)"
} | tee "$W/throughput.log"
