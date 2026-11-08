#!/usr/bin/env python3
"""Score spec-defined predictions with the public evaluation tool.

score_corpus.py SPEC DATA PREDICTIONS EVAL_DLL OUTPUT [--language CODE]
Existing JSON results are retained, so a bounded job can resume between datasets.
"""
import argparse
import json
from pathlib import Path
import subprocess


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
        # A missing annotation is not a perfect score. Only report XPOS when the treebank has it.
        xpos = any(len(c := line.split('\t')) > 4 and c[0].isdigit() and c[4] != '_'
                   for line in gold.read_text().splitlines() if line and not line.startswith('#'))
        metrics = ['UPOS', 'UFeats', 'Lemmas', 'UAS', 'LAS', 'CLAS', 'MLAS']
        if xpos:
            metrics[1:1] = ['XPOS', 'AllTags']
        for mode, prefix in (('gold', 'pred-'), ('raw', 'pred-raw-')):
            pred = a.predictions / (prefix + name + '.conllu')
            result = a.output / (name + '.' + mode + '.json')
            if not pred.exists() or result.exists():
                continue
            selected = (['Tokens', 'Sentences', 'Words'] if mode == 'raw' else []) + metrics
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
