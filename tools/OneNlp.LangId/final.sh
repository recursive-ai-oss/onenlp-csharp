#!/usr/bin/env bash
# Final scores, mixed-language check and indicative timings for a trained model.
#
#   final.sh <checkout> <work dir> <data dir> <model name> <lingua-py venv>
#
# Writes score tables, per-language CSVs and logs to <work>/out. Needs the reference predictions from tools/oracles/Lingua/run_all.sh
# and the model <work>/models/<name>.bin.
set -euo pipefail
checkout="$1"; work="$2"; data="$3"; name="$4"; venv="$5"
cd "$checkout"
out="$work/out"; mkdir -p "$out"
dotnet build tools/OneNlp.LangId/OneNlp.LangId.csproj -c Release -m:4 -v quiet -clp:NoSummary | tail -3
dll=$(find "${ONENLP_ARTIFACTS:?}" -path '*bin/OneNlp.LangId/release/RecursiveAI.OneNlp.LangIdTool.dll' | head -1)
systems="$name,lingua-py-high,lingua-py-low,lingua-net-high,lingua-net-low"
for set in main tatoeba; do
  dotnet "$dll" predict --model "$work/models/$name.bin" --eval "$data/eval/langid" --set $set --out "$work/pred" --name "$name" > /dev/null
  dotnet "$dll" score --eval "$data/eval/langid" --set $set --pred "$work/pred" --systems "$systems" --ref lingua-py-high,lingua-net-high \
    --per-language --csv "$out/per-language.$set.csv" --confusions > "$out/scores.$set.md"
done
dotnet "$dll" spans --model "$work/models/$name.bin" --eval "$data/eval/langid" --set main --out "$out/mixed.txt" > /dev/null
cat "$out/mixed.txt"

# Indicative timings: one thread, same texts, model load included separately. Not a publishable run.
{
  echo "host: $(lscpu | grep 'Model name' | sed 's/.*: *//'), $(nproc) cores; load before: $(cut -d' ' -f1-3 /proc/loadavg)"
  for rep in 1 2 3; do
    /usr/bin/time -f "ours rep $rep: wall %e s, max RSS %M KB" dotnet "$dll" predict --model "$work/models/$name.bin" --eval "$data/eval/langid" --set main --out "$work/pred-timing" --name "$name"
  done
  for mode in high low; do
    for b in words pairs sentences; do
      echo "SearchPioneer.Lingua $mode $b (single thread):"
      dotnet "${dll%/*}/../../LinguaOracle/release/LinguaOracle.dll" $mode tools/fetch/langid/languages.tsv "$data/eval/langid/main/$b.tsv" "$work/pred-timing/lingua-net-$mode.$b.txt" --single 2>&1 | tail -1
    done
  done
  for mode in high low; do
    for b in words sentences; do
      echo "lingua-language-detector $mode $b (single thread):"
      "$venv/bin/python" tools/oracles/Lingua/run_lingua.py $mode "$data/eval/langid/main/$b.tsv" "$work/pred-timing/lingua-py-$mode.$b.txt" --single 2>&1 | tail -1
    done
  done
  echo "host load after: $(cut -d' ' -f1-3 /proc/loadavg)"
} > "$out/timing-indicative.txt" 2>&1
cat "$out/timing-indicative.txt"
cp "$work/models/$name.bin" "$out/"
