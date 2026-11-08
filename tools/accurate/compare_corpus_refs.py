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
          'changed splits are information, not a claim of equal evaluation data. Entirely absent gold columns are not scored. '
          'Before is the previous nine-language model, where available.\n')
    print('| Language / treebank | Input | Metric | Before [95% CI] | Current [95% CI] | Best published view | Stanza-only view |')
    print('|---|---|---|---|---|---|---|')
    print('\n'.join(rows))


def interval(score):
    return f"{score['estimate']:.2f} [{score['low']:.2f}, {score['high']:.2f}]"


def render_entities(directory, before):
    print('\n## Entity samples (gold tokens)\n')
    print('Deterministic evenly spaced samples, at most 3,001 sentences per corpus. '
          'The published UD references above do not supply comparable entity predictions; '
          'these rows are information. The paired spaCy view below uses identical sampled sentences.\n')
    print('| Language | Metric | Before [95% CI] | Current [95% CI] |')
    print('|---|---|---|---|')
    for path in sorted(directory.glob('ner-*.json')):
        old_path = before / path.name if before else None
        old = {r['metric']: r['score'] for r in json.loads(old_path.read_text())} if old_path and old_path.exists() else {}
        for row in json.loads(path.read_text()):
            previous = interval(old[row['metric']]) if row['metric'] in old else 'not measured'
            print(f"| {row['language']} | {row['metric']} | {previous} | {interval(row['score'])} |")


def render_paired(directory):
    rows = [row for path in sorted(directory.glob('*.json')) for row in json.loads(path.read_text())]
    counts = {'ahead': 0, 'within': 0, 'behind': 0}
    for row in rows:
        difference = row['difference']
        verdict = 'ahead' if difference['low'] > 0 else 'behind' if difference['high'] < 0 else 'within'
        counts[verdict] += 1
    print('\n## Paired spaCy 3.8.18 comparison (information)\n')
    print(f"{len(rows)} rows: {counts['ahead']} ahead, {counts['within']} within the paired interval, {counts['behind']} behind. "
          'Same gold, mode and sampled entity sentences; 1,000 paired resamples, seed 20261106. '
          'This is not the published-system gate. Positive differences favor this model.\n')
    print('| Language / dataset | Input | Metric | Current [95% CI] | spaCy [95% CI] | Difference [95% CI] | Verdict |')
    print('|---|---|---|---|---|---|---|')
    for path in sorted(directory.glob('*.json')):
        mode = 'entities (gold)' if path.name.startswith('ner-') else 'gold' if '.gold.' in path.name else 'raw'
        for row in json.loads(path.read_text()):
            d = row['difference']
            verdict = 'ahead' if d['low'] > 0 else 'behind' if d['high'] < 0 else 'within'
            reference = interval(row['reference_interval']) if 'reference_interval' in row else f"{row['reference']:.2f}"
            print(f"| {row['language']}: {row['dataset']} | {mode} | {row['metric']} | {interval(row['score'])} | "
                  f"{reference} | {interval(d)} | {verdict} |")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('scores', type=Path)
    ap.add_argument('references', type=Path)
    ap.add_argument('--spec', type=Path, required=True)
    ap.add_argument('--before', type=Path)
    ap.add_argument('--treebanks', type=Path, default=Path('tools/fetch/ud/treebanks.tsv'))
    ap.add_argument('--paired', type=Path, help='Paired spaCy score JSON directory')
    a = ap.parse_args()
    render(a)
    render_entities(a.scores, a.before)
    if a.paired:
        render_paired(a.paired)


if __name__ == '__main__':
    main()
