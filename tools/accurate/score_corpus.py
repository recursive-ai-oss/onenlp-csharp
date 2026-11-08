#!/usr/bin/env python3
"""Score spec-defined predictions with the public evaluation tool.

score_corpus.py SPEC DATA PREDICTIONS EVAL_DLL OUTPUT [--language CODE]
Existing JSON results are retained, so a bounded job can resume between datasets.
"""
import argparse
import json
from pathlib import Path
import subprocess


def annotated_metrics(path):
    """Do not turn an entirely absent gold column into a perfect-accuracy row."""
    present = set()
    for line in path.read_text(encoding='utf-8').splitlines():
        columns = line.split('\t')
        if len(columns) >= 8 and columns[0].isdigit():
            present.update(i for i in (2, 3, 4, 5, 6, 7) if columns[i] != '_')
    metrics = []
    if 3 in present:
        metrics.append('UPOS')
    if 4 in present:
        metrics.append('XPOS')
        if 3 in present:
            metrics.append('AllTags')
    if 5 in present:
        metrics.append('UFeats')
    if 2 in present:
        metrics.append('Lemmas')
    if 6 in present:
        metrics.append('UAS')
        if 7 in present:
            metrics.append('LAS')
            if 3 in present:
                metrics.extend(['CLAS', 'MLAS'])
    return metrics


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    for name in ('spec', 'data', 'predictions', 'eval_dll', 'output'):
        ap.add_argument(name, type=Path)
    ap.add_argument('--language')
    a = ap.parse_args()
    a.output.mkdir(parents=True, exist_ok=True)
    languages = json.loads(a.spec.read_text())['languages']
    for language in languages:
        code = language['code']
        if a.language and a.language != code:
            continue
        gating = next(t for t in language['treebanks'] if t['role'] == 'gating')
        if not gating.get('test'):
            continue
        gold = a.data / gating['test']
        name = gold.stem
        metrics = annotated_metrics(gold)
        for mode, prefix in (('gold', 'pred-'), ('raw', 'pred-raw-')):
            pred = a.predictions / (prefix + name + '.conllu')
            result = a.output / (name + '.' + mode + '.json')
            selected = (['Tokens', 'Sentences', 'Words'] if mode == 'raw' else []) + metrics
            if result.exists():
                # Existing intervals remain valid: filtering absent annotations is
                # reporting metadata, not another model read or a new selection.
                rows = json.loads(result.read_text())
                retained = [r for r in rows if r['metric'] in selected]
                if retained != rows:
                    result.write_text(json.dumps(retained, indent=2) + '\n', encoding='utf-8')
                    result.with_suffix('.md').unlink(missing_ok=True)
                continue
            if not pred.exists():
                continue
            subprocess.run(['dotnet', str(a.eval_dll), 'ud', '--gold', str(gold), '--pred', str(pred),
                            '--metrics', ','.join(selected), '--component', 'accurate-large-' + mode,
                            '--language', code, '--dataset', name, '--resamples', '1000', '--seed', '20261106',
                            '--json', str(result), '--markdown', str(result.with_suffix('.md'))], check=True)
        pred = a.predictions / ('ner-pred-' + code + '-test.iob2')
        result = a.output / ('ner-' + code + '.json')
        if pred.exists() and not result.exists():
            subprocess.run(['dotnet', str(a.eval_dll), 'span', '--gold', str(a.predictions / ('ner-gold-' + code + '-test.iob2')),
                            '--pred', str(pred), '--component', 'accurate-large-ner', '--language', code,
                            '--dataset', 'ner-' + code, '--resamples', '1000', '--seed', '20261106',
                            '--json', str(result), '--markdown', str(result.with_suffix('.md'))], check=True)


if __name__ == '__main__':
    main()
