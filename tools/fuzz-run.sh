#!/usr/bin/env bash
# Runs one fuzz target under libFuzzer for a fixed time, with SharpFuzz coverage instrumentation on the library assemblies.
#
# usage: fuzz-run.sh <target> <seconds> <work-dir> <path-to-libfuzzer-dotnet> <path-to-sharpfuzz>
# Needs the .NET SDK on PATH. Corpus and crashes land in <work-dir>/corpus and <work-dir>/crashes.
set -euo pipefail
target="$1"; seconds="$2"; work="$3"; host="$4"; instrument="$5"
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$work/corpus" "$work/crashes"
dotnet publish "$repo/fuzz/OneNlp.Fuzz" -c Release -o "$work/publish" -v quiet -clp:NoSummary
for dll in RecursiveAI.OneNlp.dll OneFont.dll; do
  [ -f "$work/publish/$dll" ] && "$instrument" "$work/publish/$dll" || true
done
cat > "$work/run-target.sh" <<EOS
#!/usr/bin/env bash
export ONENLP_FUZZ_TARGET=$target
exec dotnet "$work/publish/RecursiveAI.OneNlp.Fuzz.dll"
EOS
chmod +x "$work/run-target.sh"
"$host" --target_path="$work/run-target.sh" -max_total_time="$seconds" -artifact_prefix="$work/crashes/" -timeout=20 "$work/corpus" > "$work/fuzz.log" 2>&1 || echo "fuzzer exited with status $?"
grep -E "INITED|DONE|Done|ERROR|SUMMARY" "$work/fuzz.log" | tail -8
echo "crash files: $(find "$work/crashes" -type f | wc -l)"
