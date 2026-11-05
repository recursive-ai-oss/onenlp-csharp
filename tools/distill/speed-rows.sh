#!/usr/bin/env bash
# env: PROBE_DLL, DATA, OUT_ROOT, MODEL_DIR, FAST_TAG, STUDENT_FROZEN and TEACHER_FROZEN (pipeline files of the frozen-encoder heads)
# Indicative tokens per second of the trained components over the gold tokens of the EWT test split (one process per row, batches of 32 documents).
set -u
export PATH=$HOME/dotnet10:$PATH DOTNET_ROOT=$HOME/dotnet10
M=$MODEL_DIR
TEST_UD=$DATA/eval/ud-en-ewt-r2.18/en_ewt-ud-test.conllu
uptime
for T in 1 8; do
  for X in "fast-tier(tagging-v2) $FAST_TAG" \
           "student-finetuned(trainable-modules) $OUT_ROOT/r3-upos-en/model.onenlp" \
           "student-frozen(graph-runtime) $STUDENT_FROZEN" \
           "teacher-frozen(graph-runtime) $TEACHER_FROZEN"; do
    set -- $X
    echo "== $1 threads=$T"
    DOTNET_PROCESSOR_COUNT=$T dotnet $PROBE_DLL speed --model $2 --model-dir $M --gold $TEST_UD --threads $T --reps 2
  done
done
uptime
