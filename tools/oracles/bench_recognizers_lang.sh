#!/usr/bin/env bash
# Indicative throughput of our recognizers against Microsoft.Recognizers.Text (given culture) on text made from a language's development split.
# Usage: bench_recognizers_lang.sh <lang> <culture> <work dir>   (from the repository root; run under the heavy-job queue, labelled indicative)
set -euo pipefail
LANG_CODE=$1; CULTURE=$2; W=$(mkdir -p "$3" && cd "$3" && pwd)
ROOT=$(pwd)
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
EVAL="$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"
ORACLE="$ART/bin/RecognizersOracle/release/RecognizersOracle.dll"
python3 tools/recognizers-gold/make_bench_text.py testdata/recognizers/$LANG_CODE/gold.jsonl "$W/text.txt" "${BENCH_MB:-5}"
python3 tools/recognizers-gold/make_bench_text.py testdata/recognizers/$LANG_CODE/gold.jsonl "$W/text-datetime.txt" "${BENCH_MB_DATETIME:-1}"
{
  echo "load before: $(cut -d' ' -f1-3 /proc/loadavg)"
  for i in 1 2 3; do
    echo "ours run $i: $(/usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$EVAL" recognize --lang "$LANG_CODE" --in "$W/text.txt" --out "$W/ours.out.jsonl" --reference 2026-03-18T10:30:00+00:00 2>&1 | tr '\n' ' ')"
  done
  for i in 1 2 3; do
    echo "Recognizers.Text 1.8.13 ($CULTURE) no date/time run $i: $(ONENLP_CULTURE=$CULTURE /usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$ORACLE" bench "$W/text.txt" 2026-03-18T10:30:00 2>&1 | tr '\n' ' ')"
  done
  for i in 1 2 3; do
    echo "Recognizers.Text 1.8.13 ($CULTURE) with date/time run $i: $(ONENLP_CULTURE=$CULTURE /usr/bin/time -f 'maxrss_kb=%M wall_s=%e' dotnet "$ORACLE" bench "$W/text-datetime.txt" 2026-03-18T10:30:00 with-datetime 2>&1 | tr '\n' ' ')"
  done
  echo "load after: $(cut -d' ' -f1-3 /proc/loadavg)"
} | tee "$W/throughput.log"
