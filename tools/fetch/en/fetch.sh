#!/usr/bin/env bash
# Fetches the English training and evaluation data and writes the manifest.
#
#   tools/fetch/en/fetch.sh <data-root>
#
# Layout under <data-root>:
#   train/ud-en-ewt-r2.18/        en_ewt-ud-train.conllu, en_ewt-ud-dev.conllu  (roles: train, dev)
#   eval/ud-en-ewt-r2.18/         en_ewt-ud-test.conllu                          (role: eval)
#   train/uner-en-ewt-<commit>/   en_ewt-ud-train.iob2, en_ewt-ud-dev.iob2       (roles: train, dev)
#   eval/uner-en-ewt-<commit>/    en_ewt-ud-test.iob2                            (role: eval)
#   notices/<dataset>/            the treebank's own LICENSE and README
#   manifest-en.json              role, URL, licence, size and SHA-256 of every file, plus the dedup report
#
# Only CC BY-SA 4.0 sources are fetched. Treebanks under non-commercial terms are never fetched.
# Re-running verifies the hashes of files that already exist and fails on a mismatch.
set -euo pipefail

ROOT=${1:?usage: fetch.sh <data-root>}
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

UD_TAG=r2.18
UD_BASE=https://raw.githubusercontent.com/UniversalDependencies/UD_English-EWT/$UD_TAG
UNER_COMMIT=8ed072de2fd24022cc62458997fe96a8fe191ea4   # UNER_English-EWT, the v1 release data with its LICENSE.txt
UNER_BASE=https://raw.githubusercontent.com/UniversalNER/UNER_English-EWT/$UNER_COMMIT
UNER_SHORT=${UNER_COMMIT:0:8}

get() { # url dest
  if [ ! -s "$2" ]; then
    mkdir -p "$(dirname "$2")"
    curl -fsSL --retry 3 -o "$2.part" "$1"
    mv "$2.part" "$2"
  fi
}

UD_TRAIN=$ROOT/train/ud-en-ewt-$UD_TAG
UD_EVAL=$ROOT/eval/ud-en-ewt-$UD_TAG
NE_TRAIN=$ROOT/train/uner-en-ewt-$UNER_SHORT
NE_EVAL=$ROOT/eval/uner-en-ewt-$UNER_SHORT

for s in train dev; do get "$UD_BASE/en_ewt-ud-$s.conllu" "$UD_TRAIN/en_ewt-ud-$s.conllu"; done
get "$UD_BASE/en_ewt-ud-test.conllu" "$UD_EVAL/en_ewt-ud-test.conllu"
get "$UD_BASE/LICENSE.txt" "$ROOT/notices/ud-en-ewt-$UD_TAG/LICENSE.txt"
get "$UD_BASE/README.md" "$ROOT/notices/ud-en-ewt-$UD_TAG/README.md"

for s in train dev; do get "$UNER_BASE/en_ewt-ud-$s.iob2" "$NE_TRAIN/en_ewt-ud-$s.iob2"; done
get "$UNER_BASE/en_ewt-ud-test.iob2" "$NE_EVAL/en_ewt-ud-test.iob2"
get "$UNER_BASE/LICENSE.txt" "$ROOT/notices/uner-en-ewt-$UNER_SHORT/LICENSE.txt"
get "$UNER_BASE/README.md" "$ROOT/notices/uner-en-ewt-$UNER_SHORT/README.md"

python3 "$HERE/manifest.py" "$ROOT" "$UD_TAG" "$UD_BASE" "$UNER_SHORT" "$UNER_BASE"
