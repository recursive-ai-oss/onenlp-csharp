#!/usr/bin/env bash
# Scores our recognizers on the English recognizer gold files and compares them with Microsoft.Recognizers.Text.
#
# Usage: score_ours.sh <dir> [<repo output dir>]
# <dir> must hold recognizers.{gold,test}.pred.jsonl (written by score_recognizers.sh); the results are written there too, and the
# committed set (ours.{gold,test}.{md,json} and the comparison tables) to the second directory when given.
# Run from the repository root with the dotnet SDK and python3 on PATH.
set -euo pipefail
OUT=$(cd "$1" && pwd)
GOLD=$(pwd)/testdata/recognizers/en
ROOT=$(pwd)
dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
EVAL="$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"

for s in gold test; do
  dotnet "$EVAL" recognize --in "$GOLD/$s.jsonl" --out "$OUT/ours.$s.raw.jsonl" 2> "$OUT/ours.$s.recognize.log"
  python3 tools/recognizers-gold/map_ours.py "$OUT/ours.$s.raw.jsonl" "$OUT/ours.$s.pred.jsonl"
  EXTRA=()
  if [ "$s" = gold ]; then EXTRA=(--errors "$OUT/ours.$s.errors.tsv"); fi
  dotnet "$EVAL" recognizers --gold "$GOLD/$s.jsonl" --pred "$OUT/ours.$s.pred.jsonl" --component "OneNLP recognizers (English)" \
    --dataset "recognizer-gold-en $s" --json "$OUT/ours.$s.score.json" --markdown "$OUT/ours.$s.score.md" \
    --ref-pred "$OUT/recognizers.$s.pred.jsonl" --ref-component "Microsoft.Recognizers.Text 1.8.13 (English)" \
    --compare-markdown "$OUT/ours-vs-recognizers.$s.md" --compare-json "$OUT/ours-vs-recognizers.$s.json" "${EXTRA[@]}" > /dev/null
done

if [ -n "${2:-}" ]; then
  mkdir -p "$2"
  for s in gold test; do
    cp "$OUT/ours.$s.score.md" "$2/ours.$s.md"
    cp "$OUT/ours.$s.score.json" "$2/ours.$s.json"
    cp "$OUT/ours-vs-recognizers.$s.md" "$OUT/ours-vs-recognizers.$s.json" "$2/"
  done
fi
echo "scores written to $OUT"
