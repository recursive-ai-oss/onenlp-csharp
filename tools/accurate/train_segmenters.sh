#!/usr/bin/env bash
# Trains the word segmenters of the unspaced-script languages of the corpus spec (those with "segmenter": "character-tagger"), one resumable chunk.
#
#   env: RECIPE_DLL  the built recipes/zh/fast recipe (its train-segmenter command is language-neutral)
#        DATA        data root of the corpus spec     SPEC  corpus.json      OUT  output root (one directory per language)
#        [LANGS="ja th vi lzh"] [SEED=1] [THREADS=4] [SECONDS_PER_LANG=1500]
#
# Run it again with the same environment until every language prints "completed=True". Development data only: no test file is read.
# The recipe is the one the Chinese segmenter was trained with (width 192, two-layer bidirectional LSTM); the epoch budget follows the size of
# the training text, so that each run is a similar number of steps.
set -euo pipefail
: "${RECIPE_DLL:?}" "${DATA:?}" "${SPEC:?}" "${OUT:?}"
LANGS=${LANGS:-"ja th vi lzh"}
for L in $LANGS; do
  read -r TRAIN DEV <<EOF
$(python3 - "$SPEC" "$L" <<'PY'
import json, sys
spec = json.load(open(sys.argv[1], encoding="utf-8"))
g = next(l for l in spec["languages"] if l["code"] == sys.argv[2])["treebanks"][0]
print(g["train"], g["dev"] or "")
PY
)
EOF
  [ -n "$DEV" ] || { echo "$L: no dev split"; continue; }
  case "$L" in
    lzh) EPOCHS=6 ;;
    ja) EPOCHS=10 ;;
    zh|th) EPOCHS=12 ;;
    *) EPOCHS=16 ;;
  esac
  mkdir -p "$OUT/$L"
  echo "== $L ($EPOCHS epochs)"
  dotnet "$RECIPE_DLL" train-segmenter --language "$L" --train "$DATA/$TRAIN" --dev "$DATA/$DEV" --out "$OUT/$L" \
    --width 192 --depth 2 --norm-rows 16384 --prefix-rows 4096 --suffix-rows 4096 --shape-rows 1024 --neighbour-rows 8192 --context-rows 8192 \
    --bigram-rows 32768 --embed-width 64 --relu 1 --recurrent 2 --recurrent-size 96 --dropout 0.5 --lr 0.001 --batch-words 1000 \
    --cosine-epochs "$EPOCHS" --seed "${SEED:-1}" --patience 100 --threads "${THREADS:-4}" --seconds "${SECONDS_PER_LANG:-1500}" | tail -n 6
done
