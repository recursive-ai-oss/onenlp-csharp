#!/usr/bin/env python3
"""Runs the released lingua-language-detector package as a black box over an evaluation file.

  run_lingua.py <high|low> <eval.tsv> <out.txt> [--single]

eval.tsv lines are "<gold code>\\t<text>"; the output has one predicted ISO 639-1 code per line (empty for no answer). The detector is
built from all 75 languages, in the mode the package README documents (default high accuracy, or with_low_accuracy_mode).
"""
import sys
import time

from lingua import LanguageDetectorBuilder

mode, src, dst = sys.argv[1:4]
single = "--single" in sys.argv
builder = LanguageDetectorBuilder.from_all_languages()
if mode == "low":
    builder = builder.with_low_accuracy_mode()
detector = builder.with_preloaded_language_models().build()
texts = [line.rstrip("\n").split("\t", 1)[1] for line in open(src, encoding="utf-8")]
t0 = time.perf_counter()
if single:
    res = [detector.detect_language_of(t) for t in texts]
else:
    res = detector.detect_languages_in_parallel_of(texts)
dt = time.perf_counter() - t0
with open(dst, "w", encoding="utf-8") as f:
    for r in res:
        f.write((r.iso_code_639_1.name.lower() if r is not None else "") + "\n")
print(f"{len(texts)} texts, {dt * 1000:.0f} ms, {'1 thread' if single else 'parallel'}", file=sys.stderr)
