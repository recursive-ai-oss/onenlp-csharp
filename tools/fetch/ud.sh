#!/usr/bin/env bash
# Fetches Universal Dependencies English-EWT (release 2.15) and the CoNLL 2018 shared-task scorer, and checks each file against
# the SHA-256 pinned in ud.sha256. Never commit what this downloads.
#
# usage: ud.sh <data-root>
#   <data-root>/eval/ud/en_ewt/en_ewt-ud-test.conllu    test split (evaluation only: never trains, never tunes)
#   <data-root>/train/ud/en_ewt/en_ewt-ud-dev.conllu    development split (tuning)
#   <data-root>/eval/ud/tools/conll18_ud_eval.py        scorer, run only as a command-line program
# The EWT training split goes under <data-root>/train/ud/en_ewt/ when a trained model needs it.
#
# Licences: English-EWT is CC BY-SA 4.0 (https://universaldependencies.org/treebanks/en_ewt/, release 2.15; credit the treebank
# authors). The scorer is published on https://universaldependencies.org/conll18/evaluation.html.
set -euo pipefail
root="${1:?usage: ud.sh <data-root>}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
base=https://raw.githubusercontent.com/UniversalDependencies/UD_English-EWT/r2.15

fetch() { # url dest
  mkdir -p "$(dirname "$2")"
  [ -s "$2" ] || curl -fsSL --retry 3 -o "$2" "$1"
}
fetch "$base/en_ewt-ud-test.conllu" "$root/eval/ud/en_ewt/en_ewt-ud-test.conllu"
fetch "$base/en_ewt-ud-dev.conllu" "$root/train/ud/en_ewt/en_ewt-ud-dev.conllu"
fetch https://universaldependencies.org/conll18/conll18_ud_eval.py "$root/eval/ud/tools/conll18_ud_eval.py"

cd "$root"
if [ -s "$here/ud.sha256" ]; then
  sha256sum -c "$here/ud.sha256"
else
  sha256sum eval/ud/en_ewt/en_ewt-ud-test.conllu train/ud/en_ewt/en_ewt-ud-dev.conllu eval/ud/tools/conll18_ud_eval.py
  echo "no ud.sha256 yet: record the lines above" >&2
fi
