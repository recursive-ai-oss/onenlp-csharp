#!/usr/bin/env python3
"""ONNX Runtime (black box; tools only) hidden states of an encoder file against a float32 dump of another implementation's states.

    ort_hidden_compare.py --model written.onnx --ids ids.json --ref hidden.bin [--texts N]

ids.json is a list of token-id lists; hidden.bin holds the states of those texts, row after row, float32. Prints the largest absolute
difference, the largest reference value and the smallest cosine similarity over all tokens.
"""
import argparse
import json

import numpy as np
import onnxruntime as ort


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--ids", required=True)
    ap.add_argument("--ref", required=True)
    ap.add_argument("--texts", type=int, default=0)
    ap.add_argument("--threads", type=int, default=8)
    a = ap.parse_args()
    ids = json.load(open(a.ids))
    if a.texts:
        ids = ids[: a.texts]
    so = ort.SessionOptions()
    so.intra_op_num_threads = a.threads
    session = ort.InferenceSession(a.model, so, providers=["CPUExecutionProvider"])
    ref = np.fromfile(a.ref, dtype=np.float32)
    width = ref.size // sum(len(x) for x in ids)
    ref = ref.reshape(-1, width)
    row = 0
    max_abs = 0.0
    max_ref = 0.0
    min_cos = 1.0
    for x in ids:
        t = np.array([x], dtype=np.int64)
        out = session.run(None, {"input_ids": t, "attention_mask": np.ones_like(t)})[0][0]
        want = ref[row : row + len(x)]
        row += len(x)
        max_abs = max(max_abs, float(np.abs(out - want).max()))
        max_ref = max(max_ref, float(np.abs(want).max()))
        cos = (out * want).sum(1) / (np.linalg.norm(out, axis=1) * np.linalg.norm(want, axis=1))
        min_cos = min(min_cos, float(cos.min()))
    print(f"onnxruntime {ort.__version__}: texts {len(ids)} tokens {row} maxAbs {max_abs:.3e} (largest |value| {max_ref:.1f}) minCos {min_cos:.8f}")


main()
