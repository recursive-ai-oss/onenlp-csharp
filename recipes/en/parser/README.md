# English dependency parser recipe

Trains the `parser` head (a biaffine arc and label scorer with Chu-Liu/Edmonds decoding, plus a sentence-start classifier) on
Universal Dependencies English-EWT and writes CoNLL-U predictions for scoring. It uses only the public training API.

```
dotnet run -c Release --project recipes/en/parser -- train --train en_ewt-ud-train.conllu --dev en_ewt-ud-dev.conllu \
    --out run1 --epochs 40 --seconds 1500 --threads 8 --seed 7
```

- **Chunks.** `--seconds` stops training cleanly after that much wall time. Run the same command again to continue: `last.ckpt` in `--out`
  holds the weights, the optimizer and the position, and a resumed run reaches the same weights as an uninterrupted one on the same machine
  and thread count. When training has finished the command writes `model.onenlp`.
- **Data grouping.** Ten consecutive sentences make one training document, so the parser sees running text and learns where sentences end.
  The dev set is grouped the same way. Selection uses dev only.
- **Predictions.** `predict --model model.onenlp --gold en_ewt-ud-test.conllu --mode raw|raw-sentencizer|gold --out pred.conllu`:
  - `raw`: the text of each document goes through the tokenizer and the parser, which sets the sentence boundaries;
  - `raw-sentencizer`: the same, with the rule-based sentencizer setting the boundaries first;
  - `gold`: gold tokens and sentences in, heads and relations out.
- **Scoring.** `tools/OneNlp.Eval ud --gold G --pred P` (UAS, LAS, CLAS, Sentences), with `--ref-pred` for a reference system's predictions.
- **Throughput.** `speed --model M --gold G --repeat 3` parses the raw test documents and prints tokens per second and allocation per token.
- **PyTorch timing.** `tools/parser_torch.py` is the same model and batching in plain PyTorch; it reports seconds per epoch. For a like-for-like comparison run the C# recipe with `--encoder hash` (the one-token hashed embedding the script reproduces).

`PROVENANCE.md` lists the data, hashes, seeds and the weights of the shipped run.
