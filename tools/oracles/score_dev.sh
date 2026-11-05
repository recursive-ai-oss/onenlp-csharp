#!/usr/bin/env bash
# Scores our recognizers on the development split of a language's gold set (never the test split).
# Usage: score_dev.sh <lang> <out dir>   (run from the repository root with dotnet and python3 on PATH)
set -euo pipefail
LANG_CODE=$1
OUT=$2
mkdir -p "$OUT"
ROOT=$(pwd)
GOLD=$ROOT/testdata/recognizers/$LANG_CODE/gold.jsonl
dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
EVAL="$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"
dotnet "$EVAL" recognize --lang "$LANG_CODE" --in "$GOLD" --out "$OUT/ours.dev.raw.jsonl" 2> "$OUT/ours.dev.recognize.log"
python3 tools/recognizers-gold/map_ours.py "$OUT/ours.dev.raw.jsonl" "$OUT/ours.dev.pred.jsonl"
dotnet "$EVAL" recognizers --gold "$GOLD" --pred "$OUT/ours.dev.pred.jsonl" --component "OneNLP recognizers ($LANG_CODE)" \
  --dataset "recognizer-gold-$LANG_CODE dev" --json "$OUT/ours.dev.score.json" --markdown "$OUT/ours.dev.score.md" --errors "$OUT/ours.dev.errors.tsv" > /dev/null
cat "$OUT/ours.dev.score.md" | head -60
