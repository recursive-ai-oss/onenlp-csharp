#!/usr/bin/env python3
"""Runs MTEB tasks under a 1.x release of `mteb` (black box, through its documented `MTEB(...).run(model)` API) for one embedding
implementation. Same arguments as mteb_run.py, which uses the 2.x API; this is the harness closest to the one the model cards' numbers
come from. Run it in the venv with mteb 1.x (setup in docs/embeddings.md).

    mteb1_run.py --impl st|ours --name NAME --repo ID --revision SHA --out FILE.json [--tool DLL --model-dir DIR --model-file F]
                 --tasks T1,T2,... [--threads N] [--doc-prefix "passage: "] [--query-prefix "query: "] [--retrieval-query-prefix TEXT]
                 [--subsets "STS22=en,de,de-en;MTOPDomainClassification=en,de"]

Writes {"tasks": {task: {main_score, per_subset: {subset: score}}}} to FILE.json (scores x100; main_score is the mean over the subsets run).
"""
import argparse, json, os, struct, subprocess, time
import numpy as np
from mteb import MTEB

ap = argparse.ArgumentParser()
ap.add_argument("--impl", required=True); ap.add_argument("--name", required=True)
ap.add_argument("--repo"); ap.add_argument("--revision"); ap.add_argument("--out", required=True)
ap.add_argument("--tool"); ap.add_argument("--model-dir"); ap.add_argument("--model-file", default="model.onnx")
ap.add_argument("--tasks", required=True); ap.add_argument("--threads", type=int, default=4)
ap.add_argument("--doc-prefix", default=""); ap.add_argument("--query-prefix", default="")
ap.add_argument("--retrieval-query-prefix", default=None)
ap.add_argument("--batch", type=int, default=32)
ap.add_argument("--subsets", default="")
a = ap.parse_args()
subsets = {k: v.split(",") for k, v in (p.split("=") for p in a.subsets.split(";") if p)}


class StBackend:
    def __init__(self):
        import sentence_transformers, torch
        torch.set_num_threads(a.threads)
        self.m = sentence_transformers.SentenceTransformer(a.repo, revision=a.revision, device="cpu")

    def encode_texts(self, texts):
        return self.m.encode(texts, batch_size=a.batch, convert_to_numpy=True, show_progress_bar=False)


class OursBackend:
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


class Model:
    """The model mteb 1.x calls: encode(sentences), plus encode_queries/encode_corpus for retrieval."""
    def encode(self, sentences, batch_size=32, **kwargs):
        return backend.encode_texts([a.query_prefix + s for s in sentences])

    def encode_queries(self, queries, batch_size=32, **kwargs):
        p = a.retrieval_query_prefix if a.retrieval_query_prefix is not None else a.query_prefix
        return backend.encode_texts([p + q for q in queries])

    def encode_corpus(self, corpus, batch_size=32, **kwargs):
        if isinstance(corpus, dict):
            corpus = [{"title": t, "text": x} for t, x in zip(corpus.get("title", [""] * len(corpus["text"])), corpus["text"])]
        texts = [(d.get("title", "") + " " + d["text"]).strip() if isinstance(d, dict) else d for d in corpus]
        return backend.encode_texts([a.doc_prefix + t for t in texts])


def main_scores(scores, task):
    """Returns {subset: main_score x100} from a task's test-split scores. mteb 1.12 gives {split: [{hf_subset, main_score}]}; mteb 1.1
    gives {split: {metric: value}} or, for multilingual tasks, {split: {lang: {metric: value}}} with the main metric named in the task description."""
    t = scores["test"]
    if isinstance(t, list):
        return {e["hf_subset"]: float(e["main_score"]) * 100 for e in t}
    main = task.description["main_score"]

    def pick(d):
        if "main_score" in d:
            return float(d["main_score"]) * 100
        # Nested metric groups, e.g. {"cos_sim": {"spearman": ..}}: main_score is the dotted path's last key found at any depth.
        stack = [d]
        while stack:
            x = stack.pop()
            if main in x and not isinstance(x[main], dict):
                return float(x[main]) * 100
            stack += [v for v in x.values() if isinstance(v, dict)]
        raise KeyError(main)

    skip = {"evaluation_time"}
    if task.is_multilingual:
        return {k: pick(v) for k, v in t.items() if k not in skip and isinstance(v, dict)}
    return {"default": pick(t)}


done = json.load(open(a.out))["tasks"] if os.path.exists(a.out) else {}
t0 = time.time()
for name in a.tasks.split(","):
    if name in done:
        continue
    old = __import__("mteb").__version__.startswith("1.1.")  # 1.1.x selects subsets with task_langs; 1.12 with the task's hf_subsets
    ev = MTEB(tasks=[name], task_langs=subsets[name]) if name in subsets and old else MTEB(tasks=[name])
    if name in subsets:
        for t in ev.tasks:
            t.hf_subsets = subsets[name]  # mteb 1.12 reads this; mteb 1.1 takes task_langs above
    task = ev.tasks[0]  # older releases empty the list while running
    out = ev.run(Model(), output_folder=None, eval_splits=["test"], verbosity=0, overwrite_results=True)
    # mteb 1.x returns a list of task results or a dict, depending on the minor release; both carry .scores or the score dict.
    r = out[0] if isinstance(out, list) else out[name]  # 1.1: {task: scores}
    scores = r.scores if hasattr(r, "scores") else r
    per = main_scores(scores, task)
    done[name] = {"main_score": sum(per.values()) / len(per), "per_subset": per, "raw": scores if isinstance(scores, dict) else None}
    json.dump({"impl": a.impl, "name": a.name, "mteb": __import__("mteb").__version__, "tasks": done, "seconds": time.time() - t0},
              open(a.out, "w"), indent=1, default=str)
    print(name, {k: v for k, v in done[name].items() if k != "raw"}, flush=True)
