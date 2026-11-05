#!/usr/bin/env bash
# Runs the reference language detectors over the evaluation sets and writes <work>/pred/<system>.<set>.<bucket>.txt.
#
#   run_all.sh <checkout> <work dir> <data dir> <lingua-py venv>
#
# Systems: lingua-py-high, lingua-py-low (the released lingua-language-detector package) and lingua-net-high, lingua-net-low
# (the released SearchPioneer.Lingua package). They are run only through their public APIs.
set -euo pipefail
checkout="$1"; work="$2"; data="$3"; venv="$4"
cd "$checkout"
mkdir -p "$work/pred"
rm -f "$work"/pred/lingua-* "$work/pred/ORACLES_DONE"
dotnet build tools/oracles/Lingua/LinguaOracle.csproj -c Release -m:4 -v quiet -clp:NoSummary | tail -3
dll=$(find "${ONENLP_ARTIFACTS:?}" -path '*bin/LinguaOracle/release/LinguaOracle.dll' | head -1)
for set in main tatoeba; do
  for b in words pairs sentences; do
    for mode in high low; do
      src="$data/eval/langid/$set/$b.tsv"
      RAYON_NUM_THREADS=8 "$venv/bin/python" tools/oracles/Lingua/run_lingua.py $mode "$src" "$work/pred/lingua-py-$mode.$set.$b.txt"
      dotnet "$dll" $mode tools/fetch/langid/languages.tsv "$src" "$work/pred/lingua-net-$mode.$set.$b.txt"
    done
  done
done
touch "$work/pred/ORACLES_DONE"
