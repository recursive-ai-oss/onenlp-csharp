using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Eval;

/// <summary>
/// Scores recognizer predictions against a recognizer gold file (docs/recognizer-gold.md): exact-span precision, recall and F1 per type,
/// overlap-relaxed F1, and resolution accuracy on the exactly matched spans, each with a bootstrap interval over sentences.
/// </summary>
internal static class Recognizers
{
    private static readonly string[] Types =
    [
        "number", "ordinal", "percentage", "dimension", "temperature", "age", "currency", "email", "url", "ip", "phone", "hashtag", "mention", "guid",
        "date", "time", "datetime", "daterange", "timerange", "datetimerange", "duration", "set",
    ];

    private static readonly HashSet<string> Temporal = new(["date", "time", "datetime", "daterange", "timerange", "datetimerange"], StringComparer.Ordinal);

    private sealed record Item(int Start, int End, string Type, string? Value, string? Grain, bool Approx, string[] Alt, string Text);

    private sealed record Sentence(string Id, string Text, Item[] Items);

    /// <summary>Per-sentence counts for one view (a type, the temporal family, or every type).</summary>
    private readonly record struct Counts(int Gold, int Pred, int Exact, int GoldOverlapped, int PredOverlapping, int Resolvable, int ValueOk, int FullOk, int ResolvableOverlap, int ValueOkOverlap);

    private sealed record View(string Name, Func<string, bool> Includes, bool IgnoreType);

    internal static int Run(Dictionary<string, List<string>> o)
    {
        string goldPath = Required(o, "gold");
        string predPath = Required(o, "pred");
        int resamples = int.Parse(Optional(o, "resamples", "1000"), CultureInfo.InvariantCulture);
        ulong seed = ulong.Parse(Optional(o, "seed", Bootstrap.DefaultSeed.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        string system = Optional(o, "component", "unspecified");
        string dataset = Optional(o, "dataset", goldPath);

        Sentence[] gold = Read(goldPath, requireText: true);
        Dictionary<string, Sentence> predicted = Read(predPath, requireText: false).ToDictionary(s => s.Id, StringComparer.Ordinal);
        Sentence[] pred = [.. gold.Select(g => predicted.TryGetValue(g.Id, out Sentence? p) ? p : new Sentence(g.Id, g.Text, []))];

        var views = new List<View>();
        foreach (string t in Types)
        {
            if (gold.Any(g => g.Items.Any(i => i.Type == t)))
            {
                views.Add(new View(t, x => x == t, false));
            }
        }

        views.Add(new View("temporal (any subtype)", Temporal.Contains, true));
        views.Add(new View("all types", _ => true, false));

        var rows = new List<RowResult>();
        foreach (View view in views)
        {
            Counts[] counts = [.. gold.Select((g, i) => Count(g, pred[i], view))];
            rows.Add(new RowResult(
                view.Name,
                counts.Sum(c => c.Gold),
                counts.Sum(c => c.Pred),
                Interval(counts, resamples, seed, Precision),
                Interval(counts, resamples, seed, Recall),
                Interval(counts, resamples, seed, F1),
                Interval(counts, resamples, seed, RelaxedF1),
                counts.Sum(c => c.Resolvable),
                counts.Sum(c => c.Resolvable) == 0 ? null : Interval(counts, resamples, seed, ValueAccuracy),
                counts.Sum(c => c.Resolvable) == 0 ? null : Interval(counts, resamples, seed, FullAccuracy),
                counts.Sum(c => c.ResolvableOverlap) == 0 ? null : Interval(counts, resamples, seed, OverlapAccuracy)));
        }

        string markdown = ToMarkdown(system, dataset, gold.Length, resamples, seed, rows);
        Console.Out.Write(markdown);
        if (o.TryGetValue("markdown", out List<string>? md) && md.Count > 0)
        {
            File.WriteAllText(md[0], markdown, new UTF8Encoding(false));
        }

        if (o.TryGetValue("json", out List<string>? js) && js.Count > 0)
        {
            WriteJson(js[0], system, dataset, gold.Length, rows);
        }

        if (o.TryGetValue("errors", out List<string>? er) && er.Count > 0)
        {
            WriteErrors(er[0], gold, pred);
        }

        return 0;
    }

    private static string Required(Dictionary<string, List<string>> o, string name) =>
        o.TryGetValue(name, out List<string>? v) && v.Count > 0 ? v[0] : throw new ArgumentException($"--{name} is required.");

    private static string Optional(Dictionary<string, List<string>> o, string name, string fallback) =>
        o.TryGetValue(name, out List<string>? v) && v.Count > 0 ? v[0] : fallback;

    private static ConfidenceInterval Interval(Counts[] counts, int resamples, ulong seed, Statistic<Counts> statistic) =>
        Bootstrap.Interval(counts, statistic, resamples, seed);

    private static Counts Total(ReadOnlySpan<Counts> units)
    {
        int g = 0, p = 0, e = 0, go = 0, po = 0, r = 0, v = 0, f = 0, ro = 0, vo = 0;
        foreach (Counts c in units)
        {
            g += c.Gold;
            p += c.Pred;
            e += c.Exact;
            go += c.GoldOverlapped;
            po += c.PredOverlapping;
            r += c.Resolvable;
            v += c.ValueOk;
            f += c.FullOk;
            ro += c.ResolvableOverlap;
            vo += c.ValueOkOverlap;
        }

        return new Counts(g, p, e, go, po, r, v, f, ro, vo);
    }

    private static double Ratio(double n, double d) => d == 0 ? 0 : 100.0 * n / d;

    private static double Precision(ReadOnlySpan<Counts> u) => Ratio(Total(u).Exact, Total(u).Pred);

    private static double Recall(ReadOnlySpan<Counts> u) => Ratio(Total(u).Exact, Total(u).Gold);

    private static double F1(ReadOnlySpan<Counts> u)
    {
        Counts t = Total(u);
        return Ratio(2.0 * t.Exact, t.Gold + t.Pred);
    }

    private static double RelaxedF1(ReadOnlySpan<Counts> u)
    {
        Counts t = Total(u);
        double p = t.Pred == 0 ? 0 : (double)t.PredOverlapping / t.Pred;
        double r = t.Gold == 0 ? 0 : (double)t.GoldOverlapped / t.Gold;
        return p + r == 0 ? 0 : 100.0 * 2 * p * r / (p + r);
    }

    private static double ValueAccuracy(ReadOnlySpan<Counts> u) => Ratio(Total(u).ValueOk, Total(u).Resolvable);

    private static double OverlapAccuracy(ReadOnlySpan<Counts> u) => Ratio(Total(u).ValueOkOverlap, Total(u).ResolvableOverlap);

    private static double FullAccuracy(ReadOnlySpan<Counts> u) => Ratio(Total(u).FullOk, Total(u).Resolvable);

    /// <summary>
    /// Counts one sentence for one view. A prediction strictly inside a gold item of another type is ignored (the "3" of "3 km" is not a
    /// number error); a prediction with the same span as such an item but another type is a false positive.
    /// </summary>
    private static Counts Count(Sentence gold, Sentence pred, View view)
    {
        Item[] g = [.. gold.Items.Where(i => view.Includes(i.Type))];
        var seen = new HashSet<(int, int, string)>();
        var p = new List<Item>();
        foreach (Item candidate in pred.Items)
        {
            if (!view.Includes(candidate.Type) || !seen.Add((candidate.Start, candidate.End, candidate.Type)))
            {
                continue;
            }

            bool ignored = gold.Items.Any(x =>
                !view.Includes(x.Type) && x.Start <= candidate.Start && candidate.End <= x.End && (x.Start, x.End) != (candidate.Start, candidate.End));
            if (!ignored)
            {
                p.Add(candidate);
            }
        }

        int exact = 0, goldOverlapped = 0, predOverlapping = 0, resolvable = 0, valueOk = 0, fullOk = 0, resolvableOverlap = 0, valueOkOverlap = 0;
        foreach (Item x in g)
        {
            Item? match = p.FirstOrDefault(y => y.Start == x.Start && y.End == x.End && (view.IgnoreType || y.Type == x.Type));
            if (match is not null)
            {
                exact++;
                if (x.Value is not null)
                {
                    resolvable++;
                    if (ValueMatches(x, match))
                    {
                        valueOk++;
                        if (string.Equals(match.Grain ?? string.Empty, x.Grain ?? string.Empty, StringComparison.Ordinal) && match.Approx == x.Approx)
                        {
                            fullOk++;
                        }
                    }
                }
            }

            Item? widest = p.Where(y => Overlaps(x, y) && (view.IgnoreType || y.Type == x.Type))
                .OrderByDescending(y => Math.Min(x.End, y.End) - Math.Max(x.Start, y.Start)).FirstOrDefault();
            if (widest is not null)
            {
                goldOverlapped++;
                if (x.Value is not null)
                {
                    resolvableOverlap++;
                    if (ValueMatches(x, widest))
                    {
                        valueOkOverlap++;
                    }
                }
            }
        }

        foreach (Item y in p)
        {
            if (g.Any(x => Overlaps(x, y) && (view.IgnoreType || y.Type == x.Type)))
            {
                predOverlapping++;
            }
        }

        return new Counts(g.Length, p.Count, exact, goldOverlapped, predOverlapping, resolvable, valueOk, fullOk, resolvableOverlap, valueOkOverlap);
    }

    private static bool Overlaps(Item a, Item b) => a.Start < b.End && b.Start < a.End;

    /// <summary>
    /// Exact match with the gold value or one of its alternatives. A gold clock time or clock range (value starting with "T" on each side) is
    /// compared by its time of day only, because a system that resolves "3pm" to a date cannot be faulted for adding one.
    /// </summary>
    private static bool ValueMatches(Item gold, Item pred)
    {
        if (pred.Value is null)
        {
            return false;
        }

        string[] accepted = [gold.Value!, .. gold.Alt];
        foreach (string a in accepted)
        {
            string p = a.StartsWith('T') || a.Contains("/T", StringComparison.Ordinal) ? StripDates(pred.Value) : pred.Value;
            if (string.Equals(DropZeroSeconds(a), DropZeroSeconds(p), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>"T10:00:00" and "T10:00" name the same minute; a value that spells out zero seconds equals one that does not.</summary>
    private static string DropZeroSeconds(string value) =>
        string.Join('/', value.Split('/').Select(part =>
        {
            int t = part.IndexOf('T', StringComparison.Ordinal);
            if (t < 0 || part.Length < t + 9 || part[t + 6] != ':' || part[(t + 7)..(t + 9)] != "00")
            {
                return part;
            }

            return part[..(t + 6)] + part[(t + 9)..];
        }));

    private static string StripDates(string value) =>
        string.Join('/', value.Split('/').Select(part =>
        {
            int t = part.IndexOf('T', StringComparison.Ordinal);
            return t > 0 && part.Length > 10 && char.IsDigit(part[0]) ? part[t..] : part;
        }));

    private static Sentence[] Read(string path, bool requireText)
    {
        var sentences = new List<Sentence>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            string id = root.GetProperty("id").GetString() ?? throw new InvalidDataException("id");
            string text = requireText ? root.GetProperty("text").GetString() ?? string.Empty : string.Empty;
            var items = new List<Item>();
            foreach (JsonElement e in root.GetProperty("items").EnumerateArray())
            {
                items.Add(new Item(
                    e.GetProperty("start").GetInt32(),
                    e.GetProperty("end").GetInt32(),
                    e.GetProperty("type").GetString() ?? string.Empty,
                    e.TryGetProperty("value", out JsonElement v) ? v.GetString() : null,
                    e.TryGetProperty("grain", out JsonElement gr) ? gr.GetString() : null,
                    e.TryGetProperty("approx", out JsonElement ap) && ap.GetBoolean(),
                    e.TryGetProperty("alt", out JsonElement alt) ? [.. alt.EnumerateArray().Select(a => a.GetString() ?? string.Empty)] : [],
                    e.TryGetProperty("text", out JsonElement tx) ? tx.GetString() ?? string.Empty : string.Empty));
            }

            sentences.Add(new Sentence(id, text, [.. items]));
        }

        return [.. sentences];
    }

    private sealed record RowResult(
        string Name, int Gold, int Pred, ConfidenceInterval Precision, ConfidenceInterval Recall, ConfidenceInterval F1,
        ConfidenceInterval RelaxedF1, int Resolvable, ConfidenceInterval? Value, ConfidenceInterval? Full, ConfidenceInterval? Overlap);

    private static string Ci(ConfidenceInterval? c) =>
        c is null ? "n/a" : string.Create(CultureInfo.InvariantCulture, $"{c.Value.Estimate:0.0} [{c.Value.Low:0.0}, {c.Value.High:0.0}]");

    private static string ToMarkdown(string system, string dataset, int sentences, int resamples, ulong seed, List<RowResult> rows)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Recognizer scores: {system} on {dataset} ({sentences} sentences; bootstrap, {resamples} resamples over sentences, seed {seed}).\n\n");
        sb.Append("| Type | Gold | Pred | Precision | Recall | F1 | Relaxed F1 | Resolvable | Value accuracy | Value+grain+approx | Value accuracy on overlapping spans |\n");
        sb.Append("|---|---:|---:|---|---|---|---|---:|---|---|---|\n");
        foreach (RowResult r in rows)
        {
            sb.Append(CultureInfo.InvariantCulture, $"| {r.Name} | {r.Gold} | {r.Pred} | {Ci(r.Precision)} | {Ci(r.Recall)} | {Ci(r.F1)} | {Ci(r.RelaxedF1)} | {r.Resolvable} | {Ci(r.Value)} | {Ci(r.Full)} | {Ci(r.Overlap)} |\n");
        }

        return sb.ToString();
    }

    private static void WriteInterval(Utf8JsonWriter w, string name, ConfidenceInterval? c)
    {
        if (c is not ConfidenceInterval ci)
        {
            return;
        }

        w.WriteStartObject(name);
        w.WriteNumber("estimate", ci.Estimate);
        w.WriteNumber("low", ci.Low);
        w.WriteNumber("high", ci.High);
        w.WriteNumber("resamples", ci.Resamples);
        w.WriteNumber("seed", ci.Seed);
        w.WriteEndObject();
    }

    private static void WriteJson(string path, string system, string dataset, int sentences, List<RowResult> rows)
    {
        using FileStream stream = File.Create(path);
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        w.WriteString("system", system);
        w.WriteString("dataset", dataset);
        w.WriteNumber("sentences", sentences);
        w.WriteStartArray("rows");
        foreach (RowResult r in rows)
        {
            w.WriteStartObject();
            w.WriteString("type", r.Name);
            w.WriteNumber("gold", r.Gold);
            w.WriteNumber("pred", r.Pred);
            WriteInterval(w, "precision", r.Precision);
            WriteInterval(w, "recall", r.Recall);
            WriteInterval(w, "f1", r.F1);
            WriteInterval(w, "relaxed_f1", r.RelaxedF1);
            w.WriteNumber("resolvable", r.Resolvable);
            WriteInterval(w, "value_accuracy", r.Value);
            WriteInterval(w, "full_accuracy", r.Full);
            WriteInterval(w, "overlap_value_accuracy", r.Overlap);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>Diagnostics for development files: one line per missed, spurious or wrongly resolved item. Not for the held-out file.</summary>
    private static void WriteErrors(string path, Sentence[] gold, Sentence[] pred)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        for (int i = 0; i < gold.Length; i++)
        {
            foreach (Item x in gold[i].Items)
            {
                Item? m = pred[i].Items.FirstOrDefault(y => y.Start == x.Start && y.End == x.End && y.Type == x.Type);
                if (m is null)
                {
                    Item? other = pred[i].Items.FirstOrDefault(y => Overlaps(x, y));
                    w.WriteLine($"MISS\t{x.Type}\t{x.Text}\tgold={x.Value}\tnear={(other is null ? "-" : $"{other.Type}:{gold[i].Text[other.Start..Math.Min(other.End, gold[i].Text.Length)]}:{other.Value}")}");
                }
                else if (x.Value is not null && !ValueMatches(x, m))
                {
                    w.WriteLine($"VALUE\t{x.Type}\t{x.Text}\tgold={x.Value}\tpred={m.Value}");
                }
            }

            foreach (Item y in pred[i].Items)
            {
                if (!gold[i].Items.Any(x => x.Start == y.Start && x.End == y.End && x.Type == y.Type)
                    && !gold[i].Items.Any(x => x.Type != y.Type && x.Start <= y.Start && y.End <= x.End && (x.Start, x.End) != (y.Start, y.End)))
                {
                    w.WriteLine($"SPURIOUS\t{y.Type}\t{gold[i].Text[y.Start..Math.Min(y.End, gold[i].Text.Length)]}\tpred={y.Value}");
                }
            }
        }
    }
}
