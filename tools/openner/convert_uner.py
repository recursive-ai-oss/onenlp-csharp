"""Turn a UNER IOB2 file (CoNLL-U-like blocks: id, form, tag, ...) into the JSONL the open-type recipe reads:
{"tokens": [...], "spans": [[start, end_exclusive, "type name"], ...]}, with plain-word type names.

  convert_uner.py IN.iob2 OUT.jsonl
"""
import json
import sys

NAMES = {"PER": "person", "ORG": "organization", "LOC": "location"}


def main(src, dst):
    rows, toks, tags = [], [], []

    def flush():
        if not toks:
            return
        spans, cur = [], None
        for i, t in enumerate(tags + ["O"]):
            if cur and not t.startswith("I-"):
                spans.append([cur[0], i, NAMES[cur[1]]])
                cur = None
            if t.startswith("B-"):
                cur = (i, t[2:])
        rows.append({"tokens": list(toks), "spans": spans})

    for line in open(src, encoding="utf-8"):
        line = line.rstrip("\n")
        if line.startswith("#"):
            continue
        if not line:
            flush()
            toks.clear()
            tags.clear()
            continue
        f = line.split("\t")
        if "-" in f[0] or "." in f[0]:
            continue
        toks.append(f[1])
        tags.append(f[2])
    flush()
    with open(dst, "w", encoding="utf-8") as out:
        for r in rows:
            out.write(json.dumps(r, ensure_ascii=False) + "\n")
    print(dst, len(rows), "sentences")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
