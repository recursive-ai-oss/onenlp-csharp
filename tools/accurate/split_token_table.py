#!/usr/bin/env python3
"""Cuts the frozen token embedding table of an exported ModernBERT-family ONNX model into row-range pieces.

The fine-tuned encoder never changes this table, so the pieces are byte-for-byte the export's own rows (little-endian float32). Each piece is
written as <prefix>-NN.bin and listed with its first row, row count, size and SHA-256 in <prefix>.parts.json.

  split_token_table.py MODEL.onnx OUT_DIR [--parts 4] [--prefix token-table]
"""
import argparse
import hashlib
import json
import os

import numpy as np
import onnx
from onnx import numpy_helper


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("model")
    ap.add_argument("out")
    ap.add_argument("--parts", type=int, default=4)
    ap.add_argument("--prefix", default="token-table")
    a = ap.parse_args()
    model = onnx.load(a.model, load_external_data=False)
    tables = [t for t in model.graph.initializer if t.name.endswith("tok_embeddings.weight")]
    if len(tables) != 1:
        raise SystemExit(f"expected one tok_embeddings.weight initializer, found {len(tables)}")
    table = numpy_helper.to_array(tables[0]).astype("<f4", copy=False)
    rows, width = table.shape
    os.makedirs(a.out, exist_ok=True)
    per = -(-rows // a.parts)
    parts = []
    for i in range(a.parts):
        first = i * per
        chunk = np.ascontiguousarray(table[first : first + per])
        if chunk.shape[0] == 0:
            break
        data = chunk.tobytes()
        name = f"{a.prefix}-{i:02d}.bin"
        with open(os.path.join(a.out, name), "wb") as f:
            f.write(data)
        parts.append({"file": name, "firstRow": first, "rows": int(chunk.shape[0]), "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    whole = hashlib.sha256(table.tobytes()).hexdigest()
    with open(os.path.join(a.out, a.prefix + ".parts.json"), "w") as f:
        json.dump({"rows": rows, "width": width, "dtype": "float32-le", "sha256": whole, "parts": parts}, f, indent=2)
    print(json.dumps({"rows": rows, "width": width, "sha256": whole, "parts": len(parts)}))


if __name__ == "__main__":
    main()
