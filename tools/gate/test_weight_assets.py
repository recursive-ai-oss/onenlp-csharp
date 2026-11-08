#!/usr/bin/env python3
"""Small synthetic tests for manifest discovery, staging and hostile assets."""
import hashlib
import io
import os
from pathlib import Path
import tarfile
import tempfile
import unittest

from stage_weights import Weight, manifests, stage, verify
from weight_assets import create, unpack


class WeightAssetsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=os.environ.get('TMPDIR'))
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        (self.source / 'data').write_bytes(b'synthetic weights')
        self.w = Weight('Multilingual.Large.Heads1', 'MlLarge', 'heads/x.onenlp', 17,
                        hashlib.sha256(b'synthetic weights').hexdigest())
        self.packages = {self.w.package: [self.w]}
        self.staged = self.root / 'stage'
        self.assets = self.root / 'assets'
        self.out = self.root / 'out'

    def make_assets(self):
        stage(self.staged, [self.source], self.packages)
        create(self.staged, self.assets, self.packages)

    def rewrite_asset(self, name, payload=b'synthetic weights', kind=tarfile.REGTYPE):
        asset = next(self.assets.glob('*.tar'))
        with tarfile.open(asset, 'w') as f:
            member = tarfile.TarInfo(name)
            member.type = kind
            member.size = len(payload) if kind == tarfile.REGTYPE else 0
            member.linkname = '../../outside' if kind == tarfile.SYMTYPE else ''
            f.addfile(member, io.BytesIO(payload))
        (self.assets / 'SHA256SUMS').write_text(f'{hashlib.sha256(asset.read_bytes()).hexdigest()}  {asset.name}\n')

    def test_round_trip_and_manifest_discovery(self):
        discovered = manifests()
        self.assertIn('Multilingual.Large.Heads11', discovered)
        self.assertIn('Multilingual.FrozenEncoder', discovered)
        self.assertEqual('Embeddings.E5', discovered['Embeddings.Multilingual.Shard3'][0].group)
        self.assertIn('LangId', discovered)
        self.make_assets()
        unpack(self.assets, self.out, self.packages)
        self.assertEqual(1, verify(self.out, self.packages))
        self.assertFalse((self.out / 'MlLarge/heads/x.onenlp').is_symlink())

    def test_missing_and_modified_stage(self):
        with self.assertRaises(ValueError):
            stage(self.staged, [], self.packages)
        self.make_assets()
        (self.source / 'data').write_bytes(b'wrong bytes')
        with self.assertRaises(ValueError):
            verify(self.staged, self.packages)

    def test_checksum_mismatch(self):
        self.make_assets()
        next(self.assets.glob('*.tar')).write_bytes(b'truncated')
        with self.assertRaises(ValueError):
            unpack(self.assets, self.out, self.packages)

    def test_untrusted_archive_contents(self):
        for name, payload, kind in [('../outside', b'synthetic weights', tarfile.REGTYPE),
                                     ('MlLarge/heads/x.onenlp', b'wrong', tarfile.REGTYPE),
                                     ('MlLarge/heads/x.onenlp', b'', tarfile.SYMTYPE),
                                     ('MlLarge/heads/x.onenlp', b'changed weights!!', tarfile.REGTYPE)]:
            with self.subTest(name=name, kind=kind, payload=payload):
                if not self.assets.exists():
                    self.make_assets()
                self.rewrite_asset(name, payload, kind)
                dest = self.root / f'out-{len(list(self.root.iterdir()))}'
                with self.assertRaises(ValueError):
                    unpack(self.assets, dest, self.packages)

    def test_missing_duplicate_and_truncated_members(self):
        self.make_assets()
        asset = next(self.assets.glob('*.tar'))
        for variant in ['empty', 'duplicate', 'truncated']:
            with self.subTest(variant=variant):
                with tarfile.open(asset, 'w') as archive:
                    if variant != 'empty':
                        for _ in range(2 if variant == 'duplicate' else 1):
                            member = tarfile.TarInfo('MlLarge/heads/x.onenlp')
                            member.size = 17
                            archive.addfile(member, io.BytesIO(b'synthetic weights'))
                if variant == 'truncated':
                    asset.write_bytes(asset.read_bytes()[:515])
                (self.assets / 'SHA256SUMS').write_text(
                    f'{hashlib.sha256(asset.read_bytes()).hexdigest()}  {asset.name}\n')
                with self.assertRaises((ValueError, tarfile.TarError)):
                    unpack(self.assets, self.root / variant, self.packages)

    def test_unknown_asset_and_missing_archive(self):
        self.make_assets()
        (self.assets / 'unexpected').touch()
        with self.assertRaises(ValueError):
            unpack(self.assets, self.out, self.packages)
        (self.assets / 'unexpected').unlink()
        next(self.assets.glob('*.tar')).unlink()
        with self.assertRaises(ValueError):
            unpack(self.assets, self.out, self.packages)


if __name__ == '__main__':
    unittest.main()
