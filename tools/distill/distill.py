#!/usr/bin/env python3
"""Distil a small multilingual encoder from mmBERT-base, in resumable chunks.

    distill.py --tok-dir DIR --run-dir DIR [--minutes 110] [--seed 1]

Teacher: jhu-clsp/mmBERT-base (MIT), frozen, bf16, used only through transformers' documented `AutoModel` API.
Student: student.StudentEncoder (6 x 384 post-norm layers, factorised embedding), plain PyTorch.

Objective: hidden-state distillation, Jiao et al. 2020 (TinyBERT) §3.2, Eq. 7 -- the mean squared error between a
learned linear map of a student layer's output and the teacher layer it is matched to (uniform layer mapping, §3.2).
The teacher's residual-stream states have a few very large dimensions, so an intermediate target is first normalised per
token (layer norm without parameters); the last layer is matched to the teacher's final hidden state (the vectors the heads
will read), standardised per feature with statistics measured on the first batches, with a cosine term added so the
direction is matched as well as the scale. The embedding table is initialised from the teacher's own table, reduced to
the student's rank by a principal-component projection (the table is then trained).

Chunks: the whole run state (student, EMA copy, optimiser, projections, step) is saved to RUN/ckpt.pt every --ckpt-every
steps and when --minutes elapse; the batch at step s is a pure function of (seed, s), so a resumed run continues the
same data stream. Evaluation and export use the EMA weights (decay --ema).
"""
import argparse
import json
import math
import os
import queue
import threading
import time

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

from student import StudentConfig, StudentEncoder

TEACHER = "jhu-clsp/mmBERT-base"
TEACHER_REV = "c5955035435e2bf121cde7f3c8863ef52ff35d82"
TEACHER_LAYERS = [4, 7, 11, 15, 18]  # hidden_states indices matched to student layers 1..5; layer 6 <- final hidden state
LENGTHS = [(128, 0.25), (256, 0.5), (512, 0.25)]


class Data:
    def __init__(self, tok_dir, alpha=0.5):
        self.langs, self.train, self.dev = [], [], []
        for fn in sorted(os.listdir(tok_dir)):
            if fn.endswith(".train.npy"):
                iso = fn[:-len(".train.npy")]
                t = np.load(os.path.join(tok_dir, fn), mmap_mode="r")
                d = np.load(os.path.join(tok_dir, iso + ".dev.npy"), mmap_mode="r")
                if len(t) == 0:
                    continue
                self.langs.append(iso)
                self.train.append(t)
                self.dev.append(d)
        n = np.array([len(t) for t in self.train], dtype=np.float64)
        p = n ** alpha
        self.p = p / p.sum()
        meta = json.load(open(os.path.join(tok_dir, "tok-stats.json")))
        self.bos, self.eos = meta["bos"], meta["eos"]

    def crop(self, row, length, rng):
        """Window row -> ids of exactly `length` (0 = pad), re-wrapped in bos/eos when cut."""
        n = int(np.count_nonzero(row)) - 2  # content tokens; ids are never 0 except the pad (id 0)
        out = np.zeros(length, dtype=np.int64)
        if n + 2 <= length:
            out[:n + 2] = row[:n + 2]
            return out
        body = length - 2
        s = int(rng.integers(0, n - body + 1))
        out[0] = self.bos
        out[1:1 + body] = row[1 + s:1 + s + body]
        out[1 + body] = self.eos
        return out

    def batch(self, seed, step, batch_tokens):
        rng = np.random.default_rng([seed, step])
        length = LENGTHS[rng.choice(len(LENGTHS), p=[p for _, p in LENGTHS])][0]
        b = batch_tokens // length
        langs = rng.choice(len(self.langs), size=b, p=self.p)
        ids = np.zeros((b, length), dtype=np.int64)
        for i, l in enumerate(langs):
            t = self.train[l]
            ids[i] = self.crop(t[int(rng.integers(0, len(t)))], length, rng)
        return ids

    def dev_batches(self, per_lang=6, length=256):
        rng = np.random.default_rng(12345)
        rows = []
        for d in self.dev:
            if len(d) == 0:
                continue
            for j in range(min(per_lang, len(d))):
                rows.append(self.crop(d[j], length, rng))
        rows = np.stack(rows)
        return [rows[i:i + 64] for i in range(0, len(rows), 64)]


def teacher_forward(teacher, ids, mask):
    with torch.no_grad(), torch.autocast("cuda", dtype=torch.bfloat16):
        out = teacher(input_ids=ids, attention_mask=mask, output_hidden_states=True)
    return out.last_hidden_state.float(), [out.hidden_states[i].float() for i in TEACHER_LAYERS]


class Distiller(nn.Module):
    def __init__(self, cfg, t_hidden):
        super().__init__()
        self.student = StudentEncoder(cfg)
        # one learned map per matched layer (TinyBERT's W_h); they are discarded after training
        self.proj = nn.ModuleList([nn.Linear(cfg.hidden, t_hidden) for _ in range(cfg.layers)])
        self.register_buffer("t_mean", torch.zeros(t_hidden))
        self.register_buffer("t_std", torch.ones(t_hidden))

    def losses(self, ids, mask, t_final, t_mid):
        s_final, s_states = self.student(ids, mask, return_all=True)
        m = mask.bool()
        parts = {}
        mid = 0.0
        for k, t in enumerate(t_mid):
            tgt = F.layer_norm(t, t.shape[-1:])
            pred = self.proj[k](s_states[k].float())
            e = F.mse_loss(pred[m], tgt[m])
            parts[f"mid{k + 1}"] = e.detach()
            mid = mid + 0.1 * e
        tgt = (t_final - self.t_mean) / self.t_std
        pred = self.proj[-1](s_final.float())
        fin = F.mse_loss(pred[m], tgt[m])
        cos = 1.0 - F.cosine_similarity(pred[m], tgt[m], dim=-1).mean()
        parts["final_mse"] = fin.detach()
        parts["final_cos"] = cos.detach()
        return fin + 0.5 * cos + mid, parts


def lr_at(step, a):
    if step < a.warmup:
        return a.lr * (step + 1) / a.warmup
    if a.decay_start is not None and step >= a.decay_start:
        return a.lr * max(0.02, 1.0 - (step - a.decay_start) / a.decay_steps)
    return a.lr


def build_optimizer(model, a):
    decay, no_decay, emb = [], [], []
    for n, p in model.named_parameters():
        if "word_embeddings" in n:
            emb.append(p)
        elif p.ndim < 2 or "LayerNorm" in n or "norm" in n or n.endswith(".bias"):
            no_decay.append(p)
        else:
            decay.append(p)
    groups = [
        {"params": decay, "weight_decay": 0.01, "lr_mult": 1.0},
        {"params": no_decay, "weight_decay": 0.0, "lr_mult": 1.0},
        {"params": emb, "weight_decay": 0.0, "lr_mult": 0.25},
    ]
    return torch.optim.AdamW(groups, lr=a.lr, betas=(0.9, 0.98), eps=1e-8, fused=True)


def init_embeddings(model, teacher, cfg):
    """Principal-component reduction of the teacher's table to `embed_rank` columns, scaled to unit variance."""
    with torch.no_grad():
        e = teacher.get_input_embeddings().weight.float()
        e = e - e.mean(0, keepdim=True)
        u, s, v = torch.svd_lowrank(e, q=cfg.embed_rank + 16, niter=6)
        coords = e @ v[:, :cfg.embed_rank]
        coords = coords / coords.std()
        model.student.embeddings.word_embeddings.weight.copy_(coords)
        energy = float((s[:cfg.embed_rank] ** 2).sum() / (e.float() ** 2).sum())
    return energy


def measure_target_stats(model, teacher, data, a, device):
    sums, sq, n = 0, 0, 0
    for step in range(-8, 0):
        ids = torch.from_numpy(data.batch(a.seed, step, a.batch_tokens // 2)).to(device)
        mask = (ids != 0).long()
        t_final, _ = teacher_forward(teacher, ids, mask)
        x = t_final[mask.bool()]
        sums = sums + x.sum(0)
        sq = sq + (x * x).sum(0)
        n += x.shape[0]
    mean = sums / n
    std = (sq / n - mean * mean).clamp_min(1e-6).sqrt()
    model.t_mean.copy_(mean)
    model.t_std.copy_(std)


def evaluate(model, ema_state, teacher, batches, device):
    live = {k: v.detach().clone() for k, v in model.student.state_dict().items()}
    model.student.load_state_dict(ema_state)
    model.eval()
    tot, cnt, parts_sum = 0.0, 0, {}
    with torch.no_grad(), torch.autocast("cuda", dtype=torch.bfloat16):
        for b in batches:
            ids = torch.from_numpy(b).to(device)
            mask = (ids != 0).long()
            t_final, t_mid = teacher_forward(teacher, ids, mask)
            loss, parts = model.losses(ids, mask, t_final, t_mid)
            k = int(mask.sum())
            tot += float(loss) * k
            cnt += k
            for n, v in parts.items():
                parts_sum[n] = parts_sum.get(n, 0.0) + float(v) * k
    model.student.load_state_dict(live)
    model.train()
    return {"dev_loss": tot / cnt, **{"dev_" + n: v / cnt for n, v in parts_sum.items()}}


def save(path, state):
    tmp = path + ".tmp"
    torch.save(state, tmp)
    os.replace(tmp, path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tok-dir", required=True)
    ap.add_argument("--run-dir", required=True)
    ap.add_argument("--minutes", type=float, default=110)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--batch-tokens", type=int, default=32768)
    ap.add_argument("--lr", type=float, default=4e-4)
    ap.add_argument("--warmup", type=int, default=3000)
    ap.add_argument("--decay-start", type=int, default=None)
    ap.add_argument("--decay-steps", type=int, default=20000)
    ap.add_argument("--ema", type=float, default=0.999)
    ap.add_argument("--ckpt-every", type=int, default=1000)
    ap.add_argument("--dev-every", type=int, default=2000)
    ap.add_argument("--max-steps", type=int, default=10 ** 9)
    ap.add_argument("--embed-rank", type=int, default=96)
    a = ap.parse_args()

    t_start = time.time()
    os.makedirs(a.run_dir, exist_ok=True)
    device = "cuda"
    torch.backends.cuda.matmul.allow_tf32 = True
    from transformers import AutoModel
    teacher = AutoModel.from_pretrained(TEACHER, revision=TEACHER_REV, torch_dtype=torch.bfloat16, attn_implementation="sdpa").to(device).eval()
    for p in teacher.parameters():
        p.requires_grad_(False)
    data = Data(a.tok_dir)
    cfg = StudentConfig(embed_rank=a.embed_rank)
    torch.manual_seed(a.seed)
    model = Distiller(cfg, teacher.config.hidden_size).to(device)
    opt = build_optimizer(model, a)
    ema = {k: v.detach().clone().float() for k, v in model.student.state_dict().items()}
    step = 0
    ckpt = os.path.join(a.run_dir, "ckpt.pt")
    log = open(os.path.join(a.run_dir, "losses.jsonl"), "a")
    if os.path.exists(ckpt):
        st = torch.load(ckpt, map_location=device, weights_only=False)
        model.load_state_dict(st["model"])
        opt.load_state_dict(st["opt"])
        ema = st["ema"]
        step = st["step"]
        print(f"resumed at step {step}", flush=True)
    else:
        energy = init_embeddings(model, teacher, cfg)
        measure_target_stats(model, teacher, data, a, device)
        ema = {k: v.detach().clone().float() for k, v in model.student.state_dict().items()}
        info = {"init": True, "embedding_rank_energy": energy, "languages": len(data.langs),
                "train_windows": int(sum(len(t) for t in data.train)),
                "student_params": sum(p.numel() for p in model.student.parameters()), "config": cfg.to_dict()}
        log.write(json.dumps(info) + "\n")
        log.flush()
        print(info, flush=True)

    dev = data.dev_batches()
    q = queue.Queue(maxsize=6)
    stop = threading.Event()

    def producer(first):
        s = first
        while not stop.is_set():
            q.put((s, data.batch(a.seed, s, a.batch_tokens)))
            s += 1

    th = threading.Thread(target=producer, args=(step,), daemon=True)
    th.start()
    model.train()
    ema_params = [ema[k] for k in ema]
    live_params = lambda: [v for v in model.student.state_dict().values()]
    t_chunk = time.time()
    run_loss, run_n, tokens = 0.0, 0, 0
    t_log = time.time()
    while step < a.max_steps:
        s, ids_np = q.get()
        assert s == step
        ids = torch.from_numpy(ids_np).to(device, non_blocking=True)
        mask = (ids != 0).long()
        lr = lr_at(step, a)
        for g in opt.param_groups:
            g["lr"] = lr * g["lr_mult"]
        t_final, t_mid = teacher_forward(teacher, ids, mask)
        with torch.autocast("cuda", dtype=torch.bfloat16):
            loss, parts = model.losses(ids, mask, t_final, t_mid)
        opt.zero_grad(set_to_none=True)
        loss.backward()
        gn = torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
        opt.step()
        with torch.no_grad():
            d = min(a.ema, (1 + step) / (10 + step))
            torch._foreach_mul_(ema_params, d)
            torch._foreach_add_(ema_params, [v.float() for v in live_params()], alpha=1 - d)
        step += 1
        tokens += ids_np.size
        run_loss += float(loss)
        run_n += 1
        if step % 100 == 0:
            rec = {"step": step, "loss": run_loss / run_n, "lr": lr, "gnorm": float(gn), "tokens": tokens,
                   "tok_per_s": round(100 * a.batch_tokens / (time.time() - t_log)), **{k: float(v) for k, v in parts.items()}}
            log.write(json.dumps(rec) + "\n")
            log.flush()
            if step % 500 == 0:
                print(rec, flush=True)
            run_loss, run_n, t_log = 0.0, 0, time.time()
        if step % a.dev_every == 0:
            rec = {"step": step, **evaluate(model, ema, teacher, dev, device)}
            log.write(json.dumps(rec) + "\n")
            log.flush()
            print(rec, flush=True)
        timeout = (time.time() - t_chunk) / 60 >= a.minutes
        if step % a.ckpt_every == 0 or timeout:
            save(ckpt, {"model": model.state_dict(), "opt": opt.state_dict(), "ema": ema, "step": step, "args": vars(a)})
        if timeout:
            break
    stop.set()
    rec = {"chunk_end": True, "step": step, "wall_minutes": round((time.time() - t_start) / 60, 1)}
    log.write(json.dumps(rec) + "\n")
    log.close()
    print(rec, flush=True)
    os._exit(0)


if __name__ == "__main__":
    main()
