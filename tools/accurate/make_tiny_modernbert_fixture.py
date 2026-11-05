#!/usr/bin/env python3
"""Generate the tiny ModernBERT-family fixture: a randomly initialised 4-layer encoder (global attention in every second layer, a
window of 8 in the others, different rotary bases for the two kinds of layer) exported to ONNX, and PyTorch's hidden states for
fixed ids with padding. The tokenizer is the one make_tiny_fixture.py writes.

    make_tiny_modernbert_fixture.py --out testdata/accurate [--seed 0]

Nothing here is trained or derived from a released model; the weights are random (seeded). Needs torch, transformers and onnx
(see setup_venv.sh); they are used through their documented API only.
"""
import argparse
import json
import os

import torch
from transformers import ModernBertConfig, ModernBertModel

from make_tiny_fixture import vocab


class Encoder(torch.nn.Module):
    def __init__(self, m):
        super().__init__()
        self.m = m

    def forward(self, input_ids, attention_mask):
        return self.m(input_ids=input_ids, attention_mask=attention_mask).last_hidden_state


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    torch.manual_seed(a.seed)
    v = vocab()
    cfg = ModernBertConfig(
        vocab_size=len(v), hidden_size=16, intermediate_size=24, num_hidden_layers=4, num_attention_heads=2,
        global_attn_every_n_layers=2, local_attention=8, global_rope_theta=160000.0, local_rope_theta=10000.0,
        max_position_embeddings=64, pad_token_id=0, bos_token_id=2, eos_token_id=3, cls_token_id=2, sep_token_id=3,
        norm_eps=1e-5, attn_implementation="eager")
    model = ModernBertModel(cfg).eval()
    # Perturb the norm scales so a wrong norm is visible (they start at 1).
    with torch.no_grad():
        for name, p in model.named_parameters():
            if "norm" in name:
                p.add_(0.1 * torch.randn_like(p))
    enc = Encoder(model).eval()
    first = [2] + [9 + (i % 40) for i in range(14)] + [3]
    second = [2] + [20 + (i % 30) for i in range(9)] + [3]
    n = max(len(first), len(second))
    ids = torch.tensor([first, second + [0] * (n - len(second))])
    mask = (ids != 0).long()
    mask[0, :] = 1
    with torch.no_grad():
        ref = enc(ids, mask)
        torch.onnx.export(enc, (ids, mask), os.path.join(a.out, "tiny-modernbert.onnx"), input_names=["input_ids", "attention_mask"],
                          output_names=["last_hidden_state"],
                          dynamic_axes={"input_ids": {0: "batch", 1: "sequence"}, "attention_mask": {0: "batch", 1: "sequence"},
                                        "last_hidden_state": {0: "batch", 1: "sequence"}},
                          opset_version=17, dynamo=False)
    with open(os.path.join(a.out, "tiny-modernbert.reference.json"), "w") as f:
        json.dump({"input_ids": ids.tolist(), "attention_mask": mask.tolist(), "hidden": ref.tolist(), "torch": torch.__version__}, f)
    print("vocab", len(v), "bytes", os.path.getsize(os.path.join(a.out, "tiny-modernbert.onnx")))


if __name__ == "__main__":
    main()
