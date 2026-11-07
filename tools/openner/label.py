"""Label passages with entity spans and free-text types using an open-weight instruction model (vLLM).

  label.py --passages passages.jsonl --out-dir DIR --model Qwen/Qwen2.5-7B-Instruct-AWQ --revision SHA [--block 500]
           [--hours-log gpu-hours.tsv]

Resumable: output goes to DIR/block-NNNNN.jsonl, one file per block of passages, and finished blocks are skipped.
Greedy decoding. Each line: {"id", "text", "raw", "entities": [{"text", "type", "spans": [[start, end], ...]}]}
where spans are character offsets of exact, word-bounded occurrences of the entity text in the passage. Entities whose
text does not occur in the passage are dropped.
"""
import argparse
import json
import os
import re
import time

PROMPT = """Extract the named entities from the passage below, for a named-entity recognition training set.

Rules:
- List every named entity: people, organizations, places, products, works, events, laws, dates only when they name an event, and so on. Do not list pronouns, common nouns or numbers on their own.
- "text" must be copied exactly from the passage, character for character, and be the full name (not a part of it).
- "type" is a short lowercase noun phrase of one to three words that says what the entity is, for example "politician", "football club", "film", "river", "pharmaceutical drug". Prefer specific types over "person" or "organization" when the passage supports it, and use the same type for the same kind of thing.
- Answer with a JSON list only, for example [{"text": "Marie Curie", "type": "physicist"}, {"text": "Sorbonne", "type": "university"}]. If there are no entities answer [].

Passage:
\"\"\"
%s
\"\"\"
"""

_JSON = re.compile(r"\[.*\]", re.S)


def parse(raw: str, text: str):
    m = _JSON.search(raw)
    if not m:
        return []
    try:
        items = json.loads(m.group(0))
    except json.JSONDecodeError:
        return []
    out, seen = [], set()
    for it in items if isinstance(items, list) else []:
        if not isinstance(it, dict) or not isinstance(it.get("text"), str) or not isinstance(it.get("type"), str):
            continue
        t, ty = it["text"].strip(), it["type"].strip().lower()
        if not t or not ty or len(t) > 80 or len(ty.split()) > 4 or (t, ty) in seen:
            continue
        seen.add((t, ty))
        spans = [[m2.start(), m2.end()] for m2 in re.finditer(r"(?<!\w)" + re.escape(t) + r"(?!\w)", text)]
        if spans:
            out.append({"text": t, "type": ty, "spans": spans})
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--passages", required=True)
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--model", required=True)
    ap.add_argument("--revision", required=True)
    ap.add_argument("--block", type=int, default=500)
    ap.add_argument("--max-new", type=int, default=450)
    ap.add_argument("--hours-log")
    a = ap.parse_args()
    os.makedirs(a.out_dir, exist_ok=True)

    rows = [json.loads(l) for l in open(a.passages, encoding="utf-8")]
    blocks = [rows[i : i + a.block] for i in range(0, len(rows), a.block)]
    todo = [i for i in range(len(blocks)) if not os.path.exists(os.path.join(a.out_dir, f"block-{i:05d}.jsonl"))]
    if not todo:
        print("all blocks done")
        return

    from vllm import LLM, SamplingParams

    t0 = time.time()
    llm = LLM(model=a.model, revision=a.revision, tokenizer_revision=a.revision, max_model_len=3072,
              gpu_memory_utilization=0.88, seed=1, quantization="awq_marlin", enable_prefix_caching=True, enforce_eager=True)
    sp = SamplingParams(temperature=0.0, max_tokens=a.max_new)
    for i in todo:
        msgs = [[{"role": "user", "content": PROMPT % r["text"]}] for r in blocks[i]]
        outs = llm.chat(msgs, sp, use_tqdm=False)
        path = os.path.join(a.out_dir, f"block-{i:05d}.jsonl")
        with open(path + ".tmp", "w", encoding="utf-8") as f:
            for r, o in zip(blocks[i], outs):
                raw = o.outputs[0].text
                f.write(json.dumps({"id": r["id"], "src": r.get("src"), "text": r["text"], "raw": raw,
                                    "entities": parse(raw, r["text"])}, ensure_ascii=False) + "\n")
        os.replace(path + ".tmp", path)
        print(f"block {i}/{len(blocks)} done at {time.time() - t0:.0f}s", flush=True)
    if a.hours_log:
        with open(a.hours_log, "a") as f:
            f.write("%s\t%s\t%.3f\n" % (time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(t0)),
                                        time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), (time.time() - t0) / 3600))


if __name__ == "__main__":
    main()
