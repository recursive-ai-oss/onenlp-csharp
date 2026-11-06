#!/usr/bin/env bash
# One resumable MTEB chunk under the 1.x release of mteb (see mteb1_run.py): the cells that miss the cards under mteb 2.x.
# Usage: mteb1_chunk.sh MODEL IMPL   (MODEL: bge | e5; IMPL: st | ours). Environment as for mteb_chunk.sh, plus MTEB1_VENV.
# Results: $OUT/mteb1/MODEL-IMPL.json (MTEB1_TAG, if set, is added to the file name: one file per mteb release)
set -euo pipefail
MODEL=$1; IMPL=$2
HERE=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$OUT/mteb1"
SUBS=""
case $MODEL in
  bge) REPO=BAAI/bge-small-en-v1.5; REV=5c38ec7c405ec4b44b94cc5a9bb96e735b38267a; DIR=$MODELS/bge-small-en-v1.5
       TASKS=Banking77Classification
       EXTRA=(--retrieval-query-prefix "Represent this sentence for searching relevant passages: ");;
  e5)  REPO=intfloat/multilingual-e5-small; REV=614241f622f53c4eeff9890bdc4f31cfecc418b3; DIR=$MODELS/multilingual-e5-small
       TASKS=Banking77Classification,AskUbuntuDupQuestions,STS22,MTOPDomainClassification
       SUBS="STS22=en,de,es,fr,it,tr,zh,de-en,de-fr,es-en,es-it,zh-en;MTOPDomainClassification=en,de,es,fr"
       EXTRA=(--query-prefix "query: " --doc-prefix "passage: ");;
esac
"$MTEB1_VENV/bin/python" "$HERE/mteb1_run.py" --impl $IMPL --name $MODEL-$IMPL --repo $REPO --revision $REV --tool "$TOOL" --model-dir "$DIR" \
  --out "$OUT/mteb1/$MODEL-$IMPL${MTEB1_TAG:-}.json" --tasks $TASKS --threads 4 --subsets "$SUBS" "${EXTRA[@]}"
