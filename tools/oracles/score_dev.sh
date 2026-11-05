#!/usr/bin/env bash
# Scores our recognizers on a split of a language's gold set and, when the comparator's predictions are there, compares them.
# Usage: score_dev.sh <lang> <out dir> [split: gold (development, default) | test]   (from the repository root; dotnet and python3 on PATH)
# Run the comparator first (comparators_lang.sh) to get the paired comparison. The test split is scored once, after the rules are settled.
set -euo pipefail
LANG_CODE=$1
OUT=$2
SPLIT=${3:-gold}
mkdir -p "$OUT"
ROOT=$(pwd)
GOLD=$ROOT/testdata/recognizers/$LANG_CODE/$SPLIT.jsonl
dotnet build -c Release -v quiet -clp:NoSummary tools/OneNlp.Eval/OneNlp.Eval.csproj
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
EVAL="$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"
dotnet "$EVAL" recognize --lang "$LANG_CODE" --in "$GOLD" --out "$OUT/ours.$SPLIT.raw.jsonl" 2> "$OUT/ours.$SPLIT.recognize.log"
python3 tools/recognizers-gold/map_ours.py "$OUT/ours.$SPLIT.raw.jsonl" "$OUT/ours.$SPLIT.pred.jsonl"
EXTRA=()
if [ "$SPLIT" = gold ]; then EXTRA+=(--errors "$OUT/ours.$SPLIT.errors.tsv"); fi
if [ -f "$OUT/recognizers.$SPLIT.pred.jsonl" ]; then
  EXTRA+=(--ref-pred "$OUT/recognizers.$SPLIT.pred.jsonl" --ref-component "Microsoft.Recognizers.Text 1.8.13 ($LANG_CODE)" \
    --compare-markdown "$OUT/ours-vs-recognizers.$SPLIT.md" --compare-json "$OUT/ours-vs-recognizers.$SPLIT.json")
fi
dotnet "$EVAL" recognizers --gold "$GOLD" --pred "$OUT/ours.$SPLIT.pred.jsonl" --component "OneNLP recognizers ($LANG_CODE)" \
  --dataset "recognizer-gold-$LANG_CODE $SPLIT" --json "$OUT/ours.$SPLIT.score.json" --markdown "$OUT/ours.$SPLIT.score.md" "${EXTRA[@]}" > /dev/null
grep -E '^\| (all types|all but|temporal)' "$OUT/ours.$SPLIT.score.md" | cut -c1-200
