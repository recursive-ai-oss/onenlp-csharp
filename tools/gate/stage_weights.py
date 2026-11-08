#!/usr/bin/env python3
"""Stage every manifest's weights by content, including nested package parts.

stage_weights.py OUT SEARCH_ROOT... [--only English,Multilingual] [--github-env FILE]
stage_weights.py OUT --verify [--github-env FILE]

Staging uses symlinks, never copies. Verification checks sizes and SHA-256 before
printing shell exports (or appending to GitHub's environment file). No downloads.
"""
import argparse
from dataclasses import dataclass
import hashlib
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import sys
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[2]


@dataclass(frozen=True)
class Weight:
    package: str
    group: str
    name: str
    size: int
    digest: str


def safe_name(name):
    p = PurePosixPath(name)
    if not name or name == '.' or p.is_absolute() or '..' in p.parts or '\\' in name or str(p) != name:
        raise ValueError(f'Unsafe weight name: {name!r}')
    return name


def manifests(repo=REPO):
    """Each manifest is an asset; parts share the root package's weights variable."""
    result = {}
    for path in sorted((repo / 'src').rglob('weights.manifest.props')):
        relative = path.relative_to(repo / 'src')
        top = relative.parts[0]
        if relative == Path('OneNlp/LanguageId/weights.manifest.props'):
            package = group = 'LangId'
        elif top.startswith('OneNlp.Models.'):
            package = '.'.join(relative.parts[:-1]).removeprefix('OneNlp.Models.').replace('.Parts.', '.')
            group = top.removeprefix('OneNlp.Models.').removesuffix('.Parts')
            if group.startswith('Embeddings.Multilingual'):
                group = 'Embeddings.E5'
            elif group == 'Multilingual.Large':
                group = 'MlLarge'
        else:
            raise ValueError(f'Unmapped manifest: {path}')
        if package in result:
            raise ValueError(f'Duplicate package: {package}')
        rows = []
        for node in ET.parse(path).iter():
            if 'Sha256' not in node.attrib:
                continue
            name = safe_name(node.attrib['Include'])
            size = int(node.attrib['Size'])
            digest = node.attrib['Sha256'].lower()
            if size < 0 or not re.fullmatch('[0-9a-f]{64}', digest):
                raise ValueError(f'Invalid manifest row: {path}: {name}')
            rows.append(Weight(package, group, name, size, digest))
        if not rows or len({w.name for w in rows}) != len(rows):
            raise ValueError(f'Empty manifest or duplicate names: {path}')
        result[package] = rows
    # Two parts may refer to the same file, but never disagree about its bytes.
    seen = {}
    for rows in result.values():
        for w in rows:
            key = (w.group, w.name)
            value = (w.size, w.digest)
            if key in seen and seen[key] != value:
                raise ValueError(f'Conflicting manifests: {key}')
            seen[key] = value
    if not result:
        raise ValueError('No weight manifests found')
    return result


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1 << 20), b''):
            h.update(block)
    return h.hexdigest()


def verify(out, packages):
    checked = set()
    for rows in packages.values():
        for w in rows:
            p = out / w.group / w.name
            if p in checked:
                continue
            if p.stat().st_size != w.size or sha256(p) != w.digest:
                raise ValueError(f'Weight size or SHA-256 mismatch: {p}')
            checked.add(p)
    return len(checked)


def environment(out, packages, github_env=None):
    lines = []
    for group in sorted({w.group for rows in packages.values() for w in rows}):
        key = 'ONENLP_' + group.upper().replace('.', '_') + '_WEIGHTS'
        value = str(out / group)
        if '\n' in value or '\r' in value:
            raise ValueError('Newline in output directory')
        print(f'export {key}={shlex.quote(value)}')
        lines.append(f'{key}={value}\n')
    if github_env:
        with open(github_env, 'a', encoding='utf-8') as f:
            f.writelines(lines)


def stage(out, roots, packages):
    wanted = {}
    for rows in packages.values():
        for w in rows:
            wanted.setdefault(w.size, {}).setdefault(w.digest, set()).add((w.group, w.name))
    found = {}
    for root in roots:
        if not Path(root).is_dir():
            raise ValueError(f'Not a search directory: {root}')
        for directory, _, files in os.walk(root):
            for name in files:
                p = Path(directory, name).absolute()
                try:
                    size = p.stat().st_size
                except OSError:
                    continue
                by_hash = wanted.get(size)
                if not by_hash or all((size, digest) in found for digest in by_hash):
                    continue
                digest = sha256(p)
                if digest in by_hash:
                    found[(size, digest)] = p
    missing = [w for rows in packages.values() for w in rows if (w.size, w.digest) not in found]
    if missing:
        raise ValueError('Missing weights:\n' + '\n'.join(f'{w.package}/{w.name} {w.digest}' for w in missing))
    for rows in packages.values():
        for w in rows:
            target = out / w.group / w.name
            target.parent.mkdir(parents=True, exist_ok=True)
            source = found[(w.size, w.digest)]
            if target.absolute() == source:
                continue
            if target.is_symlink():
                target.unlink()
            elif target.exists():
                if target.stat().st_size == w.size and sha256(target) == w.digest:
                    continue
                raise ValueError(f'Refusing to overwrite: {target}')
            target.symlink_to(source)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('out', type=Path)
    parser.add_argument('roots', nargs='*')
    parser.add_argument('--only', help='Comma-separated manifest package names')
    parser.add_argument('--verify', action='store_true')
    parser.add_argument('--github-env')
    args = parser.parse_args()
    packages = manifests()
    if args.only:
        names = set(args.only.split(','))
        if names - packages.keys():
            raise ValueError(f'Unknown packages: {names - packages.keys()}')
        packages = {k: v for k, v in packages.items() if k in names}
    out = args.out.absolute()
    if not args.verify:
        if not args.roots:
            parser.error('Supply search roots or --verify')
        stage(out, args.roots, packages)
    count = verify(out, packages)
    print(f'Verified {count} files from {len(packages)} manifests', file=sys.stderr)
    environment(out, packages, args.github_env)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, ET.ParseError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
