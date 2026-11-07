#!/usr/bin/env bash
# Runs several fuzz targets side by side, each for the same time under fuzz/run.sh, and prints one summary line per target.
#
#   fuzz/run-many.sh <seconds> <target>...
#
# Needs what run.sh needs (ONENLP_FUZZ_WORK, LIBFUZZER_DOTNET, SHARPFUZZ). Each target gets its own work directory and a copy of
# the checked-in seed folders, so the checkout is never written to. Exit status is non-zero if any target left a finding.
set -u
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
seconds=$1; shift
base="${ONENLP_FUZZ_WORK:?set ONENLP_FUZZ_WORK}"
for t in "$@"; do
  (
    export ONENLP_FUZZ_WORK="$base/$t"
    # Side-by-side builds must not share an output folder.
    export ONENLP_ARTIFACTS="${ONENLP_ARTIFACTS:-$base/artifacts}/$t"
    mkdir -p "$ONENLP_FUZZ_WORK/extra"
    for d in "$root/fuzz/seeds/$t" "$root/fuzz/corpus/$t"; do
      [[ -d "$d" ]] && cp "$d"/* "$ONENLP_FUZZ_WORK/extra/"
    done
    extra=()
    [[ -n "$(ls -A "$ONENLP_FUZZ_WORK/extra")" ]] && extra=("$ONENLP_FUZZ_WORK/extra")
    "$root/fuzz/run.sh" "$t" "$seconds" "${extra[@]}" > "$base/$t.log" 2>&1
  ) &
done
wait
status=0
for t in "$@"; do
  log="$base/$t.log"
  stats=$(grep -E 'stat::(number_of_executed_units|average_exec_per_sec|new_units_added|peak_rss_mb)' "$log" | tr -s ' ' | tr '\n' ' ')
  last=$(grep -E '^#[0-9]+' "$log" | tail -1 | cut -c1-120)
  found=$(ls "$base/$t/findings/$t" 2>/dev/null | wc -l)
  echo "RESULT $t findings=$found $stats | $last"
  [[ "$found" == 0 ]] || status=1
done
exit $status
