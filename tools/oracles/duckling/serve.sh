#!/usr/bin/env bash
# Starts the unpacked Duckling server (see fetch.sh) on a port, in the background, and prints its pid.
# Usage: serve.sh <dir used with fetch.sh> [port]
set -euo pipefail
DIR=$(cd "${1:?directory}" && pwd)
PORT=${2:-8000}
cd "$DIR"
LANG=C.UTF-8 LD_LIBRARY_PATH="$DIR/libs" nohup root/usr/local/bin/duckling-example-exe -p "$PORT" > server.log 2>&1 &
echo $! > server.pid
for _ in $(seq 1 60); do
  if curl -fsS -o /dev/null -X POST "http://localhost:$PORT/parse" --data-urlencode 'text=ok' --data-urlencode 'locale=en_US'; then
    cat server.pid
    exit 0
  fi
  sleep 1
done
echo "duckling did not start" >&2
exit 1
