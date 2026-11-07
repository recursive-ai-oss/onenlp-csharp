#!/usr/bin/env python3
"""Sets two comparison tables of compare_refs.py side by side: our score before and after, the best published score for the same row, and how
many of the published systems still beat our interval.

  before_after.py BEFORE_comparison.md AFTER_comparison.md > table.md
"""
import re
import sys
import collections


def read(path):
    rows = collections.OrderedDict()
    for line in open(path, encoding="utf-8"):
        if not line.startswith("|") or line.startswith("|---") or line.startswith("| Language"):
            continue
        c = [x.strip() for x in line.strip().strip("|").split("|")]
        if len(c) != 7:
            continue
        m = re.match(r"([\d.]+) \[([\d.]+), ([\d.]+)\]", c[3])
        if not m:
            continue
        key = (c[0], c[1], c[2])
        r = rows.setdefault(key, {"ours": float(m.group(1)), "lo": float(m.group(2)), "hi": float(m.group(3)), "refs": [], "miss": 0})
        try:
            r["refs"].append(float(c[5]))
        except ValueError:
            pass
        r["miss"] += "MISS" in c[6]
    return rows


before, after = read(sys.argv[1]), read(sys.argv[2])
print("| Treebank | Input | Metric | Before | After | 95% CI after | Best published | Systems ahead of the interval |")
print("|---|---|---|---:|---:|---|---:|---|")
for key, a in after.items():
    b = before.get(key)
    best = max(a["refs"]) if a["refs"] else float("nan")
    print(f"| {key[0].replace('-test', '')} | {key[1]} | {key[2]} | {b['ours']:.2f} | {a['ours']:.2f} | [{a['lo']:.2f}, {a['hi']:.2f}] | {best:.2f} | {a['miss']} of {len(a['refs'])} |" if b
          else f"| {key[0].replace('-test', '')} | {key[1]} | {key[2]} | n/a | {a['ours']:.2f} | [{a['lo']:.2f}, {a['hi']:.2f}] | {best:.2f} | {a['miss']} of {len(a['refs'])} |")
