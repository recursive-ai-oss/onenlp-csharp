#!/usr/bin/env bash
# Trains the toy tagger with Train twice (uninterrupted, then killed with SIGKILL after its first checkpoint and resumed) and
# prints both final weight hashes, then the same toy recipe in PyTorch on CPU for a timing comparison.
#
#   toy_proof.sh <work-dir> <torch-python>
#
# Needs a built tools/OneNlp.Toy (Release) at $TOY_DLL, and `dotnet` on PATH. <torch-python> is a Python with torch installed.
set -eu
work=$1; py=$2
dll=${TOY_DLL:?set TOY_DLL to RecursiveAI.OneNlp.Toy.dll}
threads=${THREADS:-4}
epochs=${EPOCHS:-20}
rm -rf "$work/run"; mkdir -p "$work/run/A" "$work/run/B"

dotnet "$dll" gen "$work/toy"
echo "== A: uninterrupted"
dotnet "$dll" train "$work/toy/toy-train.conllu" "$work/toy/toy-dev.conllu" "$work/run/A" "$epochs" 20 "$threads" | tee "$work/run/A.log"

echo "== B: killed after the first checkpoint, then resumed"
dotnet "$dll" train "$work/toy/toy-train.conllu" "$work/toy/toy-dev.conllu" "$work/run/B" "$epochs" 20 "$threads" > "$work/run/B1.log" &
pid=$!
while [ ! -s "$work/run/B/last.ckpt" ] && kill -0 $pid 2>/dev/null; do sleep 0.02; done
kill -9 $pid 2>/dev/null || true
wait $pid 2>/dev/null || true
echo "killed pid $pid; B/last.ckpt is $(stat -c %s "$work/run/B/last.ckpt") bytes; B1.log has $(wc -l < "$work/run/B1.log") lines"
dotnet "$dll" train "$work/toy/toy-train.conllu" "$work/toy/toy-dev.conllu" "$work/run/B" "$epochs" 20 "$threads" | tee "$work/run/B2.log"

echo "== compare"
a=$(grep -h '^weights_sha256=' "$work/run/A.log"); b=$(grep -h '^weights_sha256=' "$work/run/B2.log")
echo "A $a"; echo "B $b"
[ "$a" = "$b" ] && echo "RESUME-IDENTICAL" || { echo "RESUME-DIFFERENT"; exit 1; }
cmp "$work/run/A/model.onenlp" "$work/run/B/model.onenlp" && echo "model files identical"
dotnet "$dll" eval "$work/run/A/model.onenlp" "$work/toy/toy-test.conllu"

echo "== PyTorch CPU, same recipe"
"$py" "$(dirname "$0")/toy_tagger_torch.py" "$work/toy/toy-train.conllu" "$work/toy/toy-dev.conllu" "$epochs" "$threads"
