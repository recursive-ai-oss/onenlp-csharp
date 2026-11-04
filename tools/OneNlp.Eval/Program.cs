using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Eval;

/// <summary>One row of the results table: a component's score on a metric, with its interval and the reference it is judged against.</summary>
internal sealed record ResultRow(
    string Component,
    string Language,
    string Dataset,
    string Metric,
    ConfidenceInterval Score,
    string ReferenceSource,
    double? Reference,
    ConfidenceInterval? ReferenceInterval,
    bool? WithinNoise,
    ConfidenceInterval? Difference);

internal static class Program
{
    private const string Usage = """
        OneNlp.Eval: scores predictions and writes results rows.

          ud   --gold G.conllu --pred P.conllu [--ref-pred R.conllu] [--reference LAS=88.1,UPOS=96.0] [--reference-source TEXT]
               [--component NAME] [--language CODE] [--dataset NAME] [--metrics LAS,UPOS,...]
               [--resamples 1000] [--seed N] [--json OUT.json] [--markdown OUT.md]
          span --gold G.iob2 --pred P.iob2 [same options; metrics: micro-F1, macro-F1, precision, recall]
          segment --raw RAW.txt --out OUT.conllu [--language en] [--mwt]
               tokenizes and splits sentences with the default pipeline and writes CoNLL-U with only ID and FORM filled, for
               scoring with 'ud --metrics Tokens,Sentences,Words'. Every token is a word; --mwt also writes the pieces of a split
               contraction (do + n't) as a multi-word token line, as English UD treebanks annotate them.
          recognizers --gold GOLD.jsonl --pred PRED.jsonl [--component NAME] [--dataset NAME] [--resamples 1000] [--seed N]
               [--json OUT.json] [--markdown OUT.md] [--errors OUT.tsv]
               [--ref-pred REF.jsonl --ref-component NAME [--compare-markdown OUT.md] [--compare-json OUT.json]]
               scores recognizer predictions against a recognizer gold file (docs/recognizer-gold.md): exact-span and relaxed F1 and
               resolution accuracy per type, with bootstrap intervals over sentences. --errors lists the misses (development files only).
               --ref-pred adds a paired bootstrap against another system on the same sentences: F1 and value accuracy per type with the
               difference interval and a within-noise / ahead / behind verdict (docs/measurement.md).
          recognize --in X.jsonl --out Y.jsonl [--lang en] [--rules FILE ...]
               runs the recognizers (numbers, ordinals, percents, measures, money, e-mail, URL, IP, phone-like, hashtag, mention, GUID)
               over JSON lines {"id":..,"text":..} (or plain text lines) and writes one JSON line of results per input. Offsets are
               UTF-16 (start, end) and code points (cpStart, cpEnd). The time spent recognizing is printed to standard error.
          dump-ud   --gold G.conllu --pred P1.conllu [P2.conllu ...] --json OUT.json    (all counts, no bootstrap)
          dump-span --gold G.iob2 --pred P1.iob2 [P2.iob2 ...] --json OUT.json

        Files for 'span' hold one token per line, the tag in the last column, and a blank line between sentences.
        """;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            Dictionary<string, List<string>> options = ParseOptions(args.AsSpan(1));
            switch (args[0])
            {
                case "ud":
                    return RunUd(options);
                case "span":
                    return RunSpan(options);
                case "segment":
                    return Segment.Run(options);
                case "recognizers":
                    return Recognizers.Run(options);
                case "recognize":
                    return RecognizeCommand.Run(options);
                case "dump-ud":
                    return DumpUd(options);
                case "dump-span":
                    return DumpSpan(options);
                default:
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException or KeyNotFoundException or FormatException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static Dictionary<string, List<string>> ParseOptions(ReadOnlySpan<string> args)
    {
        var options = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (string arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                current = [];
                options[arg[2..]] = current;
            }
            else if (current is not null)
            {
                current.Add(arg);
            }
            else
            {
                throw new ArgumentException($"Unexpected argument '{arg}'.");
            }
        }

        return options;
    }

    private static string One(Dictionary<string, List<string>> o, string name, string? fallback = null) =>
        o.TryGetValue(name, out List<string>? v) && v.Count > 0 ? v[0] : fallback ?? throw new ArgumentException($"--{name} is required.");

    private static int RunUd(Dictionary<string, List<string>> o)
    {
        UdCorpus gold = UdCorpus.ReadFile(One(o, "gold"));
        UdEvaluation ours = UdScorer.Evaluate(gold, UdCorpus.ReadFile(One(o, "pred")));
        UdEvaluation? reference = o.ContainsKey("ref-pred") ? UdScorer.Evaluate(gold, UdCorpus.ReadFile(One(o, "ref-pred"))) : null;
        string[] metrics = One(o, "metrics", string.Join(',', UdEvaluation.MetricNames)).Split(',');
        var rows = new List<ResultRow>();
        foreach (string metric in metrics)
        {
            rows.Add(MakeRow(o, metric, ours.PerUnit(metric), reference?.PerUnit(metric), Bootstrap.F1Points));
        }

        return Emit(o, rows);
    }

    private static int RunSpan(Dictionary<string, List<string>> o)
    {
        IReadOnlyList<IReadOnlyList<string>> gold = ReadTags(One(o, "gold"));
        SpanEvaluation ours = SpanScorer.ScoreIob2(gold, ReadTags(One(o, "pred")));
        SpanEvaluation? reference = o.ContainsKey("ref-pred") ? SpanScorer.ScoreIob2(gold, ReadTags(One(o, "ref-pred"))) : null;
        var rows = new List<ResultRow>
        {
            MakeRow(o, "micro-F1", ours.PerDocument, reference?.PerDocument, Bootstrap.F1Points),
            MakeRow(o, "micro-precision", ours.PerDocument, reference?.PerDocument, u => Sum(u).Precision * 100),
            MakeRow(o, "micro-recall", ours.PerDocument, reference?.PerDocument, u => Sum(u).Recall * 100),
        };
        return Emit(o, rows);
    }

    private static MetricCounts Sum(ReadOnlySpan<MetricCounts> units)
    {
        MetricCounts sum = default;
        foreach (MetricCounts c in units)
        {
            sum += c;
        }

        return sum;
    }

    private static ResultRow MakeRow(
        Dictionary<string, List<string>> o,
        string metric,
        IReadOnlyList<MetricCounts> ours,
        IReadOnlyList<MetricCounts>? reference,
        Statistic<MetricCounts> statistic)
    {
        int resamples = int.Parse(One(o, "resamples", "1000"), CultureInfo.InvariantCulture);
        ulong seed = ulong.Parse(One(o, "seed", Bootstrap.DefaultSeed.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        ConfidenceInterval score;
        ConfidenceInterval? referenceInterval = null;
        ConfidenceInterval? difference = null;
        bool? within = null;
        double? published = PublishedValue(o, metric);
        string source = One(o, "reference-source", reference is not null ? "system run on the same data" : published is not null ? "published" : "none");
        if (reference is not null)
        {
            PairedResult paired = Bootstrap.Paired(ours, reference, statistic, resamples, seed);
            score = paired.First;
            referenceInterval = paired.Second;
            difference = paired.Difference;
            within = NoiseRule.WithinNoise(score, paired.Second);
        }
        else
        {
            score = Bootstrap.Interval(ours, statistic, resamples, seed);
            if (published is not null)
            {
                within = NoiseRule.WithinNoise(score, published.Value);
            }
        }

        return new ResultRow(
            One(o, "component", "unspecified"),
            One(o, "language", "und"),
            One(o, "dataset", "unspecified"),
            metric,
            score,
            source,
            referenceInterval?.Estimate ?? published,
            referenceInterval,
            within,
            difference);
    }

    private static double? PublishedValue(Dictionary<string, List<string>> o, string metric)
    {
        if (!o.TryGetValue("reference", out List<string>? values))
        {
            return null;
        }

        foreach (string pair in values.SelectMany(v => v.Split(',')))
        {
            int eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0 && string.Equals(pair[..eq], metric, StringComparison.Ordinal))
            {
                return double.Parse(pair[(eq + 1)..], CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static List<IReadOnlyList<string>> ReadTags(string path)
    {
        var sentences = new List<IReadOnlyList<string>>();
        var current = new List<string>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    sentences.Add(current);
                    current = [];
                }

                continue;
            }

            if (line[0] == '#')
            {
                continue;
            }

            int cut = line.LastIndexOfAny([' ', '\t']);
            current.Add(cut < 0 ? line : line[(cut + 1)..]);
        }

        if (current.Count > 0)
        {
            sentences.Add(current);
        }

        return sentences;
    }

    private static int Emit(Dictionary<string, List<string>> o, List<ResultRow> rows)
    {
        string markdown = ToMarkdown(rows);
        Console.Out.Write(markdown);
        if (o.TryGetValue("markdown", out List<string>? md) && md.Count > 0)
        {
            File.WriteAllText(md[0], markdown, new UTF8Encoding(false));
        }

        if (o.TryGetValue("json", out List<string>? js) && js.Count > 0)
        {
            using FileStream stream = File.Create(js[0]);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartArray();
            foreach (ResultRow r in rows)
            {
                WriteRow(writer, r);
            }

            writer.WriteEndArray();
        }

        return 0;
    }

    private static void WriteInterval(Utf8JsonWriter w, string name, ConfidenceInterval c)
    {
        w.WriteStartObject(name);
        w.WriteNumber("estimate", c.Estimate);
        w.WriteNumber("low", c.Low);
        w.WriteNumber("high", c.High);
        w.WriteNumber("resamples", c.Resamples);
        w.WriteNumber("seed", c.Seed);
        w.WriteEndObject();
    }

    private static void WriteRow(Utf8JsonWriter w, ResultRow r)
    {
        w.WriteStartObject();
        w.WriteString("component", r.Component);
        w.WriteString("language", r.Language);
        w.WriteString("dataset", r.Dataset);
        w.WriteString("metric", r.Metric);
        WriteInterval(w, "score", r.Score);
        w.WriteString("reference_source", r.ReferenceSource);
        if (r.Reference is double rv)
        {
            w.WriteNumber("reference", rv);
        }

        if (r.ReferenceInterval is ConfidenceInterval ri)
        {
            WriteInterval(w, "reference_interval", ri);
        }

        if (r.Difference is ConfidenceInterval di)
        {
            WriteInterval(w, "difference", di);
        }

        if (r.WithinNoise is bool wn)
        {
            w.WriteBoolean("within_noise", wn);
        }

        w.WriteEndObject();
    }

    private static string ToMarkdown(List<ResultRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("| Component | Language | Metric | Score | 95% CI | Reference | Within noise |\n");
        sb.Append("|---|---|---|---:|---|---|---|\n");
        foreach (ResultRow r in rows)
        {
            string reference = r.Reference is double v
                ? string.Create(CultureInfo.InvariantCulture, $"{v:0.00}")
                    + (r.ReferenceInterval is ConfidenceInterval ci ? string.Create(CultureInfo.InvariantCulture, $" [{ci.Low:0.00}, {ci.High:0.00}]") : string.Empty)
                    + " (" + r.ReferenceSource + ")"
                : "n/a";
            string within = r.WithinNoise is bool b ? (b ? "yes" : "no") : "n/a";
            sb.Append(CultureInfo.InvariantCulture, $"| {r.Component} | {r.Language} | {r.Metric} | {r.Score.Estimate:0.00} | [{r.Score.Low:0.00}, {r.Score.High:0.00}] | {reference} | {within} |\n");
        }

        return sb.ToString();
    }

    private static int DumpUd(Dictionary<string, List<string>> o)
    {
        UdCorpus gold = UdCorpus.ReadFile(One(o, "gold"));
        using FileStream stream = File.Create(One(o, "json"));
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        foreach (string pred in o["pred"])
        {
            UdEvaluation e = UdScorer.Evaluate(gold, UdCorpus.ReadFile(pred));
            w.WriteStartObject(pred);
            foreach (string metric in UdEvaluation.MetricNames)
            {
                MetricCounts c = e.Total(metric);
                w.WriteStartObject(metric);
                w.WriteNumber("precision", c.Precision);
                w.WriteNumber("recall", c.Recall);
                w.WriteNumber("f1", c.F1);
                w.WriteNumber("aligned_accuracy", c.AlignedAccuracy);
                w.WriteEndObject();
            }

            w.WriteEndObject();
        }

        w.WriteEndObject();
        return 0;
    }

    private static int DumpSpan(Dictionary<string, List<string>> o)
    {
        IReadOnlyList<IReadOnlyList<string>> gold = ReadTags(One(o, "gold"));
        using FileStream stream = File.Create(One(o, "json"));
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        foreach (string pred in o["pred"])
        {
            SpanEvaluation e = SpanScorer.ScoreIob2(gold, ReadTags(pred));
            w.WriteStartObject(pred);
            WriteCounts(w, "micro", e.Micro);
            w.WriteNumber("macro_f1", e.MacroF1);
            foreach ((string label, MetricCounts c) in e.PerLabel)
            {
                WriteCounts(w, label, c);
            }

            w.WriteEndObject();
        }

        w.WriteEndObject();
        return 0;
    }

    private static void WriteCounts(Utf8JsonWriter w, string name, MetricCounts c)
    {
        w.WriteStartObject(name);
        w.WriteNumber("precision", c.Precision);
        w.WriteNumber("recall", c.Recall);
        w.WriteNumber("f1", c.F1);
        w.WriteNumber("support", c.Gold);
        w.WriteEndObject();
    }
}
