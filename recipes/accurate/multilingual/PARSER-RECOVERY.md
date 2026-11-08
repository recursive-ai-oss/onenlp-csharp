# Parser-only recovery on a frozen accurate encoder

The `recover-parser` command adapts one of the original nine language parsers without updating the shared transformer or any other head. It uses the same `corpus.json` reader as joint training, including its train/dev separation and extra-treebank exclusions.

```sh
dotnet run --project recipes/accurate/multilingual -- recover-parser \
  --lang nl --spec recipes/accurate/common/corpus.json --data "$DATA" \
  --export "$EXPORT" --out "$RUN/nl" --cache "$CACHE" \
  --epochs 8 --lr 0.0002 --dropout 0.1 --batch-words 1500 \
  --seed 66 --threads 4 --budget-seconds 1000
```

`EXPORT` contains the frozen `encoder.onnx`, `tokenizer.json` and `heads/<language>.onenlp`. Only the loaded parser is passed to the public C# `Train` API. All other heads retain their existing configuration and checkpoint bytes, verified before and after training. The encoder is inference-only. Disk-cached float32 vectors survive resumption; keys identify the encoder graph, tokenizer, text, token offsets and word forms. Cache files are private generated intermediates, not model assets.

Training groups four consecutive sentences per document. Development selection uses **all** development sentences, encoded separately as in gold-token evaluation, with gold sentence boundaries. AdamW starts fresh; training resumes its optimizer, shuffle and dropout state from `last.ckpt`. Each epoch evaluates full dev; patience is three evaluations. The selected training checkpoint is tested against the unmodified parser using 1,000 paired sentence bootstrap resamples, seed 66. The candidate is kept only when the lower bound of its LAS difference is strictly positive; otherwise `selected.onenlp` is an exact copy of the original file. A chunk may stop at a step boundary and resume with the same options. `--select-only 1` re-exports the best checkpoint and recomputes the paired selection without training; it requires an existing `selection.json`. After a wall-time cancellation, the recipe explicitly restores `best.ckpt` for selection while leaving `last.ckpt` intact for resumption. No test set is loaded by this command except the pre-existing corpus-reader exclusion keys for extra treebanks.

Outputs are `baseline.tsv` and `candidate.tsv` (gold/system/correct/aligned LAS counts per sentence), `selection.json` (intervals, hashes and selection), checkpoints, and `selected.onenlp`. Selected files are saved and reloaded and their per-sentence development counts must be identical. `--smoke-docs N` bounds training and development to the first N documents, never selects a candidate for shipping, and is only for a wiring check.

Data and algorithm references are unchanged: the UD r2.18 treebanks and licences in the corpus specification/manifests; Dozat and Manning's biaffine parser; the existing C# training implementation on OneModel 1.0.0. UD data has ShareAlike lineage. This command adds no teacher, synthetic labels, data source or model dependency.

For a parser-only test read, `predict-package --raw 1 --skip-ner 1` writes both gold-token and raw-text syntactic predictions without re-reading unchanged entity evaluation corpora. Use the same corpus specification and segmenter arguments as the original model.

`run-parser-chunk.sh` accounts CPU against an append-only ledger. Each queue chunk reserves its worst-case CPU time before launching; a normal exit replaces that reservation with measured user+system seconds through a signed correction row. A hard queue kill leaves the reservation intact, so it cannot silently reset the budget. Run chunks serially for a ledger.
