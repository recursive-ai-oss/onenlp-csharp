#!/usr/bin/env python3
"""Times one fine-tuning step of the student encoder in plain PyTorch on CPU, to compare with `step-time` of tools/OneNlp.Accurate
(same batch, thread count, frozen token table, AdamW at 5e-5, gradient clipping at 1, a linear head over the last hidden states).
Weights are random: only the cost is compared. Information only.

    torch_step_time.py [--rows 24] [--length 32] [--steps 4] [--threads 8]
"""
import argparse
import os
import sys
import time

import torch

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from student import StudentConfig, StudentEncoder  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rows", type=int, default=24)
    ap.add_argument("--length", type=int, default=32)
    ap.add_argument("--steps", type=int, default=4)
    ap.add_argument("--threads", type=int, default=8)
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    model = StudentEncoder(StudentConfig())
    model.train()
    for name, p in model.named_parameters():
        if "embeddings.word" in name or "embeddings.tok" in name:
            p.requires_grad_(False)
    head = torch.nn.Linear(model.config.hidden, 17)
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
        h = model(ids, mask)
        loss = torch.nn.functional.cross_entropy(head(h.reshape(-1, h.shape[-1])), labels)
        t1 = time.perf_counter()
        loss.backward()
        t2 = time.perf_counter()
        torch.nn.utils.clip_grad_norm_(params, 1.0)
        t3 = time.perf_counter()
        opt.step()
        t4 = time.perf_counter()
        print(f"step {step}: forward {t1 - t0:.2f} s, backward {t2 - t1:.2f} s, clip {t3 - t2:.2f} s, adamw {t4 - t3:.2f} s, total {t4 - t0:.2f} s")


if __name__ == "__main__":
    main()
