#!/usr/bin/env bash
# Scores tokenization and sentence segmentation against a UD split with tools/OneNlp.Eval (the `ud` scorer), for this library and
# for spaCy's rule sentencizer (a black-box oracle). With a fifth argument (the CoNLL 2018 script) it also scores with that script,
# run only as a program, as a cross-check.
#
# usage: segmentation.sh <gold.conllu> <work-dir> <oracle-python> [conll18_ud_eval.py]
set -euo pipefail
gold="$1"; work="$2"; oracle_py="$3"; script="${4:-}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$here/../.."
eval_tool=(dotnet run -c Release --project "$repo/tools/OneNlp.Eval" --)
mkdir -p "$work"
python3 "$here/ud_raw.py" "$gold" "$work/raw.txt"
"${eval_tool[@]}" segment --raw "$work/raw.txt" --out "$work/onenlp.conllu" --language en
"${eval_tool[@]}" segment --raw "$work/raw.txt" --out "$work/onenlp-mwt.conllu" --language en --mwt
"$oracle_py" "$here/spacy_segment.py" "$work/raw.txt" "$work/spacy.conllu"
"$oracle_py" "$here/spacy_segment.py" "$work/raw.txt" "$work/spacy-mwt.conllu" mwt
for system in onenlp onenlp-mwt spacy spacy-mwt; do
  echo "== $system"
  "${eval_tool[@]}" ud --gold "$gold" --pred "$work/$system.conllu" --metrics Tokens,Sentences,Words --component "$system" --dataset "$(basename "$gold")"
  if [ -n "$script" ]; then
    python3 "$script" -v "$gold" "$work/$system.conllu" | grep -E '^(Tokens|Sentences|Words)'
  fi
done
sha256sum "$gold"
