#!/usr/bin/env python3
"""The English parser recipe in plain PyTorch on CPU, for comparing training time per epoch with the C# recipe.

usage: parser_torch.py TRAIN.conllu [--epochs 3] [--threads 8] [--seed 7] [--sentences-per-doc 10] [--batch-words 1500]

Same data grouping (ten sentences per document), same architecture and sizes as recipes/en/parser (one hashed token embedding with
a linear layer and ReLU, window layers, a bidirectional LSTM, biaffine arcs and labels, sentence-start classifier), same batching by words, AdamW at
0.002, dropout 0.25 on the encoder output. It only trains and reports seconds per epoch; it does not decode or score.
"""
import argparse
import random
import time

import torch
import torch.nn as nn
import torch.nn.functional as F


def fnv1a(text):
    h = 14695981039346656037
    data = text.encode("utf-16-le")
    for i in range(0, len(data), 2):
        h = ((h ^ (data[i] | (data[i + 1] << 8))) * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return h


def read(path, per_doc):
    sentences, rows = [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("#"):
                continue
            if not line:
                if rows:
                    sentences.append(rows)
                rows = []
                continue
            c = line.split("\t")
            if "-" in c[0] or "." in c[0]:
                continue
            rows.append(c)
    if rows:
        sentences.append(rows)
    docs = []
    for k in range(0, len(sentences), per_doc):
        words, heads, deps, starts, spans = [], [], [], [], []
        for s in sentences[k:k + per_doc]:
            base = len(words)
            spans.append((base, base + len(s)))
            for i, c in enumerate(s):
                words.append(c[1])
                heads.append(-1 if c[6] == "0" else base + int(c[6]) - 1)
                deps.append(c[7])
                starts.append(1 if i == 0 else 0)
        docs.append((words, heads, deps, starts, spans))
    return docs


class Parser(nn.Module):
    def __init__(self, labels, width=128, buckets=32768, arc=256, rel=128, context=0, lstm=192, lstm_layers=2):
        super().__init__()
        self.labels, self.rel = labels, rel
        self.buckets = buckets
        self.embed = nn.Embedding(buckets, width)
        self.project = nn.Linear(width, width)
        self.window = nn.ModuleList([nn.Linear(3 * width, width) for _ in range(context)])
        self.lstm = nn.LSTM(width, lstm, num_layers=lstm_layers, bidirectional=True) if lstm else None
        top = 2 * lstm if lstm else width
        self.arc_dep, self.arc_head = nn.Linear(top, arc), nn.Linear(top, arc)
        self.arc_bilinear = nn.Linear(arc, arc, bias=False)
        self.arc_head_bias = nn.Linear(arc, 1)
        self.rel_dep, self.rel_head = nn.Linear(top, rel), nn.Linear(top, rel)
        self.rel_bilinear = nn.Linear(rel, labels * rel, bias=False)
        self.rel_pair = nn.Linear(2 * rel, labels)
        self.boundary = nn.Linear(top, 2)
        self.root_arc = nn.Parameter(torch.randn(1, arc))
        self.root_rel = nn.Parameter(torch.randn(1, rel))
        self.dropout = nn.Dropout(0.25)

    def forward(self, ids, doc_bounds, sentences, rel_dep, rel_head, rel_target, boundary_target):
        x = F.relu(self.project(self.embed(ids)))
        x = self.dropout(x)
        n = x.shape[0]
        prev = torch.full((n,), n, dtype=torch.long)
        nxt = torch.full((n,), n, dtype=torch.long)
        for a, b in doc_bounds:
            prev[a + 1:b] = torch.arange(a, b - 1)
            nxt[a:b - 1] = torch.arange(a + 1, b)
        zero = torch.zeros(1, x.shape[1])
        for layer in self.window:
            padded = torch.cat([x, zero], 0)
            x = x + F.relu(layer(torch.cat([padded[prev], x, padded[nxt]], 1)))
        if self.lstm is not None:
            seqs = [x[a:b] for a, b in doc_bounds]
            packed = nn.utils.rnn.pack_sequence(seqs, enforce_sorted=False)
            out, _ = self.lstm(packed)
            out, lens = nn.utils.rnn.pad_packed_sequence(out)
            x = torch.cat([out[:int(l), i] for i, l in enumerate(lens)], 0)
        loss = F.cross_entropy(self.boundary(x), boundary_target, reduction="sum")
        ad, ah = F.relu(self.arc_dep(x)), F.relu(self.arc_head(x))
        rd, rh = F.relu(self.rel_dep(x)), F.relu(self.rel_head(x))
        for a, b, heads in sentences:
            m = b - a
            head = torch.cat([self.root_arc, ah[a:b]], 0)
            s = self.arc_bilinear(ad[a:b]) @ head.T + self.arc_head_bias(head).view(1, m + 1)
            mask = torch.zeros(m, m + 1)
            mask[torch.arange(m), torch.arange(m) + 1] = -1e4
            s = s + mask
            loss = loss + F.cross_entropy(s, heads, reduction="sum")
        d = rd[rel_dep]
        h = torch.cat([rh, self.root_rel], 0)[rel_head]
        logits = (self.rel_bilinear(d).view(-1, self.labels, self.rel) * h.unsqueeze(1)).sum(2) + self.rel_pair(torch.cat([d, h], 1))
        loss = loss + F.cross_entropy(logits, rel_target, reduction="sum")
        return loss / n


def batches(docs, batch_words, rng):
    order = list(range(len(docs)))
    rng.shuffle(order)
    cur, words = [], 0
    for i in order:
        if cur and words + len(docs[i][0]) > batch_words:
            yield cur
            cur, words = [], 0
        cur.append(i)
        words += len(docs[i][0])
    if cur:
        yield cur


def main():
    p = argparse.ArgumentParser()
    p.add_argument("train")
    p.add_argument("--epochs", type=int, default=3)
    p.add_argument("--threads", type=int, default=8)
    p.add_argument("--seed", type=int, default=7)
    p.add_argument("--sentences-per-doc", type=int, default=10)
    p.add_argument("--batch-words", type=int, default=1500)
    p.add_argument("--context", type=int, default=0)
    p.add_argument("--lstm", type=int, default=192)
    p.add_argument("--lstm-layers", type=int, default=2)
    a = p.parse_args()
    torch.set_num_threads(a.threads)
    torch.manual_seed(a.seed)
    rng = random.Random(a.seed)
    docs = read(a.train, a.sentences_per_doc)
    labels = sorted({d for doc in docs for d in doc[2]})
    lid = {l: i for i, l in enumerate(labels)}
    model = Parser(len(labels), context=a.context, lstm=a.lstm, lstm_layers=a.lstm_layers)
    opt = torch.optim.AdamW(model.parameters(), lr=0.002, weight_decay=0.01)
    for epoch in range(a.epochs):
        model.train()
        start = time.perf_counter()
        steps = 0
        for batch in batches(docs, a.batch_words, rng):
            ids, bounds, sents, rd, rh, rt, bt = [], [], [], [], [], [], []
            for i in batch:
                words, heads, deps, starts, spans = docs[i]
                off = len(ids)
                ids += [fnv1a(w.lower()) % model.buckets for w in words]
                bounds.append((off, off + len(words)))
                bt += starts
                for sa, sb in spans:
                    tgt = [0 if heads[t] < 0 else heads[t] - sa + 1 for t in range(sa, sb)]
                    sents.append((off + sa, off + sb, torch.tensor(tgt)))
                    for t in range(sa, sb):
                        rd.append(off + t)
                        rh.append(-1 if heads[t] < 0 else off + heads[t])
                        rt.append(lid[deps[t]])
            n = len(ids)
            rh = [n if h < 0 else h for h in rh]
            loss = model(torch.tensor(ids), bounds, sents, torch.tensor(rd), torch.tensor(rh), torch.tensor(rt), torch.tensor(bt))
            opt.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            opt.step()
            steps += 1
        print(f"epoch={epoch} steps={steps} seconds={time.perf_counter() - start:.2f} last_loss={loss.item():.3f} threads={a.threads}")


main()
