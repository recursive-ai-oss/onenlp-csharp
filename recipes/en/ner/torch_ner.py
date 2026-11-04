#!/usr/bin/env python3
"""The tagging recipe of this directory in plain PyTorch on CPU, for a training-time comparison.

usage: torch_ner.py <train.jsonl> <epochs> <threads> [seed]

Same data (the JSON Lines file `convert` writes), same model (4 hashed features per token -> 64-wide embeddings joined and projected to
256 with ReLU -> dropout 0.4 -> window of 5 tokens -> 256 ReLU -> 13 BILUO tags), same optimizer (AdamW, lr 0.003, weight decay 0.01,
gradient clip 1), same batching (sentences shuffled each epoch, batches of up to 1000 words). Feature hashing is done once before the
timed epochs, which favours PyTorch; the C# recipe hashes inside every step.
"""
import json
import sys
import time
import zlib

import torch
import torch.nn as nn
import torch.nn.functional as F

BUCKETS, DIM, WIDTH, HIDDEN, CONTEXT, DROPOUT = 1 << 15, 64, 256, 256, 2, 0.4


def shape(text):
    out, prev, run = [], "", 0
    for c in text:
        s = "X" if c.isupper() else "x" if c.islower() else "d" if c.isdigit() else c
        run = run + 1 if s == prev else 1
        prev = s
        if run <= 4:
            out.append(s)
    return "".join(out)


def bucket(salt, key):
    return zlib.crc32(key.encode("utf-16-le"), salt) % BUCKETS


def load(path):
    docs, labels = [], {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            d = json.loads(line)
            text, toks = d["text"], d["tokens"]
            ents = {(s, e): lab for s, e, lab in d["entities"]}
            starts = {s: i for i, (s, _) in enumerate(toks)}
            tags = [0] * len(toks)
            for (s, e), lab in ents.items():
                labels.setdefault(lab, len(labels))
            docs.append((text, toks, ents, starts, tags))
    names = sorted(labels)
    index = {n: i for i, n in enumerate(names)}
    data = []
    for text, toks, ents, starts, tags in docs:
        feats = []
        for s, e in toks:
            w = text[s:e]
            low = w.lower()
            feats.append([bucket(1, low), bucket(2, low[:3]), bucket(3, low[-3:]), bucket(4, shape(w))])
        for (s, e), lab in ents.items():
            a = starts[s]
            b = next(i for i, (_, te) in enumerate(toks) if te == e)
            k = index[lab]
            if a == b:
                tags[a] = 1 + 4 * k + 3
            else:
                tags[a] = 1 + 4 * k
                for t in range(a + 1, b):
                    tags[t] = 1 + 4 * k + 1
                tags[b] = 1 + 4 * k + 2
        data.append((torch.tensor(feats), torch.tensor(tags)))
    return data, 1 + 4 * len(names)


class Model(nn.Module):
    def __init__(self, tags):
        super().__init__()
        self.embed = nn.Embedding(BUCKETS, DIM)
        self.project = nn.Linear(4 * DIM, WIDTH)
        self.window = nn.Linear((2 * CONTEXT + 1) * WIDTH, HIDDEN)
        self.out = nn.Linear(HIDDEN, tags)
        self.drop = nn.Dropout(DROPOUT)

    def forward(self, feats, lengths):
        x = F.relu(self.project(self.embed(feats).reshape(feats.shape[0], -1)))
        x = self.drop(x)
        pieces, start = [], 0
        for n in lengths:
            d = x[start:start + n]
            pad = F.pad(d, (0, 0, CONTEXT, CONTEXT))
            pieces.append(torch.cat([pad[o:o + n] for o in range(2 * CONTEXT + 1)], dim=1))
            start += n
        return self.out(F.relu(self.window(torch.cat(pieces))))


def main():
    path, epochs, threads = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
    seed = int(sys.argv[4]) if len(sys.argv) > 4 else 7
    torch.set_num_threads(threads)
    torch.manual_seed(seed)
    data, tags = load(path)
    model = Model(tags)
    opt = torch.optim.AdamW(model.parameters(), lr=0.003, weight_decay=0.01)
    g = torch.Generator().manual_seed(seed)
    times = []
    for epoch in range(epochs):
        order = torch.randperm(len(data), generator=g).tolist()
        batches, cur, words = [], [], 0
        for i in order:
            n = len(data[i][1])
            if cur and words + n > 1000:
                batches.append(cur)
                cur, words = [], 0
            cur.append(i)
            words += n
        if cur:
            batches.append(cur)
        model.train()
        t0 = time.perf_counter()
        total = 0.0
        for b in batches:
            feats = torch.cat([data[i][0] for i in b])
            gold = torch.cat([data[i][1] for i in b])
            logits = model(feats, [len(data[i][1]) for i in b])
            loss = F.cross_entropy(logits, gold)
            opt.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            opt.step()
            total += loss.item()
        times.append(time.perf_counter() - t0)
        print(f"epoch={epoch} steps={len(batches)} loss={total / len(batches):.4f} epoch_s={times[-1]:.2f}", flush=True)
    print(f"torch={torch.__version__} threads={threads} mean_epoch_s={sum(times) / len(times):.2f}")


if __name__ == "__main__":
    main()
