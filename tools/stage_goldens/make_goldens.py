#!/usr/bin/env python3
"""Writes black-box reference outputs for the tokenizer.json pipeline stages.

usage: make_goldens.py <output.json> [<tokenizer-model-dir> <precompiled-output.json>]

The stage configurations are written here with the documented Python API of the `tokenizers` package; the reference outputs are what
that package returns for them. Offsets are converted from Python code point indexes to UTF-16 indexes. The package version is recorded in
the output. Nothing from the package's files is read: it is only called.
"""
import json
import sys

import tokenizers
from tokenizers import AddedToken, Regex, Tokenizer, decoders, models, normalizers, pre_tokenizers, processors

TEXTS = [
    "",
    "Hello, World!",
    "  leading and trailing  ",
    "Tab\tand\nnewline\r\nend",
    "naïve café résumé",
    "ÀÉÎÕÜ àéîõü",
    "e\u0301 a\u0308 composed \u1100\u1161 \uac00",
    "日本語のテキスト、中文文本。ＡＢＣ",
    "한국어 텍스트",
    "Привет, мир! Ελληνικά ΑΣ",
    "emoji 😀 and 👨‍👩‍👧 family 🏳️‍🌈",
    "It's don't we'll I'm they've I'D",
    "3.14 + 2 = 5; 1,000,000 $5 #tag @user",
    "ﬁne ﬂow ② ¹²³ ㎏ ½",
    "İstanbul ß ǅ",
    "mixed123abc 45def x2y",
    "a--b__c  d...e!?",
    "\u00a0nbsp\u2003em\u2028ls\u3000ideo",
    "ctl\u0000x\u200by\u0085z\ufeffw\ue000v",
    "http://example.com/a?b=c#d [CLS] x [SEP] <s> hi </s>",
    "𝒜𝔹ℂ math 𠀀 ext ⅷ ٣٤",
    "deva: कि क्षत्रिय ; arab: مرحبا",
    "hello world hello",
    "x",
]
PAIRS = [("Hello there", "General Kenobi!"), ("naïve café", "日本語 emoji 😀"), ("", "only second"), ("only first", ""), ("a b c d e f g h", "1 2 3 4 5 6")]


def u16(text, index):
    """UTF-16 index of a code point index."""
    return len(text[:index].encode("utf-16-le")) // 2


def stage_config(kind, obj):
    tok = Tokenizer(models.BPE())
    setattr(tok, kind, obj)
    return json.loads(tok.to_str())[kind]


def encode_dict(enc, texts):
    """texts: the string each sequence id indexes into (offsets are per sequence)."""
    seq = enc.sequence_ids
    offsets = []
    for (s, e), sid in zip(enc.offsets, seq):
        src = texts[sid if sid is not None else 0]
        offsets.append([u16(src, s), u16(src, e)])
    return {
        "ids": enc.ids,
        "tokens": enc.tokens,
        "offsets": offsets,
        "type_ids": enc.type_ids,
        "word_ids": [-1 if w is None else w for w in enc.word_ids],
        "special": enc.special_tokens_mask,
        "attention": enc.attention_mask,
    }


# ---- normalizers -------------------------------------------------------------------------------------------------------------

def normalizer_specs():
    S = normalizers
    return {
        "bert-default": S.BertNormalizer(),
        "bert-nolower": S.BertNormalizer(lowercase=False),
        "bert-strip-nolower": S.BertNormalizer(lowercase=False, strip_accents=True),
        "bert-lower-nostrip": S.BertNormalizer(strip_accents=False),
        "bert-noclean": S.BertNormalizer(clean_text=False, handle_chinese_chars=False),
        "nfc": S.NFC(), "nfd": S.NFD(), "nfkc": S.NFKC(), "nfkd": S.NFKD(),
        "lowercase": S.Lowercase(),
        "strip": S.Strip(), "strip-left": S.Strip(left=True, right=False), "strip-right": S.Strip(left=False, right=True),
        "strip-accents-after-nfd": S.Sequence([S.NFD(), S.StripAccents()]),
        "strip-accents-alone": S.StripAccents(),
        "replace-string": S.Replace("a", "AA"),
        "replace-string-empty": S.Replace(" ", ""),
        "replace-regex": S.Replace(Regex(r"\s+"), " "),
        "replace-regex-class": S.Replace(Regex(r"[\p{P}\p{S}]+"), "_"),
        "prepend": S.Prepend("▁"),
        "bytelevel": S.ByteLevel(),
        "sequence": S.Sequence([S.NFKC(), S.Replace(Regex(" {2,}"), " "), S.Lowercase(), S.Prepend("▁")]),
    }


# ---- pre-tokenizers ----------------------------------------------------------------------------------------------------------

GPT2 = r"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+"
CL100K = r"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+"
O200K = (r"[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]*[\p{Ll}\p{Lm}\p{Lo}\p{M}]+(?i:'s|'t|'re|'ve|'m|'ll|'d)?"
         r"|[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]+[\p{Ll}\p{Lm}\p{Lo}\p{M}]*(?i:'s|'t|'re|'ve|'m|'ll|'d)?"
         r"|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n/]*|\s*[\r\n]+|\s+(?!\S)|\s+")
POSSESSIVE = r"[^\r\n\p{L}\p{N}]?+\p{L}++|\p{N}{1,3}+| ?[^\s\p{L}\p{N}]++[\r\n]*+|\s*+[\r\n]|\s++$|\s+(?!\S)|\s"


def pretok_specs():
    P = pre_tokenizers
    specs = {
        "bert": P.BertPreTokenizer(), "whitespace": P.Whitespace(), "whitespace-split": P.WhitespaceSplit(),
        "digits": P.Digits(), "digits-individual": P.Digits(individual_digits=True),
        "unicode-scripts": P.UnicodeScripts(),
        "char-delimiter": P.CharDelimiterSplit("-"),
        "sequence": P.Sequence([P.WhitespaceSplit(), P.Digits(individual_digits=True), P.Punctuation()]),
        "bytelevel": P.ByteLevel(), "bytelevel-noprefix": P.ByteLevel(add_prefix_space=False),
        "bytelevel-noregex": P.ByteLevel(add_prefix_space=False, use_regex=False),
        "bytelevel-noprefix-noregex": P.ByteLevel(add_prefix_space=True, use_regex=False),
    }
    for b in ["removed", "isolated", "merged_with_previous", "merged_with_next", "contiguous"]:
        specs[f"punctuation-{b}"] = P.Punctuation(behavior=b)
        specs[f"split-string-{b}"] = P.Split("-", behavior=b)
        specs[f"split-string-inv-{b}"] = P.Split(" ", behavior=b, invert=True)
        specs[f"split-regex-{b}"] = P.Split(Regex(r"[,;.!]+|\d"), behavior=b)
    specs["split-gpt2"] = P.Split(Regex(GPT2), behavior="isolated")
    specs["split-cl100k"] = P.Split(Regex(CL100K), behavior="isolated")
    specs["split-o200k"] = P.Split(Regex(O200K), behavior="isolated")
    specs["split-possessive"] = P.Split(Regex(POSSESSIVE), behavior="isolated")
    specs["split-regex-invert"] = P.Split(Regex(r"\w+"), behavior="removed", invert=True)
    for scheme in ["always", "first", "never"]:
        for split in [True, False]:
            specs[f"metaspace-{scheme}-{split}"] = P.Metaspace(prepend_scheme=scheme, split=split)
    specs["metaspace-custom"] = P.Metaspace(replacement="_", prepend_scheme="always")
    specs["sequence-metaspace-first"] = P.Sequence([P.WhitespaceSplit(), P.Metaspace(prepend_scheme="first")])
    return specs


# ---- decoders ----------------------------------------------------------------------------------------------------------------

def decoder_specs():
    D = decoders
    return {
        "wordpiece": (D.WordPiece(), [["hello", "world"], ["un", "##believ", "##able", "!"], ["it", "'", "s", "a", "dog", ".", "do", "not", "you", "n't"], ["a", ",", "b", "?", "c", "!"], ["x", "##", "y"], []]),
        "wordpiece-nocleanup": (D.WordPiece(cleanup=False), [["hello", ".", "world"], ["a", "##b"]]),
        "wordpiece-prefix": (D.WordPiece(prefix="@@"), [["a", "@@b", "##c"]]),
        "metaspace-always": (D.Metaspace(prepend_scheme="always"), [["▁Hello", "▁world"], ["Hello", "▁world"], ["▁▁a"], []]),
        "metaspace-never": (D.Metaspace(prepend_scheme="never"), [["▁Hello", "▁world"]]),
        "metaspace-first": (D.Metaspace(prepend_scheme="first"), [["▁Hello", "▁world"]]),
        "bytelevel": (D.ByteLevel(), [["Hello", "Ġworld", "!"], ["Ã©", "Ġcaf", "Ã©"], ["ðŁĺĢ"], ["Ã"], ["Ġ", "Ċ", "ĉ"], ["a", "日"], []]),
        "bytefallback": (D.ByteFallback(), [["a", "<0xE6>", "<0x97>", "<0xA5>", "b"], ["<0xE6>", "<0x97>", "x"], ["<0xFF>", "<0x41>"], ["<0x41>", "<0x42>"], ["<0xZZ>", "<0x4>"]]),
        "fuse": (D.Fuse(), [["a", "b", "c"], [], ["x"]]),
        "strip": (D.Strip(content=" ", left=1, right=0), [[" a", "  b", "c "], []]),
        "strip-both": (D.Strip(content="x", left=2, right=1), [["xxxaxxx", "x", "xx"]]),
        "replace": (D.Replace("▁", " "), [["▁a", "b▁▁c"]]),
        "bpe": (D.BPEDecoder(suffix="</w>"), [["he", "llo</w>", "wor", "ld</w>"], ["a</w>"], []]),
        "ctc": (D.CTC(), [["<pad>", "h", "h", "e", "<pad>", "l", "l", "<pad>", "l", "o", "|", "w", "o", "r", "l", "d"], ["a", "|", "|", "b", "<pad>"], ["h", "i", "?", "|", "!"], []]),
        "ctc-nocleanup": (D.CTC(cleanup=False), [["<pad>", "a", "a", "|", "b"]]),
        "sequence": (D.Sequence([D.Replace("▁", " "), D.ByteFallback(), D.Fuse(), D.Strip(content=" ", left=1, right=0)]),
                     [["▁Hello", "▁wor", "ld", "<0xE6>", "<0x97>", "<0xA5>"], ["▁a"], []]),
    }


# ---- whole tokenizers --------------------------------------------------------------------------------------------------------

def build(setup, texts, pairs):
    """Builds a char-level tokenizer: BPE with no merges gives one token per code point of every piece."""
    tok = Tokenizer(models.BPE(vocab={"[UNK]": 0}, merges=[], unk_token="[UNK]"))
    setup(tok)
    chars = set()
    for t in texts + [p for pr in pairs for p in pr]:
        n = tok.normalizer.normalize_str(t) if tok.normalizer is not None else t
        if tok.pre_tokenizer is not None:
            for piece, _ in tok.pre_tokenizer.pre_tokenize_str(n):
                chars.update(piece)
        else:
            chars.update(n)
    if tok.pre_tokenizer is not None and "ByteLevel" in tok.pre_tokenizer.__class__.__name__:
        chars.update(pre_tokenizers.ByteLevel.alphabet())
    vocab = {"[UNK]": 0}
    for c in sorted(chars):
        vocab[c] = len(vocab)
    tok.model = models.BPE(vocab=vocab, merges=[], unk_token="[UNK]")
    return tok


def specials(tok, names):
    tok.add_special_tokens([AddedToken(n, special=True, normalized=False) for n in names])


def tokenizer_specs():
    N, P, D, PP = normalizers, pre_tokenizers, decoders, processors

    def bert(tok):
        tok.normalizer = N.BertNormalizer()
        tok.pre_tokenizer = P.BertPreTokenizer()
        specials(tok, ["[CLS]", "[SEP]", "[PAD]", "[MASK]"])
        tok.post_processor = PP.BertProcessing(("[SEP]", tok.token_to_id("[SEP]")), ("[CLS]", tok.token_to_id("[CLS]")))
        tok.decoder = D.WordPiece()

    def bert_template(tok):
        bert(tok)
        tok.post_processor = PP.TemplateProcessing(single="[CLS] $A [SEP]", pair="[CLS] $A:0 [SEP]:0 $B:1 [SEP]:1",
                                                    special_tokens=[("[CLS]", tok.token_to_id("[CLS]")), ("[SEP]", tok.token_to_id("[SEP]"))])

    def roberta(tok):
        tok.pre_tokenizer = P.ByteLevel(add_prefix_space=True)
        tok.add_special_tokens([AddedToken(n, special=True, normalized=False) for n in ["<s>", "<pad>", "</s>", "<unk>"]])
        tok.add_special_tokens([AddedToken("<mask>", special=True, lstrip=True, normalized=False)])
        tok.post_processor = PP.RobertaProcessing(("</s>", tok.token_to_id("</s>")), ("<s>", tok.token_to_id("<s>")), trim_offsets=True, add_prefix_space=True)
        tok.decoder = D.ByteLevel()

    def roberta_notrim(tok):
        roberta(tok)
        tok.post_processor = PP.RobertaProcessing(("</s>", tok.token_to_id("</s>")), ("<s>", tok.token_to_id("<s>")), trim_offsets=False, add_prefix_space=False)

    def gpt2(tok):
        tok.pre_tokenizer = P.ByteLevel(add_prefix_space=False)
        tok.post_processor = PP.ByteLevel(trim_offsets=False)
        tok.decoder = D.ByteLevel()
        tok.add_special_tokens([AddedToken("<|endoftext|>", special=True, normalized=False)])

    def gpt2_trim(tok):
        gpt2(tok)
        tok.pre_tokenizer = P.ByteLevel(add_prefix_space=True)
        tok.post_processor = PP.ByteLevel(trim_offsets=True)

    def sentencepiece(tok):
        tok.normalizer = N.Sequence([N.Prepend("▁"), N.Replace(" ", "▁")])
        specials(tok, ["<s>", "</s>", "<unk>"])
        tok.post_processor = PP.TemplateProcessing(single="<s> $A", pair="<s> $A <s> $B:1",
                                                    special_tokens=[("<s>", tok.token_to_id("<s>"))])
        tok.decoder = D.Sequence([D.Replace("▁", " "), D.ByteFallback(), D.Fuse(), D.Strip(content=" ", left=1, right=0)])

    def metaspace(tok):
        tok.pre_tokenizer = P.Metaspace(prepend_scheme="first")
        tok.normalizer = N.NFKC()
        specials(tok, ["<s>", "</s>"])
        tok.decoder = D.Metaspace(prepend_scheme="first")

    def added_flags(tok):
        tok.normalizer = N.Lowercase()
        tok.pre_tokenizer = P.WhitespaceSplit()
        tok.add_tokens([AddedToken("Foo", normalized=True), AddedToken("Bar", normalized=False),
                        AddedToken("baz", single_word=True), AddedToken("qux", lstrip=True), AddedToken("quux", rstrip=True),
                        AddedToken("<m>", special=True, lstrip=True, rstrip=True, normalized=False),
                        AddedToken("hello world", normalized=False), AddedToken("hello", normalized=False), AddedToken("ab", normalized=True),
                        AddedToken("abc", normalized=False)])
        tok.decoder = D.Fuse()

    def norm_only(n, pre=None):
        def f(tok):
            tok.normalizer = n
            if pre is not None:
                tok.pre_tokenizer = pre
        return f

    def seq_post(tok):
        gpt2_trim(tok)
        specials(tok, ["[CLS]", "[SEP]"])
        tok.post_processor = PP.Sequence([PP.ByteLevel(trim_offsets=True),
                                           PP.TemplateProcessing(single="[CLS] $A [SEP]", pair="[CLS] $A [SEP] $B:1 [SEP]:1",
                                                                  special_tokens=[("[CLS]", tok.token_to_id("[CLS]")), ("[SEP]", tok.token_to_id("[SEP]"))])])

    W = P.Whitespace()
    specs = [
        ("bert", bert, {}), ("bert-template", bert_template, {}),
        ("bert-trunc-longest", bert_template, {"trunc": dict(max_length=9)}),
        ("bert-trunc-left", bert_template, {"trunc": dict(max_length=7, direction="left")}),
        ("bert-trunc-first", bert_template, {"trunc": dict(max_length=8, strategy="only_first")}),
        ("bert-trunc-second", bert_template, {"trunc": dict(max_length=8, strategy="only_second")}),
        ("bert-pad-fixed", bert_template, {"pad": dict(length=40, pad_id=1, pad_token="[PAD]"), "single_only": True}),
        ("bert-pad-left-multiple", bert_template, {"pad": dict(direction="left", pad_to_multiple_of=8, pad_id=1, pad_token="[PAD]", pad_type_id=3), "single_only": True}),
        ("bert-pad-longest", bert, {"pad": dict(pad_id=1, pad_token="[PAD]"), "single_only": True}),
        ("roberta", roberta, {}), ("roberta-notrim", roberta_notrim, {}),
        ("gpt2", gpt2, {}), ("gpt2-trim", gpt2_trim, {}), ("sequence-post", seq_post, {}),
        ("sentencepiece-like", sentencepiece, {}), ("metaspace", metaspace, {}),
        ("added-flags", added_flags, {"extra_texts": [
            "Foo foo FOO bar Bar baz bazz xbaz baz. qux  qux   quux  quux", "hello world hello", "abc ab abcd  <m>  x <m>", "a<m>b", "foo", "hello  world", "x baz y",
            "  Foo  ", "Barbaz", "baz-baz", "😀baz😀"]}),
        ("norm-nfd-strip", norm_only(N.Sequence([N.NFD(), N.StripAccents()]), W), {}),
        ("norm-nfc", norm_only(N.NFC(), W), {}), ("norm-nfkc", norm_only(N.NFKC(), W), {}), ("norm-nfkd", norm_only(N.NFKD(), W), {}),
        ("norm-replace-regex", norm_only(N.Replace(Regex(r"\s+"), "_"), W), {}),
        ("norm-replace-string", norm_only(N.Replace("l", "LL"), W), {}),
        ("norm-replace-delete", norm_only(N.Replace("e", ""), W), {}),
        ("norm-prepend", norm_only(N.Prepend("▁"), W), {}),
        ("norm-lowercase", norm_only(N.Lowercase(), W), {}), ("norm-strip", norm_only(N.Strip(), W), {}),
        ("norm-bert-chinese", norm_only(N.BertNormalizer(lowercase=False), W), {}),
        ("norm-bytelevel", norm_only(N.ByteLevel(), None), {}),
        ("pretok-whitespace", norm_only(None, P.Whitespace()), {}), ("pretok-unicode-scripts", norm_only(None, P.UnicodeScripts()), {}),
        ("pretok-digits", norm_only(None, P.Digits(individual_digits=True)), {}),
        ("pretok-split-cl100k", norm_only(None, P.Sequence([P.Split(Regex(CL100K), behavior="isolated"), P.ByteLevel(add_prefix_space=False, use_regex=False)])), {}),
        ("pretok-split-o200k", norm_only(None, P.Sequence([P.Split(Regex(O200K), behavior="isolated"), P.ByteLevel(add_prefix_space=False, use_regex=False)])), {}),
    ]
    return specs


def main():
    out = sys.argv[1]
    result = {"oracle": "tokenizers " + tokenizers.__version__, "normalizers": [], "pre_tokenizers": [], "decoders": [], "tokenizers": []}

    for name, n in normalizer_specs().items():
        result["normalizers"].append({"name": name, "config": stage_config("normalizer", n),
                                      "cases": [{"in": t, "out": n.normalize_str(t)} for t in TEXTS]})

    for name, p in pretok_specs().items():
        cases = []
        for t in TEXTS:
            cases.append({"in": t, "pieces": [[piece, u16(t, s), u16(t, e)] for piece, (s, e) in p.pre_tokenize_str(t)]})
        result["pre_tokenizers"].append({"name": name, "config": stage_config("pre_tokenizer", p), "cases": cases})

    for name, (d, lists) in decoder_specs().items():
        cases = []
        for ts in lists:
            try:
                cases.append({"tokens": ts, "out": d.decode(ts)})
            except BaseException as ex:  # the package panics on some inputs (Strip on short tokens); those have no reference output
                print("no reference for", name, ts, type(ex).__name__)
        result["decoders"].append({"name": name, "config": stage_config("decoder", d), "cases": cases})

    for name, setup, opts in tokenizer_specs():
        texts = TEXTS + opts.get("extra_texts", [])
        pairs = [] if opts.get("single_only") else PAIRS
        tok = build(setup, texts, pairs)
        if "trunc" in opts:
            tok.enable_truncation(**opts["trunc"])
        if "pad" in opts:
            tok.enable_padding(**opts["pad"])
        cases = []
        for add in (True, False):
            if "pad" in opts:
                # Padding to the longest needs the whole batch.
                for t, e in zip(texts, tok.encode_batch(texts, add_special_tokens=add)):
                    cases.append({"text": t, "pair": None, "add_special": add, **encode_dict(e, [t])})
                continue
            for t in texts:
                try:
                    e = tok.encode(t, add_special_tokens=add)
                except Exception:  # only_second truncation has nothing to shorten in a single text
                    break
                cases.append({"text": t, "pair": None, "add_special": add, **encode_dict(e, [t])})
            for a, b in pairs:
                try:
                    e = tok.encode(a, b, add_special_tokens=add)
                except Exception as ex:  # truncation cannot always respect the limit (for example only_first on a short first text)
                    print("no reference for", name, (a, b), ex)
                    continue
                cases.append({"text": a, "pair": b, "add_special": add, **encode_dict(e, [a, b])})
        decode = []
        for c in cases[: len(texts)]:
            for skip in (True, False):
                decode.append({"ids": c["ids"], "skip": skip, "out": tok.decode(c["ids"], skip_special_tokens=skip)})
        result["tokenizers"].append({"name": name, "json": tok.to_str(), "batch_padding": "pad" in opts, "cases": cases, "decode": decode})

    data = json.dumps(result, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    if out.endswith(".gz"):
        import gzip

        data = gzip.compress(data, 9, mtime=0)
    with open(out, "wb") as f:
        f.write(data)
    print("wrote", out, {k: len(v) for k, v in result.items() if isinstance(v, list)})

    if len(sys.argv) > 3:
        write_precompiled(sys.argv[2], sys.argv[3])


def write_precompiled(model_dir, out):
    """Normalizer outputs of real model files that carry a SentencePiece charsmap, for the optional Precompiled check."""
    import glob
    import os

    result = []
    for path in sorted(glob.glob(os.path.join(model_dir, "*", "tokenizer.json"))):
        tok = Tokenizer.from_file(path)
        cfg = json.loads(tok.to_str()).get("normalizer")
        if tok.normalizer is None or "Precompiled" not in json.dumps(cfg):
            continue
        result.append({"model": os.path.basename(os.path.dirname(path)), "cases": [{"in": t, "out": tok.normalizer.normalize_str(t)} for t in TEXTS],
                       "encode": [{"text": t, **encode_dict(tok.encode(t, add_special_tokens=False), [t])} for t in TEXTS]})
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(result, f, ensure_ascii=False, separators=(",", ":"))
    print("wrote", out, [r["model"] for r in result])


if __name__ == "__main__":
    main()
