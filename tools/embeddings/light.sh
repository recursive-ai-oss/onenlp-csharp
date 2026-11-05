#!/usr/bin/env bash
# Runs a command at low priority and kills it if the benchmark lock appears. Usage: light.sh CMD...
[ -e "$HOME/WORKER-BENCH.lock" ] && { echo "bench lock present"; exit 1; }
setsid nice "$@" &
P=$!
while kill -0 $P 2>/dev/null; do
  if [ -e "$HOME/WORKER-BENCH.lock" ]; then kill -- -$P; echo "killed: bench lock"; exit 1; fi
  sleep 20
done
wait $P
