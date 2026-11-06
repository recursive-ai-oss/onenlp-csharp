#!/usr/bin/env python3
"""Indicative speed of the reference path: ONNX Runtime on the same exported model, `tokenizers` for tokenization, numpy pooling.
Texts/s at batch 1 and batch 32 over the texts of a parity sample, tokenization included, threads fixed.

    speed_ort.py MODEL_DIR TEXTS.jsonl --pool cls|mean [--prefix P] [--threads N] [--limit N]
"""
import argparse, json, time
import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

ap = argparse.ArgumentParser()
ap.add_argument("model_dir"); ap.add_argument("texts"); ap.add_argument("--pool", required=True)
ap.add_argument("--prefix", default=""); ap.add_argument("--threads", type=int, default=4); ap.add_argument("--limit", type=int, default=512)
a = ap.parse_args()
texts = [a.prefix + json.loads(l) for l in open(a.texts, encoding="utf-8")][: a.limit]
tok = Tokenizer.from_file(a.model_dir + "/tokenizer.json")
tok.enable_truncation(max_length=512)
o = ort.SessionOptions(); o.intra_op_num_threads = a.threads; o.inter_op_num_threads = 1
sess = ort.InferenceSession(a.model_dir + "/model.onnx", o, providers=["CPUExecutionProvider"])
pad = tok.token_to_id("[PAD]") or tok.token_to_id("<pad>") or 0


def embed(batch):
    encs = tok.encode_batch(batch)
    n = max(len(e.ids) for e in encs)
    ids = np.full((len(batch), n), pad, dtype=np.int64); mask = np.zeros((len(batch), n), dtype=np.int64)
    for i, e in enumerate(encs):
        ids[i, : len(e.ids)] = e.ids; mask[i, : len(e.ids)] = 1
    h = sess.run(None, {"input_ids": ids, "attention_mask": mask})[0]
    v = h[:, 0] if a.pool == "cls" else (h * mask[..., None]).sum(1) / mask.sum(1, keepdims=True)
    return v / np.linalg.norm(v, axis=1, keepdims=True)


for bs in (1, 32):
    embed(texts[:bs])  # warm up
    t = time.perf_counter()
    for i in range(0, len(texts), bs):
        embed(texts[i : i + bs])
    dt = time.perf_counter() - t
    print(f"onnxruntime {ort.__version__} batch {bs}: {len(texts) / dt:.1f} texts/s ({len(texts)} texts, {a.threads} threads)")
