#!/usr/bin/env bash
# One resumable distillation chunk; run it inside the GPU queue. Appends the wall time to the GPU-hours log.
#   chunk.sh N   (env: PY, TOOLS, TOK_DIR, RUN_DIR, HOURS_LOG, extra arguments for distill.py in DISTILL_ARGS)
set -u
N=${1:?chunk number}
START=$(date +%s)
cd "$TOOLS"
"$PY" distill.py --tok-dir "$TOK_DIR" --run-dir "$RUN_DIR" --minutes "${MINUTES:-108}" ${DISTILL_ARGS:-} 2>&1 | grep -v -e Warning -e UNEXPECTED -e '^$' | tail -n 400
CODE=${PIPESTATUS[0]}
END=$(date +%s)
printf '%s\t%s\t%s\t%s\n' "$N" "$(date -u -d @$START +%FT%TZ)" "$(date -u -d @$END +%FT%TZ)" "$(awk -v s=$START -v e=$END 'BEGIN{printf "%.3f", (e-s)/3600}')" >> "$HOURS_LOG"
exit "$CODE"
