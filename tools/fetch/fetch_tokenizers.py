#!/usr/bin/env python3
"""Fetches the tokenizer model files the parity harness runs against, and verifies them against the pins in tokenizer-models.lock.json.

  fetch_tokenizers.py --out DIR              download what is missing, verify every file against the lock
  fetch_tokenizers.py --out DIR --update-lock  (re)write the lock from what is on disk, after fetching (reviewed by hand)

Layout under DIR: <org>_<model>/<file> for Hugging Face repositories, tiktoken/<name>.tiktoken for Tiktoken encodings, SHA256SUMS
(sha256sum format) and, per repository, api.json (the Hub's description of the repository, kept for its commit SHA and licence).
Files are requested at the pinned commit SHA, so the same bytes come back later. Nothing here is committed to the repository.
Standard library only.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
MODELS = os.path.join(HERE, "tokenizer-models.json")
LOCK = os.path.join(HERE, "tokenizer-models.lock.json")


def get(url: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "onenlp-parity-fetch"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def sha256(b: bytes) -> str:
    return hashlib.sha256(b).hexdigest()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--update-lock", action="store_true")
    a = ap.parse_args()
    with open(MODELS, encoding="utf-8") as f:
        models = json.load(f)
    lock = {"hf": {}, "tiktoken": {}}
    if os.path.exists(LOCK):
        with open(LOCK, encoding="utf-8") as f:
            lock = json.load(f)
    new_lock = {"hf": {}, "tiktoken": {}}
    sums: list[tuple[str, str]] = []
    bad = 0

    for m in models["hf"]:
        repo, d = m["repo"], os.path.join(a.out, m["dir"])
        os.makedirs(d, exist_ok=True)
        api_path = os.path.join(d, "api.json")
        pinned = lock["hf"].get(repo)
        if not os.path.exists(api_path):
            with open(api_path, "wb") as f:
                f.write(get(f"https://huggingface.co/api/models/{repo}"))
        with open(api_path, encoding="utf-8") as f:
            api = json.load(f)
        sha = pinned["revision"] if pinned else api["sha"]
        licence = (api.get("cardData") or {}).get("license") or next((t.split(":", 1)[1] for t in api.get("tags", []) if t.startswith("license:")), "unknown")
        entry = {"revision": sha, "license": licence, "files": {}}
        for name in m["files"]:
            p = os.path.join(d, name)
            url = f"https://huggingface.co/{repo}/resolve/{sha}/{name}"
            if not os.path.exists(p):
                data = get(url)
                with open(p, "wb") as f:
                    f.write(data)
            with open(p, "rb") as f:
                h = sha256(f.read())
            entry["files"][name] = {"url": url, "sha256": h}
            sums.append((h, f"./{m['dir']}/{name}"))
            want = pinned and pinned["files"].get(name, {}).get("sha256")
            if want and want != h:
                print(f"MISMATCH {repo}/{name}: have {h}, lock says {want}", file=sys.stderr)
                bad += 1
        new_lock["hf"][repo] = entry

    for t in models["tiktoken"]:
        d = os.path.join(a.out, "tiktoken")
        os.makedirs(d, exist_ok=True)
        p = os.path.join(d, t["name"] + ".tiktoken")
        if not os.path.exists(p):
            with open(p, "wb") as f:
                f.write(get(t["url"]))
        with open(p, "rb") as f:
            h = sha256(f.read())
        new_lock["tiktoken"][t["name"]] = {"url": t["url"], "sha256": h, "license": t["license"]}
        sums.append((h, f"./tiktoken/{t['name']}.tiktoken"))
        want = lock["tiktoken"].get(t["name"], {}).get("sha256")
        if want and want != h:
            print(f"MISMATCH {t['name']}: have {h}, lock says {want}", file=sys.stderr)
            bad += 1

    with open(os.path.join(a.out, "SHA256SUMS.families"), "w", encoding="utf-8") as f:
        for h, name in sorted(sums, key=lambda x: x[1].lower()):
            f.write(f"{h}  {name}\n")
    if a.update_lock:
        with open(LOCK, "w", encoding="utf-8") as f:
            json.dump(new_lock, f, indent=2, sort_keys=True)
            f.write("\n")
    print(f"{len(sums)} files, {bad} mismatches against the lock")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
