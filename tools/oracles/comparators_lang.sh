#!/usr/bin/env bash
# Runs Microsoft.Recognizers.Text (in the given culture) over a language's recognizer gold file and scores it against ours.
#
# Usage: comparators_lang.sh <lang> <culture, e.g. fr-fr> <split: gold|test> <out dir>   (from the repository root; needs dotnet and python3)
# Duckling is run separately (tools/oracles/duckling) and is information only.
set -euo pipefail
LANG_CODE=$1; CULTURE=$2; SPLIT=$3; OUT=$(mkdir -p "$4" && cd "$4" && pwd)
ROOT=$(pwd)
GOLD=$ROOT/testdata/recognizers/$LANG_CODE/$SPLIT.jsonl
ART=${ONENLP_ARTIFACTS:+$ONENLP_ARTIFACTS/$(basename "$ROOT")}
ART=${ART:-$ROOT/artifacts}
dotnet build -c Release -v quiet -clp:NoSummary tools/oracles/Recognizers/RecognizersOracle.csproj
ONENLP_CULTURE=$CULTURE dotnet "$ART/bin/RecognizersOracle/release/RecognizersOracle.dll" raw "$GOLD" "$OUT/recognizers.$SPLIT.raw.jsonl"
python3 tools/oracles/Recognizers/map_raw.py "$OUT/recognizers.$SPLIT.raw.jsonl" "$OUT/recognizers.$SPLIT.pred.jsonl" "$GOLD"
python3 tools/oracles/Recognizers/probe_units.py "$OUT/recognizers.$SPLIT.raw.jsonl" > "$OUT/recognizers.$SPLIT.units.txt"
EXTRA=()
if [ "$SPLIT" = gold ]; then EXTRA=(--errors "$OUT/recognizers.$SPLIT.errors.tsv"); fi
EVAL="$ART/bin/OneNlp.Eval/release/RecursiveAI.OneNlp.Eval.dll"
dotnet "$EVAL" recognizers --gold "$GOLD" --pred "$OUT/recognizers.$SPLIT.pred.jsonl" --component "Microsoft.Recognizers.Text 1.8.13 ($CULTURE)" \
  --dataset "recognizer-gold-$LANG_CODE $SPLIT" --json "$OUT/recognizers.$SPLIT.score.json" --markdown "$OUT/recognizers.$SPLIT.score.md" "${EXTRA[@]}" > /dev/null
echo "comparator scores written to $OUT"
