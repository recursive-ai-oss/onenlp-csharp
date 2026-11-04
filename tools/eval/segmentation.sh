#!/usr/bin/env bash
# Scores tokenization and sentence segmentation against a UD split with the CoNLL 2018 scorer (run only as a scorer), for
# this library and for spaCy's rule sentencizer (a black-box oracle).
#
# usage: segmentation.sh <gold.conllu> <work-dir> <scorer.py> <oracle-python>
set -euo pipefail
gold="$1"; work="$2"; scorer="$3"; oracle_py="$4"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$here/../.."
mkdir -p "$work"
python3 "$here/ud_raw.py" "$gold" "$work/raw.txt"
dotnet run -c Release --project "$repo/eval/OneNlp.Eval" -- segment "$work/raw.txt" "$work/onenlp.conllu" en
dotnet run -c Release --project "$repo/eval/OneNlp.Eval" -- segment "$work/raw.txt" "$work/onenlp-mwt.conllu" en mwt
"$oracle_py" "$here/spacy_segment.py" "$work/raw.txt" "$work/spacy.conllu"
"$oracle_py" "$here/spacy_segment.py" "$work/raw.txt" "$work/spacy-mwt.conllu" mwt
for system in onenlp onenlp-mwt spacy spacy-mwt; do
  echo "== $system"
  python3 "$scorer" -v "$gold" "$work/$system.conllu" | grep -E '^(Tokens|Sentences|Words)'
done
sha256sum "$gold" "$scorer"
