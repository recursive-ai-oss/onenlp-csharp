# Duckling (black-box comparator)

Runs the released Duckling server (BSD) from the published container image `rasa/duckling`, without a container runtime. Comparator only. Nothing of the binary is read.

```
fetch.sh <dir> [tag]       # downloads the image layers from the registry and unpacks them under <dir>
serve.sh <dir> [port]      # starts duckling-example-exe on the port (default 8000), prints its pid
run_duckling.py --in gold.jsonl --out raw.jsonl --url http://localhost:8000   # POST /parse per sentence, replies stored unchanged
map_duckling.py raw.jsonl predictions.jsonl                                    # into the gold schema
```

The image is linux/amd64; `fetch.sh` keeps the image's `libpcre.so.3` apart (the binary needs it) and uses the host's libc. The mapping is described in `docs/recognizer-gold.md`.
