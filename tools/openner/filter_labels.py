"""The labelling recipe's precision filter, applied to label.py output.

An entity is dropped when its text has no uppercase letter and no digit (common nouns), or when its type is one of a short list of
words that do not name a kind of entity. Blocks go in, filtered blocks come out under the same names.

  filter_labels.py --in DIR --out DIR
"""
import argparse
import glob
import json
import os

NOT_TYPES = {"acronym", "adjective", "job title", "position", "event"}


def keep(e):
    return any(c.isupper() or c.isdigit() for c in e["text"]) and e["type"] not in NOT_TYPES


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    kept = dropped = 0
    for f in sorted(glob.glob(a.src + "/block-*.jsonl")):
        with open(os.path.join(a.out, os.path.basename(f)), "w", encoding="utf-8") as out:
            for line in open(f, encoding="utf-8"):
                r = json.loads(line)
                es = [e for e in r["entities"] if keep(e)]
                kept += len(es)
                dropped += len(r["entities"]) - len(es)
                r["entities"] = es
                out.write(json.dumps(r, ensure_ascii=False) + "\n")
    print("kept", kept, "dropped", dropped)


if __name__ == "__main__":
    main()
