#!/usr/bin/env bash
# Runs both black-box comparators over the English recognizer gold files and scores them.
#
# Usage: score_recognizers.sh <out dir> <duckling dir (see duckling/fetch.sh)>
# Needs: the dotnet SDK on PATH, python3, and a free local port 8011. Run from the repository root.
set -euo pipefail
OUT=$(mkdir -p "$1" && cd "$1" && pwd)
DUCK=$(cd "$2" && pwd)
ROOT=$(pwd)
GOLD=$ROOT/testdata/recognizers/en

dotnet build -c Release -v quiet -clp:NoSummary tools/oracles/Recognizers/RecognizersOracle.csproj
dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}

bash tools/oracles/duckling/serve.sh "$DUCK" 8011 > /dev/null
trap 'kill "$(cat "$DUCK/server.pid")" 2>/dev/null || true' EXIT

for s in gold test; do
  dotnet "$ART/bin/RecognizersOracle/release/RecognizersOracle.dll" raw "$GOLD/$s.jsonl" "$OUT/recognizers.$s.raw.jsonl"
  python3 tools/oracles/Recognizers/map_raw.py "$OUT/recognizers.$s.raw.jsonl" "$OUT/recognizers.$s.pred.jsonl" "$GOLD/$s.jsonl"
  python3 tools/oracles/duckling/run_duckling.py --in "$GOLD/$s.jsonl" --out "$OUT/duckling.$s.raw.jsonl" --url http://localhost:8011
  python3 tools/oracles/duckling/map_duckling.py "$OUT/duckling.$s.raw.jsonl" "$OUT/duckling.$s.pred.jsonl"
done

for sys in recognizers duckling; do
  case $sys in
    recognizers) NAME="Microsoft.Recognizers.Text 1.8.13 (English)";;
    duckling) NAME="Duckling rasa/duckling 0.2.0.2-r4 (en_US)";;
  esac
  for s in gold test; do
    EXTRA=()
    if [ "$s" = gold ]; then EXTRA=(--errors "$OUT/$sys.$s.errors.tsv"); fi
    dotnet "$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll" recognizers --gold "$GOLD/$s.jsonl" --pred "$OUT/$sys.$s.pred.jsonl" \
      --component "$NAME" --dataset "recognizer-gold-en $s" --json "$OUT/$sys.$s.score.json" --markdown "$OUT/$sys.$s.score.md" "${EXTRA[@]}" > /dev/null
  done
done
echo "scores written to $OUT"
