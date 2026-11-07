#!/usr/bin/env bash
# Scores one fast-tier language from the weight files its model package packs: CoNLL-U predictions in gold-token and raw-text modes,
# paired against stored predictions of the reference system, and the entity rows.
#
#   eval_fast.sh <lang: en|de|fr|es|it|nl|pt|tr|zh|hi> <out-dir>
#
# Environment (all directories, none baked in):
#   WEIGHTS   directory of <Package>/<file> links made by stage_weights.py
#   DATA      evaluation data root (eval/ud/<lang>/, eval/ner/<lang>/, eval/uner/<lang>/, ...)
#   UDREFS    reference-system predictions as pred-ud-<treebank>-<gold|raw>.conllu (English: pred-ud-<gold|raw>.conllu)
#   NERREFS   reference-system entity tags, <lang>/pred-ner-<set>.tsv
#   BUILD     the ONENLP_ARTIFACTS directory holding the Release builds of tools/OneNlp.Eval and the recipes
# Scores use 1,000 bootstrap resamples over sentences, seed 1 (the Hindi rows keep the seed their recipe documents).
set -uo pipefail
L=$1; OUT=$2/$1; mkdir -p "$OUT"
dll() { find "$BUILD" -path '*bin*release*' -name "RecursiveAI.OneNlp.$1.dll" | head -1; }
EV=$(dll Eval)
run() { dotnet "$@"; }
declare -A PKG=([en]=English [de]=German [fr]=French [es]=Spanish [it]=Italian [nl]=Dutch [pt]=Portuguese [tr]=Turkish [zh]=Chinese [hi]=Hindi)
W=$WEIGHTS/${PKG[$L]}
M=$OUT/models; rm -rf "$M"; mkdir -p "$M"
for f in "$W"/*.onenlp; do ln -s "$(readlink -f "$f")" "$M/$(basename "$f")"; done

score() { # mode gold pred ref seed dataset
  run "$EV" ud --gold "$2" --pred "$3" ${4:+--ref-pred "$4"} --language "$L" --dataset "$6" --resamples 1000 --seed "${5:-1}" \
    --markdown "$OUT/rows-$1.md" --json "$OUT/rows-$1.json" > /dev/null
  echo "== $L $1"; cut -d'|' -f2-8 "$OUT/rows-$1.md" | grep -E 'Tokens|Sentences|Words|UPOS|UFeats|Lemmas|UAS|LAS|MLAS' | head -12
}
ner_score_note() { echo "== $L entities ($1)"; cat "$OUT/$2/summary.md" 2>/dev/null | head -12 || ls "$OUT/$2"; }

case $L in
en)
  G=$DATA/eval/ud-en-ewt-r2.18/en_ewt-ud-test.conllu
  for mode in gold raw; do
    run "$(dll Recipe.EnJoint)" predict --mwt true --model "$M/english.onenlp" --gold "$G" --mode $mode --out "$OUT/pred-$mode.conllu" | tail -1
    score $mode "$G" "$OUT/pred-$mode.conllu" "$UDREFS/pred-ud-$mode.conllu" 1 "en_ewt test $mode"
  done
  run "$(dll Recipes.EnNer)" convert "$DATA/eval/uner-en-ewt-8ed072de/en_ewt-ud-test.iob2" "$OUT/ner-test.jsonl" | tail -1
  run "$(dll Recipes.EnNer)" eval "$M/english.onenlp" "$OUT/ner-test.jsonl" "$OUT/ner" | tail -15
  ;;
fr)
  G=$DATA/eval/ud/fr/fr_gsd-ud-test.conllu
  run "$(dll Recipe.EnAssemble)" "$OUT/asm.onenlp" "$M/tagging.onenlp" "$M/parser.onenlp" | tail -1
  for mode in gold raw; do
    run "$(dll Recipes.FrenchFastTagging)" predict --model "$OUT/asm.onenlp" --gold "$G" --mode $mode --mwt true --out "$OUT/pred-$mode.conllu" | tail -1
    score $mode "$G" "$OUT/pred-$mode.conllu" "$UDREFS/pred-ud-fr_gsd-$mode.conllu" 1 "fr_gsd test $mode"
  done
  run "$(dll Recipes.FrNer)" convert "$DATA/eval/ner/fr/wikiner-fr-wp3/wikiner-fr-wp3-test.iob2" "$OUT/ner-test.jsonl" | tail -1
  run "$(dll Recipes.FrNer)" eval "$M/ner.onenlp" "$OUT/ner-test.jsonl" "$OUT/ner" --ref "$NERREFS/fr/pred-ner-wikiner-fr-wp3.tsv" | tail -15
  ;;
hi)
  G=$DATA/eval/ud/hi/hi_pud-ud-test.conllu
  R=$(dll Recipes.Hindi)
  for mode in gold raw; do
    run "$R" predict --models "$M" --gold "$G" --mode $mode --out "$OUT/pred-$mode.conllu" --identity-lemmas | tail -1
    score $mode "$G" "$OUT/pred-$mode.conllu" "$NERREFS/hi-spacy2/pred-$mode.conllu" 20261107 "hi_pud test $mode"
  done
  ;;
*)
  case $L in
  de) T=de_gsd; UDD=de; REC=German; NERSET=$DATA/eval/ner/de/germeval-2014/germeval-2014-test.iob2; REFN=de/pred-ner-germeval-2014.tsv;;
  es) T=es_ancora; UDD=es; REC=Spanish; NERSET=$DATA/eval/ner/es/wikiner-es-wp3/wikiner-es-wp3-test.iob2; REFN=es/pred-ner-wikiner-es-wp3.tsv;;
  it) T=it_parlamint; UDD=it; REC=Italian; NERSET=$DATA/eval/ner/it/wikiner-it-wp3/wikiner-it-wp3-test.iob2; REFN=it/pred-ner-wikiner-it-wp3.tsv;;
  nl) T=nl_alpino; UDD=nl; REC=Dutch; NERSET=$DATA/eval/ner/nl/wikiner-nl-wp3/wikiner-nl-wp3-test.iob2; REFN=nl/pred-ner-wikiner-nl-wp3.tsv;;
  pt) T=pt_bosque; UDD=pt; REC=Portuguese; NERSET=$DATA/eval/uner/pt/pt_bosque-ud-test.iob2; REFN=pt/pred-ner-pt_bosque-ud.tsv;;
  tr) T=tr_boun; UDD=tr; REC=Turkish; NERSET=$DATA/eval/ner/tr/turkish-wikiner/turkish-wikiner-test.iob2; REFN=tr/pred-ner-turkish-wikiner.tsv;;
  zh) T=zh_gsdsimp; UDD=zh; REC=Chinese; NERSET=$DATA/eval/uner/zh/zh_gsdsimp-ud-test.iob2; REFN=zh/pred-ner-zh_gsdsimp-ud-gp.tsv;;
  esac
  G=$DATA/eval/ud/$UDD/$T-ud-test.conllu
  R=$(dll Recipes.$REC)
  X=(); [ $L = pt ] && X=(--lexicon "$DATA/train/ud/pt/pt_bosque-ud-train.conllu")
  for mode in gold raw; do
    P=(); if [ $L = tr ]; then if [ $mode = raw ]; then P=(--parts splitter,tagging,parser); else P=(--parts tagging,parser); fi; fi
    run "$R" predict --models "$M" "${P[@]}" --gold "$G" --mode $mode "${X[@]}" --out "$OUT/pred-$mode.conllu" | tail -1
    ref=$UDREFS/pred-ud-$T-$mode.conllu; [ $L = zh ] && [ $mode = gold ] && ref=$UDREFS/pred-ud-$T-gold-gp.conllu
    score $mode "$G" "$OUT/pred-$mode.conllu" "$ref" 1 "$T test $mode"
  done
  # Entities
  if [ $L = pt ]; then
    run "$R" ner-convert "$G" "$NERSET" "$OUT/ner-test.jsonl" | tail -1
  else
    run "$R" ner-convert "$NERSET" "$OUT/ner-test.jsonl" | tail -1
  fi
  PF=(); if [ $L = es ] || [ $L = it ]; then
    run "$R" ner-convert "$NERSET" "$OUT/ner-test-pieces.jsonl" --pieces | tail -1; PF=(--pieces-file "$OUT/ner-test-pieces.jsonl"); fi
  run "$R" ner-eval "$M/ner.onenlp" "$OUT/ner-test.jsonl" "$OUT/ner" --ref "$NERREFS/$REFN" "${PF[@]}" | tail -12
  if [ $L = zh ]; then
    run "$R" ner-eval "$M/ner.onenlp" "$OUT/ner-test.jsonl" "$OUT/ner-raw" --raw "$M/segmenter.onenlp" --ref "$NERREFS/zh/pred-ner-zh_gsdsimp-ud.tsv" | tail -12
  fi
  ;;
esac
