"""Lists the places where two CoNLL-U files tokenize differently (surface tokens, multiword tokens as one).
usage: token_diff.py gold.conllu pred.conllu [rows]"""
import sys, difflib, collections

def surface_tokens(path):
    toks = []
    skip_to = 0
    for line in open(path, encoding='utf8'):
        line = line.rstrip('\n')
        if not line:
            skip_to = 0
            continue
        if line[0] == '#':
            continue
        c = line.split('\t')
        if '.' in c[0]:
            continue
        if '-' in c[0]:
            a, b = map(int, c[0].split('-'))
            skip_to = b
            toks.append(c[1])
            continue
        if int(c[0]) <= skip_to:
            continue
        toks.append(c[1])
    return toks

g = surface_tokens(sys.argv[1])
p = surface_tokens(sys.argv[2])
print(len(g), len(p))
sm = difflib.SequenceMatcher(None, g, p, autojunk=False)
errs = collections.Counter()
n = 0
for tag, i1, i2, j1, j2 in sm.get_opcodes():
    if tag == 'equal':
        continue
    key = (' '.join(g[i1:i2]), ' '.join(p[j1:j2]))
    errs[key] += 1
    n += 1
print('diff blocks', n)
for (a, b), c in errs.most_common(int(sys.argv[3]) if len(sys.argv) > 3 else 60):
    print(c, '|', a, '|', b)
