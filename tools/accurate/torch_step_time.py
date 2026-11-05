#!/usr/bin/env python3
"""Times one fine-tuning step of a Hugging Face encoder in plain PyTorch on CPU, to compare with `step-time` of tools/OneNlp.Accurate
(same batch, thread count, frozen embedding table, AdamW at 5e-5, gradient clipping at 1, a linear head over the last hidden states).
Information only. The model is loaded and run through its documented API.

    torch_step_time.py --repo-dir DIR_WITH_HF_MODEL [--rows 24] [--length 32] [--steps 4] [--threads 8]
"""
import argparse
import time

import torch
from transformers import AutoModel


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo-dir", required=True)
    ap.add_argument("--rows", type=int, default=24)
    ap.add_argument("--length", type=int, default=32)
    ap.add_argument("--steps", type=int, default=4)
    ap.add_argument("--threads", type=int, default=8)
    ap.add_argument("--attn", default="sdpa")
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    model = AutoModel.from_pretrained(a.repo_dir, attn_implementation=a.attn, torch_dtype=torch.float32)
    model.train()
    for name, p in model.named_parameters():
        if "tok_embeddings" in name or "word_embeddings" in name:
            p.requires_grad_(False)
    head = torch.nn.Linear(model.config.hidden_size, 17)
    params = [p for p in model.parameters() if p.requires_grad] + list(head.parameters())
    opt = torch.optim.AdamW(params, lr=5e-5)
    g = torch.Generator().manual_seed(1)
    ids = torch.randint(10, 5000, (a.rows, a.length), generator=g)
    mask = torch.ones_like(ids)
    labels = torch.arange(a.rows * a.length) % 17
    print(f"threads {a.threads}, batch {a.rows} x {a.length}, trainable parameters {sum(p.numel() for p in params):,}, torch {torch.__version__}")
    for step in range(a.steps):
        opt.zero_grad()
        t0 = time.perf_counter()
        h = model(input_ids=ids, attention_mask=mask).last_hidden_state
        loss = torch.nn.functional.cross_entropy(head(h.reshape(-1, h.shape[-1])), labels)
        t1 = time.perf_counter()
        loss.backward()
        t2 = time.perf_counter()
        torch.nn.utils.clip_grad_norm_(params, 1.0)
        t3 = time.perf_counter()
        opt.step()
        t4 = time.perf_counter()
        print(f"step {step}: forward {t1 - t0:.2f} s, backward {t2 - t1:.2f} s, clip {t3 - t2:.2f} s, adamw {t4 - t3:.2f} s, total {t4 - t0:.2f} s")
    model.eval()
    with torch.no_grad():
        for _ in range(2):
            t0 = time.perf_counter()
            model(input_ids=ids, attention_mask=mask)
            print(f"forward without gradients: {time.perf_counter() - t0:.2f} s")


if __name__ == "__main__":
    main()
