#!/usr/bin/env bash
# Downloads the published Duckling container image rasa/duckling (BSD-3-Clause) layer by layer from the registry
# and unpacks it into a directory, so the released duckling-example-exe binary can run without a container runtime.
# Only the released binary is used, as a black box. Usage: fetch.sh <target-dir> [tag]
set -euo pipefail
DIR=${1:?target directory}
TAG=${2:-0.2.0.2-r4}
mkdir -p "$DIR/layers" "$DIR/root"
cd "$DIR"
TOKEN=$(curl -fsS "https://auth.docker.io/token?service=registry.docker.io&scope=repository:rasa/duckling:pull" |
  python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')
curl -fsS -H "Authorization: Bearer $TOKEN" \
  -H 'Accept: application/vnd.docker.distribution.manifest.v2+json' \
  -D manifest.headers -o manifest.json "https://registry-1.docker.io/v2/rasa/duckling/manifests/$TAG"
grep -i '^docker-content-digest' manifest.headers | tee image-digest.txt
for d in $(python3 -c 'import json;print(" ".join(l["digest"] for l in json.load(open("manifest.json"))["layers"]))'); do
  f="layers/${d#sha256:}.tar.gz"
  if [ ! -s "$f" ]; then
    curl -fsSL -H "Authorization: Bearer $TOKEN" -o "$f" "https://registry-1.docker.io/v2/rasa/duckling/blobs/$d"
  fi
  echo "${d#sha256:}  $f" | sha256sum -c --quiet -
  tar -xzf "$f" -C root --no-same-owner --no-same-permissions --exclude='dev/*' 2>/dev/null || true
done
mkdir -p libs && cp -a root/usr/lib/x86_64-linux-gnu/libpcre.so.3* libs/
ls -la root/usr/local/bin
