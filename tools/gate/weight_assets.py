#!/usr/bin/env python3
"""Build or unpack release assets, always checked against committed manifests.

weight_assets.py create STAGED_DIR ASSET_DIR
weight_assets.py unpack ASSET_DIR OUT_DIR [--github-env FILE]
weight_assets.py list

One uncompressed tar per manifest keeps parts independently below the 2 GiB asset
limit. Checksums cover archives; manifests, not downloaded checksums, trust files.
"""
import argparse
import hashlib
from pathlib import Path
import sys
import tarfile

from stage_weights import environment, manifests, sha256, verify

LIMIT = 2 * 1024 ** 3


def asset_name(package):
    return f'weights-{package}.tar'


def create(staged, assets, packages):
    verify(staged, packages)
    assets.mkdir(parents=True, exist_ok=False)
    checksums = []
    for package, rows in packages.items():
        target = assets / asset_name(package)
        with tarfile.open(target, 'w', format=tarfile.USTAR_FORMAT) as archive:
            for w in rows:
                # Only regular file bytes enter the archive, never staging symlinks.
                info = tarfile.TarInfo(f'{w.group}/{w.name}')
                info.size = w.size
                info.mode = 0o644
                with open(staged / w.group / w.name, 'rb') as source:
                    archive.addfile(info, source)
        if target.stat().st_size >= LIMIT:
            raise ValueError(f'Asset exceeds limit; split its package manifest: {target}')
        checksums.append(f'{sha256(target)}  {target.name}\n')
        print(f'{target.name} {target.stat().st_size}')
    (assets / 'SHA256SUMS').write_text(''.join(checksums), encoding='ascii')


def unpack(assets, out, packages):
    expected = {asset_name(p) for p in packages}
    checksums = {}
    checksum_file = assets / 'SHA256SUMS'
    if checksum_file.stat().st_size > 256 * len(expected):
        raise ValueError('Oversized checksum file')
    for line in checksum_file.read_text(encoding='ascii').splitlines():
        digest, name = line.split('  ')
        if name in checksums or name not in expected:
            raise ValueError(f'Unexpected or duplicate checksum: {name}')
        checksums[name] = digest
    if checksums.keys() != expected or {p.name for p in assets.iterdir()} != expected | {'SHA256SUMS'}:
        raise ValueError('Missing or unexpected assets/checksums')
    # A fresh output and rejecting link members prevent an archive from redirecting writes.
    out.mkdir(parents=True, exist_ok=False)
    for package, rows in packages.items():
        asset = assets / asset_name(package)
        if asset.stat().st_size >= LIMIT or sha256(asset) != checksums[asset.name]:
            raise ValueError(f'Asset size or SHA-256 mismatch: {asset}')
        wanted = {f'{w.group}/{w.name}': w for w in rows}
        seen = set()
        # Streaming and a strict allowlist bound memory and writes, and reject links,
        # traversal, duplicate members and extra data even with valid archive checksums.
        with tarfile.open(asset, 'r|') as archive:
            for member in archive:
                w = wanted.get(member.name)
                if w is None or member.name in seen or not member.isfile() or member.size != w.size:
                    raise ValueError(f'Unexpected archive member: {asset.name}: {member.name}')
                seen.add(member.name)
                target = out / w.group / w.name
                target.parent.mkdir(parents=True, exist_ok=True)
                # Shared files in two manifests must already have the same digest.
                temporary = target.with_name(target.name + '.partial')
                h = hashlib.sha256()
                with archive.extractfile(member) as source, open(temporary, 'xb') as dest:
                    for block in iter(lambda: source.read(1 << 20), b''):
                        h.update(block)
                        dest.write(block)
                if h.hexdigest() != w.digest:
                    temporary.unlink()
                    raise ValueError(f'Weight SHA-256 mismatch: {member.name}')
                temporary.replace(target)
        if seen != wanted.keys():
            raise ValueError(f'Missing members: {asset.name}: {wanted.keys() - seen}')
    verify(out, packages)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['create', 'unpack', 'list'])
    parser.add_argument('source', nargs='?', type=Path)
    parser.add_argument('destination', nargs='?', type=Path)
    parser.add_argument('--github-env')
    args = parser.parse_args()
    packages = manifests()
    if args.command == 'list':
        print('\n'.join([asset_name(p) for p in packages] + ['SHA256SUMS']))
        return
    if args.source is None or args.destination is None:
        parser.error('source and destination are required')
    if args.command == 'create':
        create(args.source, args.destination, packages)
    else:
        out = args.destination.absolute()
        unpack(args.source, out, packages)
        environment(out, packages, args.github_env)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, tarfile.TarError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
