#!/usr/bin/env python3
"""Embedding parity, comparison. Compares the reference (sentence-transformers) token ids and embeddings with ours.

    parity_compare.py PREFIX [--ours SUFFIX]   (reads PREFIX.ref.* and PREFIX.<ours>.ids.json / .f32; default ours = "ours")

Prints token-id identity, max abs diff, min and mean cosine, and the worst text indexes.
"""
import json, sys
import numpy as np

prefix = sys.argv[1]
ours = sys.argv[sys.argv.index("--ours") + 1] if "--ours" in sys.argv else "ours"
ref_ids = json.load(open(prefix + ".ref.ids.json")); our_ids = json.load(open(f"{prefix}.{ours}.ids.json"))
meta = json.load(open(prefix + ".ref.json")); d = meta["dim"]
ref = np.fromfile(prefix + ".ref.f32", dtype="<f4").reshape(-1, d)
our = np.fromfile(f"{prefix}.{ours}.f32", dtype="<f4").reshape(-1, d)
assert len(ref_ids) == len(our_ids) == len(ref) == len(our), (len(ref_ids), len(our_ids), len(ref), len(our))
mism = [i for i, (x, y) in enumerate(zip(ref_ids, our_ids)) if x != y]
diff = np.abs(ref - our)
cos = (ref * our).sum(1) / (np.linalg.norm(ref, axis=1) * np.linalg.norm(our, axis=1))
out = {"texts": len(ref), "token_id_mismatches": len(mism), "mismatch_examples": mism[:10],
       "max_abs_diff": float(diff.max()), "min_cosine": float(cos.min()), "mean_cosine": float(cos.mean()),
       "worst_cosine_idx": [int(i) for i in np.argsort(cos)[:5]],
       "sentence_transformers": meta["sentence_transformers"], "torch": meta["torch"],
       "longer_than_max": meta["longer_than_max"]}
print(json.dumps(out, indent=1))
