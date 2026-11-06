#!/usr/bin/env python3
"""Cross-check of the harness: runs a task with mteb's own sentence-transformers path (documented: mteb.evaluate(SentenceTransformer(...)))
instead of our wrapper, to see whether a gap to the published score comes from our wrapper or from the mteb release.

    mteb_direct.py REPO REVISION TASK[,TASK...] [mteb]
"""
import sys, mteb, sentence_transformers, torch
torch.set_num_threads(4)
repo, rev, tasks = sys.argv[1:4]
# "mteb" as a fourth argument loads the model through mteb.get_model (mteb's own implementation, with its per-model prompts).
m = mteb.get_model(repo, revision=rev) if len(sys.argv) > 4 and sys.argv[4] == "mteb" else sentence_transformers.SentenceTransformer(repo, revision=rev, device="cpu")
for t in tasks.split(","):
    res = mteb.evaluate(m, tasks=mteb.get_tasks(tasks=[t]), cache=None)
    for r in res:
        print("DIRECT", r.task_name, round(r.get_score() * 100, 2), flush=True)
