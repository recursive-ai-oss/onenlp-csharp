#!/usr/bin/env bash
# Build, instrument and run a fuzz target under libFuzzer.
#
#   fuzz/run.sh <target> <seconds> [extra corpus dir] [extra libFuzzer flags...]
#   fuzz/run.sh replay <target> <file>...      # run saved inputs once, without libFuzzer (reproduce a finding)
#
# Targets: see fuzz/README.md
#
# Environment (nothing is baked into the script):
#   ONENLP_FUZZ_WORK    required: where builds, corpora and findings go. Keep it outside the checkout.
#   ONENLP_FUZZ_TOOLS   directory holding `libfuzzer-dotnet` (the libFuzzer host) and `sharpfuzz` (the instrumenter).
#                       LIBFUZZER_DOTNET and SHARPFUZZ override the two paths individually.
#   ONENLP_ARTIFACTS    build output root, as for every other project in the repo
#   DOTNET              the dotnet executable (default: dotnet from PATH)
#   FUZZ_TIMEOUT        per-input timeout in seconds (default 10)
#   FUZZ_RSS_MB         memory limit in MB (default 2048)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_bin="${DOTNET:-dotnet}"
work="${ONENLP_FUZZ_WORK:?set ONENLP_FUZZ_WORK to a scratch directory outside the checkout}"
case "$(readlink -f "$work")/" in
  "$root"/*) echo "ONENLP_FUZZ_WORK must be outside the checkout" >&2; exit 2 ;;
esac
tools="${ONENLP_FUZZ_TOOLS:-}"
host="${LIBFUZZER_DOTNET:-$tools/libfuzzer-dotnet}"
instrument="${SHARPFUZZ:-$tools/sharpfuzz}"

mode=fuzz
if [[ "${1:-}" == replay ]]; then
  mode=replay
  shift
fi
target="${1:?usage: run.sh <target> <seconds> [corpus dir] | run.sh replay <target> <file>...}"
shift

case "$target" in
  word-tokenizer|segmentation|langid-detect) maxlen=4096 ;;
  langid-model) maxlen=65536 ;;
  doc-json|doc-binary|docbin) maxlen=8192 ;;
  tokenizer-json | tokenizer-json-models | code-point-regex | vocab-txt | sentencepiece-model | bpe-model | tiktoken-ranks) maxlen=65536 ;;
  charsmap) maxlen=262144 ;;
  recognizers) maxlen=4096 ;;
  recognizer-rules) maxlen=16384 ;;
  edit-tree) maxlen=8192 ;;
  fast-settings) maxlen=1024 ;;
  pipeline-config) maxlen=16384 ;;
  pipeline-file) maxlen=65536 ;;
  *) echo "unknown target '$target'" >&2; exit 2 ;;
esac

bin="$work/bin"
rm -rf "$bin"
mkdir -p "$bin"

# Build once per run: release, then instrument only the library assembly (the harness itself needs no coverage).
"$dotnet_bin" build "$root/fuzz/OneNlp.Fuzz/OneNlp.Fuzz.csproj" -c Release -v quiet -clp:NoSummary -o "$bin"

if [[ "$mode" == replay ]]; then
  "$dotnet_bin" "$bin/RecursiveAI.OneNlp.Fuzz.dll" replay "$target" "$@"
  exit $?
fi

seconds="${1:?usage: run.sh <target> <seconds> [corpus dir]}"
shift
extra=""
if [[ $# -gt 0 && "${1:0:1}" != "-" ]]; then
  extra="$1"
  shift
fi
[[ -x "$host" ]] || { echo "libFuzzer host not found at '$host' (set ONENLP_FUZZ_TOOLS)" >&2; exit 2; }
[[ -x "$instrument" ]] || { echo "sharpfuzz not found at '$instrument' (set ONENLP_FUZZ_TOOLS)" >&2; exit 2; }

export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$(readlink -f "$(command -v "$dotnet_bin")")")}"
corpus="$work/corpus/$target"
findings="$work/findings/$target"
mkdir -p "$corpus" "$findings"

# Seeds come from the uninstrumented build (instrumented code needs libFuzzer's shared memory).
"$dotnet_bin" "$bin/RecursiveAI.OneNlp.Fuzz.dll" seeds "$target" "$corpus" "$root/testdata" >/dev/null
[[ -n "$(ls -A "$corpus")" ]] || printf '\x00' >"$corpus/seed-empty"

"$instrument" "$bin/RecursiveAI.OneNlp.dll" >/dev/null
"$instrument" "$bin/RecursiveAI.OneNlp.Neural.dll" >/dev/null

dirs=("$corpus")
[[ -z "$extra" ]] || dirs+=("$extra")

# -max_total_time bounds the run; crashes, hangs and out-of-memory inputs are written to the findings folder.
ONENLP_FUZZ_TARGET="$target" "$host" \
  --target_path="$(command -v "$dotnet_bin")" --target_arg="$bin/RecursiveAI.OneNlp.Fuzz.dll" \
  -max_total_time="$seconds" -timeout="${FUZZ_TIMEOUT:-10}" -rss_limit_mb="${FUZZ_RSS_MB:-2048}" \
  -max_len="$maxlen" -artifact_prefix="$findings/" -print_final_stats=1 "$@" "${dirs[@]}"
