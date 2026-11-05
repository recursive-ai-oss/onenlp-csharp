#!/usr/bin/env python3
"""ONNX Runtime CPU latency of an exported encoder (black-box comparator; tools only).

    ort_latency.py --model m.onnx --length 64 --batches 1,8 --iters 20 --threads 8
"""
import argparse
import statistics
import time

import numpy as np
import onnxruntime as ort


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--length", type=int, default=64)
    ap.add_argument("--batches", default="1,8")
    ap.add_argument("--iters", type=int, default=20)
    ap.add_argument("--threads", type=int, default=8)
    a = ap.parse_args()
    so = ort.SessionOptions()
    so.intra_op_num_threads = a.threads
    so.inter_op_num_threads = 1
    s = ort.InferenceSession(a.model, so, providers=["CPUExecutionProvider"])
    rng = np.random.default_rng(1)
    for b in (int(x) for x in a.batches.split(",")):
        ids = rng.integers(5, 1000, size=(b, a.length), dtype=np.int64)
        feed = {"input_ids": ids, "attention_mask": np.ones_like(ids)}
        for _ in range(3):
            s.run(None, feed)
        t = []
        for _ in range(a.iters):
            t0 = time.perf_counter()
            s.run(None, feed)
            t.append((time.perf_counter() - t0) * 1000)
        print(f"onnxruntime {ort.__version__} batch {b} len {a.length} median {statistics.median(t):.1f} ms min {min(t):.1f} ms")


if __name__ == "__main__":
    main()
