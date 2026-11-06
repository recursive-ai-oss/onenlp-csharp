#!/usr/bin/env python3
"""Builds the MTEB comparison table for one model from the published model-card scores, the same-harness sentence-transformers
run and our runs.

    mteb_table.py --repo ID --st ST.json --ours OURS.json [--int8 INT8.json] [--tasks T1,T2] > table.md

Scores are x100. Published: the model card's model-index, the metric each task type uses as its main score. For multilingual
tasks the published number is the mean over the subsets the run covered (the harness reports the mean over its subsets).
"""
import argparse, json, re, urllib.request

MAIN = {"STS": "cos_sim_spearman", "Classification": "accuracy", "Clustering": "v_measure", "PairClassification": "max_ap",
        "Reranking": "map", "Retrieval": "ndcg_at_10"}
TYPE = {"STSBenchmark": "STS", "STS22": "STS", "Banking77Classification": "Classification", "MTOPDomainClassification": "Classification",
        "TwentyNewsgroupsClustering": "Clustering", "TwitterSemEval2015": "PairClassification", "AskUbuntuDupQuestions": "Reranking",
        "SciFact": "Retrieval"}
ap = argparse.ArgumentParser()
ap.add_argument("--repo", required=True); ap.add_argument("--st", required=True); ap.add_argument("--ours", required=True)
ap.add_argument("--int8"); ap.add_argument("--subsets", default="")
a = ap.parse_args()
card = json.load(urllib.request.urlopen("https://huggingface.co/api/models/" + a.repo))["cardData"]["model-index"][0]["results"]
subsets = set(filter(None, a.subsets.split(",")))  # e.g. de,en,es,fr for multilingual tasks


def published(task):
    vals = []
    for r in card:
        n = r["dataset"]["name"]
        m = re.match(r"MTEB (\w+)(?: \((.+)\))?$", n)
        if not m or m.group(1) != task or r["dataset"].get("split") not in ("test", None):
            continue
        if m.group(2) and subsets and m.group(2) not in subsets:
            continue
        want = MAIN[TYPE[task]]
        for x in r["metrics"]:
            if x["type"] == want:
                vals.append(x["value"])
    return sum(vals) / len(vals) if vals else None


st = json.load(open(a.st))["tasks"]; ours = json.load(open(a.ours))["tasks"]
i8 = json.load(open(a.int8))["tasks"] if a.int8 else {}
print("| Task | Published | sentence-transformers (same harness) | Ours fp32 | Ours - ST | Ours - published | int8 | int8 - fp32 |")
print("|---|---|---|---|---|---|---|---|")
for t in st:
    p = published(t); s = st[t]["main_score"] * 100; o = ours.get(t, {}).get("main_score", float("nan")) * 100
    q = i8.get(t, {}).get("main_score")
    pp = f"{p:.2f}" if p is not None else "n/a"
    dp = f"{o - p:+.2f}" if p is not None else "n/a"
    print(f"| {t} | {pp} | {s:.2f} | {o:.2f} | {o - s:+.2f} | {dp} | " + (f"{q * 100:.2f} | {q * 100 - o:+.2f} |" if q is not None else "n/a | n/a |"))
