#!/usr/bin/env bash
# Runs the released Duckling binary (see fetch.sh) over a language's gold file and scores it. Information only.
# Usage: duckling_lang.sh <lang> <locale, e.g. fr_FR> <split: gold|test> <out dir> <duckling dir>   (from the repository root)
set -euo pipefail
LANG_CODE=$1; LOCALE=$2; SPLIT=$3; OUT=$(mkdir -p "$4" && cd "$4" && pwd); DUCK=$(cd "$5" && pwd)
ROOT=$(pwd)
GOLD=$ROOT/testdata/recognizers/$LANG_CODE/$SPLIT.jsonl
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
bash tools/oracles/duckling/serve.sh "$DUCK" 8011 > /dev/null
trap 'kill "$(cat "$DUCK/server.pid")" 2>/dev/null || true' EXIT
python3 tools/oracles/duckling/run_duckling.py --in "$GOLD" --out "$OUT/duckling.$SPLIT.raw.jsonl" --url http://localhost:8011 --locale "$LOCALE"
python3 tools/oracles/duckling/map_duckling.py "$OUT/duckling.$SPLIT.raw.jsonl" "$OUT/duckling.$SPLIT.pred.jsonl"
dotnet "$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll" recognizers --gold "$GOLD" --pred "$OUT/duckling.$SPLIT.pred.jsonl" \
  --component "Duckling rasa/duckling 0.2.0.2-r4 ($LOCALE)" --dataset "recognizer-gold-$LANG_CODE $SPLIT" \
  --json "$OUT/duckling.$SPLIT.score.json" --markdown "$OUT/duckling.$SPLIT.score.md" > /dev/null
echo "duckling scores written to $OUT"
