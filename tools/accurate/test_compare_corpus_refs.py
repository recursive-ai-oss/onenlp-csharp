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

import compare_corpus_refs as report
import stage_large_weights as stage


class ReferencesTests(unittest.TestCase):
    def row(self, **extra):
        return dict(treebank='UD_Finnish-FTB', input='raw', system='Stanza', system_version='1.5.1',
                    model='pretrained, charlm, no transformer', upos='91', ud_release='2.12', test_changed='changed', **extra)

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

    def test_same_treebank_mode_and_model(self):
        good = self.row()
        others = [dict(good, treebank='UD_Finnish-TDT'), dict(good, input='gold'),
                  dict(good, model='transformer', upos='99'), dict(good, system_version='1.6', upos='98')]
        self.assertEqual(report.eligible([good] + others, 'UD_Finnish-FTB', 'raw', 'UPOS', True), [(91.0, good)])
        self.assertEqual(report.eligible([good] + others, 'UD_Finnish-FTB', 'raw', 'UPOS')[0][0], 99)
        self.assertEqual(report.eligible([good], 'UD_Finnish-FTB', 'gold', 'UPOS'), [])

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
