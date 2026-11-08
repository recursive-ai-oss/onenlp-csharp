#!/usr/bin/env python3
"""Two published-reference views of the spec-defined accurate tier.

compare_corpus_refs.py SCORES REFERENCES.tsv --spec CORPUS.json [--before SCORES]
A reference is eligible only for the same treebank, input mode and metric. Published
numbers have no paired predictions; the verdict asks whether they exceed our upper
95% bootstrap bound. Different UD releases remain explicitly labelled information.
"""
import argparse
import csv
import json
from pathlib import Path

COLUMNS = {'Tokens': 'tokens', 'Sentences': 'sents', 'Words': 'words', 'UPOS': 'upos',
           'XPOS': 'xpos', 'UFeats': 'ufeats', 'AllTags': 'alltags', 'Lemmas': 'lemmas',
           'UAS': 'uas', 'LAS': 'las', 'CLAS': 'clas', 'MLAS': 'mlas', 'BLEX': 'blex'}


def scores(directory):
    result = {}
    for path in sorted(Path(directory).glob('*.json')):
        mode = 'gold' if path.name.endswith('.gold.json') else 'raw' if path.name.endswith('.raw.json') else None
        if mode is None:
            continue
        for row in json.loads(path.read_text()):
            metric = {'Sents': 'Sentences'}.get(row['metric'], row['metric'])
            if metric in COLUMNS:
                result[(path.name.rsplit('.', 2)[0], mode, metric)] = row['score']
    return result


def eligible(references, treebank, mode, metric, stanza=False):
    candidates = []
    for row in references:
        if row['treebank'] != treebank or row['input'] != mode:
            continue
        if stanza and (row['system'] != 'Stanza' or row['system_version'] != '1.5.1'
                       or row['model'] != 'pretrained, charlm, no transformer'):
            continue
        try:
            value = float(row[COLUMNS[metric]])
        except (ValueError, KeyError):
            continue
        candidates.append((value, row))
    return sorted(candidates, key=lambda x: (-x[0], x[1]['system']))


def cell(candidate, score):
    if not candidate:
        return 'not published', None
    value, row = candidate[0]
    miss = value > score['high']
    text = (f"{value:.2f} ({row['system']} {row['system_version']}, UD {row['ud_release']}; "
            f"test {row['test_changed']}) — " + ('**MISS**' if miss else 'within or ahead'))
    return text, miss


def render(a):
    spec = json.loads(a.spec.read_text())
    catalogue = {}
    if a.treebanks is not None:
        with a.treebanks.open(encoding='utf-8') as stream:
            catalogue = {r['prefix']: r['repo'] for r in csv.DictReader(stream, delimiter='\t')}
    datasets = {}
    for language in spec['languages']:
        for treebank in language['treebanks']:
            if treebank.get('test'):
                dataset = Path(treebank['test']).stem
                # A combined training entry can test another treebank (Italian trains
                # on ParlaMint + MarkIT but its spec points to MarkIT test).
                actual = catalogue.get(dataset.split('-ud-')[0], 'UD_' + treebank['name'])
                datasets[dataset] = (actual, language['code'])
    with a.references.open(encoding='utf-8') as stream:
        references = list(csv.DictReader(stream, delimiter='\t'))
    ours = scores(a.scores)
    before = scores(a.before) if a.before else {}
    rows = []
    counts = {'best': [0, 0], 'stanza': [0, 0], 'all': [0, 0]}
    for key, score in sorted(ours.items()):
        dataset, mode, metric = key
        if dataset not in datasets:
            raise ValueError('Score dataset is not in the corpus specification: ' + dataset)
        treebank, code = datasets[dataset]
        best = eligible(references, treebank, mode, metric)
        stanza = eligible(references, treebank, mode, metric, stanza=True)
        best_cell, bm = cell(best, score)
        stanza_cell, sm = cell(stanza, score)
        for kind, miss in (('best', bm), ('stanza', sm)):
            counts[kind][0] += miss is not None
            counts[kind][1] += miss is True
        counts['all'][0] += len(best)
        counts['all'][1] += sum(value > score['high'] for value, _ in best)
        previous = before.get(key)
        old = f"{previous['estimate']:.2f} [{previous['low']:.2f}, {previous['high']:.2f}]" if previous else 'not measured'
        current = f"{score['estimate']:.2f} [{score['low']:.2f}, {score['high']:.2f}]"
        rows.append(f'| {code}: {treebank} | {mode} | {metric} | {old} | {current} | {best_cell} | {stanza_cell} |')
    print('# Base-size accurate tier: per-row results\n')
    print('Two views of the same rows (no averaging over languages or metrics):')
    for kind, title in (('best', 'Best published system per row'), ('stanza', 'Stanza 1.5.1 pretrained/charlm, no transformer'),
                        ('all', 'All published system comparisons')):
        total, misses = counts[kind]
        print(f'- **{title}: {total} comparisons, {misses} misses**, {total - misses} within the interval or ahead.')
    print('\nIntervals: 95% bootstrap, 1,000 resamples, seed 20261106. The published reference passes when it is no higher '
          'than our upper bound; these are not paired intervals against the reference. Gold and raw input are never crossed. '
          'A missing reference is not a pass. The reference UD version and test-split change flag accompany every comparison; '
          'changed splits are information, not a claim of equal evaluation data. Before is the previous nine-language model, where available.\n')
    print('| Language / treebank | Input | Metric | Before [95% CI] | Current [95% CI] | Best published view | Stanza-only view |')
    print('|---|---|---|---|---|---|---|')
    print('\n'.join(rows))


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('scores', type=Path)
    ap.add_argument('references', type=Path)
    ap.add_argument('--spec', type=Path, required=True)
    ap.add_argument('--before', type=Path)
    ap.add_argument('--treebanks', type=Path, default=Path('tools/fetch/ud/treebanks.tsv'))
    render(ap.parse_args())


if __name__ == '__main__':
    main()
