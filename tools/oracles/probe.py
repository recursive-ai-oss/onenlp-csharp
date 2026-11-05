"""Prints what the recognizers found in each line of a raw `recognize` output: span, kind and value, one line per text."""
import json
import sys

lines = [l.rstrip("\n") for l in open(sys.argv[1], encoding="utf-8")]
for line, row in zip(lines, open(sys.argv[2], encoding="utf-8")):
    d = json.loads(row)
    found = " ; ".join(f"[{r['text']}]={r.get('kind')}:{r.get('value')}{('/' + r['unit']) if r.get('unit') else ''}" for r in d["results"])
    print(f"{line}\n    {found}")
