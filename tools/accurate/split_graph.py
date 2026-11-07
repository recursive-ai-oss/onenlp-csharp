#!/usr/bin/env python3
"""Cuts the fine-tuned encoder graph (encoder.onnx) into consecutive pieces of nearly equal size, so that each model package stays under the size
limit of the package feed, and writes the manifest lines for them.

  split_graph.py encoder.onnx OUT_DIR [--parts 6] [--prefix encoder.onnx.part]

Writes OUT_DIR/<prefix>1 ... <prefix>N and prints one <LargeWeight .../> line per piece (name, size, sha256) for the parts' weights.manifest.props.
The pieces are read back in order as one stream by the model package.
"""
import argparse
import hashlib
import os


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("graph")
    ap.add_argument("out")
    ap.add_argument("--parts", type=int, default=6)
    ap.add_argument("--prefix", default="encoder.onnx.part")
    a = ap.parse_args()
    data = open(a.graph, "rb").read()
    os.makedirs(a.out, exist_ok=True)
    per = -(-len(data) // a.parts)
    for i in range(a.parts):
        piece = data[i * per : (i + 1) * per]
        name = f"{a.prefix}{i + 1}"
        with open(os.path.join(a.out, name), "wb") as f:
            f.write(piece)
        print(f'<LargeWeight Include="{name}" Size="{len(piece)}" Sha256="{hashlib.sha256(piece).hexdigest()}" />')
    print(f'whole: size={len(data)} sha256={hashlib.sha256(data).hexdigest()}')


if __name__ == "__main__":
    main()
