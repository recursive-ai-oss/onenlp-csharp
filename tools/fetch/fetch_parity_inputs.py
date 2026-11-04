#!/usr/bin/env python3
"""Fetches the raw text the parity corpus is built from.

  fetch_parity_inputs.py --out DIR

Writes DIR/udhr/ (the Unicode UDHR-in-Unicode data tree, Unicode licence) and DIR/sentences.csv (Tatoeba sentence export, tab
separated id, language, text; sentences carry mostly CC BY 2.0 FR terms, some are CC0 - see https://tatoeba.org/en/terms_of_use).
Both are upstream files that are republished over time, so the SHA-256 of the archives are pins: a different digest is reported
and the run fails unless --accept-changed is given, in which case the corpus built from them has a different hash.
Never committed. Standard library only.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import shutil
import sys
import tarfile
import urllib.request

SOURCES = {
    "udhr": ("https://github.com/unicode-org/udhr/archive/refs/heads/main.tar.gz", "4086b12bf529185934944d8cc003fea2476b0b052e29aada35ef7e8431f689ed"),
    "tatoeba": ("https://downloads.tatoeba.org/exports/sentences.tar.bz2", "7821ce661c3c093c32cd864c7e1d089c528a3de8a3f6ab6a2127f1eff5ebf36a"),
}


def download(url: str, path: str) -> str:
    h = hashlib.sha256()
    req = urllib.request.Request(url, headers={"User-Agent": "onenlp-parity-fetch"})
    with urllib.request.urlopen(req, timeout=600) as r, open(path, "wb") as f:
        while True:
            b = r.read(1 << 20)
            if not b:
                break
            h.update(b)
            f.write(b)
    return h.hexdigest()


def sha_of(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def safe_members(tar: tarfile.TarFile):
    for m in tar.getmembers():
        parts = m.name.split("/")
        if m.name.startswith("/") or ".." in parts or not (m.isfile() or m.isdir()):
            continue
        yield m


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--accept-changed", action="store_true")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    rc = 0
    for name, (url, pin) in SOURCES.items():
        archive = os.path.join(a.out, "udhr.tar.gz" if name == "udhr" else "tatoeba_sentences.tar.bz2")
        if not os.path.exists(archive):
            download(url, archive)
        got = sha_of(archive)
        print(f"{name}: sha256 {got} ({'pinned' if got == pin else 'DIFFERENT from pin ' + pin})")
        if got != pin and not a.accept_changed:
            rc = 1
            continue
        if name == "udhr":
            dest = os.path.join(a.out, "udhr")
            if not os.path.isdir(dest):
                with tarfile.open(archive) as tar:
                    for m in safe_members(tar):
                        m.name = m.name.split("/", 1)[1] if "/" in m.name else ""
                        if m.name:
                            tar.extract(m, dest, filter="data")
        else:
            dest = os.path.join(a.out, "sentences.csv")
            if not os.path.exists(dest):
                with tarfile.open(archive, "r:bz2") as tar:
                    member = next(m for m in tar.getmembers() if m.name.endswith("sentences.csv"))
                    with tar.extractfile(member) as src, open(dest, "wb") as out:
                        shutil.copyfileobj(src, out)
    return rc


if __name__ == "__main__":
    sys.exit(main())
