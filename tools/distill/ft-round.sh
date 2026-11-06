#!/usr/bin/env bash
# One chunk of a fine-tuning round: the three tasks (English UPOS, French UPOS, English NER) train side by side, each on a share of the
# cores, resuming from its checkpoint directory. Run it again with the same arguments for the next chunk.
#   ft-round.sh NAME LR STEPS [recipe options...]
# env: as finetune.sh; THREADS per task (default 3), BUDGET seconds per chunk (default 1650), TASKS (default "upos-en upos-fr ner-en")
set -u
NAME=$1 LR=$2 STEPS=$3; shift 3
HERE=$(dirname "$0")
export THREADS=${THREADS:-3} BUDGET=${BUDGET:-1650}
for T in ${TASKS:-upos-en upos-fr ner-en}; do
  bash "$HERE/finetune.sh" "$NAME-$T" "$T" --finetune --lr "$LR" --total-steps "$STEPS" --max-steps "$STEPS" --warmup 50 \
    --eval-every 100 --epochs 100 --patience 100 --batch-words 1000 "$@" >> "$OUT_ROOT/$NAME-$T.log" 2>&1 &
done
wait
for T in ${TASKS:-upos-en upos-fr ner-en}; do echo "== $NAME-$T"; tail -n 3 "$OUT_ROOT/$NAME-$T.log"; done
