"""Generates the inputs for the matcher differential test: documents with hand-set annotations, token patterns, phrase lists
and entity-ruler rule sets, all built from a small vocabulary so that patterns match often. Pure Python, deterministic.

usage: python generate_cases.py OUT.json
"""
import json
import random
import sys

SEED = 20261104

# surface, lemma, pos, tag, morph, dep
WORDS = [
    ("the", "the", "DET", "DT", "Definite=Def|PronType=Art", "det"),
    ("The", "the", "DET", "DT", "Definite=Def|PronType=Art", "det"),
    ("a", "a", "DET", "DT", "Definite=Ind|PronType=Art", "det"),
    ("an", "a", "DET", "DT", "Definite=Ind|PronType=Art", "det"),
    ("cat", "cat", "NOUN", "NN", "Number=Sing", "nsubj"),
    ("cats", "cat", "NOUN", "NNS", "Number=Plur", "nsubj"),
    ("dog", "dog", "NOUN", "NN", "Number=Sing", "dobj"),
    ("dogs", "dog", "NOUN", "NNS", "Number=Plur", "dobj"),
    ("mat", "mat", "NOUN", "NN", "Number=Sing", "pobj"),
    ("sat", "sit", "VERB", "VBD", "Tense=Past|VerbForm=Fin", "ROOT"),
    ("sits", "sit", "VERB", "VBZ", "Number=Sing|Person=3|Tense=Pres|VerbForm=Fin", "ROOT"),
    ("is", "be", "AUX", "VBZ", "Number=Sing|Person=3|Tense=Pres|VerbForm=Fin", "ROOT"),
    ("are", "be", "AUX", "VBP", "Number=Plur|Tense=Pres|VerbForm=Fin", "ROOT"),
    ("running", "run", "VERB", "VBG", "Tense=Pres|VerbForm=Part", "ROOT"),
    ("runs", "run", "VERB", "VBZ", "Number=Sing|Person=3|Tense=Pres|VerbForm=Fin", "ROOT"),
    ("quickly", "quickly", "ADV", "RB", "", "advmod"),
    ("big", "big", "ADJ", "JJ", "Degree=Pos", "amod"),
    ("Big", "big", "ADJ", "JJ", "Degree=Pos", "amod"),
    ("small", "small", "ADJ", "JJ", "Degree=Pos", "amod"),
    ("on", "on", "ADP", "IN", "", "prep"),
    ("in", "in", "ADP", "IN", "", "prep"),
    ("of", "of", "ADP", "IN", "", "prep"),
    ("and", "and", "CCONJ", "CC", "", "cc"),
    ("or", "or", "CCONJ", "CC", "", "cc"),
    ("not", "not", "PART", "RB", "Polarity=Neg", "neg"),
    ("it", "it", "PRON", "PRP", "Case=Nom|Gender=Neut|Number=Sing|Person=3|PronType=Prs", "nsubj"),
    ("I", "I", "PRON", "PRP", "Case=Nom|Number=Sing|Person=1|PronType=Prs", "nsubj"),
    ("we", "we", "PRON", "PRP", "Case=Nom|Number=Plur|Person=1|PronType=Prs", "nsubj"),
    ("Apple", "apple", "PROPN", "NNP", "Number=Sing", "nsubj"),
    ("apple", "apple", "NOUN", "NN", "Number=Sing", "dobj"),
    ("John", "John", "PROPN", "NNP", "Number=Sing", "nsubj"),
    ("Mary", "Mary", "PROPN", "NNP", "Number=Sing", "nsubj"),
    ("Paris", "Paris", "PROPN", "NNP", "Number=Sing", "pobj"),
    ("London", "London", "PROPN", "NNP", "Number=Sing", "pobj"),
    ("New", "New", "PROPN", "NNP", "Number=Sing", "compound"),
    ("York", "York", "PROPN", "NNP", "Number=Sing", "pobj"),
    ("Acme", "Acme", "PROPN", "NNP", "Number=Sing", "compound"),
    ("Corp", "Corp", "PROPN", "NNP", "Number=Sing", "nsubj"),
    ("hello", "hello", "INTJ", "UH", "", "intj"),
    ("Hello", "hello", "INTJ", "UH", "", "intj"),
    ("WORLD", "world", "NOUN", "NN", "Number=Sing", "pobj"),
    ("world", "world", "NOUN", "NN", "Number=Sing", "pobj"),
    ("iPhone", "iPhone", "PROPN", "NNP", "Number=Sing", "dobj"),
    ("2024", "2024", "NUM", "CD", "NumType=Card", "nummod"),
    ("10", "10", "NUM", "CD", "NumType=Card", "nummod"),
    ("3.14", "3.14", "NUM", "CD", "NumType=Card", "nummod"),
    ("1,000", "1,000", "NUM", "CD", "NumType=Card", "nummod"),
    ("ten", "ten", "NUM", "CD", "NumType=Card", "nummod"),
    ("first", "first", "ADJ", "JJ", "NumType=Ord", "amod"),
    ("12-3456", "12-3456", "NUM", "CD", "NumType=Card", "nummod"),
    ("98-7654", "98-7654", "NUM", "CD", "NumType=Card", "nummod"),
    ("x1", "x1", "NOUN", "NN", "Number=Sing", "dobj"),
    ("ABC", "ABC", "PROPN", "NNP", "Number=Sing", "nsubj"),
    ("$", "$", "SYM", "$", "", "quantmod"),
    ("5", "5", "NUM", "CD", "NumType=Card", "nummod"),
    (".", ".", "PUNCT", ".", "PunctType=Peri", "punct"),
    (",", ",", "PUNCT", ",", "PunctType=Comm", "punct"),
    ("-", "-", "PUNCT", "HYPH", "PunctType=Dash", "punct"),
    ("(", "(", "PUNCT", "-LRB-", "PunctSide=Ini|PunctType=Brck", "punct"),
    (")", ")", "PUNCT", "-RRB-", "PunctSide=Fin|PunctType=Brck", "punct"),
    ("\"", "\"", "PUNCT", "''", "PunctType=Quot", "punct"),
    ("'", "'", "PUNCT", "''", "PunctType=Quot", "punct"),
    ("...", "...", "PUNCT", ":", "", "punct"),
    ("U.S.", "U.S.", "PROPN", "NNP", "Number=Sing", "pobj"),
    ("e-mail", "e-mail", "NOUN", "NN", "Number=Sing", "dobj"),
    ("test@example.com", "test@example.com", "X", "ADD", "", "dep"),
    ("www.example.com", "www.example.com", "X", "ADD", "", "dep"),
    ("http://example.org/a", "http://example.org/a", "X", "ADD", "", "dep"),
    ("\n", "\n", "SPACE", "_SP", "", "dep"),
    ("\u00e9cole", "\u00e9cole", "NOUN", "NN", "Number=Sing", "dobj"),
    ("\u00c9cole", "\u00e9cole", "PROPN", "NNP", "Number=Sing", "nsubj"),
    ("colour", "colour", "NOUN", "NN", "Number=Sing", "dobj"),
    ("\u65e5\u672c", "\u65e5\u672c", "PROPN", "NNP", "", "pobj"),
    ("\u00bd", "\u00bd", "NUM", "CD", "NumType=Frac", "nummod"),
    ("\U0001F600", "\U0001F600", "SYM", "NFP", "", "dep"),
    ("a\U0001F600", "a\U0001F600", "X", "FW", "", "dep"),
]
CUSTOM_NORM = {"colour": "color", "Apple": "APPLE", "iPhone": "iphone"}
STOP = ["the", "a", "an", "of", "and", "or", "in", "on", "is", "are", "it", "i", "we", "not"]
ENT_LABELS = ["PER", "ORG", "LOC"]
ALPHA_WORDS = [w[0] for w in WORDS if w[0].isalpha() and w[0].isascii()]
REGEXES = [r"^[A-Z]", r"ing$", r"\d", r"^[a-zA-Z]{3}$", r"^[a-z]+$", r"(?i)^the$", r"o", r"^\W+$", r"^[A-Z][a-z]+$", r"\d{2}-\d{4}", r"^(cat|dog)s?$", r"@", r"^[a-z]{1,2}$"]


def shape(s):
    out, last, n = [], "", 0
    for ch in s:
        if ch.isalpha():
            c = "X" if ch.isupper() else "x"
        elif ch.isdigit():
            c = "d"
        else:
            c = ch
        if c == last:
            n += 1
        else:
            last, n = c, 1
        if n <= 4:
            out.append(c)
    return "".join(out)


def main(out_path):
    rng = random.Random(SEED)
    by_surface = {w[0]: w for w in WORDS}
    # Python's \w counts every numeric character (such as the vulgar fraction) and .NET's does not; keep such words out of documents
    # that regular-expression tests run over. They are still covered by the lexical-flag comparison.
    surfaces = [w for w in by_surface if w != "½"]

    # ---- documents
    docs = []
    for i in range(520):
        n = rng.randint(1, 14)
        # A whitespace token is allowed only first: after another token, whether that token is "followed by a space" differs between
        # a document that records spacing per token and one that reads it from the text.
        words = [rng.choice(surfaces) for _ in range(n)]
        words = [w if (w != "\n" or k == 0) else "the" for k, w in enumerate(words)]
        if rng.random() < 0.3 and n >= 3:  # a repeated word makes runs for + and * patterns
            words[1] = words[0]
            words[2] = words[0]
        spaces = [rng.random() < 0.7 for _ in words]
        d = {
            "words": words,
            "spaces": spaces,
            "lemmas": [by_surface[w][1] for w in words],
            "pos": [by_surface[w][2] for w in words],
            "tags": [by_surface[w][3] for w in words],
            "morphs": [by_surface[w][4] for w in words],
            "deps": [by_surface[w][5] for w in words],
            "norms": [CUSTOM_NORM.get(w) for w in words],
            "sent_starts": None,
            "ents": None,
        }
        if rng.random() < 0.5:
            starts = [True] + [rng.random() < 0.25 for _ in words[1:]]
            d["sent_starts"] = starts
        if rng.random() < 0.55:
            ents = ["O"] * n
            t = 0
            while t < n:
                if rng.random() < 0.3:
                    length = min(rng.randint(1, 3), n - t)
                    label = rng.choice(ENT_LABELS)
                    ents[t] = "B-" + label
                    for k in range(1, length):
                        ents[t + k] = "I-" + label
                    t += length
                else:
                    t += 1
            d["ents"] = ents
        docs.append(d)

    # ---- matcher specs
    def string_value(attr):
        w = rng.choice(WORDS)
        return {
            "ORTH": w[0], "TEXT": w[0], "NORM": CUSTOM_NORM.get(w[0], w[0].lower()), "LOWER": w[0].lower(), "LEMMA": w[1], "POS": w[2],
            "TAG": w[3], "DEP": w[5], "MORPH": w[4], "ENT_TYPE": rng.choice(ENT_LABELS + [""]), "ENT_IOB": rng.choice(["B", "I", "O", ""]),
            "SHAPE": shape(w[0]),
        }[attr]

    string_attrs = ["ORTH", "TEXT", "NORM", "LOWER", "LEMMA", "POS", "TAG", "DEP", "MORPH", "ENT_TYPE", "ENT_IOB", "SHAPE"]
    flag_attrs = ["IS_ALPHA", "IS_ASCII", "IS_DIGIT", "IS_LOWER", "IS_UPPER", "IS_TITLE", "IS_PUNCT", "IS_SPACE", "IS_STOP", "IS_SENT_START",
                  "SENT_START", "IS_CURRENCY", "IS_LEFT_PUNCT", "IS_RIGHT_PUNCT", "IS_QUOTE", "IS_BRACKET", "LIKE_NUM", "LIKE_URL", "LIKE_EMAIL", "SPACY"]
    ops = ["", "!", "?", "+", "*", "{2}", "{1,2}", "{2,}", "{,2}", "{0,3}", "{2,3}", "{3}"]

    def constraint(kind=None):
        kind = kind or rng.choice(["str", "str", "flag", "flag", "in", "notin", "regex", "len", "morphset", "lencmp"])
        if kind == "str":
            a = rng.choice(string_attrs)
            return {a: string_value(a)}
        if kind == "flag":
            return {rng.choice(flag_attrs): rng.random() < 0.7}
        if kind in ("in", "notin"):
            a = rng.choice([x for x in string_attrs if x != "ENT_IOB"])
            vals = [string_value(a) for _ in range(rng.randint(1, 3))]
            return {a: {"IN" if kind == "in" else "NOT_IN": vals}}
        if kind == "regex":
            a = rng.choice(["ORTH", "LOWER", "LEMMA", "TAG", "MORPH", "POS", "SHAPE", "NORM"])
            return {a: {"REGEX": rng.choice(REGEXES)}}
        if kind == "len":
            return {"LENGTH": rng.randint(1, 8)}
        if kind == "lencmp":
            p = rng.choice(["==", "!=", ">=", "<=", ">", "<", "IN", "NOT_IN"])
            if p in ("IN", "NOT_IN"):
                return {"LENGTH": {p: [rng.randint(1, 6) for _ in range(rng.randint(1, 3))]}}
            return {"LENGTH": {p: rng.randint(1, 8)}}
        feats = list({f for w in rng.sample(WORDS, 2) for f in w[4].split("|") if f})
        return {"MORPH": {rng.choice(["IS_SUBSET", "IS_SUPERSET", "INTERSECTS"]): feats[: rng.randint(1, 3)]}}

    def token(op=None, kinds=None):
        t = {}
        for _ in range(1 if rng.random() < 0.7 else 2):
            c = constraint()
            if list(c)[0] not in t:
                t.update(c)
        if op is None:
            op = rng.choice(ops + ["", "", ""])
        if op:
            t["OP"] = op
        return t

    matcher = []
    # coverage block: each string attribute, flag, operator and predicate form at least twice
    for a in string_attrs:
        for _ in range(2):
            matcher.append({"key": "K", "patterns": [[{a: string_value(a)}]], "greedy": None})
    for a in flag_attrs:
        for v in (True, False):
            matcher.append({"key": "K", "patterns": [[{a: v}]], "greedy": None})
    for op in ops:
        for _ in range(2):
            base = {"ORTH": rng.choice(["the", "big", "cat", "a", "dog", "and"])} if rng.random() < 0.6 else {"IS_ALPHA": True}
            patt = [dict(base, OP=op) if op else dict(base)]
            if rng.random() < 0.7:
                patt.append(token("")) if rng.random() < 0.5 else patt.insert(0, token(""))
            matcher.append({"key": "K", "patterns": [patt], "greedy": None})
    for kind in ("in", "notin", "regex", "len", "lencmp", "morphset"):
        for _ in range(4):
            matcher.append({"key": "K", "patterns": [[token("", None), {**constraint(kind)}]], "greedy": None})
    hand = [
        [{}], [{}, {}], [{"ORTH": "the"}, {}, {"POS": "NOUN"}], [{"ORTH": "the"}, {"OP": "!"}], [{"OP": "!"}], [{"OP": "*"}, {"ORTH": "."}],
        [{"ORTH": "the"}, {"OP": "*"}, {"ORTH": "."}], [{"ORTH": "a", "OP": "?"}], [{"OP": "+"}], [{"POS": "ADJ", "OP": "*"}, {"POS": "NOUN"}],
        [{"IS_SENT_START": True}, {"IS_ALPHA": True, "OP": "+"}], [{"ENT_TYPE": "PER", "OP": "+"}], [{"ENT_IOB": "B"}, {"ENT_IOB": "I", "OP": "*"}],
        [{"MORPH": ""}], [{"MORPH": "Number=Sing"}], [{"MORPH": "Number=Sing|Person=3|Tense=Pres|VerbForm=Fin"}],
        [{"LEMMA": "be"}, {"LOWER": "not", "OP": "?"}, {"POS": {"IN": ["VERB", "ADJ"]}}],
        [{"ORTH": "Apple"}], [{"NORM": "apple"}], [{"NORM": "color"}], [{"LENGTH": 0}], [{"SHAPE": "dd-dddd"}], [{"SHAPE": "Xxxx"}],
        [{"SHAPE": "xxxx"}, {"SHAPE": "xxx"}], [{"ORTH": {"IN": []}}], [{"ORTH": {"NOT_IN": []}}], [{"ORTH": "the", "OP": "!"}, {"ORTH": "cat"}],
        [{"ORTH": "a", "OP": "+"}, {"ORTH": "a"}], [{"ORTH": "a", "OP": "*"}, {"ORTH": "a", "OP": "*"}],
        [{"IS_PUNCT": True, "OP": "+"}], [{"LIKE_NUM": True}, {"LIKE_NUM": True, "OP": "?"}], [{"LENGTH": {">=": 4}, "OP": "{2}"}],
        [{"ORTH": "the"}, {"IS_ALPHA": True, "OP": "{1,3}"}, {"ORTH": "."}], [{"IS_UPPER": True}, {"IS_TITLE": True}],
    ]
    for p in hand:
        matcher.append({"key": "K", "patterns": [p], "greedy": None})
    while len(matcher) < 230:
        n = rng.randint(1, 4)
        matcher.append({"key": "K", "patterns": [[token() for _ in range(n)]], "greedy": None})
    # several patterns under one key, and greedy modes
    for i in range(40):
        pats = [[token() for _ in range(rng.randint(1, 3))] for _ in range(rng.randint(2, 3))]
        if i % 4 == 0:  # make sure alternatives overlap and sometimes cover the same span
            pats.append(pats[0])
        matcher.append({"key": "K", "patterns": pats, "greedy": [None, "FIRST", "LONGEST", "LONGEST"][i % 4] if i >= 8 else None})
    for i in range(30):
        pats = [[dict({"POS": rng.choice(["ADJ", "NOUN", "PROPN"])}, **rng.choice([{"OP": "+"}, {"OP": "*"}, {"OP": "?"}, {}])), token("")] for _ in range(rng.randint(1, 2))]
        matcher.append({"key": "K", "patterns": pats, "greedy": rng.choice(["FIRST", "LONGEST"])})
    for i, m in enumerate(matcher):
        m["id"] = i

    # ---- phrase matcher specs
    def window(d, length):
        if len(d["words"]) < length:
            return None
        s = rng.randint(0, len(d["words"]) - length)
        return s, s + length

    phrases = []
    attrs = ["ORTH", "LOWER", "NORM", "SHAPE", "LEMMA", "POS", "TAG", "DEP", "ORTH", "LOWER"]
    for i in range(50):
        attr = attrs[i % len(attrs)]
        keys = {}
        for k in range(rng.randint(1, 3)):
            plist = []
            for _ in range(rng.randint(2, 7)):
                d = rng.choice(docs)
                w = window(d, rng.randint(1, 3))
                if w is None:
                    continue
                s, e = w
                plist.append({f: (d[f][s:e]) for f in ("words", "lemmas", "pos", "tags", "morphs", "deps")})
            keys["P%d" % k] = plist
        # the same phrase under two keys
        if i % 5 == 0 and keys["P0"]:
            keys["Q"] = [keys["P0"][0]]
        phrases.append({"id": i, "attr": attr, "keys": keys})

    # ---- entity ruler specs
    rulers = []
    for i in range(60):
        patterns = []
        for _ in range(rng.randint(1, 5)):
            label = rng.choice(ENT_LABELS + ["MISC", "DATE"])
            ent_id = rng.choice([None, None, "e1", "e2", "e3"])
            if rng.random() < 0.45:
                words = [rng.choice(ALPHA_WORDS) for _ in range(rng.randint(1, 3))]
                p = {"label": label, "pattern": " ".join(words)}
            else:
                p = {"label": label, "pattern": [token(op="") if rng.random() < 0.5 else token() for _ in range(rng.randint(1, 3))]}
            if ent_id and not (isinstance(p["pattern"], list) and False):
                p["id"] = ent_id
            patterns.append(p)
        rulers.append({"id": i, "overwrite": bool(i % 2), "attr": "LOWER" if i % 3 == 0 else "ORTH", "patterns": patterns})

    # ---- lexical strings
    lexical = [w[0] for w in WORDS]
    lexical += """Hello HELLO hello Hello1 12345 123456789 a1B2 x-y X-Y Mr. U.S.A. e-mail 3.14 1,000,000 abcdefg ABCDEFGH \u00c9COLE \u00c9cole \u00e9cole \u65e5\u672c\u8a9e \u3072\u3089\u304c\u306a \ud55c\uad6d\uc5b4 \u0627\u0644\u0639\u0631\u0628\u064a\u0629 \u0430\u0431\u0432 \u0410\u0411\u0412 \u041f\u0440\u0438\u0432\u0435\u0442 \u01c5 \u01c5a x_y x__y x____y x_____y ---- ----- ..... a....b e\u0301 e\u0301e\u0301 \U0001F600\U0001F600\U0001F600\U0001F600\U0001F600\U0001F600 \U0001F600a \u00b2 \u00b3\u00b2 \u2460 \u2460\u2461 \u00bd x\u00b2 \u2167 \u2177 a\u2167 \U0001d400\U0001d401 \U0001d41a\U0001d41b \U0001d7cf\U0001d7d0 \u00df \u1e9e \u00aa \u00ba \u02b0 \u02b0a a\u200bb \u200b ab\u00ad \u0130 \u0131 \ufb01 \ufb03""".split()
    lexical += ["\t", " ", "\n", "\u00a0", "\u2003", "\x1c", "\x85", "\u3000", "\r\n", "  ", " a", "\x0b"]
    lexical += """zero one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty thirty forty fifty sixty seventy eighty ninety hundred thousand million billion trillion quadrillion quintillion sextillion septillion dozen Ten TEN Hundred first second third fourth fifth sixth seventh eighth ninth tenth eleventh twelfth thirteenth twentieth thirtieth hundredth thousandth millionth billionth trillionth quadrillionth quintillionth 1st 2nd 10th 3rd 1th 5st 11th 21st 0th 1,000th 1.5th 1ST 1St 3RD ABCth th st 1stx 1,000 1.5 1,000.50 1.2.3 1,,2 ,5 5, . , 1/2 3/4 10/20 1/ /2 1/2/3 a/b -5 +5 --5 +5.5 -1,000 1e5 \u0663 \u0663\u0664 12a a12 one-half forty-five 1-2 10% $5 nil none half couple several many 0 00 007 \uff12 10,5 1.000.000 1_000 \u2461 \u2469 zwei""".split()
    lexical += """http://a.com https://example.org/path?x=1 www.example.com example.com a.b ftp://x.y foo.bar.baz localhost 3.14 file.txt e.g. www. http:// http://a mailto:a@b.com x.co.uk a.com/b //a.com a.com. (a.com) a.com, http://a.com. http://localhost:8080 127.0.0.1 1.2.3.4 a_b.com my-site.com sub.my-site.com/a/b.html abc.xyz Example.COM HTTP://A.COM ok.py README.md node.js v1.2 a..b .com a. a.com/ a.com:80 a.com?x a.com#x EXAMPLE.com Ab.cd ab.cD a.Cd 172.32.0.1 192.168.0.1 10.0.0.1 8.8.8.8 223.1.1.1 224.1.1.1 1.2.3.255 1.2.3.254 user@host.com/x ftp://h.com ftp://h.co a.\u00e9 a.\u00e9\u00e9 \u00fcber.de \u65e5\u672c.jp www.a www.A.com WWW.A.COM""".split()
    lexical += """a@b.com a@b @b.com a@ user.name+tag@domain.co.uk a@b.c a@@b.com @ a@b@c.com x@y. x@.y <a@b.com> mailto:a@b.com a@b.com. A@B.COM @handle a.@b.c a@b.co a@1.2 a@-b.c a.b@c.d a+b@c.d a_b@c.d a%b@c.d a@b_c.d a@b.c_d a@b..c a@b.\u00e9 \u00e9@b.c zz@y""".split()
    lexical += ["``", "''", "-LRB-", "((", "))", "[]", "<>", "<<", "\u00ab\u00bb", "\"\"", "'s", "($)", "US$", "$$", "\u20ac\u20ac", "$5", "C$", "kr", "\u00a2", "$.", "..", "...", "--", "?!", "+", "=", "%", "&", "_", "~", "`", "\u00b4", "\u300c", "\u300d", "\u2018", "\u2019", "\u201c", "\u201d", "\u201e", "\u201a", "'", "\""]
    # Harder forms for LIKE_URL and LIKE_EMAIL, where the published description leaves the exact rules open.
    lexical += """README.md Readme.md readme.MD Example.com example.COM EXAMPLE.COM Www.a.com Http://a.com https://A.com http://A.COM a-.com -a.com a.c-m a.c1 a.12 1a.com a1.com ab.c1 ab.1c ab.cd1 a.co ab.cde.fg 1.1.1.1 1.2.3.4.5 172.16.0.1 169.254.1.1 255.255.255.255 0.0.0.0 1.2.3.0 d.com/ e.org:8080/x e.org:1 e.org:123456 u:p@h.com u:p@h.com/x http:/a.com http:a.com http//a.com https://a https://a.b http://1 http:// http://. http://http://a www.a. www.a.b www..a w.a.com www.1 www.- www.\u00e4 \u00e4.com \u00e4\u00f6.de \u00e9.fr a.\u00e9\u00e9 \u65e5\u672c\u8a9e.com a.\u65e5\u672c xn--a.com a.com\u00a1 a_b.c_m a.b_c ab.c_ _.ab a.bc/ a.bc? a.bc# f.gh i.jk x.yz. a b.c d.e a@b.c@d.e a@[1.2.3.4] a@1.2.3.4 a@b.c/d a@b.c:80 a..b@c.d .a@b.c a@b.c. a@b.c, ab@cd.ef.gh a@bc.d.e 1@2.3 a@b.1 a@-b.c a@b-.c \u65e5@\u672c.jp a@. a@.. 1th 1stx 22nd 101st quintillion sextillion septillion octillion nonillion decillion undecillion septillionth octillionth nonillionth decillionth zillion googol zeroth nought score gross fiftyfive sixty-five one-hundred hundreds thousands millions twos --5 -+5 +-5 +, -, ++ -  1// 1/2/ -1/2 +1/2 1.5/2 1,5/2,5 \u0663/\u0664 1/\u0664 \uff10 \uff11\uff12\uff13 \u06f1\u06f2 \u2469 \u216b \u4e09 \u4e00\u4e8c \u4e07 \u96f6""".split()
    seen, uniq = set(), []
    for s in lexical:
        if s not in seen:
            seen.add(s)
            uniq.append(s)

    invalid = [
        [{"FOO": 1}], [{"ORTH": "a", "OP": "x"}], [{"ORTH": "a", "OP": ""}], [{"ORTH": 3}], [{"LENGTH": "a"}], [], [{"IS_ALPHA": "x"}],
        [{"IS_ALPHA": {"IN": [True]}}], [{"ENT_IOB": {"IN": ["B"]}}], [{"ENT_IOB": "X"}], [{"MORPH": {"==": "Number=Sing"}}],
        [{"ORTH": "a", "OP": "{3,2}"}], [{"ORTH": "a", "OP": "{}"}], [{"ORTH": "a", "OP": "{,}"}], [{"ORTH": {"FOO": "a"}}],
        [{"ORTH": {"IN": "a"}}], [{"LENGTH": {">=": "a"}}], [{"ORTH": {">=": 1}}], [{"IS_SUBSET": ["a"]}],
    ]
    valid_odd = [
        [{"orth": "a"}], [{"lower": "a"}], [{"TEXT": "a"}], [{"LENGTH": {"IN": [1, 2]}}], [{"ENT_IOB": 3}], [{"ORTH": "a", "OP": "{0,1}"}],
        [{"ORTH": "a", "OP": "{1}"}], [{}], [{"SENT_START": True}], [{"LENGTH": {"NOT_IN": []}}],
    ]
    out = {"invalid": invalid, "valid_odd": valid_odd, "seed": SEED, "stop_words": STOP, "docs": docs, "matcher": matcher, "phrase": phrases, "ruler": rulers, "lexical": uniq}
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=True, separators=(",", ":"))
    print(len(docs), "docs", len(matcher), "matcher specs", len(phrases), "phrase specs", len(rulers), "ruler specs", len(uniq), "lexical strings")


if __name__ == "__main__":
    main(sys.argv[1])
