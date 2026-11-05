#!/usr/bin/env python3
"""Generate the tiny transformer-encoder fixture: a randomly initialised 2-layer BERT-style encoder exported to ONNX, a small
WordPiece tokenizer.json for it, and PyTorch's hidden states for fixed ids.

    make_tiny_fixture.py --out testdata/accurate [--seed 0]

Nothing here is trained or derived from a released model; the weights are random (seeded). The files are small enough to commit.
Needs torch, transformers and onnx (see setup_venv.sh).
"""
import argparse
import json
import os
import string

import torch
from transformers import BertConfig, BertModel

SPECIAL = ["[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]"]


def vocab():
    pieces = SPECIAL + list(string.ascii_lowercase) + list(string.digits)
    pieces += ["##" + c for c in string.ascii_lowercase]
    pieces += ["the", "a", "of", "and", "cat", "sat", "on", "mat", "dog", "ran", "##s", "##ing", "##ed", "un", "##able", ".", ",", "!", "?"]
    seen, out = set(), []
    for p in pieces:
        if p not in seen:
            seen.add(p)
            out.append(p)
    return out


def tokenizer_json(v):
    return {
        "version": "1.0", "truncation": None, "padding": None,
        "added_tokens": [{"id": i, "content": t, "single_word": False, "lstrip": False, "rstrip": False, "normalized": False, "special": True}
                         for i, t in enumerate(v) if t in SPECIAL],
        "normalizer": {"type": "BertNormalizer", "clean_text": True, "handle_chinese_chars": True, "strip_accents": None, "lowercase": True},
        "pre_tokenizer": {"type": "BertPreTokenizer"},
        "post_processor": {
            "type": "TemplateProcessing",
            "single": [{"SpecialToken": {"id": "[CLS]", "type_id": 0}}, {"Sequence": {"id": "A", "type_id": 0}}, {"SpecialToken": {"id": "[SEP]", "type_id": 0}}],
            "pair": [{"SpecialToken": {"id": "[CLS]", "type_id": 0}}, {"Sequence": {"id": "A", "type_id": 0}}, {"SpecialToken": {"id": "[SEP]", "type_id": 0}},
                     {"Sequence": {"id": "B", "type_id": 1}}, {"SpecialToken": {"id": "[SEP]", "type_id": 1}}],
            "special_tokens": {"[CLS]": {"id": "[CLS]", "ids": [v.index("[CLS]")], "tokens": ["[CLS]"]},
                               "[SEP]": {"id": "[SEP]", "ids": [v.index("[SEP]")], "tokens": ["[SEP]"]}},
        },
        "decoder": {"type": "WordPiece", "prefix": "##", "cleanup": True},
        "model": {"type": "WordPiece", "unk_token": "[UNK]", "continuing_subword_prefix": "##", "max_input_chars_per_word": 100,
                  "vocab": {t: i for i, t in enumerate(v)}},
    }


class Encoder(torch.nn.Module):
    def __init__(self, m):
        super().__init__()
        self.m = m

    def forward(self, input_ids, attention_mask):
        return self.m(input_ids=input_ids, attention_mask=attention_mask).last_hidden_state


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    torch.manual_seed(a.seed)
    v = vocab()
    cfg = BertConfig(vocab_size=len(v), hidden_size=16, num_hidden_layers=2, num_attention_heads=2, intermediate_size=32,
                     max_position_embeddings=64, type_vocab_size=1, hidden_act="gelu", attn_implementation="eager")
    enc = Encoder(BertModel(cfg, add_pooling_layer=False)).eval()
    ids = torch.tensor([[2, 9, 10, 11, 3, 0, 0], [2, 40, 41, 42, 43, 44, 3]])
    mask = (ids != 0).long()
    with torch.no_grad():
        ref = enc(ids, mask)
        torch.onnx.export(enc, (ids, mask), os.path.join(a.out, "tiny-bert.onnx"), input_names=["input_ids", "attention_mask"],
                          output_names=["last_hidden_state"],
                          dynamic_axes={"input_ids": {0: "batch", 1: "sequence"}, "attention_mask": {0: "batch", 1: "sequence"},
                                        "last_hidden_state": {0: "batch", 1: "sequence"}},
                          opset_version=17, dynamo=False)
    with open(os.path.join(a.out, "tiny-bert.tokenizer.json"), "w") as f:
        json.dump(tokenizer_json(v), f)
    with open(os.path.join(a.out, "tiny-bert.reference.json"), "w") as f:
        json.dump({"input_ids": ids.tolist(), "attention_mask": mask.tolist(), "hidden": ref.tolist(), "torch": torch.__version__}, f)
    print("vocab", len(v), "bytes", os.path.getsize(os.path.join(a.out, "tiny-bert.onnx")))


if __name__ == "__main__":
    main()
