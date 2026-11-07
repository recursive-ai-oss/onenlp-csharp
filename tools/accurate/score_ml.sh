#!/usr/bin/env bash
# Scores the predictions of recipes/accurate/multilingual (--predict-split test [--raw 1]) with tools/OneNlp.Eval: one markdown table and one
# JSON file per language, input mode and metric group, each with paired-free bootstrap 95% intervals (1000 resamples, seed 20261106).
#   score_ml.sh RUN_DIR EVAL_DLL DATA_DIR OUT_DIR
set -euo pipefail
RUN=${1:?run dir}; EVAL=${2:?Eval dll}; DATA=${3:?data dir}; OUT=${4:?output dir}
mkdir -p "$OUT"
declare -A GOLD=(
  [en_ewt-ud-test]=en_ewt [de_gsd-ud-test]=de [fr_gsd-ud-test]=fr [es_ancora-ud-test]=es [pt_bosque-ud-test]=pt
  [it_parlamint-ud-test]=it [it_markit-ud-test]=it [nl_alpino-ud-test]=nl [tr_boun-ud-test]=tr [zh_gsdsimp-ud-test]=zh
)
# Treebanks without XPOS: AllTags and XPOS are meaningless there.
NOXPOS=" fr_gsd-ud-test pt_bosque-ud-test tr_boun-ud-test "
for name in "${!GOLD[@]}"; do
  dir=${GOLD[$name]}
  gold="$DATA/eval/ud/$dir/$name.conllu"
  lang=${name%%_*}
  if [[ "$NOXPOS" == *" $name "* ]]; then M=UPOS,UFeats,Lemmas,UAS,LAS; else M=UPOS,XPOS,UFeats,AllTags,Lemmas,UAS,LAS; fi
  if [ -f "$RUN/pred-$name.conllu" ]; then
    dotnet "$EVAL" ud --gold "$gold" --pred "$RUN/pred-$name.conllu" --metrics "$M" --component accurate-large-gold --language "$lang" --dataset "$name" \
      --resamples 1000 --seed 20261106 --json "$OUT/$name.gold.json" --markdown "$OUT/$name.gold.md" > /dev/null
  fi
  if [ -f "$RUN/pred-raw-$name.conllu" ]; then
    dotnet "$EVAL" ud --gold "$gold" --pred "$RUN/pred-raw-$name.conllu" --metrics "Tokens,Sentences,Words,$M" --component accurate-large-raw --language "$lang" --dataset "$name" \
      --resamples 1000 --seed 20261106 --json "$OUT/$name.raw.json" --markdown "$OUT/$name.raw.md" > /dev/null
  fi
done
for f in "$RUN"/ner-pred-*-test.iob2; do
  [ -f "$f" ] || continue
  l=$(basename "$f" | sed 's/ner-pred-\(.*\)-test.iob2/\1/')
  dotnet "$EVAL" span --gold "$RUN/ner-gold-$l-test.iob2" --pred "$f" --component accurate-large-ner --language "$l" --dataset "ner-$l" \
    --resamples 1000 --seed 20261106 --json "$OUT/ner-$l.json" --markdown "$OUT/ner-$l.md" > /dev/null
done
cat "$OUT"/*.md > "$OUT/all.md"
echo "wrote $OUT/all.md"
