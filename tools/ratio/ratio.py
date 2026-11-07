#!/usr/bin/env python3
"""Training-time comparator for the text-category heads: the same data, architecture, batch size and thread count as the C# recipes, in plain PyTorch on CPU.

usage: ratio.py textcat|multilabel|spancat <train.jsonl> --labels N [--threads 4] [--epochs 2] [--batch-words 2000]

The network is the fast tier as the C# recipes build it: four 32,768-row x 64 hash-embedding tables (norm, prefix, suffix, shape; one hash each),
joined and mixed to 256 with GELU and layer norm, two residual window layers (x + LayerNorm(GELU(Linear([x[t-1], x[t], x[t+1]])))), dropout
0.2, AdamW at 0.003, batches cut by word count after a shuffle. Heads: mean-pooled linear (textcat: cross entropy; multilabel: BCE) or, for
spancat, every span of 1 to 3 tokens as [mean; first; last] -> Linear(768, 256) -> ReLU -> Linear(256, labels) with BCE. Token ids are hashed once
before the clock starts, so the timing covers the forward pass, backward pass and optimizer step, as the C# epoch time does.
Prints the wall time of each epoch. Run only in the dev tooling environment (torch CPU); nothing here ships.
"""
import argparse
import json
import random
import re
import time
import zlib

import torch
from torch import nn

ROWS = 32768
TOKEN = re.compile(r"\w+|[^\w\s]", re.UNICODE)


def shape(w):
    out, last, run = [], "", 0
    for ch in w:
        c = "X" if ch.isupper() else "x" if ch.islower() else "d" if ch.isdigit() else ch
        run = run + 1 if c == last else 1
        last = c
        if run <= 4:
            out.append(c)
    return "".join(out)


def ids(word):
    h = lambda s, salt: zlib.crc32((salt + s).encode("utf-8")) % ROWS
    return [h(word.lower(), "n"), h(word[:3], "p"), h(word[-3:], "s"), h(shape(word), "h")]


def load(path, mode, labels_cap):
    docs, labels = [], {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            j = json.loads(line)
            text = j["text"]
            if mode == "spancat":
                words = [text[s:e] for s, e in j["tokens"]]
                gold = [(a, b, l) for a, b, l in j["spans"]["sc"]]
                starts = {s: i for i, (s, _) in enumerate(j["tokens"])}
                ends = {e: i for i, (_, e) in enumerate(j["tokens"])}
                spans = {}
                for a, b, l in gold:
                    if a in starts and b in ends:
                        spans[(starts[a], ends[b] + 1)] = labels.setdefault(l, len(labels))
                docs.append((torch.tensor([ids(w) for w in words]), spans))
            else:
                words = TOKEN.findall(text)[:2000] or ["."]
                cats = j["cats"]
                for k in cats:
                    labels.setdefault(k, len(labels))
                docs.append((torch.tensor([ids(w) for w in words]), {k: v for k, v in cats.items()}))
    return docs, labels


class Net(nn.Module):
    def __init__(self, mode, n_labels):
        super().__init__()
        self.mode = mode
        self.tables = nn.ModuleList(nn.Embedding(ROWS, 64) for _ in range(4))
        self.join = nn.Linear(256, 256)
        self.join_norm = nn.LayerNorm(256)
        self.windows = nn.ModuleList(nn.Linear(768, 256) for _ in range(2))
        self.norms = nn.ModuleList(nn.LayerNorm(256) for _ in range(2))
        self.drop = nn.Dropout(0.2)
        if mode == "spancat":
            self.hidden = nn.Linear(768, 256)
            self.out = nn.Linear(256, n_labels)
        else:
            self.out = nn.Linear(256, n_labels)

    def encode(self, ids, lengths):
        x = torch.cat([t(ids[:, i]) for i, t in enumerate(self.tables)], 1)
        x = self.join_norm(nn.functional.gelu(self.join(x)))
        # windows never cross a document edge: zero rows between documents
        offsets = torch.cumsum(torch.tensor([0] + lengths), 0)
        for lin, norm in zip(self.windows, self.norms):
            prev = torch.zeros_like(x)
            nxt = torch.zeros_like(x)
            prev[1:] = x[:-1]
            nxt[:-1] = x[1:]
            prev[offsets[:-1]] = 0
            nxt[offsets[1:] - 1] = 0
            x = x + norm(nn.functional.gelu(lin(torch.cat([prev, x, nxt], 1))))
        return x, offsets

    def forward(self, ids, lengths, spans=None):
        x, offsets = self.encode(ids, lengths)
        if self.mode != "spancat":
            seg = torch.repeat_interleave(torch.arange(len(lengths)), torch.tensor(lengths))
            pooled = torch.zeros(len(lengths), 256).index_add(0, seg, x) / torch.tensor(lengths, dtype=torch.float32).unsqueeze(1)
            return self.out(self.drop(pooled))
        csum = torch.cat([torch.zeros(1, 256), torch.cumsum(x, 0)])
        a, b = spans
        size = (b - a).unsqueeze(1).float()
        reps = torch.cat([(csum[b] - csum[a]) / size, x[a], x[b - 1]], 1)
        return self.out(self.drop(torch.relu(self.hidden(reps))))


def prep_spans(n, gold):
    """Every span of 1 to 3 tokens: start, end and the gold label index (-1 for none), built once before the clock starts."""
    a, b, y = [], [], []
    for size in (1, 2, 3):
        for st in range(0, n - size + 1):
            a.append(st)
            b.append(st + size)
            y.append(gold.get((st, st + size), -1))
    return torch.tensor(a), torch.tensor(b), torch.tensor(y)


def batches(docs, batch_words, rng):
    order = list(range(len(docs)))
    rng.shuffle(order)
    cur, words = [], 0
    for i in order:
        cur.append(i)
        words += len(docs[i][0])
        if words >= batch_words:
            yield cur
            cur, words = [], 0
    if cur:
        yield cur


def main():
    p = argparse.ArgumentParser()
    p.add_argument("mode", choices=["textcat", "multilabel", "spancat"])
    p.add_argument("train")
    p.add_argument("--threads", type=int, default=4)
    p.add_argument("--epochs", type=int, default=2)
    p.add_argument("--batch-words", type=int, default=2000)
    a = p.parse_args()
    torch.set_num_threads(a.threads)
    torch.manual_seed(7)
    rng = random.Random(7)
    docs, labels = load(a.train, a.mode, None)
    if a.mode == "spancat":
        docs = [(ids_, prep_spans(len(ids_), sp)) for ids_, sp in docs]
    net = Net(a.mode, len(labels))
    opt = torch.optim.AdamW(net.parameters(), lr=0.003)
    n_tokens = sum(len(d[0]) for d in docs)
    for epoch in range(a.epochs):
        net.train()
        start, steps, total = time.perf_counter(), 0, 0.0
        for batch in batches(docs, a.batch_words, rng):
            lengths = [len(docs[i][0]) for i in batch]
            ids_ = torch.cat([docs[i][0] for i in batch])
            if a.mode == "spancat":
                base = torch.cumsum(torch.tensor([0] + lengths[:-1]), 0)
                sa = torch.cat([docs[i][1][0] + base[k] for k, i in enumerate(batch)])
                sb = torch.cat([docs[i][1][1] + base[k] for k, i in enumerate(batch)])
                sy = torch.cat([docs[i][1][2] for i in batch])
                logits = net(ids_, lengths, (sa, sb))
                y = torch.zeros_like(logits)
                hit = sy >= 0
                y[hit.nonzero().squeeze(1), sy[hit]] = 1
                loss = nn.functional.binary_cross_entropy_with_logits(logits, y)
            else:
                logits = net(ids_, lengths)
                if a.mode == "textcat":
                    y = torch.tensor([labels[max(docs[i][1], key=docs[i][1].get)] for i in batch])
                    loss = nn.functional.cross_entropy(logits, y)
                else:
                    y = torch.tensor([[float(docs[i][1].get(k, 0)) for k in labels] for i in batch])
                    loss = nn.functional.binary_cross_entropy_with_logits(logits, y)
            opt.zero_grad()
            loss.backward()
            opt.step()
            steps += 1
            total += float(loss)
        print(json.dumps({"mode": a.mode, "epoch": epoch + 1, "seconds": round(time.perf_counter() - start, 2), "steps": steps, "documents": len(docs),
                          "tokens": n_tokens, "mean_loss": round(total / steps, 4), "threads": a.threads, "torch": torch.__version__}), flush=True)


main()
