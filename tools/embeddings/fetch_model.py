#!/usr/bin/env python3
"""Fetches the published files of a sentence-transformers model repo at a pinned revision into DIR/<name>/ (tokenizer.json,
modules.json, pooling and length configs; weights are exported to ONNX separately by tools/accurate/export_onnx.py).

    fetch_model.py --repo BAAI/bge-small-en-v1.5 --revision 5c38ec7c... --out DIR --name bge-small-en-v1.5

Development tooling only; nothing here ships. huggingface_hub is used through its documented download API.
"""
import argparse, hashlib, json, os, shutil
from huggingface_hub import hf_hub_download

FILES = ["tokenizer.json", "modules.json", "1_Pooling/config.json", "sentence_bert_config.json", "config.json",
         "config_sentence_transformers.json", "README.md"]

ap = argparse.ArgumentParser()
ap.add_argument("--repo", required=True); ap.add_argument("--revision", required=True)
ap.add_argument("--out", required=True); ap.add_argument("--name", required=True)
a = ap.parse_args()
dest = os.path.join(a.out, a.name)
os.makedirs(os.path.join(dest, "1_Pooling"), exist_ok=True)
sums = {}
for f in FILES:
    try:
        p = hf_hub_download(a.repo, f, revision=a.revision)
    except Exception as e:  # optional files
        print("skip", f, type(e).__name__); continue
    shutil.copyfile(p, os.path.join(dest, f))
    sums[f] = hashlib.sha256(open(p, "rb").read()).hexdigest()
json.dump({"repo": a.repo, "revision": a.revision, "files": sums}, open(os.path.join(dest, "fetch.json"), "w"), indent=1)
print(json.dumps(sums, indent=1))
