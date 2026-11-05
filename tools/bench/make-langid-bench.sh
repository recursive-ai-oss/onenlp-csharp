#!/usr/bin/env bash
# Builds the language-ID speed inputs: every 15th line of the held-out main evaluation set, per bucket (about 5,000 texts each).
# Usage: make-langid-bench.sh <eval dir> <out dir>      (eval dir holds main/{words,pairs,sentences}.tsv)
set -euo pipefail
mkdir -p "$2"
for b in words pairs sentences; do
  awk 'NR % 15 == 0' "$1/main/$b.tsv" > "$2/$b.tsv"
  echo "$b: $(wc -l < "$2/$b.tsv") texts, sha256 $(sha256sum "$2/$b.tsv" | cut -c1-16)"
done
