#!/usr/bin/env bash
# Fetches what the scorer cross-check needs, into $ONENLP_HOME (a writable directory):
#   eval/ud/en_ewt/en_ewt-ud-test.conllu   UD English-EWT test split, release r2.15 (CC BY-SA 4.0, evaluation only)
#   eval/ud/tools/conll18_ud_eval.py       the CoNLL 2018 shared-task evaluation script (run as a black box, never read or copied into the repository)
#   u10/venv                               a Python environment with seqeval (MIT), pinned
# Every download is checked against the SHA-256 below; a mismatch stops the script.
set -euo pipefail

: "${ONENLP_HOME:?set ONENLP_HOME to a writable directory}"
UD_RELEASE=r2.15
EWT_URL="https://raw.githubusercontent.com/UniversalDependencies/UD_English-EWT/${UD_RELEASE}/en_ewt-ud-test.conllu"
EWT_SHA=0612e45914359e4b36d187ef2bfeaf66fc8933210925be985c5fbf6a9e311036
EVAL_URL="https://universaldependencies.org/conll18/conll18_ud_eval.py"
EVAL_SHA=1072e02af00b1a56205b5e8216d51dee9b8944a104d80744afaccc78859fcb16
SEQEVAL_VERSION=1.2.2

fetch() { # url sha256 destination
  if [ -f "$3" ] && [ "$(sha256sum "$3" | cut -d' ' -f1)" = "$2" ]; then return; fi
  mkdir -p "$(dirname "$3")"
  curl -sfL "$1" -o "$3.part"
  got=$(sha256sum "$3.part" | cut -d' ' -f1)
  if [ "$got" != "$2" ]; then echo "checksum mismatch for $1: $got" >&2; rm -f "$3.part"; exit 1; fi
  mv "$3.part" "$3"
}

fetch "$EWT_URL" "$EWT_SHA" "$ONENLP_HOME/eval/ud/en_ewt/en_ewt-ud-test.conllu"
fetch "$EVAL_URL" "$EVAL_SHA" "$ONENLP_HOME/eval/ud/tools/conll18_ud_eval.py"

VENV="$ONENLP_HOME/u10/venv"
if [ ! -x "$VENV/bin/python" ]; then
  uv venv --python 3.13 "$VENV"
fi
uv pip install --python "$VENV/bin/python" --quiet "seqeval==${SEQEVAL_VERSION}"
echo "ready: $ONENLP_HOME"
