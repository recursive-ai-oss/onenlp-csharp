#!/usr/bin/env bash
# Fetches the de training and evaluation data and writes manifest-de.json (and MANIFEST.md with --md).
#
#   tools/fetch/de/fetch.sh <data-root>
#
# Needs python3; Turkish also needs pyarrow (PYTHON may name an interpreter that has it). See fetch_lang.py for the layout.
set -euo pipefail
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
exec "${PYTHON:-python3}" "$HERE/../fetch_lang.py" de "${1:?usage: fetch.sh <data-root>}" --md "$HERE/MANIFEST.md"
