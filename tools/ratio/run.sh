#!/usr/bin/env bash
# Times one epoch of each text-category head in the C# recipes and in plain PyTorch (tools/ratio/ratio.py), same data, batch size and threads.
#
#   run.sh <recipes-bin-dir> <data-root> <work-dir> [threads]
#
# <recipes-bin-dir> holds the Release builds (<name>/release/RecursiveAI.<name>.dll); <data-root> is the data directory the fetch scripts wrote. PYTORCH_PY names a Python with PyTorch installed. The C# epoch time is the
# difference between the elapsed times of the first two development evaluations, which are on a 100-document development file so that scoring is
# negligible; the PyTorch time is the second epoch. The EUR-LEX timing trains on the 5,000-document development split.
set -euo pipefail
BIN=$1; DATA=$2; WORK=$3; T=${4:-4}
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
: "${PYTORCH_PY:?set PYTORCH_PY to a Python with PyTorch installed}"
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10
mkdir -p "$WORK"
head -100 "$DATA/train/textcat/banking77/dev.jsonl" > "$WORK/b77-dev100.jsonl"
head -100 "$DATA/train/textcat/eurlex/dev.jsonl" > "$WORK/eurlex-dev100.jsonl"
head -100 "$DATA/train/spancat/uner-en-ewt/dev.jsonl" > "$WORK/spancat-dev100.jsonl"
run() { # name recipe train dev torch-mode
  local name=$1 recipe=$2 train=$3 dev=$4 mode=$5
  echo "== $name: C# ($recipe)"
  dotnet "$BIN/$recipe/release/RecursiveAI.$recipe.dll" train "$train" "$dev" "$WORK/run-$name" --epochs 2 --patience 100 --threads "$T" 2>&1 | grep -E 'step=|completed' | tee "$WORK/cs-$name.log"
  echo "== $name: PyTorch"
  "$PYTORCH_PY" "$HERE/ratio.py" "$mode" "$train" --threads "$T" --batch-words "${BATCH:-2000}" 2>&1 | grep '^{' | tee "$WORK/torch-$name.log"
}
run textcat OneNlp.Recipes.EnTextcat "$DATA/train/textcat/banking77/train.jsonl" "$WORK/b77-dev100.jsonl" textcat
run multilabel OneNlp.Recipes.EnTextcatMultilabel "$DATA/train/textcat/eurlex/dev.jsonl" "$WORK/eurlex-dev100.jsonl" multilabel
BATCH=1000 run spancat OneNlp.Recipes.EnSpancat "$DATA/train/spancat/uner-en-ewt/train.jsonl" "$WORK/spancat-dev100.jsonl" spancat
