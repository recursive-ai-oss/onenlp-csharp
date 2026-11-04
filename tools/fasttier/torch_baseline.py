#!/usr/bin/env python3
"""Times the English fast-tier training recipe in plain PyTorch on CPU, for comparison with the C# recipe.

The model is the same shape as the C# FastEncoder with a tagger, a morphologizer and a lemmatizer head on top: four hash-embedding
tables (two hashes per token, summed), a linear mix with GELU and layer norm, four residual window layers (a linear layer over
the previous, current and next vector, GELU, layer norm), and three linear softmax heads. Same data, batches by words, AdamW,
dropout, thread count and epochs. Feature ids are precomputed once with crc32 (not the C# hash; only the distribution matters for
timing) and are not part of the timed region. The lemma classes are a simple suffix-rewrite stand-in with a class count of the
same order as the edit trees.

usage: torch_baseline.py <train.conllu> --threads N --epochs N [--batch-words 1000] [--seed 1] [--heads all|tagger|morphologizer|lemmatizer]
"""
import argparse
import random
import time
import zlib

import torch
import torch.nn as nn
import torch.nn.functional as F

WIDTH, DEPTH = 96, 4
ROWS = [20000, 2000, 5000, 2500]


def read(path):
    sents, cur = [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line:
                if cur:
                    sents.append(cur)
                    cur = []
            elif not line.startswith("#"):
                c = line.split("\t")
                if c[0].isdigit():
                    cur.append(c)
    if cur:
        sents.append(cur)
    return sents


def shape(w):
    out, last, run = [], "", 0
    for ch in w:
        s = "X" if ch.isupper() else "x" if ch.islower() else "d" if ch.isdigit() else ch
        run = run + 1 if s == last else 1
        last = s
        if run <= 4:
            out.append(s)
    return "".join(out)


def h(s, a, rows):
    x = zlib.crc32(s.encode(), a)
    y = zlib.crc32(s.encode(), a + 17)
    return x % rows, y % rows


def lemma_class(form, lemma):
    f = form.lower()
    i = 0
    while i < min(len(f), len(lemma)) and f[i] == lemma[i]:
        i += 1
    return (len(f) - i, lemma[i:])


class Net(nn.Module):
    def __init__(self, n_tag, n_morph, n_lemma, heads="all"):
        super().__init__()
        self.heads = heads
        self.emb = nn.ModuleList([nn.Embedding(r, WIDTH) for r in ROWS])
        self.mix = nn.Linear(4 * WIDTH, WIDTH)
        self.mix_norm = nn.LayerNorm(WIDTH)
        self.win = nn.ModuleList([nn.Linear(3 * WIDTH, WIDTH) for _ in range(DEPTH)])
        self.norm = nn.ModuleList([nn.LayerNorm(WIDTH) for _ in range(DEPTH)])
        self.tag = nn.Linear(WIDTH, n_tag)
        self.morph = nn.Linear(WIDTH, n_morph)
        self.lemma = nn.Linear(WIDTH, n_lemma)
        self.drop = nn.Dropout(0.1)

    def forward(self, ids):  # ids: [B, L, 4, 2]
        parts = [self.emb[a](ids[:, :, a, 0]) + self.emb[a](ids[:, :, a, 1]) for a in range(4)]
        x = self.mix_norm(F.gelu(self.mix(torch.cat(parts, -1))))
        for lin, nrm in zip(self.win, self.norm):
            p = F.pad(x, (0, 0, 1, 1))
            w = torch.cat([p[:, :-2], x, p[:, 2:]], -1)
            x = x + nrm(F.gelu(lin(w)))
        x = self.drop(x)
        use = lambda name: self.heads in ("all", name)
        return (self.tag(x) if use("tagger") else None, self.morph(x) if use("morphologizer") else None,
                self.lemma(x) if use("lemmatizer") else None)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("train")
    ap.add_argument("--threads", type=int, default=8)
    ap.add_argument("--epochs", type=int, default=2)
    ap.add_argument("--batch-words", type=int, default=1000)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--heads", default="all", choices=["all", "tagger", "morphologizer", "lemmatizer"])
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    torch.manual_seed(a.seed)
    rnd = random.Random(a.seed)
    sents = read(a.train)
    tags = sorted({c[4] for s in sents for c in s})
    morphs = sorted({f"{c[3]}|{c[5]}" for s in sents for c in s})
    lemmas = sorted({lemma_class(c[1], c[2]) for s in sents for c in s})
    ti, mi, li = ({t: i for i, t in enumerate(v)} for v in (tags, morphs, lemmas))
    data = []
    for s in sents:
        ids = torch.tensor([[[*h(c[1].lower(), 1, ROWS[0])], [*h(c[1][:1], 2, ROWS[1])], [*h(c[1][-3:], 3, ROWS[2])], [*h(shape(c[1]), 4, ROWS[3])]] for c in s])
        y = torch.tensor([[ti[c[4]], mi[f"{c[3]}|{c[5]}"], li[lemma_class(c[1], c[2])]] for c in s])
        data.append((ids, y))
    print(f"sentences={len(data)} tokens={sum(len(d[1]) for d in data)} tags={len(tags)} morph={len(morphs)} lemma_classes={len(lemmas)}")
    net = Net(len(tags), len(morphs), len(lemmas), a.heads)
    opt = torch.optim.AdamW(net.parameters(), lr=0.002, weight_decay=0.01)
    for epoch in range(a.epochs):
        order = list(range(len(data)))
        rnd.shuffle(order)
        batches, cur, words = [], [], 0
        for i in order:
            n = len(data[i][1])
            if cur and words + n > a.batch_words:
                batches.append(cur)
                cur, words = [], 0
            cur.append(i)
            words += n
        if cur:
            batches.append(cur)
        net.train()
        t0 = time.perf_counter()
        total = 0.0
        for b in batches:  # padding to the longest sentence of the batch is part of the timed work
            L = max(len(data[i][1]) for i in b)
            ids = torch.zeros(len(b), L, 4, 2, dtype=torch.long)
            y = torch.full((len(b), L, 3), -100, dtype=torch.long)
            for k, i in enumerate(b):
                n = len(data[i][1])
                ids[k, :n] = data[i][0]
                y[k, :n] = data[i][1]
            lt, lm, ll = net(ids)
            loss = sum(F.cross_entropy(z.reshape(-1, z.shape[-1]), y[..., k].reshape(-1))
                       for k, z in enumerate((lt, lm, ll)) if z is not None)
            opt.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(net.parameters(), 1.0)
            opt.step()
            total += loss.item()
        dt = time.perf_counter() - t0
        print(f"epoch={epoch} steps={len(batches)} loss={total / len(batches):.3f} seconds={dt:.1f} heads={a.heads} threads={a.threads} torch={torch.__version__}")


main()
