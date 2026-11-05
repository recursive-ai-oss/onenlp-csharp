"""Sweeps the table's count and purity thresholds and prints the estimated Words F1 on the development file (see mwt_table.py)."""
import importlib.util, sys, os
spec = importlib.util.spec_from_file_location('mt', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'mwt_table.py'))
src = open(spec.origin, encoding='utf8').read().replace('\nmain()\n', '\n')
mt = type(sys)('mt'); exec(compile(src, 'mt', 'exec'), mt.__dict__)
train, dev = sys.argv[1], sys.argv[2]
toks = list(mt.tokens(dev))
T = len(toks)
G = sum(2 if p is not None else 1 for _, p in toks)
M = sum(1 for _, p in toks if p is not None)
for mc in (1, 2, 3, 5, 8):
    for pur in (0.5, 0.6, 0.7, 0.8, 0.9, 1.0):
        table, _ = mt.build(train, mc, pur)
        tp = fp = 0; p_words = 0
        for s, p in toks:
            k = mt.keyed(s)
            o = mt.outcome(s, p)
            pred = mt.decide(table, k) if k else 0
            if pred > 0:
                p_words += 2
                if o == pred: tp += 1
                elif o is None or o == 0:
                    if o == 0: fp += 1
                # wrong cut or inexpressible: no match
            else:
                p_words += 1
        matched = (T - M - fp) + 2 * tp
        print(f'min {mc} purity {pur}: entries {len(table)} tp {tp} fp {fp} words-F1~{2*matched/(G+p_words)*100:.2f}')
