#!/usr/bin/env python3
"""Export a Hugging Face encoder to ONNX for OneModel: (input_ids, attention_mask) -> last_hidden_state.

Development tooling only; nothing here ships. PyTorch and `transformers` are used through their documented
APIs (AutoModel.from_pretrained, torch.onnx.export) and never read.

    export_onnx.py --repo jhu-clsp/mmBERT-base --out DIR [--opset 17] [--tiny]

`--tiny` builds a randomly initialised 2-layer model of the same architecture from the repo's config (config only,
no weights downloaded) and is how test fixtures are generated.

Writes DIR/<name>.onnx (+ .onnx.data when the weights are large) and DIR/<name>.export.json with the repo
revision, versions, opset, file sizes and SHA-256 hashes.
"""
import argparse
import hashlib
import json
import os
import sys

import torch
import transformers
from transformers import AutoConfig, AutoModel


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


class Encoder(torch.nn.Module):
    """The graph we export: token ids and a 0/1 attention mask in, last hidden states out."""

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, input_ids, attention_mask):
        return self.model(input_ids=input_ids, attention_mask=attention_mask).last_hidden_state


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--name", default=None)
    ap.add_argument("--opset", type=int, default=17)
    ap.add_argument("--revision", default=None)
    ap.add_argument("--tiny", action="store_true")
    ap.add_argument("--attn", default="eager", help="attention implementation to trace; eager gives plain MatMul/Softmax")
    ap.add_argument("--seed", type=int, default=0)
    args = ap.parse_args()

    name = args.name or args.repo.split("/")[-1].lower() + ("-tiny" if args.tiny else "")
    os.makedirs(args.out, exist_ok=True)
    torch.manual_seed(args.seed)

    revision = args.revision
    if args.tiny:
        cfg = AutoConfig.from_pretrained(args.repo, revision=revision)
        cfg.num_hidden_layers = 2
        cfg.hidden_size = 64
        cfg.intermediate_size = 128
        cfg.num_attention_heads = 4
        cfg.vocab_size = 512
        for k in ("pad_token_id", "bos_token_id", "eos_token_id", "cls_token_id", "sep_token_id"):
            if getattr(cfg, k, None) is not None and getattr(cfg, k) >= 512:
                setattr(cfg, k, 1)
        cfg._attn_implementation = args.attn
        model = AutoModel.from_config(cfg, attn_implementation=args.attn)
    else:
        model = AutoModel.from_pretrained(args.repo, revision=revision, attn_implementation=args.attn, torch_dtype=torch.float32)
    model.eval()

    sha = None
    try:
        from huggingface_hub import HfApi
        sha = HfApi().model_info(args.repo, revision=revision).sha
    except Exception as e:  # offline or private: record that we could not
        sha = f"unknown ({type(e).__name__})"

    enc = Encoder(model).eval()
    vocab = model.config.vocab_size
    ids = torch.randint(5, min(vocab, 1000), (2, 11), dtype=torch.long)
    mask = torch.ones_like(ids)
    mask[1, 8:] = 0
    path = os.path.join(args.out, name + ".onnx")
    with torch.no_grad():
        ref = enc(ids, mask)
        torch.onnx.export(
            enc, (ids, mask), path,
            input_names=["input_ids", "attention_mask"], output_names=["last_hidden_state"],
            dynamic_axes={"input_ids": {0: "batch", 1: "sequence"}, "attention_mask": {0: "batch", 1: "sequence"},
                          "last_hidden_state": {0: "batch", 1: "sequence"}},
            opset_version=args.opset, do_constant_folding=True, dynamo=False)

    files = {}
    for fn in sorted(os.listdir(args.out)):
        if fn.startswith(name + ".onnx"):
            p = os.path.join(args.out, fn)
            files[fn] = {"bytes": os.path.getsize(p), "sha256": sha256(p)}
    info = {
        "repo": args.repo, "revision_requested": revision, "revision_sha": sha, "tiny": args.tiny,
        "opset": args.opset, "attn_implementation": args.attn,
        "torch": torch.__version__, "transformers": transformers.__version__,
        "hidden_size": model.config.hidden_size, "layers": model.config.num_hidden_layers,
        "vocab_size": vocab, "files": files,
        "probe": {"input_ids": ids.tolist(), "attention_mask": mask.tolist(), "last_hidden_state_sum": float(ref.sum())},
    }
    with open(os.path.join(args.out, name + ".export.json"), "w") as f:
        json.dump(info, f, indent=1)
    print(json.dumps({k: info[k] for k in ("repo", "revision_sha", "opset", "files")}, indent=1))


if __name__ == "__main__":
    sys.exit(main())
