"""Lists the (model, unit, currency) triples a raw Recognizers.Text output holds, with an example span; used to write the unit name maps."""
import json
import sys

seen = {}
for line in open(sys.argv[1], encoding="utf-8"):
    for r in json.loads(line)["results"]:
        if r["model"] in ("dimension", "currency", "age", "temperature"):
            res = r["resolution"]
            seen[(r["model"], res.get("unit"), res.get("isoCurrency"))] = r["text"]
for k, v in sorted(seen.items(), key=str):
    print(k, v)
