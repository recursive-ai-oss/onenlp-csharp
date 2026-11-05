#!/usr/bin/env bash
# Creates the export/parity venv. Usage: setup_venv.sh VENV_DIR
# CPU PyTorch is enough for export and parity; ONNX Runtime is the black-box latency comparator.
set -euo pipefail
V=${1:?venv dir}
uv venv --python 3.13 "$V"
uv pip install --python "$V/bin/python" torch --index-url https://download.pytorch.org/whl/cpu
uv pip install --python "$V/bin/python" transformers onnx onnxruntime numpy huggingface_hub
"$V/bin/python" - <<'EOF'
import torch, transformers, onnx, onnxruntime
print("torch", torch.__version__, "transformers", transformers.__version__, "onnx", onnx.__version__, "onnxruntime", onnxruntime.__version__)
EOF
