"""Information column for the tokenizer speed rows: the native references on the same inputs.

Hugging Face `tokenizers` for the WordPiece, Unigram, SentencePiece-BPE and byte-level BPE families, and `tiktoken` for the
Tiktoken encodings, called only through their documented Python APIs. Medians per operation, one thread.

    python native_speed.py --lib hf|tiktoken --inputs DIR --models DIR

`--inputs` is the directory written by `OneNlp.Bench.Tokenizers --dump-inputs` (short.txt, medium.txt, long.txt). For tiktoken the
ranks files must be reachable through TIKTOKEN_CACHE_DIR (no download is attempted when they are).
"""
import argparse
import os
import statistics
import time


def median_ns(op, budget_s=1.0):
    op()
    t0 = time.perf_counter()
    op()
    one = max(time.perf_counter() - t0, 1e-7)
    inner = max(1, int(0.04 / one))
    samples = max(5, min(25, int(budget_s / (inner * one))))
    times = []
    for _ in range(samples):
        t = time.perf_counter()
        for _ in range(inner):
            op()
        times.append((time.perf_counter() - t) / inner * 1e9)
    return statistics.median(times)


def fmt(ns):
    return f"{ns:.0f} ns" if ns < 1e3 else f"{ns/1e3:.2f} us" if ns < 1e6 else f"{ns/1e6:.2f} ms"


def rows(name, encode, decode, texts):
    for size, text in texts.items():
        ids = encode(text)
        for op, fn in (
            ("encode-to-ids", lambda: encode(text)),
            ("count", lambda: len(encode(text))),
            ("decode", lambda: decode(ids)),
        ):
            print(f"{name}\t{op}\t{size}\t{fmt(median_ns(fn))}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lib", required=True, choices=["hf", "tiktoken"])
    ap.add_argument("--inputs", required=True)
    ap.add_argument("--models", required=True)
    a = ap.parse_args()
    texts = {s: open(os.path.join(a.inputs, s + ".txt"), encoding="utf-8").read() for s in ("short", "medium", "long")}
    if a.lib == "hf":
        import tokenizers
        from tokenizers import Tokenizer
        print(f"# tokenizers {tokenizers.__version__}")
        for name, d in [
            ("WordPiece (bert-base-uncased)", "google-bert_bert-base-uncased"),
            ("Unigram (t5-small)", "google-t5_t5-small"),
            ("SentencePiece BPE, byte fallback (TinyLlama)", "TinyLlama_TinyLlama-1.1B-Chat-v1.0"),
            ("Byte-level BPE (gpt2)", "openai-community_gpt2"),
        ]:
            tk = Tokenizer.from_file(os.path.join(a.models, d, "tokenizer.json"))
            rows(name, lambda t, tk=tk: tk.encode(t, add_special_tokens=False).ids, lambda i, tk=tk: tk.decode(i, skip_special_tokens=True), texts)
    else:
        import tiktoken
        print(f"# tiktoken {tiktoken.__version__}")
        os.environ.setdefault("TIKTOKEN_CACHE_DIR", os.path.join(a.models, "tiktoken-cache"))
        for name, enc in [("Tiktoken cl100k_base", "cl100k_base"), ("Tiktoken o200k_base", "o200k_base")]:
            e = tiktoken.get_encoding(enc)
            rows(name, e.encode_ordinary, e.decode, texts)


main()
