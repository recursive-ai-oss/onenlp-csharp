#!/usr/bin/env python3
"""PyTorch reference hidden states for token ids our tokenizer produced.

    reference_hidden.py --repo jhu-clsp/mmBERT-base --ids ids.json --out ref.bin [--revision SHA]

ids.json: a JSON array of arrays of token ids (special tokens included), one per text. Each text runs alone (no padding),
float32, eager attention. ref.bin is the float32 hidden states of all texts back to back, [sum(len), hidden], little endian;
the lengths come from ids.json.
"""
import argparse
import json

import numpy as np
import torch
from transformers import AutoModel


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--revision", default=None)
    ap.add_argument("--ids", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    torch.set_num_threads(4)
    model = AutoModel.from_pretrained(a.repo, revision=a.revision, attn_implementation="eager", torch_dtype=torch.float32).eval()
    ids = json.load(open(a.ids))
    chunks = []
    with torch.no_grad():
        for row in ids:
            x = torch.tensor([row], dtype=torch.long)
            h = model(input_ids=x, attention_mask=torch.ones_like(x)).last_hidden_state[0]
            chunks.append(h.numpy().astype("<f4"))
    np.concatenate(chunks).tofile(a.out)
    print(sum(len(r) for r in ids), "rows written")


if __name__ == "__main__":
    main()
