#!/usr/bin/env bash
# One resumable, CPU-accounted parser recovery chunk. Call inside the worker queue.
# Required: RECIPE_DLL SPEC DATA EXPORT OUT CACHE LEDGER LANG_CODE. Optional: CPU_CAP_SECONDS (default 36000), THREADS (4).
set -euo pipefail
: "${RECIPE_DLL:?}" "${SPEC:?}" "${DATA:?}" "${EXPORT:?}" "${OUT:?}" "${CACHE:?}" "${LEDGER:?}" "${LANG_CODE:?}"
mkdir -p "$OUT/$LANG_CODE"
remaining=$(python3 - "$LEDGER" "${CPU_CAP_SECONDS:-36000}" <<'PY'
import os,sys
used=0
if os.path.exists(sys.argv[1]):
    for row in open(sys.argv[1]):
        f=row.rstrip().split('\t')
        if len(f)==6 and not row.startswith('#'):
            used+=float(f[2])+float(f[3])
print(max(0,int(float(sys.argv[2])-used)-30))
PY
)
if (( remaining < 60 )); then echo 'CPU budget exhausted'; exit 3; fi
# Persist a worst-case reservation before starting: a hard queue kill cannot erase CPU accounting.
reserved=$(( ${THREADS:-4} * 1800 ))
if (( reserved > remaining )); then reserved=$remaining; fi
python3 - "$LEDGER" "$LANG_CODE" "$reserved" <<'PYRESERVE'
import datetime,sys
with open(sys.argv[1],'a') as out:
    out.write('\t'.join([datetime.datetime.now(datetime.timezone.utc).isoformat(),sys.argv[2]+'-reserved',sys.argv[3],'0','0','0'])+'\n')
PYRESERVE
stats="$OUT/$LANG_CODE/cpu-stat.txt"
set +e
(ulimit -t "$reserved"; /usr/bin/time -o "$stats" -f '%e\t%U\t%S\t%x' \
  dotnet "$RECIPE_DLL" recover-parser --lang "$LANG_CODE" --spec "$SPEC" --data "$DATA" \
    --export "$EXPORT" --out "$OUT/$LANG_CODE" --cache "$CACHE" \
    --epochs 8 --lr 0.0002 --dropout 0.1 --batch-words 1500 --seed 66 \
    --threads "${THREADS:-4}" --budget-seconds "${CHUNK_SECONDS:-1000}")
status=$?
set -e
# Include failed chunks; the ledger is append-only. Run chunks serially against this ledger.
python3 - "$LEDGER" "$stats" "$LANG_CODE" "$status" "$reserved" <<'PY'
import datetime,sys
rows=open(sys.argv[2]).read().splitlines()
f=next(r.split('\t') for r in reversed(rows) if len(r.split('\t'))==4)
with open(sys.argv[1],'a') as out:
    out.write('\t'.join([datetime.datetime.now(datetime.timezone.utc).isoformat(),sys.argv[3]+'-actual-minus-reservation',str(float(f[1])-float(sys.argv[5])),f[2],f[0],sys.argv[4]])+'\n')
PY
exit "$status"
