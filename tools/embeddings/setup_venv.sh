#!/usr/bin/env bash
# Creates the embeddings oracle/export venv: sentence-transformers, mteb and ONNX Runtime, used only as black boxes
# through their documented APIs (never read). Usage: setup_venv.sh VENV_DIR
set -euo pipefail
V=${1:?venv dir}
uv venv --python 3.13 "$V"
uv pip install --python "$V/bin/python" torch --index-url https://download.pytorch.org/whl/cpu
uv pip install --python "$V/bin/python" sentence-transformers mteb onnx onnxruntime numpy huggingface_hub
"$V/bin/python" - <<'EOF'
import torch, sentence_transformers, mteb, onnx, onnxruntime, transformers, tokenizers
print("torch", torch.__version__, "sentence-transformers", sentence_transformers.__version__, "mteb", mteb.__version__,
      "transformers", transformers.__version__, "tokenizers", tokenizers.__version__, "onnx", onnx.__version__, "onnxruntime", onnxruntime.__version__)
EOF
