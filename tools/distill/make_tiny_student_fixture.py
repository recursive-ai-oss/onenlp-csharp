#!/usr/bin/env python3
"""Generate the tiny distilled-student fixture: a randomly initialised 2-layer student-layout encoder exported to ONNX
(exactly as export_student.py does), its config file, and PyTorch's hidden states for fixed ids.

    make_tiny_student_fixture.py --out testdata/accurate [--seed 0]

Nothing is trained or derived from a released model. The tokenizer is the tiny WordPiece file made by
tools/accurate/make_tiny_fixture.py (84 pieces).
"""
import argparse
import json
import os

import torch

from student import ExportWrapper, StudentConfig, StudentEncoder


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()
    torch.manual_seed(a.seed)
    cfg = StudentConfig(vocab_size=84, embed_rank=8, hidden=16, layers=2, heads=4, ffn=32, max_positions=64)
    model = StudentEncoder(cfg)
    with torch.no_grad():  # make biases and norms non-trivial so a wrong mapping shows
        for n, p in model.named_parameters():
            if n.endswith("bias") or "norm" in n.lower():
                p.add_(torch.randn_like(p) * 0.1)
    model.eval()
    wrap = ExportWrapper(model).eval()
    ids = torch.tensor([[2, 5, 9, 11, 30, 31, 33, 3, 0, 0], [2, 7, 8, 12, 13, 14, 15, 16, 17, 3]], dtype=torch.long)
    mask = (ids != 0).long()
    os.makedirs(a.out, exist_ok=True)
    path = os.path.join(a.out, "tiny-student.onnx")
    with torch.no_grad():
        ref = wrap(ids, mask)
        torch.onnx.export(wrap, (ids, mask), path, input_names=["input_ids", "attention_mask"], output_names=["last_hidden_state"],
                          dynamic_axes={"input_ids": {0: "batch", 1: "sequence"}, "attention_mask": {0: "batch", 1: "sequence"},
                                        "last_hidden_state": {0: "batch", 1: "sequence"}},
                          opset_version=17, do_constant_folding=True, dynamo=False)
    json.dump(cfg.to_dict(), open(os.path.join(a.out, "tiny-student.config.json"), "w"))
    json.dump({"input_ids": ids.tolist(), "attention_mask": mask.tolist(), "hidden": ref.tolist(), "torch": torch.__version__},
              open(os.path.join(a.out, "tiny-student.reference.json"), "w"))
    print(os.path.getsize(path), "bytes")


if __name__ == "__main__":
    main()
