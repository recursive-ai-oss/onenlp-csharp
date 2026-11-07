#!/usr/bin/env python3
"""Export the distilled student to ONNX for OneModel, and write its PyTorch reference hidden states.

    export_student.py --ckpt RUN/ckpt.pt --out DIR --name student-v1 [--ids ids.json]

Uses the EMA weights. The graph is (input_ids, attention_mask) -> last_hidden_state with both axes dynamic, opset 17,
attention written as MatMul/Softmax (`StudentEncoder.manual_attention`), so OneModel imports it with no special cases.
Each linear weight is its own named initializer ([in, out]; see student.py). With --ids (a JSON array of id arrays,
special tokens included) it also writes <name>.ref.bin: float32 hidden states of each text run alone, back to back,
the format `tools/OneNlp.Accurate parity` reads. Writes <name>.config.json (the architecture, read by BertStyleEncoder) and <name>.export.json with sizes and SHA-256.
"""
import argparse
import hashlib
import json
import os

import numpy as np
import torch

from student import ExportWrapper, StudentConfig, StudentEncoder


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ckpt", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--ids", default=None)
    ap.add_argument("--embed-rank", type=int, default=96)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    st = torch.load(a.ckpt, map_location="cpu", weights_only=False)
    ca = st.get("args", {})
    cfg = StudentConfig(embed_rank=ca.get("embed_rank", a.embed_rank), hidden=ca.get("hidden", 384), heads=ca.get("heads", 12),
                        ffn=ca.get("ffn", 1536), layers=ca.get("layers", 6))
    model = StudentEncoder(cfg)
    model.load_state_dict({k: v.float() for k, v in st["ema"].items()})
    model.eval()
    wrap = ExportWrapper(model).eval()
    ids = torch.randint(5, 1000, (2, 11), dtype=torch.long)
    mask = torch.ones_like(ids)
    mask[1, 8:] = 0
    path = os.path.join(a.out, a.name + ".onnx")
    with torch.no_grad():
        torch.onnx.export(wrap, (ids, mask), path, input_names=["input_ids", "attention_mask"], output_names=["last_hidden_state"],
                          dynamic_axes={"input_ids": {0: "batch", 1: "sequence"}, "attention_mask": {0: "batch", 1: "sequence"},
                                        "last_hidden_state": {0: "batch", 1: "sequence"}},
                          opset_version=17, do_constant_folding=True, dynamo=False)
    if a.ids:
        rows = json.load(open(a.ids))
        chunks = []
        with torch.no_grad():
            for row in rows:
                x = torch.tensor([row], dtype=torch.long)
                chunks.append(wrap(x, torch.ones_like(x))[0].numpy().astype("<f4"))
        np.concatenate(chunks).tofile(os.path.join(a.out, a.name + ".ref.bin"))
    files = {fn: {"bytes": os.path.getsize(os.path.join(a.out, fn)), "sha256": sha256(os.path.join(a.out, fn))}
             for fn in sorted(os.listdir(a.out)) if fn.startswith(a.name + ".onnx")}
    json.dump(cfg.to_dict(), open(os.path.join(a.out, a.name + ".config.json"), "w"))  # the head count is not in the graph
    info = {"checkpoint_step": st["step"], "config": cfg.to_dict(), "torch": torch.__version__, "opset": 17, "files": files}
    json.dump(info, open(os.path.join(a.out, a.name + ".export.json"), "w"), indent=1)
    print(json.dumps(info))


if __name__ == "__main__":
    main()
