#!/usr/bin/env python3
"""Collects the weight files that model packages pack, so a pack or an evaluation uses exactly the bytes the manifests name.

    stage_weights.py <out-dir> <search-root>... [--only Name,Name]

Reads every src/OneNlp.Models.*/weights.manifest.props (rows with Include, Size and Sha256), finds a file of that size and hash under
the search roots, and symlinks it as <out-dir>/<Package>/<name>. Prints one `export ONENLP_<PACKAGE>_WEIGHTS=...` line per package
(the variable each project reads) and exits 1 if any file is missing. Python standard library only.
"""
import hashlib
import os
import re
import sys

ROW = re.compile(r'Include="([^"]+)"[^>]*?\bSize="(\d+)"[^>]*?\bSha256="([0-9a-fA-F]{64})"', re.S)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def main(argv):
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1].split(","))
        del argv[i:i + 2]
    out, roots = os.path.abspath(argv[0]), argv[1:]
    repo = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    src = os.path.join(repo, "src")
    wanted = {}  # (size) -> list of (package, name, sha)
    packages = []
    for d in sorted(os.listdir(src)):
        manifest = os.path.join(src, d, "weights.manifest.props")
        if not d.startswith("OneNlp.Models.") or not os.path.exists(manifest):
            continue
        package = d[len("OneNlp.Models."):]
        if only and package not in only:
            continue
        packages.append(package)
        text = open(manifest, encoding="utf-8").read()
        for name, size, sha in ROW.findall(text):
            wanted.setdefault(int(size), []).append((package, name, sha.lower()))
    found = {}
    cache = {}
    for root in roots:
        for dirpath, _, files in os.walk(root):
            for fn in files:
                p = os.path.join(dirpath, fn)
                try:
                    size = os.path.getsize(p)
                except OSError:
                    continue
                if size not in wanted:
                    continue
                digest = cache.get(p) or sha256(p)
                cache[p] = digest
                for package, name, sha in wanted[size]:
                    if sha == digest and (package, name) not in found:
                        found[(package, name)] = p
    missing = 0
    for package in packages:
        for size, rows in wanted.items():
            for pk, name, sha in rows:
                if pk != package:
                    continue
                target_dir = os.path.join(out, package)
                os.makedirs(target_dir, exist_ok=True)
                src_path = found.get((pk, name))
                if src_path is None:
                    print(f"MISSING {package}/{name} {sha}", file=sys.stderr)
                    missing += 1
                    continue
                link = os.path.join(target_dir, name)
                if os.path.lexists(link):
                    os.remove(link)
                os.symlink(src_path, link)
        print(f"export ONENLP_{package.upper().replace('.', '_')}_WEIGHTS={os.path.join(out, package)}")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
