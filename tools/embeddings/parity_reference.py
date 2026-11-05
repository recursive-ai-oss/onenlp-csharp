#!/usr/bin/env python3
"""Embedding parity, reference side. Selects a deterministic text sample from the parity corpus, embeds it with the pinned
sentence-transformers through its documented API, and writes the texts, the token ids the model sees and the embeddings.

    parity_reference.py --corpus DIR --repo ID --revision SHA --out PREFIX [--prefix TEXT] [--max-texts N]

Outputs PREFIX.texts.jsonl (one JSON string per line, prefix NOT applied), PREFIX.ref.ids.json, PREFIX.ref.f32, PREFIX.ref.json.
The C# side (OneNlp.EmbedTool run) reads the texts and applies its own prefix; compare.py then compares.
Black-box use only: model.tokenize / model.encode are documented public methods.
"""
import argparse, json, os, random, collections
import numpy as np
import sentence_transformers, torch

ap = argparse.ArgumentParser()
ap.add_argument("--corpus", required=True); ap.add_argument("--repo", required=True); ap.add_argument("--revision", required=True)
ap.add_argument("--out", required=True); ap.add_argument("--prefix", default=""); ap.add_argument("--max-texts", type=int, default=2400)
ap.add_argument("--threads", type=int, default=4)
a = ap.parse_args()
torch.set_num_threads(a.threads)

# Sample: every synthetic class (up to 60 each, valid text only), the long UDHR documents (past the 512-token limit), then
# UDHR paragraphs and Tatoeba sentences across all languages.
rng = random.Random(20260105)
by = collections.defaultdict(list)
for line in open(os.path.join(a.corpus, "records.jsonl"), encoding="utf-8"):
    r = json.loads(line)
    if "text" in r:
        by[r["class"]].append(r["text"])
quota = {"udhr-full": 60, "udhr-para": 700, "tatoeba": 700}
texts = []
for cls in sorted(by):
    items = by[cls]
    n = quota.get(cls, 60)
    texts += rng.sample(items, min(n, len(items)))
texts = texts[: a.max_texts] if len(texts) > a.max_texts else texts
with open(a.out + ".texts.jsonl", "w", encoding="utf-8") as f:
    for t in texts:
        f.write(json.dumps(t, ensure_ascii=False) + "\n")

model = sentence_transformers.SentenceTransformer(a.repo, revision=a.revision, device="cpu")
inputs = [a.prefix + t for t in texts]
ids = []
for i in range(0, len(inputs), 64):
    enc = model.tokenize(inputs[i:i + 64])
    for row, m in zip(enc["input_ids"].tolist(), enc["attention_mask"].tolist()):
        ids.append([x for x, k in zip(row, m) if k])
emb = model.encode(inputs, batch_size=32, convert_to_numpy=True, show_progress_bar=False).astype("<f4")
json.dump(ids, open(a.out + ".ref.ids.json", "w"))
emb.tofile(a.out + ".ref.f32")
json.dump({"repo": a.repo, "revision": a.revision, "texts": len(texts), "dim": int(emb.shape[1]),
           "max_seq_length": model.max_seq_length, "sentence_transformers": sentence_transformers.__version__,
           "torch": torch.__version__, "classes": {k: min(quota.get(k, 60), len(v)) for k, v in by.items()},
           "longer_than_max": sum(1 for x in ids if len(x) >= model.max_seq_length)},
          open(a.out + ".ref.json", "w"), indent=1)
print(open(a.out + ".ref.json").read())
