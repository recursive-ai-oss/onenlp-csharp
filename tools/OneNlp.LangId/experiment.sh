#!/usr/bin/env bash
# Trains one or more configurations, predicts the evaluation sets and prints the score tables next to the reference systems.
#
#   experiment.sh <checkout> <work dir> <data dir> <name>:<train args> [<name>:<train args> ...]
#
# Example: experiment.sh . ~/work ~/data "b17:--epochs 4 --samples 80000 --bits Latin=17"
# Needs the reference predictions in <work>/pred (see tools/oracles/Lingua) named <system>.<set>.<bucket>.txt.
set -euo pipefail
checkout="$1"; work="$2"; data="$3"; shift 3
cd "$checkout"
dotnet build tools/OneNlp.LangId/OneNlp.LangId.csproj -c Release -m:4 -v quiet -clp:NoSummary | tail -3
dll=$(find "${ONENLP_ARTIFACTS:?}" -path '*bin/OneNlp.LangId/release/RecursiveAI.OneNlp.LangIdTool.dll' | head -1)
mkdir -p "$work/models" "$work/logs" "$work/pred"
refs="${REFS:-lingua-py-high,lingua-py-low,lingua-net-high,lingua-net-low}"
# The reference predictions are written by tools/oracles/Lingua/run_all.sh; wait for them.
for i in $(seq 1 240); do [[ -e "$work/pred/ORACLES_DONE" ]] && break; sleep 10; done
for cfg in "$@"; do
  name="${cfg%%:*}"; args="${cfg#*:}"
  echo "=== $name: $args"
  /usr/bin/time -f "train wall %e s, max RSS %M KB" dotnet "$dll" train --train "$data/train/langid" \
    --languages tools/fetch/langid/languages.tsv --out "$work/models/$name.bin" $args > "$work/logs/train-$name.log" 2> "$work/logs/train-$name.time" || { tail -5 "$work/logs/train-$name.log"; continue; }
  grep -E 'epoch|calibration|wrote' "$work/logs/train-$name.log" | tail -${TAILN:-12}
  cat "$work/logs/train-$name.time"
  for set in main tatoeba; do
    dotnet "$dll" predict --model "$work/models/$name.bin" --eval "$data/eval/langid" --set $set --out "$work/pred" --name "$name" > /dev/null
    echo "--- $name on $set"
    dotnet "$dll" score --eval "$data/eval/langid" --set $set --pred "$work/pred" --systems "$name,$refs" --ref "${refs%%,*}" | grep -E '^###|^\| (system|'"$name"'|lingua)' | grep -v '^|---'
  done
done
