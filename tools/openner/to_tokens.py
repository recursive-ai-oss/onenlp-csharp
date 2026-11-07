"""Turn filtered labelling output into training JSONL for the recipe.

  to_tokens.py --in DIR --out FILE [--min-type-count 3] [--max-tokens 100]

Each passage is cut into windows of at most --max-tokens tokens at sentence ends. A window row is
{"tokens": [...], "spans": [[first, last_exclusive, type], ...], "types": [...]} where "types" are the types the passage
was labelled with: those absent from the window are true negatives for it, because the labeller listed every mention in
the passage. Types seen fewer than --min-type-count times in the whole output are dropped (typos, one-off phrasing).
Mentions that do not start and end on token boundaries are dropped.
"""
import argparse
import collections
import glob
import json
import re

TOKEN = re.compile(r"\w+|[^\w\s]", re.UNICODE)
END = {".", "!", "?"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--min-type-count", type=int, default=3)
    ap.add_argument("--max-tokens", type=int, default=100)
    a = ap.parse_args()

    rows = []
    for f in sorted(glob.glob(a.src + "/block-*.jsonl")):
        rows += [json.loads(l) for l in open(f, encoding="utf-8")]
    counts = collections.Counter(e["type"] for r in rows for e in r["entities"])
    keep_types = {t for t, n in counts.items() if n >= a.min_type_count}

    written = spans_written = 0
    with open(a.out, "w", encoding="utf-8") as out:
        for r in rows:
            text = r["text"]
            toks = [(m.group(), m.start(), m.end()) for m in TOKEN.finditer(text)]
            if not toks:
                continue
            ents = [e for e in r["entities"] if e["type"] in keep_types]
            if not ents:
                continue
            types = sorted({e["type"] for e in ents})
            by_start = {t[1]: i for i, t in enumerate(toks)}
            by_end = {t[2]: i for i, t in enumerate(toks)}
            mentions = []
            for e in ents:
                for s, en in e["spans"]:
                    if s in by_start and en in by_end and by_end[en] >= by_start[s]:
                        mentions.append((by_start[s], by_end[en] + 1, e["type"]))
            # windows
            cuts, begin = [], 0
            for i, (tok, _, _) in enumerate(toks):
                if i + 1 - begin >= a.max_tokens or (tok in END and i + 1 - begin >= 8):
                    cuts.append((begin, i + 1))
                    begin = i + 1
            if begin < len(toks):
                cuts.append((begin, len(toks)))
            for b, e_ in cuts:
                sp = [[s - b, en - b, ty] for s, en, ty in mentions if s >= b and en <= e_]
                if not sp:
                    continue
                out.write(json.dumps({"tokens": [t[0] for t in toks[b:e_]], "spans": sp, "types": types}, ensure_ascii=False) + "\n")
                written += 1
                spans_written += len(sp)
    print(f"types kept {len(keep_types)} of {len(counts)}; windows {written}; spans {spans_written}")


if __name__ == "__main__":
    main()
