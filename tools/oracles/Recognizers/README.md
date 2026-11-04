# Microsoft.Recognizers.Text (black-box comparator)

Runs Microsoft.Recognizers.Text 1.8.13 (MIT) over a recognizer gold file. Comparator only: it lives in `tools/` and never ships. The calls follow the package's published usage (the `Recognize…` method of each recognizer class with a culture and, for date/time, a reference time); the package contents were not unpacked or read.

```
dotnet run -c Release -- raw <gold.jsonl> <raw.jsonl>        # every model's results as the package returns them
dotnet run -c Release -- probe 2026-03-18T10:30:00 "text"     # one text, printed
python3 map_raw.py <raw.jsonl> <predictions.jsonl> <gold.jsonl> # into the gold schema (mapping described in docs/recognizer-gold.md)
```

The tool needs ICU (the package builds `CultureInfo` objects), so it turns invariant globalization off.
