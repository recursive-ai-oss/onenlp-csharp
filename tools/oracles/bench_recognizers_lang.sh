#!/usr/bin/env bash
# Indicative throughput of our recognizers against Microsoft.Recognizers.Text (given culture) on text made from a language's development split.
# Usage: bench_recognizers_lang.sh <lang> <culture> <work dir>   (from the repository root; run under the heavy-job queue, labelled indicative)
#
# Sizes: BENCH_MB (text for the number, unit, currency and sequence rows, default 5) and BENCH_MB_DATETIME (text for the comparator's
# date/time row, default 1), in megabytes. The comparator is slow on some cultures: Turkish and Hindi run at 0.01 to 0.03 million
# characters a second, so at the default sizes one row would take 15 to 30 minutes. Those two languages default to 0.25 MB for both.
# BENCH_TIMEOUT (seconds, default 900) bounds every single run; a run that exceeds it is logged as timed out and the script goes on.
set -euo pipefail
LANG_CODE=$1; CULTURE=$2; W=$(mkdir -p "$3" && cd "$3" && pwd)
ROOT=$(pwd)
# Build here, so the binaries are in the output folder of this checkout (Directory.Build.props names it after the checkout's folder).
dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj
dotnet build -c Release -v quiet -clp:NoSummary tools/oracles/Recognizers/RecognizersOracle.csproj
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
EVAL="$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"
ORACLE="$ART/bin/RecognizersOracle/release/RecognizersOracle.dll"
case $LANG_CODE in tr|hi) DEFAULT_MB=0.25; DEFAULT_MB_DATETIME=0.25 ;; *) DEFAULT_MB=5; DEFAULT_MB_DATETIME=1 ;; esac
T=${BENCH_TIMEOUT:-900}
python3 tools/recognizers-gold/make_bench_text.py testdata/recognizers/$LANG_CODE/gold.jsonl "$W/text.txt" "${BENCH_MB:-$DEFAULT_MB}"
python3 tools/recognizers-gold/make_bench_text.py testdata/recognizers/$LANG_CODE/gold.jsonl "$W/text-datetime.txt" "${BENCH_MB_DATETIME:-$DEFAULT_MB_DATETIME}"
{
  echo "load before: $(cut -d' ' -f1-3 /proc/loadavg)"
  for i in 1 2 3; do
    echo "ours run $i: $(timeout "$T" /usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$EVAL" recognize --lang "$LANG_CODE" --in "$W/text.txt" --out "$W/ours.out.jsonl" --reference 2026-03-18T10:30:00+00:00 2>&1 | tr '\n' ' '; [ "${PIPESTATUS[0]}" -ne 124 ] || echo "TIMED OUT after ${T}s")"
  done
  for i in 1 2 3; do
    echo "Recognizers.Text 1.8.13 ($CULTURE) no date/time run $i: $(ONENLP_CULTURE=$CULTURE timeout "$T" /usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$ORACLE" bench "$W/text.txt" 2026-03-18T10:30:00 2>&1 | tr '\n' ' '; [ "${PIPESTATUS[0]}" -ne 124 ] || echo "TIMED OUT after ${T}s")"
  done
  for i in 1 2 3; do
    echo "Recognizers.Text 1.8.13 ($CULTURE) with date/time run $i: $(ONENLP_CULTURE=$CULTURE timeout "$T" /usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$ORACLE" bench "$W/text-datetime.txt" 2026-03-18T10:30:00 with-datetime 2>&1 | tr '\n' ' '; [ "${PIPESTATUS[0]}" -ne 124 ] || echo "TIMED OUT after ${T}s")"
  done
  echo "load after: $(cut -d' ' -f1-3 /proc/loadavg)"
} | tee "$W/throughput.log"
