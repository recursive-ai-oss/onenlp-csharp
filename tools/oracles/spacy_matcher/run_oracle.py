"""Runs spaCy as a black box over the generated cases and records what it returns.

usage: python run_oracle.py CASES.json OUT.json

Requires spaCy (pinned in the oracle environment); only its documented public API is called: Doc(vocab, words=..., spaces=..., ...),
Matcher, PhraseMatcher, the entity_ruler pipe, and the token attributes. Documents are built from the given words, so tokenization
differences between implementations do not enter the comparison.
"""
import json
import sys
import unicodedata as ud

import spacy
from spacy.matcher import Matcher, PhraseMatcher
from spacy.tokens import Doc


def make_doc(vocab, d):
    n = len(d["words"])
    # Sentence boundaries come from the heads: every token points at the first token of its sentence, so IS_SENT_START is true exactly
    # at the given starts (and only at token 0 when none are given).
    starts = d.get("sent_starts") or [i == 0 for i in range(n)]
    heads, first = [], 0
    for i in range(n):
        if starts[i]:
            first = i
        heads.append(first)
    kw = dict(words=d["words"], spaces=d["spaces"], lemmas=d["lemmas"], pos=d["pos"], tags=d["tags"], morphs=d["morphs"],
              heads=heads, deps=d["deps"])
    if d.get("ents") is not None:
        kw["ents"] = d["ents"]
    doc = Doc(vocab, **kw)
    for i, norm in enumerate(d.get("norms") or []):
        if norm:
            doc[i].norm_ = norm
    return doc


def make_phrase_doc(vocab, p):
    n = len(p["words"])
    return Doc(vocab, words=p["words"], lemmas=p["lemmas"], pos=p["pos"], tags=p["tags"], morphs=p["morphs"], heads=[0] * n, deps=p["deps"])


def set_stop(vocab, words, stop):
    for w in words:
        vocab[w].is_stop = w.lower() in stop


def ranges(cps):
    out = []
    for c in sorted(cps):
        if out and c == out[-1][1] + 1:
            out[-1][1] = c
        else:
            out.append([c, c])
    return " ".join(f"{a:X}" if a == b else f"{a:X}-{b:X}" for a, b in out)


def main(cases_path, out_path):
    cases = json.load(open(cases_path, encoding="utf-8"))
    nlp = spacy.blank("en")
    V = nlp.vocab
    stop = set(cases["stop_words"])
    all_words = set()
    for d in cases["docs"]:
        all_words.update(d["words"])
    set_stop(V, all_words, stop)

    result = {"spacy": spacy.__version__, "python": sys.version.split()[0], "errors": {}}

    def accepted(p):
        try:
            Matcher(V).add("K", [p])
            return True
        except Exception:
            return False

    result["invalid"] = [accepted(p) for p in cases["invalid"]]
    result["valid_odd"] = [accepted(p) for p in cases["valid_odd"]]

    docs = [make_doc(V, d) for d in cases["docs"]]
    matcher_out = {}
    for spec in cases["matcher"]:
        m = Matcher(V)
        try:
            m.add(spec["key"], spec["patterns"], greedy=spec["greedy"])
        except Exception as e:  # a pattern spaCy rejects; recorded so the C# side can be checked to reject it too
            result["errors"]["matcher/%d" % spec["id"]] = type(e).__name__
            continue
        flat = []
        for di, doc in enumerate(docs):
            for _, s, e in m(doc):
                flat.append([di, s, e])
        matcher_out[str(spec["id"])] = flat
    result["matcher"] = matcher_out

    phrase_out = {}
    for spec in cases["phrase"]:
        phrase_matcher = PhraseMatcher(V, attr=spec["attr"])
        for key, plist in spec["keys"].items():
            if plist:
                phrase_matcher.add(key, [make_phrase_doc(V, p) for p in plist])
        flat = []
        for di, doc in enumerate(docs):
            for mid, s, e in phrase_matcher(doc):
                flat.append([di, s, e, V.strings[mid]])
        phrase_out[str(spec["id"])] = flat
    result["phrase"] = phrase_out

    ruler_out = {}
    for spec in cases["ruler"]:
        rnlp = spacy.blank("en")
        set_stop(rnlp.vocab, all_words, stop)
        config = {"overwrite_ents": spec["overwrite"]}
        if spec["attr"] != "ORTH":
            config["phrase_matcher_attr"] = spec["attr"]
        ruler = rnlp.add_pipe("entity_ruler", config=config)
        try:
            ruler.add_patterns(spec["patterns"])
        except Exception as e:
            result["errors"]["ruler/%d" % spec["id"]] = type(e).__name__
            continue
        per_doc = []
        for d in cases["docs"]:
            doc = make_doc(rnlp.vocab, d)
            doc = ruler(doc)
            per_doc.append([[e.start, e.end, e.label_, doc[e.start].ent_id_] for e in doc.ents])
        ruler_out[str(spec["id"])] = per_doc
    result["ruler"] = ruler_out

    lex = []
    for s in cases["lexical"]:
        t = Doc(V, words=[s])[0]
        lex.append([t.is_alpha, t.is_ascii, t.is_digit, t.is_lower, t.is_upper, t.is_title, t.is_punct, t.is_space, t.like_num, t.like_url,
                    t.like_email, t.is_currency, t.is_left_punct, t.is_right_punct, t.is_quote, t.is_bracket, t.shape_, len(s)])
    result["lexical"] = lex
    result["lexical_columns"] = ["is_alpha", "is_ascii", "is_digit", "is_lower", "is_upper", "is_title", "is_punct", "is_space", "like_num",
                                 "like_url", "like_email", "is_currency", "is_left_punct", "is_right_punct", "is_quote", "is_bracket", "shape", "length"]

    # Single-code-point sweep: which code points each flag holds for. Code points the Python build leaves unassigned are listed so a
    # comparison can skip characters that a newer Unicode version defines.
    cps = [c for c in range(0x20000) if not (0xD800 <= c < 0xE000)]
    flags = {k: [] for k in ("is_alpha", "is_digit", "is_lower", "is_upper", "is_title", "is_punct", "is_space", "is_currency", "is_ascii")}
    unassigned = []
    shapes = {}
    for c in cps:
        ch = chr(c)
        if ud.category(ch) == "Cn":
            unassigned.append(c)
            continue
        t = Doc(V, words=[ch])[0]
        for k in flags:
            if getattr(t, k):
                flags[k].append(c)
        sh = t.shape_
        if sh not in ("x", "X", "d", ch):
            shapes[c] = sh
    result["chars"] = {k: ranges(v) for k, v in flags.items()}
    result["chars_unassigned"] = ranges(unassigned)
    result["chars_odd_shapes"] = {str(k): v for k, v in shapes.items()}
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=True, separators=(",", ":"))
    print("spacy", spacy.__version__, "matcher", len(matcher_out), "phrase", len(phrase_out), "ruler", len(ruler_out), "errors", result["errors"])


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
