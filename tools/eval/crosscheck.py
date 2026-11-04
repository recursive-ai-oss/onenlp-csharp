#!/usr/bin/env python3
"""Cross-checks OneNlp.Eval's scorers against the reference scorers, which run as black boxes.

UD metrics: the shared-task evaluation script, called through its public functions (full precision) and once as a program
(`-v` table, two decimals). Span F1: seqeval in strict IOB2 mode. Every number must agree to 4 decimal places (in points).

usage: crosscheck.py --eval "dotnet OneNlp.Eval.dll" --home HOME --work WORKDIR [--sets 24]
  HOME holds eval/ud/en_ewt/en_ewt-ud-test.conllu and eval/ud/tools/conll18_ud_eval.py (see tools/fetch/conll18_crosscheck.sh).
Run it with the Python from HOME/u10/venv so seqeval is importable.
"""
import argparse
import glob
import importlib.metadata
import json
import os
import re
import shlex
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOLERANCE = 5e-5  # points: agreement to 4 decimal places


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit("command failed: %s\n%s" % (" ".join(cmd), r.stderr[-2000:]))
    return r.stdout


def check_ud(args, eval_cmd):
    tools = os.path.join(args.home, "eval/ud/tools")
    sys.path.insert(0, tools)
    import conll18_ud_eval as ref
    gold_path = os.path.join(args.home, "eval/ud/en_ewt/en_ewt-ud-test.conllu")
    out = os.path.join(args.work, "ud")
    run([sys.executable, os.path.join(HERE, "perturb_ud.py"), gold_path, out, str(args.sets)])
    preds = sorted(glob.glob(os.path.join(out, "pred_*.conllu")))
    ours_json = os.path.join(out, "ours.json")
    run(eval_cmd + ["dump-ud", "--gold", gold_path, "--pred"] + preds + ["--json", ours_json])
    ours = json.load(open(ours_json))
    gold = ref.load_conllu_file(gold_path)
    compared, bad = 0, []
    for p in preds:
        try:
            theirs = ref.evaluate(gold, ref.load_conllu_file(p))
        except ref.UDError as e:
            bad.append((p, "reference rejected the input: %s" % e))
            continue
        # the program itself, two decimals
        table = run([sys.executable, os.path.join(tools, "conll18_ud_eval.py"), "-v", gold_path, p])
        printed = {}
        for line in table.splitlines():
            m = re.match(r"^(\w+)\s*\|\s*([\d.]+)\s*\|\s*([\d.]+)\s*\|\s*([\d.]+)\s*\|\s*([\d.]+)?", line)
            if m:
                printed[m.group(1)] = [float(m.group(i)) for i in (2, 3, 4)]
        for name, s in theirs.items():
            mine = ours[p][name]
            pairs = [("precision", s.precision), ("recall", s.recall), ("f1", s.f1), ("aligned_accuracy", s.aligned_accuracy)]
            for key, value in pairs:
                if value is None:
                    continue  # the reference leaves aligned accuracy undefined for tokens, sentences and words
                compared += 1
                if abs(100 * mine[key] - 100 * value) > TOLERANCE:
                    bad.append((p, "%s %s ours=%.6f theirs=%.6f" % (name, key, 100 * mine[key], 100 * value)))
            if name in printed:
                for got, key in zip(printed[name], ("precision", "recall", "f1")):
                    compared += 1
                    if abs(got - 100 * mine[key]) > 0.0051:
                        bad.append((p, "%s %s program-table=%.2f ours=%.4f" % (name, key, got, 100 * mine[key])))
    return len(preds), compared, bad


def check_span(args, eval_cmd):
    from seqeval.metrics import classification_report
    from seqeval.scheme import IOB2
    out = os.path.join(args.work, "span")
    run([sys.executable, os.path.join(HERE, "perturb_span.py"), out, str(args.sets)])
    compared, bad, sets = 0, [], 0
    for gold_path in sorted(glob.glob(os.path.join(out, "gold_*.iob2"))):
        pred_path = gold_path.replace("gold_", "pred_")
        sets += 1

        def read(path):
            sents, cur = [], []
            for line in open(path, encoding="utf-8").read().split("\n"):
                if not line:
                    if cur:
                        sents.append(cur)
                    cur = []
                else:
                    cur.append(line.split(" ")[-1])
            return sents

        report = classification_report(read(gold_path), read(pred_path), mode="strict", scheme=IOB2, output_dict=True, zero_division=0)
        j = os.path.join(out, "ours.json")
        run(eval_cmd + ["dump-span", "--gold", gold_path, "--pred", pred_path, "--json", j])
        ours = json.load(open(j))[pred_path]
        expected = {k: v for k, v in report.items()}
        pairs = [("micro", expected["micro avg"])] + [(k, v) for k, v in expected.items() if k not in ("micro avg", "macro avg", "weighted avg")]
        for label, v in pairs:
            mine = ours[label]
            for key, ref_key in (("precision", "precision"), ("recall", "recall"), ("f1", "f1-score"), ("support", "support")):
                compared += 1
                if abs(100 * mine[key] - 100 * v[ref_key]) > TOLERANCE * (1 if key != "support" else 100):
                    bad.append((pred_path, "%s %s ours=%.6f theirs=%.6f" % (label, key, mine[key], v[ref_key])))
        compared += 1
        if abs(100 * ours["macro_f1"] - 100 * expected["macro avg"]["f1-score"]) > TOLERANCE:
            bad.append((pred_path, "macro f1 ours=%.6f theirs=%.6f" % (ours["macro_f1"], expected["macro avg"]["f1-score"])))
    return sets, compared, bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--eval", required=True)
    ap.add_argument("--home", required=True)
    ap.add_argument("--work", required=True)
    ap.add_argument("--sets", type=int, default=24)
    ap.add_argument("--only", choices=["ud", "span"])
    args = ap.parse_args()
    eval_cmd = shlex.split(args.eval)
    os.makedirs(args.work, exist_ok=True)
    failed = False
    if args.only in (None, "ud"):
        sets, compared, bad = check_ud(args, eval_cmd)
        print("UD: %d perturbation sets, %d numbers compared, %d differences" % (sets, compared, len(bad)))
        for b in bad[:40]:
            print("  ", *b)
        failed |= bool(bad)
    if args.only in (None, "span"):
        sets, compared, bad = check_span(args, eval_cmd)
        print("span: %d synthetic sets, %d numbers compared, %d differences (seqeval %s)" % (sets, compared, len(bad), importlib.metadata.version("seqeval")))
        for b in bad[:40]:
            print("  ", *b)
        failed |= bool(bad)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
