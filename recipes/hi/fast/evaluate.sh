#!/usr/bin/env bash
# Scores the Hindi student, and the labeller it learned from, on a gold file. Two stages:
#
#   evaluate.sh dev   <models-dir> <weights-dir> <out-dir>    the hand-annotated sentences (testdata/hi/selfdev.conllu, testdata/hi/nerspot.iob2)
#   evaluate.sh final <models-dir> <weights-dir> <out-dir> <hi_pud-ud-test.conllu>
#                                                            the evaluation treebank, once, for the final model
#
# models-dir holds tagging.onenlp, parser.onenlp and ner.onenlp; weights-dir holds the accurate tier's exported files. Needs the repository
# built in Release (recipes/hi/fast and tools/OneNlp.Eval).
set -euo pipefail
STAGE=${1:?dev|final}; MODELS=${2:?models directory}; WEIGHTS=${3:?accurate-tier weights directory}; OUT=${4:?output directory}
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
R="dotnet run -c Release --no-build --project $ROOT/recipes/hi/fast --"
E="dotnet run -c Release --no-build --project $ROOT/tools/OneNlp.Eval --"
THREADS=${THREADS:-4}
mkdir -p "$OUT"

# Labels a CoNLL-U file's tokens with the source heads and writes the ensemble (and each source) as CoNLL-U files, unfiltered.
labeller() { # in.conllu out-dir
  $R label --conllu "$1" --weights "$WEIGHTS" --out "$2.jsonl.gz" --threads "$THREADS" > /dev/null
  $R ensemble --in "$2.jsonl.gz" --out "$2" --per-source --ner-all --pos-agree 0 --uas-agree 0 --dev-mod 1 > /dev/null
}

score() { # name gold pred [ref-pred]
  local ref=()
  [ -n "${4:-}" ] && ref=(--ref-pred "$4")
  $E ud --gold "$2" --pred "$3" "${ref[@]}" --component "$1" --language hi --dataset "$(basename "$2")" --resamples 1000 --seed 20261107 \
    --json "$OUT/$1.json" --markdown "$OUT/$1.md" > /dev/null
}

case $STAGE in
dev)
  G=$ROOT/testdata/hi/selfdev.conllu
  $R predict --models "$MODELS" --gold "$G" --mode gold --out "$OUT/student-gold.conllu" --identity-lemmas
  labeller "$G" "$OUT/labeller-gold"
  score student-gold "$G" "$OUT/student-gold.conllu" "$OUT/labeller-gold/src-ensemble.conllu"
  score labeller-gold "$G" "$OUT/labeller-gold/src-ensemble.conllu"
  # Entities on the hand-labelled sentences: the student, and the labeller's own output (agreement rule, no filtering of sentences).
  $R ner-convert "$ROOT/testdata/hi/nerspot.iob2" "$OUT/nerspot.jsonl"
  $R ner-eval "$MODELS/ner.onenlp" "$OUT/nerspot.jsonl" "$OUT/ner-student"
  python3 "$ROOT/tools/fetch/hi/iob2_to_conllu.py" "$ROOT/testdata/hi/nerspot.iob2" "$OUT/nerspot.conllu"
  labeller "$OUT/nerspot.conllu" "$OUT/labeller-ner"
  tsv() { awk -F'\t' '/^#/{next} NF>=3{print $2"\t"$3; next} {print ""}' "$1"; }
  tsv "$ROOT/testdata/hi/nerspot.iob2" > "$OUT/nerspot-gold.tsv"
  tsv "$OUT/labeller-ner/ner-all.iob2" > "$OUT/labeller-ner.tsv"
  $E span --gold "$OUT/nerspot-gold.tsv" --pred "$OUT/labeller-ner.tsv" --component labeller-ner --language hi --dataset nerspot --resamples 1000 --seed 20261107 \
    --json "$OUT/labeller-ner.json" --markdown "$OUT/labeller-ner.md" > /dev/null
  ;;
final)
  PUD=${5:?evaluation treebank}
  sha256sum "$PUD" | tee "$OUT/pud.sha256"
  $R predict --models "$MODELS" --gold "$PUD" --mode gold --out "$OUT/student-gold.conllu" --identity-lemmas
  $R predict --models "$MODELS" --gold "$PUD" --mode raw --out "$OUT/student-raw.conllu" --identity-lemmas
  $R segment --gold "$PUD" --out "$OUT/segment.conllu"
  labeller "$PUD" "$OUT/labeller-gold"
  labeller "$OUT/segment.conllu" "$OUT/labeller-raw"
  score student-gold "$PUD" "$OUT/student-gold.conllu" "$OUT/labeller-gold/src-ensemble.conllu"
  score student-raw "$PUD" "$OUT/student-raw.conllu"
  score labeller-gold "$PUD" "$OUT/labeller-gold/src-ensemble.conllu"
  score labeller-raw "$PUD" "$OUT/labeller-raw/src-ensemble.conllu"
  score segment "$PUD" "$OUT/segment.conllu"
  ;;
*) echo "unknown stage $STAGE" >&2; exit 2 ;;
esac
ls "$OUT"
