#!/usr/bin/env bash
# Fetches Universal Dependencies English-EWT at a pinned release and checks its SHA-256 sums.
#
#   ud_ewt.sh <data-root>
#
# Train and dev go to <data-root>/train/ud/en_ewt/, the test file to <data-root>/eval/ud/en_ewt/, so training code that is
# pointed at train/ cannot read the test split. The treebank is CC BY-SA 4.0 (credit: Universal Dependencies, English Web
# Treebank); fetch it where it is used, never commit it. Needs curl and sha256sum.
set -eu
root=$1
tag=r2.15
base="https://raw.githubusercontent.com/UniversalDependencies/UD_English-EWT/$tag"
mkdir -p "$root/train/ud/en_ewt" "$root/eval/ud/en_ewt"
fetch() { # <split> <destination directory>
  local f="en_ewt-ud-$1.conllu"
  [ -s "$2/$f" ] || curl -sSLf -o "$2/$f" "$base/$f"
}
fetch train "$root/train/ud/en_ewt"
fetch dev "$root/train/ud/en_ewt"
fetch test "$root/eval/ud/en_ewt"
sums="$(cd "$(dirname "$0")" && pwd)/ud_ewt.sha256"
if [ -s "$sums" ]; then
  ( cd "$root" && sha256sum -c "$sums" )
else
  ( cd "$root" && sha256sum train/ud/en_ewt/*.conllu eval/ud/en_ewt/*.conllu )
  echo "no ud_ewt.sha256 next to the script yet: copy the sums above into it to pin release $tag" >&2
fi
