import sys, collections
whole = collections.Counter(); split = collections.Counter()
for p in sys.argv[1:]:
    sent = []
    for line in open(p, encoding='utf-8'):
        line = line.rstrip('\n')
        if not line:
            prev = None
            skip = 0
            for c in sent:
                if '-' in c[0]:
                    a, b = c[0].split('-'); skip = int(b)
                    if c[1].lower() in ('dei', 'delle', 'degli'):
                        split[prev] += 1
                    prev = c[1].lower()  # surface form of the MWT as the word before
                    continue
                if skip and int(c[0]) <= skip:
                    if int(c[0]) == skip: skip = 0
                    continue
                if c[1].lower() in ('dei', 'delle', 'degli'):
                    whole[prev] += 1
                prev = c[1].lower()
            sent = []
            continue
        if line.startswith('#'):
            continue
        c = line.split('\t')
        if '.' in c[0]:
            continue
        sent.append(c)
print('whole after:', sorted(whole.items(), key=lambda kv: -kv[1]))
print('split after:', sorted(split.items(), key=lambda kv: -kv[1]))
