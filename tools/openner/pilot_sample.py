"""Draw a fixed random sample of labelled passages for hand checking and print them compactly.

  pilot_sample.py --labels DIR --n 200 --seed 20261107 [--start 0 --count 50]

Prints passages (id, text) and each labelled entity as "[k] text <type>" so a reviewer can write a judgement per mention.
Also writes DIR/sample-ids.txt.
"""
import argparse
import glob
import json
import random


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--labels", required=True)
    ap.add_argument("--n", type=int, default=200)
    ap.add_argument("--seed", type=int, default=20261107)
    ap.add_argument("--start", type=int, default=0)
    ap.add_argument("--count", type=int, default=50)
    a = ap.parse_args()
    rows = []
    for f in sorted(glob.glob(a.labels + "/block-*.jsonl")):
        rows += [json.loads(l) for l in open(f, encoding="utf-8")]
    rows.sort(key=lambda r: r["id"])
    pick = sorted(random.Random(a.seed).sample(range(len(rows)), a.n))
    with open(a.labels + "/sample-ids.txt", "w") as f:
        f.write("\n".join(str(rows[i]["id"]) for i in pick) + "\n")
    for i in pick[a.start : a.start + a.count]:
        r = rows[i]
        print(f"### {r['id']}: {r['text']}")
        for k, e in enumerate(r["entities"]):
            print(f"  [{k}] {e['text']} <{e['type']}> x{len(e['spans'])}")


if __name__ == "__main__":
    main()
