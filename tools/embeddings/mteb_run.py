#!/usr/bin/env python3
"""Runs MTEB tasks (mteb, black box, through its documented evaluate API) for one embedding implementation:

    mteb_run.py --impl st|ours --name NAME --repo ID --revision SHA --out FILE.json [--tool DLL --model-dir DIR --model-file F]
                --tasks T1,T2,... [--threads N] [--doc-prefix "passage: "] [--query-prefix "query: "] [--retrieval-query-prefix TEXT]

impl st   : sentence-transformers model wrapped by the same prefixing wrapper as ours (the same-harness reference).
impl ours : texts are sent to OneNlp.EmbedTool serve (our SentenceEmbedder) over a pipe.
Prefixes: documents (prompt_type document) get --doc-prefix; everything else --query-prefix; for retrieval tasks the
queries get --retrieval-query-prefix when given (BGE's published retrieval instruction).
Writes {task: {main_score, ...}} to FILE.json.
"""
import argparse, json, struct, subprocess, sys, time
import numpy as np
import mteb
from mteb.types import PromptType

ap = argparse.ArgumentParser()
ap.add_argument("--impl", required=True); ap.add_argument("--name", required=True)
ap.add_argument("--repo"); ap.add_argument("--revision"); ap.add_argument("--out", required=True)
ap.add_argument("--tool"); ap.add_argument("--model-dir"); ap.add_argument("--model-file", default="model.onnx")
ap.add_argument("--tasks", required=True); ap.add_argument("--threads", type=int, default=4)
ap.add_argument("--doc-prefix", default=""); ap.add_argument("--query-prefix", default="")
ap.add_argument("--retrieval-query-prefix", default=None)
ap.add_argument("--batch", type=int, default=32)
ap.add_argument("--langs", default="")
a = ap.parse_args()


class Backend:
    def encode_texts(self, texts): raise NotImplementedError


class StBackend(Backend):
    def __init__(self):
        import sentence_transformers, torch
        torch.set_num_threads(a.threads)
        self.m = sentence_transformers.SentenceTransformer(a.repo, revision=a.revision, device="cpu")

    def encode_texts(self, texts):
        return self.m.encode(texts, batch_size=a.batch, convert_to_numpy=True, show_progress_bar=False)


class OursBackend(Backend):
    def __init__(self):
        cmd = ["dotnet", a.tool, "serve", a.model_dir, "--model", a.model_file, "--batch", str(a.batch)]
        self.p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE)

    def _read(self, n):
        buf = b""
        while len(buf) < n:
            chunk = self.p.stdout.read(n - len(buf))
            if not chunk: raise RuntimeError("EmbedTool ended")
            buf += chunk
        return buf

    def encode_texts(self, texts):
        out = []
        for i in range(0, len(texts), 256):
            part = texts[i:i + 256]
            self.p.stdin.write((json.dumps(part, ensure_ascii=True) + "\n").encode()); self.p.stdin.flush()
            n, d = struct.unpack("<ii", self._read(8))
            out.append(np.frombuffer(self._read(4 * n * d), dtype="<f4").reshape(n, d))
        return np.concatenate(out) if out else np.zeros((0, 0), dtype="<f4")


backend = StBackend() if a.impl == "st" else OursBackend()


class Wrapper:
    """Minimal custom model for mteb: encode(inputs, task_metadata, hf_split, hf_subset, prompt_type)."""
    def __init__(self):
        self.calls = 0
        # The documented way to give a custom model its metadata (docs.mteb.org, "Adding a Model").
        from mteb.models import ModelMeta
        self.mteb_model_meta = ModelMeta(
            name=f"onenlp-test/{a.name}-{a.impl}", loader=None, languages=None, open_weights=True, revision=a.revision or "x",
            release_date=None, n_parameters=None, memory_usage_mb=None, max_tokens=512, embed_dim=None, license=None,
            reference=None, similarity_fn_name="cosine", framework=[], use_instructions=False,
            public_training_code=None, public_training_data=None, training_datasets=set())

    def __init_subclass__(cls): pass

    def encode(self, inputs, *, task_metadata, hf_split, hf_subset, prompt_type=None, **kwargs):
        texts = []
        for batch in inputs:
            texts += list(batch["text"])
        is_doc = prompt_type == PromptType.document
        prefix = a.doc_prefix if is_doc else a.query_prefix
        if (not is_doc and prompt_type == PromptType.query and a.retrieval_query_prefix is not None
                and task_metadata.type == "Retrieval"):
            prefix = a.retrieval_query_prefix
        self.calls += 1
        return backend.encode_texts([prefix + t for t in texts])


def _cos(x, y):
    import torch
    x = torch.as_tensor(np.asarray(x, dtype="float32")); y = torch.as_tensor(np.asarray(y, dtype="float32"))
    if x.ndim == 1: x = x[None]
    if y.ndim == 1: y = y[None]
    return torch.nn.functional.normalize(x, dim=-1), torch.nn.functional.normalize(y, dim=-1)


def _similarity(self, embeddings1, embeddings2):
    x, y = _cos(embeddings1, embeddings2)
    return x @ y.T


def _similarity_pairwise(self, embeddings1, embeddings2):
    x, y = _cos(embeddings1, embeddings2)
    return (x * y).sum(-1)


Wrapper.similarity = _similarity
Wrapper.similarity_pairwise = _similarity_pairwise

import os
done = json.load(open(a.out))["tasks"] if os.path.exists(a.out) else {}
t0 = time.time()
for name in a.tasks.split(","):
    # Resumable: a task already in the output file is not run again (the base name, without a .vN suffix, is what mteb reports).
    if name in done:
        continue
    tasks = mteb.get_tasks(tasks=[name], languages=a.langs.split(",")) if a.langs else mteb.get_tasks(tasks=[name])
    res = mteb.evaluate(Wrapper(), tasks=tasks, cache=None)
    for r in res:
        done[name] = {"main_score": float(r.get_score()), "reported_name": r.task_name}
    json.dump({"impl": a.impl, "name": a.name, "mteb": mteb.__version__, "tasks": done, "seconds": time.time() - t0},
              open(a.out, "w"), indent=1)
    print(name, done[name], flush=True)
