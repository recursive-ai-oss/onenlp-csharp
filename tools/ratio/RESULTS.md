# C# against PyTorch training time (indicative)

`run.sh` times the second epoch of each text-category head in the C# recipe and in `ratio.py` (plain PyTorch 2.15.0 CPU, same data, architecture, batch size, 4 threads).
2026-11-06, shared 32-thread x64 worker (Ryzen 9 7950X3D) with other jobs running (load 8 to 16), .NET 10.0.403, OneModel 1.0.0.

| Head | Data | C# epoch | PyTorch epoch | Ratio |
|---|---|---|---|---|
| `TextClassifier`, multi-class | BANKING77 train, 8,980 texts | 9.1 s | 4.1 s | 2.2 |
| `TextClassifier`, multi-label | EUR-LEX dev split, 5,000 documents (2.5M tokens) | 225.7 s | 92.6 s | 2.4 |
| `SpanCategorizer` | UNER English-EWT train, 12,543 sentences | 42.3 s | 16.6 s | 2.5 |

All are under 3, so no onemodel performance bug is filed. The C# time is the difference between the first two development evaluations (100 development documents).
The PyTorch network uses the same shapes as `FastEncoder.English()` (hash tables of 32,768 x 64, a 256-wide join, two residual window layers) with its own hash of the token
attributes, so the comparison is of cost, not of weights.
