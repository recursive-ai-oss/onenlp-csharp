#!/usr/bin/env bash
# Creates the distillation venv. Usage: setup_venv.sh VENV_DIR
# PyTorch from the CUDA wheel index; transformers and tokenizers are used for the teacher only, through their documented API.
set -euo pipefail
V=${1:?venv dir}
uv venv --python 3.13 "$V"
uv pip install --python "$V/bin/python" torch --index-url https://download.pytorch.org/whl/cu128
uv pip install --python "$V/bin/python" transformers tokenizers datasets huggingface_hub numpy pyarrow onnx onnxruntime matplotlib
"$V/bin/python" - <<'EOF'
import torch, transformers, tokenizers, datasets, onnx
print("torch", torch.__version__, "cuda", torch.version.cuda, "available", torch.cuda.is_available())
print("transformers", transformers.__version__, "tokenizers", tokenizers.__version__, "datasets", datasets.__version__, "onnx", onnx.__version__)
EOF
