#!/usr/bin/env bash
# Runs one libFuzzer target for a fixed time in a single process.
#
#   run_fuzz.sh <workdir> <target> <seconds> [seed-dir]...
#
# <workdir> must already hold what setup_fuzz.sh prepared. Run from the repository root with `dotnet` on PATH. Seeds are files of
# any name under the seed directories (at most 256 KB each). Findings (crash-*, oom-*, timeout-*) are left in
# <workdir>/<target>/findings. The run stops early when a timing lock file named by ONENLP_TIMING_LOCK (default
# ~/WORKER-BENCH.lock) appears, because a timing run is under way.
set -u
work=$1; target=$2; seconds=$3; shift 3
repo=$(pwd)
out="$work/$target"
rm -rf "$out"; mkdir -p "$out/corpus" "$out/findings"

dotnet publish fuzz/OneNlp.Fuzz -c Release -o "$out/pub" -v quiet -clp:NoSummary >/dev/null || exit 1
DOTNET_ROLL_FORWARD=Major "$work/tools/sharpfuzz" "$out/pub/RecursiveAI.OneNlp.dll" >/dev/null || exit 1

i=0
for d in "$@"; do
  while IFS= read -r f; do
    i=$((i+1)); cp "$f" "$out/corpus/seed-$i"
  done < <(find "$d" -type f -size -256k)
done
echo "seeds: $i"

export ONENLP_FUZZ_TARGET="$target"
setsid "$work/libfuzzer-dotnet" --target_path="$out/pub/RecursiveAI.OneNlp.Fuzz" \
  "$out/corpus" -max_total_time="$seconds" -timeout=20 -rss_limit_mb=4096 -artifact_prefix="$out/findings/" -print_final_stats=1 \
  > "$out/fuzz.log" 2>&1 &
pid=$!
trap 'kill -- -$pid 2>/dev/null' EXIT
lock="${ONENLP_TIMING_LOCK:-$HOME/WORKER-BENCH.lock}"
while kill -0 $pid 2>/dev/null; do
  sleep 20
  if [ -e "$lock" ]; then
    echo "a timing lock appeared; stopping"; kill -- -$pid; break
  fi
done
wait $pid
echo "exit: $?"
tail -25 "$out/fuzz.log"
ls "$out/findings"
