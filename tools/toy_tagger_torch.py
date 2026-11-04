#!/usr/bin/env python3
"""The toy tagger of tools/OneNlp.Toy in plain PyTorch on CPU, for a first timing comparison.

    toy_tagger_torch.py <train.conllu> <dev.conllu> [epochs] [threads]

Same data, architecture (hash embedding 16384 x 64, linear + ReLU, dropout 0.1, linear to the tag set), optimizer (AdamW 0.003,
weight decay 0.01), gradient clipping (norm 1), batch size (about 1000 words per batch from shuffled sentences) and a dev-loss
evaluation after every epoch, as the C# run. The initial weights and the shuffles are PyTorch's own, so the two runs are
comparable in cost, not bit for bit. Prints key=value lines.
"""
import sys
import time
import random

import torch
import torch.nn as nn

BUCKETS, WIDTH, BATCH_WORDS = 16384, 64, 1000


def fnv1a(word: str) -> int:
    h = 14695981039346656037
    for unit in word.lower().encode("utf-16-le")[::2]:  # toy words are ASCII: one UTF-16 unit per character
        h = ((h ^ unit) * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return h % BUCKETS


def read(path):
    sentences, current = [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line:
                if current:
                    sentences.append(current)
                    current = []
            elif not line.startswith("#"):
                cols = line.split("\t")
                current.append((fnv1a(cols[1]), cols[4]))
    if current:
        sentences.append(current)
    return sentences


def batches(sentences, rng):
    order = list(range(len(sentences)))
    rng.shuffle(order)
    batch, words = [], 0
    for i in order:
        if batch and words + len(sentences[i]) > BATCH_WORDS:
            yield batch
            batch, words = [], 0
        batch.append(i)
        words += len(sentences[i])
    if batch:
        yield batch


def tensors(sentences, idx, tag_ids):
    ids = torch.tensor([w for i in idx for (w, _) in sentences[i]], dtype=torch.long)
    tags = torch.tensor([tag_ids[t] for i in idx for (_, t) in sentences[i]], dtype=torch.long)
    return ids, tags


def main():
    train, dev = read(sys.argv[1]), read(sys.argv[2])
    epochs = int(sys.argv[3]) if len(sys.argv) > 3 else 10
    torch.set_num_threads(int(sys.argv[4]) if len(sys.argv) > 4 else 1)
    torch.manual_seed(11)
    rng = random.Random(11)
    tag_ids = {t: i for i, t in enumerate(sorted({t for s in train for (_, t) in s}))}
    model = nn.Sequential(nn.Embedding(BUCKETS, WIDTH), nn.Linear(WIDTH, WIDTH), nn.ReLU(), nn.Dropout(0.1), nn.Linear(WIDTH, len(tag_ids)))
    opt = torch.optim.AdamW(model.parameters(), lr=0.003, weight_decay=0.01)
    loss_fn = nn.CrossEntropyLoss()
    steps = 0
    start = time.perf_counter()
    for epoch in range(epochs):
        model.train()
        for idx in batches(train, rng):
            ids, tags = tensors(train, idx, tag_ids)
            loss = loss_fn(model(ids), tags)
            opt.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            opt.step()
            steps += 1
        model.eval()
        with torch.no_grad():
            total = 0.0
            n = 0
            for i in range(0, len(dev), 32):
                ids, tags = tensors(dev, range(i, min(i + 32, len(dev))), tag_ids)
                total += loss_fn(model(ids), tags).item()
                n += 1
        print(f"epoch={epoch} dev_loss={total / n:.4f} elapsed_s={time.perf_counter() - start:.2f}", flush=True)
    wall = time.perf_counter() - start
    print(f"steps={steps} train_wall_s={wall:.2f} per_epoch_s={wall / epochs:.3f} threads={torch.get_num_threads()} torch={torch.__version__}")


if __name__ == "__main__":
    main()
