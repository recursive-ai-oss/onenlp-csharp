#!/usr/bin/env python3
"""Synthetic checks for reference eligibility and two-view reporting."""
import contextlib
import csv
import io
import json
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

import compare_corpus_refs as report
import stage_large_weights as stage
import paired_spacy as paired


class ReferencesTests(unittest.TestCase):
    def row(self, **extra):
        return dict(treebank='UD_Finnish-FTB', input='raw', system='Stanza', system_version='1.5.1',
                    model='pretrained, charlm, no transformer', upos='91', ud_release='2.12', test_changed='changed', **extra)

    def test_ner_pairing_uses_the_recipe_sample_not_a_prefix(self):
        self.assertEqual(paired.sample_indices(5), [0, 1, 2, 3, 4])
        self.assertEqual(paired.sample_indices(10, 3), [0, 3, 6])
        selected = paired.sample_indices(6468)
        self.assertEqual(3001, len(selected))
        self.assertEqual(6467, selected[-1])
        self.assertEqual([0, 2, 4, 6, 8], selected[:5])

    def test_head_parts_bound_and_order(self):
        with tempfile.TemporaryDirectory() as d:
            files = []
            for code, size in [('b', 6), ('a', 4), ('c', 7)]:
                path = Path(d) / code
                path.write_bytes(bytes(size))
                files.append((code, path))
            self.assertEqual([[code for code, _ in g] for g in stage.groups(files, 10)], [['a', 'b'], ['c']])
            with self.assertRaises(ValueError):
                stage.groups(files, 5)

    def test_generated_heads_are_transitive_assets_and_keep_bytes(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            repo, export, weights = root / 'repo', root / 'export', root / 'weights'
            package = repo / 'src/OneNlp.Models.Multilingual.Large'
            package.mkdir(parents=True)
            parts = repo / 'src/OneNlp.Models.Multilingual.Large.Parts'
            for name in ['Encoder1', 'Encoder2', 'TokenTable1', 'TokenTable2', 'TokenTable3', 'TokenTable4', 'Chinese']:
                (parts / name).mkdir(parents=True)
            (export / 'heads').mkdir(parents=True)
            languages = sorted(stage.NINE | {'cs'})
            for code in languages:
                (export / 'heads' / (code + '.onenlp')).write_bytes(code.encode())
            (export / 'encoder.onnx').write_bytes(b'0123456789ab')
            (export / 'tokenizer.json').write_text('{}')
            spec = root / 'corpus.json'
            spec.write_text(json.dumps({'languages': [{'code': code} for code in languages]}))
            (repo / 'OneNlp.slnx').write_text(
                '    <Project Path="src/OneNlp.Models.Multilingual.Large/OneNlp.Models.Multilingual.Large.csproj" />')
            with patch('sys.argv', ['stage', str(export), str(weights), '--repo', str(repo), '--spec', str(spec)]):
                with contextlib.redirect_stdout(io.StringIO()):
                    stage.main()
            generated = parts / 'Heads1'
            targets = (generated / 'RecursiveAI.OneNlp.Models.Multilingual.Large.Heads1.targets').read_text()
            self.assertIn('$(MSBuildThisFileDirectory)../weights/multilingual-large/heads/cs.onenlp', targets)
            self.assertIn('CopyToPublishDirectory="PreserveNewest"', targets)
            self.assertIn('<PartFiles>true</PartFiles>', (generated / 'OneNlp.Models.Multilingual.Large.Heads1.csproj').read_text())
            self.assertIn('"cs" => "cs.onenlp"', (generated / 'Part.cs').read_text())
            self.assertEqual(b'cs', (weights / 'heads/cs.onenlp').read_bytes())
            self.assertEqual(b'0123456789ab', b''.join((weights / f'encoder.onnx.part{i}').read_bytes() for i in range(1, 7)))

    def test_same_treebank_mode_and_model(self):
        good = self.row()
        others = [dict(good, treebank='UD_Finnish-TDT'), dict(good, input='gold'),
                  dict(good, model='transformer', upos='99'), dict(good, system_version='1.6', upos='98')]
        self.assertEqual(report.eligible([good] + others, 'UD_Finnish-FTB', 'raw', 'UPOS', True), [(91.0, good)])
        self.assertEqual(report.eligible([good] + others, 'UD_Finnish-FTB', 'raw', 'UPOS')[0][0], 99)
        self.assertEqual(report.eligible([good], 'UD_Finnish-FTB', 'gold', 'UPOS'), [])

    def test_paired_rows_keep_modes_and_direction(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            score = {'estimate': 92, 'low': 90, 'high': 94}
            row = dict(language='fi', dataset='fi_ftb-ud-test', metric='UPOS', score=score,
                       reference=90, difference={'estimate': 2, 'low': 1, 'high': 3})
            (root / 'fi_ftb-ud-test.gold.json').write_text(json.dumps([row]))
            (root / 'fi_ftb-ud-test.raw.json').write_text(json.dumps([
                dict(row, difference={'estimate': -2, 'low': -3, 'high': -1})]))
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                report.render_paired(root)
            text = out.getvalue()
            self.assertIn('2 rows: 1 ahead, 0 within the paired interval, 1 behind', text)
            self.assertIn('| gold | UPOS |', text)
            self.assertIn('| raw | UPOS |', text)

    def test_two_views_counts_and_missing_reference(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            spec = root / 'corpus.json'
            spec.write_text(json.dumps({'languages': [{'code': 'fi', 'treebanks': [
                {'name': 'Finnish-FTB', 'test': 'eval/fi_ftb-ud-test.conllu'}]}]}))
            score = {'estimate': 92, 'low': 90, 'high': 94}
            (root / 'fi_ftb-ud-test.raw.json').write_text(json.dumps([
                {'metric': 'UPOS', 'score': score}, {'metric': 'LAS', 'score': score}]))
            refs = root / 'refs.tsv'
            rows = [self.row(), dict(self.row(), system='Other', upos='96')]
            with refs.open('w') as f:
                writer = csv.DictWriter(f, fieldnames=rows[0].keys(), delimiter='\t')
                writer.writeheader()
                writer.writerows(rows)
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                report.render(types.SimpleNamespace(spec=spec, scores=root, references=refs, before=None, treebanks=None))
            text = out.getvalue()
            self.assertIn('Best published system per row: 1 comparisons, 1 misses', text)
            self.assertIn('Stanza 1.5.1 pretrained/charlm, no transformer: 1 comparisons, 0 misses', text)
            self.assertIn('| LAS | not measured | 92.00 [90.00, 94.00] | not published | not published |', text)
            self.assertIn('UD_Finnish-FTB', text)


if __name__ == '__main__':
    unittest.main()
