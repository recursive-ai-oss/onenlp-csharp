"""The student encoder: a small BERT-style transformer in plain PyTorch.

Written from the papers: Vaswani et al. 2017 §3 (post-norm encoder layer, learned absolute positions as in Devlin et al.
2019 §3), and the factorised embedding of Lan et al. 2020 (ALBERT) §3.1: the token table has a small width `rank` and a
projection lifts it to the model width, so a 256k-piece vocabulary costs `256000 * rank` weights instead of
`256000 * hidden`.

Every linear map stores its weight as [in, out] and computes `x @ weight + bias`, so the exported ONNX graph names each
weight after its parameter and a consumer can rebuild the module from the initializers by name (docs/accurate-encoder.md).
Padding is handled by an additive key mask. `manual_attention=True` writes attention as MatMul/Softmax for export;
training uses `scaled_dot_product_attention`.
"""
import math
from dataclasses import dataclass, asdict

import torch
import torch.nn as nn
import torch.nn.functional as F


@dataclass
class StudentConfig:
    vocab_size: int = 256000
    embed_rank: int = 96
    hidden: int = 384
    layers: int = 6
    heads: int = 12
    ffn: int = 1536
    max_positions: int = 512
    eps: float = 1e-5

    def to_dict(self):
        return asdict(self)


class Dense(nn.Module):
    def __init__(self, n_in, n_out, std=0.02):
        super().__init__()
        self.weight = nn.Parameter(torch.randn(n_in, n_out) * std)
        self.bias = nn.Parameter(torch.zeros(n_out))

    def forward(self, x):
        return x @ self.weight + self.bias


class Attention(nn.Module):
    def __init__(self, c: StudentConfig):
        super().__init__()
        self.heads = c.heads
        self.query = Dense(c.hidden, c.hidden)
        self.key = Dense(c.hidden, c.hidden)
        self.value = Dense(c.hidden, c.hidden)
        self.output = Dense(c.hidden, c.hidden)

    def forward(self, x, mask, manual, probs_out=None):
        b, n, d = x.shape
        h, dh = self.heads, d // self.heads
        q = self.query(x).view(b, n, h, dh).transpose(1, 2)
        k = self.key(x).view(b, n, h, dh).transpose(1, 2)
        v = self.value(x).view(b, n, h, dh).transpose(1, 2)
        if probs_out is not None:
            # training with an attention objective: the probabilities are needed, so attention is written out (boolean mask)
            s = (q @ k.transpose(-1, -2)) * (1.0 / math.sqrt(dh))
            p = torch.softmax(s.float().masked_fill(~mask, -1e9), dim=-1)
            probs_out.append(p)
            a = p.to(v.dtype) @ v
        elif manual:
            s = (q @ k.transpose(-1, -2)) * (1.0 / math.sqrt(dh)) + mask
            a = torch.softmax(s, dim=-1) @ v
        else:
            a = F.scaled_dot_product_attention(q, k, v, attn_mask=mask)
        return self.output(a.transpose(1, 2).reshape(b, n, d))


class Layer(nn.Module):
    """x1 = LN(x + Attn(x)); y = LN(x1 + W2 GELU(W1 x1)) with the exact (erf) GELU."""

    def __init__(self, c: StudentConfig):
        super().__init__()
        self.attention = Attention(c)
        self.attn_norm = nn.LayerNorm(c.hidden, eps=c.eps)
        self.intermediate = Dense(c.hidden, c.ffn)
        self.output = Dense(c.ffn, c.hidden)
        self.output_norm = nn.LayerNorm(c.hidden, eps=c.eps)

    def forward(self, x, mask, manual, probs_out=None):
        x = self.attn_norm(x + self.attention(x, mask, manual, probs_out))
        return self.output_norm(x + self.output(F.gelu(self.intermediate(x))))


class Embeddings(nn.Module):
    def __init__(self, c: StudentConfig):
        super().__init__()
        self.word_embeddings = nn.Embedding(c.vocab_size, c.embed_rank)
        self.projection = Dense(c.embed_rank, c.hidden, std=1.0 / math.sqrt(c.embed_rank))
        self.position_embeddings = nn.Embedding(c.max_positions, c.hidden)
        self.LayerNorm = nn.LayerNorm(c.hidden, eps=c.eps)
        nn.init.normal_(self.word_embeddings.weight, std=1.0)
        nn.init.normal_(self.position_embeddings.weight, std=0.02)

    def forward(self, ids):
        n = ids.shape[1]
        pos = torch.arange(n, device=ids.device)
        return self.LayerNorm(self.projection(self.word_embeddings(ids)) + self.position_embeddings(pos))


class Encoder(nn.Module):
    def __init__(self, c: StudentConfig):
        super().__init__()
        self.layer = nn.ModuleList([Layer(c) for _ in range(c.layers)])


class StudentEncoder(nn.Module):
    def __init__(self, c: StudentConfig):
        super().__init__()
        self.config = c
        self.embeddings = Embeddings(c)
        self.encoder = Encoder(c)
        self.manual_attention = False

    def forward(self, input_ids, attention_mask, return_all=False, probs_out=None):
        if self.manual_attention:
            mask = (1.0 - attention_mask[:, None, None, :].to(torch.float32)) * -1e9  # additive, as OneModel's attention takes it
        else:
            mask = attention_mask[:, None, None, :].bool()
        x = self.embeddings(input_ids)
        states = []
        for layer in self.encoder.layer:
            x = layer(x, mask, self.manual_attention, probs_out)
            states.append(x)
        return (x, states) if return_all else x


class ExportWrapper(nn.Module):
    """(input_ids, attention_mask) -> last_hidden_state; the parameter names start with `model.`."""

    def __init__(self, model):
        super().__init__()
        self.model = model
        model.manual_attention = True

    def forward(self, input_ids, attention_mask):
        return self.model(input_ids, attention_mask)
