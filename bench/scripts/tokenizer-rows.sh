#!/usr/bin/env bash
# Runs the tokenizer speed rows against Microsoft.ML.Tokenizers and writes the table plus the machine record.
#
#   bench/scripts/tokenizer-rows.sh MODELS_DIR TATOEBA_FILE OUT_DIR [FILTER]
#
# MODELS_DIR holds one directory per Hugging Face model and a tiktoken directory; TATOEBA_FILE is the sentence export.
# Writes OUT_DIR/rows.md, OUT_DIR/native.txt (when ONENLP_ORACLES is set), OUT_DIR/run.log and OUT_DIR/machine.txt (CPU, cores, versions, and the load before, during and after).
# For a publishable run, start it through the host's benchmark queue so the machine is otherwise idle.
set -euo pipefail
MODELS=$1; TATOEBA=$2; OUT=$3; FILTER=${4:-}
HERE=$(cd "$(dirname "$0")" && pwd)
REPO=$(cd "$HERE/../.." && pwd)
mkdir -p "$OUT"

load() { if [ -r /proc/loadavg ]; then cut -d' ' -f1-3 /proc/loadavg; else uptime | sed 's/.*load averages*: //'; fi; }
cpu() {
  if [ -r /proc/cpuinfo ]; then grep -m1 'model name' /proc/cpuinfo | cut -d: -f2- | sed 's/^ //'; else sysctl -n machdep.cpu.brand_string; fi
}
cores() { getconf _NPROCESSORS_ONLN; }

dotnet build -c Release "$REPO/bench/OneNlp.Bench.Tokenizers" -v q -clp:NoSummary
DLL=$(find "${ONENLP_ARTIFACTS:-$REPO/artifacts}" -path '*bin/OneNlp.Bench.Tokenizers/release/RecursiveAI.OneNlp.Bench.Tokenizers.dll' | head -1)

{
  echo "date: $(date -u +%FT%TZ)"
  echo "host arch: $(uname -m)  os: $(uname -sr)"
  echo "cpu: $(cpu)"
  echo "cores: $(cores)"
  echo "dotnet: $(dotnet --version)"
  echo "load before: $(load)"
} > "$OUT/machine.txt"

( while true; do echo "$(date -u +%T) $(load)" >> "$OUT/load-during.txt"; sleep 10; done ) &
SAMPLER=$!
trap 'kill $SAMPLER 2>/dev/null || true' EXIT
: > "$OUT/load-during.txt"

ARGS=(--models "$MODELS" --tatoeba "$TATOEBA" --out "$OUT/rows.md" --dump-inputs "$OUT/inputs")
[ -n "$FILTER" ] && ARGS+=(--filter "$FILTER")
dotnet "$DLL" "${ARGS[@]}" > "$OUT/run.log" 2>&1

# Information column: the native references on the same inputs, when ONENLP_ORACLES names a directory holding the two
# virtual environments (hf-tokenizers, tiktoken). They run after our rows, so they never share the machine with them.
if [ -n "${ONENLP_ORACLES:-}" ]; then
  "$ONENLP_ORACLES/hf-tokenizers/bin/python" "$REPO/tools/bench/native_speed.py" --lib hf --inputs "$OUT/inputs" --models "$MODELS" > "$OUT/native.txt"
  "$ONENLP_ORACLES/tiktoken/bin/python" "$REPO/tools/bench/native_speed.py" --lib tiktoken --inputs "$OUT/inputs" --models "$MODELS" >> "$OUT/native.txt"
fi

kill $SAMPLER 2>/dev/null || true
{
  echo "load during (10 s samples, 1/5/15 min averages):"; sed 's/^/  /' "$OUT/load-during.txt"
  echo "load after: $(load)"
} >> "$OUT/machine.txt"
rm -f "$OUT/load-during.txt"
cat "$OUT/machine.txt"
