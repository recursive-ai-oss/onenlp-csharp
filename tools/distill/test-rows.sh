#!/usr/bin/env bash
# env: PROBE_DLL EVAL_DLL (the recipe and the OneNlp.Eval tool), DATA, OUT_ROOT (fine-tuning output), MODEL_DIR (the folder holding the encoder files the pipelines name),
#      RESULT_DIR, FAST_TAG and FAST_NER (the English fast-tier pipelines to compare with)
# Test-split rows and indicative speed for the student, read once. env from ft-env.sh; EVAL_DLL, FT root.
set -u
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10
P="dotnet $PROBE_DLL"
E="dotnet $EVAL_DLL"
M=$MODEL_DIR; R=$RESULT_DIR; mkdir -p $R
TEST_UD=$DATA/eval/ud-en-ewt-r2.18/en_ewt-ud-test.conllu
# Each prediction is written once; rerunning the script only fills in what is missing.
need() { [ -s "$1" ] && { echo "have $1"; return 1; }; return 0; }
need $R/uner-test.jsonl && $P convert --in $DATA/eval/uner-en-ewt-8ed072de/en_ewt-ud-test.iob2 --out $R/uner-test.jsonl
# UPOS (gold tokens): student fine-tuned (chosen on dev: r3), fast tier
need $R/upos-student.conllu && $P predict-upos --model $OUT_ROOT/r3-upos-en/model.onenlp --model-dir $M --gold $TEST_UD --out $R/upos-student.conllu --threads 8
need $R/upos-fast.conllu && $P predict-upos --model $FAST_TAG --gold $TEST_UD --out $R/upos-fast.conllu --threads 8
$E ud --gold $TEST_UD --pred $R/upos-student.conllu --ref-pred $R/upos-fast.conllu --metrics UPOS --component student-ft --language en --dataset EWT-r2.18-test --resamples 1000 --seed 1 --json $R/upos-student-vs-fast.json
$E ud --gold $TEST_UD --pred $R/upos-fast.conllu --metrics UPOS --component fast --language en --dataset EWT-r2.18-test --resamples 1000 --seed 1 --json $R/upos-fast.json
# NER: dev-chosen config (frozen head lr 1e-3), the fine-tuned r4 (information), fast tier
for X in "student-frozen $OUT_ROOT/fz-lr0.001-ner-en/model.onenlp" "student-ft-r4 $OUT_ROOT/r4-ner-en/model.onenlp" "fast $FAST_NER"; do
  set -- $X
  need $R/ner-$1.pred.iob2 && $P predict-ner --model $2 --model-dir $M --gold $R/uner-test.jsonl --out $R/ner-$1 --threads 8
done
for N in student-frozen student-ft-r4 fast; do
  head -c 0 /dev/null
  echo "== NER $N vs fast"
  $E span --gold $R/ner-$N.gold.iob2 --pred $R/ner-$N.pred.iob2 --ref-pred $R/ner-fast.pred.iob2 --metrics micro-F1 --component $N --language en --dataset UNER-EWT-test --resamples 1000 --seed 1 --json $R/ner-$N-vs-fast.json
done
