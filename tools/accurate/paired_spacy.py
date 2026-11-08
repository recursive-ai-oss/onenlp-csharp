#!/usr/bin/env python3
"""Paired bootstrap against the saved, pinned spaCy baselines of the original nine languages.

paired_spacy.py SPEC DATA PREDICTIONS EVAL_DLL REFERENCES OUTPUT
The baseline files are outputs of our black-box harness, not installed package files.
NER uses the same deterministic evenly spaced sentence sample as the recipe.
"""
import argparse
import json
from pathlib import Path
import subprocess


def sentences(path):
    block = []
    for line in path.read_text(encoding='utf-8').splitlines():
        if not line:
            if block:
                yield block
            block = []
        elif not line.startswith('#'):
            block.append(line)
    if block:
        yield block


def sample_indices(count, limit=3000):
    """The binary64 cursor of CorpusReader.NerExamples, not an integer stride."""
    step = count / limit if limit > 0 and count > limit else 1.0
    at = 0.0
    result = []
    while int(at) < count:
        result.append(int(at))
        at += step
    return result


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    for name in ('spec', 'data', 'predictions', 'eval_dll', 'references', 'output'):
        ap.add_argument(name, type=Path)
    a = ap.parse_args()
    a.output.mkdir(parents=True, exist_ok=True)
    for language in json.loads(a.spec.read_text())['languages']:
        if not language['nine']:
            continue
        code = language['code']
        gold = a.data / language['treebanks'][0]['test']
        prefix = gold.stem.removesuffix('-ud-test')
        for mode, model_prefix in (('gold', 'pred-'), ('raw', 'pred-raw-')):
            base = a.references / code / ('pred-ud-' + ('gold' if mode == 'gold' else 'raw') + '.conllu') if code == 'en' else (
                a.references / code / ('pred-ud-' + prefix + '-' + mode + ('-gp' if code == 'zh' and mode == 'gold' else '') + '.conllu'))
            pred = a.predictions / (model_prefix + gold.name)
            if not base.is_file() or not pred.is_file():
                raise FileNotFoundError(f'Missing prediction for {code} {mode}: {base}, {pred}')
            out = a.output / (gold.stem + '.' + mode + '.json')
            if out.exists():
                continue
            subprocess.run(['dotnet', str(a.eval_dll), 'ud', '--gold', str(gold), '--pred', str(pred), '--ref-pred', str(base),
                            '--metrics', 'Tokens,Sentences,Words,UPOS,UFeats,Lemmas,UAS,LAS' if mode == 'raw' else 'UPOS,UFeats,Lemmas,UAS,LAS',
                            '--component', 'accurate-large-paired', '--language', code, '--dataset', gold.stem,
                            '--reference-source', 'spaCy 3.8.18 CLI-default baseline (tools/baselines/spacy)',
                            '--resamples', '1000', '--seed', '20261106', '--json', str(out), '--markdown', str(out.with_suffix('.md'))], check=True)
        if not language['ner']:
            continue
        name = Path(language['ner'][0]['test']).stem.removesuffix('-test')
        base = a.references / code / ('pred-ner.tsv' if code == 'en' else 'pred-ner-' + name + ('-gp' if code == 'zh' else '') + '.span.tsv')
        gold = a.predictions / ('ner-gold-' + code + '-test.iob2')
        pred = a.predictions / ('ner-pred-' + code + '-test.iob2')
        out = a.output / ('ner-' + code + '.json')
        if out.exists():
            continue
        reference = list(sentences(base))
        expected = list(sentences(gold))
        # CorpusReader.NerExamples advances a binary64 cursor by N/3000;
        # reproduce that exact sample, including its possible final extra row.
        reference = [reference[i] for i in sample_indices(len(reference))]
        if len(reference) != len(expected):
            raise ValueError('NER sample length differs for ' + code)
        if [[line.split('\t')[0] for line in s] for s in expected] != [[line.split('\t')[0] for line in s] for s in reference]:
            raise ValueError('NER sentence/token alignment differs for ' + code)
        matched = a.output / ('reference-ner-' + code + '.tsv')
        matched.write_text(''.join('\n'.join(s) + '\n\n' for s in reference), encoding='utf-8')
        subprocess.run(['dotnet', str(a.eval_dll), 'span', '--gold', str(gold), '--pred', str(pred), '--ref-pred', str(matched),
                        '--component', 'accurate-large-paired-ner', '--language', code, '--dataset', name,
                        '--reference-source', 'spaCy 3.8.18 CLI-default baseline (tools/baselines/spacy)',
                        '--resamples', '1000', '--seed', '20261106', '--json', str(out), '--markdown', str(out.with_suffix('.md'))], check=True)


if __name__ == '__main__':
    main()
