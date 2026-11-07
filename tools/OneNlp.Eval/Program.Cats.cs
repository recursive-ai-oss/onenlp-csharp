using System.Globalization;
using System.Text.Json;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Eval;

// Scores text classification and span categorization from JSON Lines files, with a bootstrap over documents. One line per
// document, in the same order in every file:
//   gold:   the training-data line ({"text": ..., "cats": {...}} or {"text": ..., "spans": {"sc": [[start, end, "LABEL"], ...]}})
//   pred:   {"cats": {"LABEL": score, ...}} or {"spans": [[start, end, "LABEL", score], ...]}, with character offsets into the text.
// Any system that writes this shape can be scored and compared (ours, and the spaCy baseline's driver).
internal static partial class Program
{
    private readonly record struct Scored(string Gold, string Predicted);

    private readonly record struct MultiScored(bool[] Gold, float[] Scores);

    private static int RunCats(Dictionary<string, List<string>> o)
    {
        bool multi = One(o, "mode") switch
        {
            "single" => false,
            "multi" => true,
            var m => throw new ArgumentException($"--mode is single or multi, not '{m}'."),
        };
        List<Dictionary<string, float>> gold = ReadCats(One(o, "gold"), "cats");
        List<Dictionary<string, float>> ours = ReadCats(One(o, "pred"), "cats");
        List<Dictionary<string, float>>? reference = o.ContainsKey("ref-pred") ? ReadCats(One(o, "ref-pred"), "cats") : null;
        if (ours.Count != gold.Count || (reference is not null && reference.Count != gold.Count))
        {
            throw new InvalidDataException($"The files differ in length: gold {gold.Count}, pred {ours.Count}{(reference is null ? string.Empty : $", ref {reference.Count}")}.");
        }

        string[] labels = [.. gold.SelectMany(g => g.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        var rows = new List<ResultRow>();
        if (!multi)
        {
            List<Scored> a = Pair(gold, ours);
            List<Scored>? b = reference is null ? null : Pair(gold, reference);
            rows.Add(MakeRow(o, "accuracy", a, b, u => 100 * ClassificationScorer.Accuracy([.. u.ToArray().Select(x => x.Gold)], [.. u.ToArray().Select(x => x.Predicted)])));
            rows.Add(MakeRow(o, "macro-F1", a, b, u => 100 * ClassificationScorer.MacroF1([.. u.ToArray().Select(x => x.Gold)], [.. u.ToArray().Select(x => x.Predicted)])));
        }
        else
        {
            List<MultiScored> a = Matrix(gold, ours, labels);
            List<MultiScored>? b = reference is null ? null : Matrix(gold, reference, labels);
            rows.Add(MakeRow(o, "micro-F1", a, b, u => 100 * MultiF1(u).Micro));
            rows.Add(MakeRow(o, "macro-F1", a, b, u => 100 * MultiF1(u).Macro));
            rows.Add(MakeRow(o, "macro-ROC-AUC", a, b, u => 100 * ClassificationScorer.MacroRocAuc([.. u.ToArray().Select(x => x.Gold)], [.. u.ToArray().Select(x => x.Scores)])));
        }

        return Emit(o, rows);
    }

    private static (double Macro, double Micro) MultiF1(ReadOnlySpan<MultiScored> units) =>
        ClassificationScorer.MultiLabelF1([.. units.ToArray().Select(x => x.Gold)], [.. units.ToArray().Select(x => x.Scores)]);

    private static int RunSpanGroup(Dictionary<string, List<string>> o)
    {
        string group = One(o, "group", "sc");
        List<HashSet<LabeledSpan>> gold = ReadGoldSpans(One(o, "gold"), group);
        List<HashSet<LabeledSpan>> ours = ReadPredictedSpans(One(o, "pred"));
        List<HashSet<LabeledSpan>>? reference = o.ContainsKey("ref-pred") ? ReadPredictedSpans(One(o, "ref-pred")) : null;
        if (ours.Count != gold.Count || (reference is not null && reference.Count != gold.Count))
        {
            throw new InvalidDataException($"The files differ in length: gold {gold.Count}, pred {ours.Count}{(reference is null ? string.Empty : $", ref {reference.Count}")}.");
        }

        IReadOnlyList<MetricCounts> a = SpanScorer.Score([.. gold], [.. ours]).PerDocument;
        IReadOnlyList<MetricCounts>? b = reference is null ? null : SpanScorer.Score([.. gold], [.. reference]).PerDocument;
        return Emit(o, [
            MakeRow(o, "span-F1", a, b, Bootstrap.F1Points),
            MakeRow(o, "span-precision", a, b, u => Sum(u).Precision * 100),
            MakeRow(o, "span-recall", a, b, u => Sum(u).Recall * 100),
        ]);
    }

    // The gold category of a single-label document is its highest-scored label (ties go to the label first in ordinal order);
    // a document without any gold score above 0 is labelled "" and counts as an error for every system.
    private static string Top(Dictionary<string, float> cats)
    {
        string best = string.Empty;
        float top = 0f;
        foreach (KeyValuePair<string, float> c in cats.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            if (c.Value > top)
            {
                top = c.Value;
                best = c.Key;
            }
        }

        return best;
    }

    private static List<Scored> Pair(List<Dictionary<string, float>> gold, List<Dictionary<string, float>> predicted) =>
        [.. gold.Select((g, i) => new Scored(Top(g), Top(predicted[i])))];

    private static List<MultiScored> Matrix(List<Dictionary<string, float>> gold, List<Dictionary<string, float>> predicted, string[] labels) =>
        [.. gold.Select((g, i) => new MultiScored(
            [.. labels.Select(l => g.TryGetValue(l, out float v) && v >= 0.5f)],
            [.. labels.Select(l => predicted[i].TryGetValue(l, out float v) ? v : 0f)]))];

    private static List<Dictionary<string, float>> ReadCats(string path, string property)
    {
        var list = new List<Dictionary<string, float>>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.AsSpan().Trim().Length == 0)
            {
                continue;
            }

            using JsonDocument json = JsonDocument.Parse(line);
            var cats = new Dictionary<string, float>(StringComparer.Ordinal);
            if (json.RootElement.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in element.EnumerateObject())
                {
                    cats[p.Name] = p.Value.ValueKind switch
                    {
                        JsonValueKind.True => 1f,
                        JsonValueKind.False => 0f,
                        _ => p.Value.GetSingle(),
                    };
                }
            }

            list.Add(cats);
        }

        return list;
    }

    private static List<HashSet<LabeledSpan>> ReadGoldSpans(string path, string group)
    {
        var list = new List<HashSet<LabeledSpan>>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.AsSpan().Trim().Length == 0)
            {
                continue;
            }

            using JsonDocument json = JsonDocument.Parse(line);
            var set = new HashSet<LabeledSpan>();
            if (json.RootElement.TryGetProperty("spans", out JsonElement spans) && spans.TryGetProperty(group, out JsonElement items))
            {
                foreach (JsonElement s in items.EnumerateArray())
                {
                    set.Add(new LabeledSpan(s[0].GetInt32(), s[1].GetInt32(), s[2].GetString() ?? string.Empty));
                }
            }

            list.Add(set);
        }

        return list;
    }

    private static List<HashSet<LabeledSpan>> ReadPredictedSpans(string path)
    {
        var list = new List<HashSet<LabeledSpan>>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.AsSpan().Trim().Length == 0)
            {
                continue;
            }

            using JsonDocument json = JsonDocument.Parse(line);
            var set = new HashSet<LabeledSpan>();
            if (json.RootElement.TryGetProperty("spans", out JsonElement items))
            {
                foreach (JsonElement s in items.EnumerateArray())
                {
                    set.Add(new LabeledSpan(s[0].GetInt32(), s[1].GetInt32(), s[2].GetString() ?? string.Empty));
                }
            }

            list.Add(set);
        }

        return list;
    }
}
