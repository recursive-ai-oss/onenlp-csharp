#!/usr/bin/env bash
# One resumable MTEB chunk. Usage: mteb_chunk.sh MODEL IMPL   (MODEL: bge | e5; IMPL: st | ours | int8)
# Environment as for parity.sh (ST_VENV, MODELS, TOOL) plus OUT. Results: $OUT/mteb/MODEL-IMPL.json
set -euo pipefail
MODEL=$1; IMPL=$2
HERE=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$OUT/mteb"
case $MODEL in
  bge) REPO=BAAI/bge-small-en-v1.5; REV=5c38ec7c405ec4b44b94cc5a9bb96e735b38267a; DIR=$MODELS/bge-small-en-v1.5
       TASKS=STSBenchmark,Banking77Classification,TwitterSemEval2015,AskUbuntuDupQuestions,SciFact,TwentyNewsgroupsClustering
       EXTRA=(--retrieval-query-prefix "Represent this sentence for searching relevant passages: ");;
  e5)  REPO=intfloat/multilingual-e5-small; REV=614241f622f53c4eeff9890bdc4f31cfecc418b3; DIR=$MODELS/multilingual-e5-small
       TASKS=STSBenchmark,Banking77Classification,TwitterSemEval2015,AskUbuntuDupQuestions,SciFact,TwentyNewsgroupsClustering,STS22,MTOPDomainClassification
       EXTRA=(--query-prefix "query: " --doc-prefix "passage: ");;
esac
FILE=model.onnx
if [ "$IMPL" = int8 ]; then
  FILE=model.int8.onnx
  [ -e "$DIR/$FILE" ] || "$ST_VENV/bin/python" "$HERE/quantize_int8.py" "$DIR/model.onnx" "$DIR/$FILE"
fi
I=ours; [ "$IMPL" = st ] && I=st
# multilingual subsets: STS22 monolingual de es fr it tr zh; MTOP en de fr es
LANGS_ARG=()
[ "$MODEL" = e5 ] && LANGS_ARG=(--langs eng,deu,fra,spa,ita,tur,zho)
"$ST_VENV/bin/python" "$HERE/mteb_run.py" --impl $I --name $MODEL-$IMPL --repo $REPO --revision $REV --tool "$TOOL" --model-dir "$DIR" \
  --model-file $FILE --out "$OUT/mteb/$MODEL-$IMPL.json" --tasks $TASKS --threads 4 "${EXTRA[@]}" "${LANGS_ARG[@]}"
