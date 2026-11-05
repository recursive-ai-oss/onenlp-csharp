#!/usr/bin/env bash
# Single-thread speed rows for language ID (against SearchPioneer.Lingua 2.0.0) and the English recognizers (against Microsoft.Recognizers.Text 1.8.13).
#
#   bench/scripts/langid-recognizer-rows.sh <work dir> <langid.bin> <langid bench dir>
#
# Run from the repository root, alone on the machine (under the machine's benchmark queue). The langid bench dir holds
# words.tsv, pairs.tsv and sentences.tsv made by tools/bench/make-langid-bench.sh. The log goes to <work dir>/rows.log.
# Ours and each comparator time the same inputs in one process: a load row, a warm-up over 200 inputs, then three timed repetitions.
set -euo pipefail
W=$(mkdir -p "$1" && cd "$1" && pwd)
MODEL=$(cd "$(dirname "$2")" && pwd)/$(basename "$2")
LB=$(cd "$3" && pwd)
ROOT=$(pwd)
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}

for p in tools/OneNlp.LangId/OneNlp.LangId.csproj tools/OneNlp.Eval/OneNlp.Eval.csproj tools/oracles/Lingua/LinguaOracle.csproj tools/oracles/Recognizers/RecognizersOracle.csproj; do
  dotnet build "$p" -c Release -m:4 -v quiet -clp:NoSummary | tail -2
done
find_dll() { find "$ART" -path "*/bin/$1/release/$2" | head -1; }
LANGID=$(find "$ART" -path '*/bin/*release*' -name 'RecursiveAI.OneNlp.LangIdTool.dll' | head -1)
EVAL=$(find_dll OneNlp.Eval RecursiveAI.OneNlp.Eval.dll)
LINGUA=$(find_dll LinguaOracle LinguaOracle.dll)
RECOG=$(find_dll RecognizersOracle RecognizersOracle.dll)
echo "tools: $LANGID $EVAL $LINGUA $RECOG"

load() { echo "load average $1: $(uptime | sed 's/.*load average[s]*: //')"; }
RULES=()
for f in currency numbers sequence units; do RULES+=("$ROOT/src/OneNlp/Recognizers/Data/en/$f.rules"); done
REF=2026-03-18T10:30:00+00:00

python3 tools/recognizers-gold/make_bench_text.py testdata/recognizers/en/gold.jsonl "$W/text.txt" 10
# The comparator with its date/time model runs at about 0.03 M chars/s, so the date/time rows use the first quarter of the same text
# (both systems get the same lines) to fit the benchmark group's time limit; throughput is per character.
head -n $(( $(wc -l < "$W/text.txt") / 4 )) "$W/text.txt" > "$W/text-quarter.txt"

{
  echo "date: $(date -u +%FT%TZ)"
  echo "host: $(uname -srm)"
  if [ "$(uname)" = Darwin ]; then
    echo "cpu: $(sysctl -n machdep.cpu.brand_string), $(sysctl -n hw.ncpu) cores"
  else
    echo "cpu: $(grep -m1 'model name' /proc/cpuinfo | cut -d: -f2 | xargs), $(nproc) threads"
    echo "flags: $(grep -m1 '^flags' /proc/cpuinfo | tr ' ' '\n' | grep -E '^(avx2|avx512f|fma|bmi2|sse4_2)$' | tr '\n' ' ')"
  fi
  echo "dotnet: $(dotnet --version)"
  echo "bench text: $(wc -c < "$W/text.txt") bytes, $(wc -l < "$W/text.txt") lines; date/time rows: $(wc -c < "$W/text-quarter.txt") bytes, $(wc -l < "$W/text-quarter.txt") lines"
  load before

  echo "== language ID: ours (model $(basename "$MODEL"), $(wc -c < "$MODEL") bytes)"
  dotnet "$LANGID" bench --model "$MODEL" --eval "$(dirname "$LB")" --set "$(basename "$LB")"
  load "after ours"
  for mode in high low; do
    echo "== language ID: SearchPioneer.Lingua 2.0.0, $mode accuracy"
    dotnet "$LINGUA" bench "$mode" tools/fetch/langid/languages.tsv "$LB/words.tsv,$LB/pairs.tsv,$LB/sentences.tsv"
    load "after lingua $mode"
  done

  echo "== recognizers: ours, non-temporal rules (numbers, units, currency, sequence)"
  dotnet "$EVAL" recognize-bench --in "$W/text.txt" --reference "$REF" --rules "${RULES[@]}"
  echo "== recognizers: Microsoft.Recognizers.Text 1.8.13, number/unit/currency/sequence models"
  dotnet "$RECOG" bench "$W/text.txt" 2026-03-18T10:30:00
  load "after non-temporal"
  echo "== recognizers: ours, all rules (with date/time)"
  dotnet "$EVAL" recognize-bench --in "$W/text-quarter.txt" --reference "$REF"
  echo "== recognizers: Microsoft.Recognizers.Text 1.8.13, with date/time"
  dotnet "$RECOG" bench "$W/text-quarter.txt" 2026-03-18T10:30:00 with-datetime
  load after
} 2>&1 | tee "$W/rows.log"
