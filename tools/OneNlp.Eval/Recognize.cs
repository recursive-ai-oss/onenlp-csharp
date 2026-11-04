using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Eval;

/// <summary>
/// The <c>recognize</c> command: runs the recognizers over a file of texts and writes one JSON line of results per text.
/// </summary>
/// <remarks>
/// Input lines are either JSON objects with a <c>text</c> string (and an optional <c>id</c>), or plain text. Output lines are
/// <c>{"id":..., "results":[{"start","end","cpStart","cpEnd","text","label","kind","value","unit","dimension","currency","normalized","confidence"}]}</c>.
/// <c>start</c> and <c>end</c> are UTF-16 offsets; <c>cpStart</c> and <c>cpEnd</c> count code points. <c>value</c> is a string, so no digit is lost.
/// </remarks>
internal static class RecognizeCommand
{
    internal static int Run(Dictionary<string, List<string>> o)
    {
        if (o.ContainsKey("gold"))
        {
            return RunGold(o);
        }

        string input = Option(o, "in");
        string output = Option(o, "out");
        Language language = o.TryGetValue("lang", out List<string>? l) && l.Count > 0 ? Language.FromCode(l[0]) : Language.English;
        Recognizer recognizer = o.TryGetValue("rules", out List<string>? files) && files.Count > 0
            ? Recognizer.FromFiles([.. files])
            : Recognizer.For(language);

        DateTimeOffset? reference = o.TryGetValue("reference", out List<string>? rf) && rf.Count > 0
            ? DateTimeOffset.Parse(rf[0], System.Globalization.CultureInfo.InvariantCulture)
            : null;
        string? region = o.TryGetValue("region", out List<string>? rg) && rg.Count > 0 ? rg[0] : null;
        var context = new RecognitionContext(reference) { Region = region };
        DateTimeOffset? defaultReference = o.TryGetValue("reference", out List<string>? rf) && rf.Count > 0
            ? DateTimeOffset.Parse(rf[0], System.Globalization.CultureInfo.InvariantCulture)
            : null;
        string? region = o.TryGetValue("region", out List<string>? rg) && rg.Count > 0 ? rg[0] : null;
        long chars = 0;
        int docs = 0;
        int results = 0;
        var timer = new Stopwatch();
        using var outFile = new FileStream(output, FileMode.Create, FileAccess.Write);
        using var writer = new Utf8JsonWriter(outFile, new JsonWriterOptions { SkipValidation = true });
        foreach (string line in File.ReadLines(input, Encoding.UTF8))
        {
            if (line.Length == 0)
            {
                continue;
            }

            string? id = null;
            string text = line;
            DateTimeOffset? reference = defaultReference;
            if (line[0] == '{')
            {
                using JsonDocument json = JsonDocument.Parse(line);
                text = json.RootElement.GetProperty("text").GetString() ?? string.Empty;
                id = json.RootElement.TryGetProperty("id", out JsonElement e) ? e.ToString() : null;
                if (json.RootElement.TryGetProperty("reference", out JsonElement rj) && rj.GetString() is string rs)
                {
                    reference = new DateTimeOffset(DateTime.Parse(rs, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None), TimeSpan.Zero);
                }
            }

            timer.Start();
            IReadOnlyList<Recognition> found = recognizer.Recognize(text, context);
            timer.Stop();
            chars += text.Length;
            docs++;
            results += found.Count;
            Write(writer, id, text, found);
            writer.Flush();
            outFile.WriteByte((byte)'\n');
            writer.Reset(outFile);
        }

        double seconds = timer.Elapsed.TotalSeconds;
        Console.Error.WriteLine(
            $"{docs} texts, {chars} chars, {results} results; recognition time {seconds:F3} s ({(seconds > 0 ? chars / seconds / 1e6 : 0):F2} M chars/s)");
        return 0;
    }

    /// <summary>
    /// <c>--gold FILE --out PRED.jsonl [--region us|gb]</c>: reads a recognizer gold file (docs/recognizer-gold.md), recognizes each sentence with
    /// its own reference time (read as UTC wall-clock) and writes predictions in the gold item shape for the <c>recognizers</c> scorer.
    /// </summary>
    private static int RunGold(Dictionary<string, List<string>> o)
    {
        string output = Option(o, "out");
        string? region = o.TryGetValue("region", out List<string>? rg) && rg.Count > 0 ? rg[0] : null;
        Recognizer recognizer = Recognizer.English;
        using var outFile = new FileStream(output, FileMode.Create, FileAccess.Write);
        using var writer = new Utf8JsonWriter(outFile, new JsonWriterOptions { SkipValidation = true });
        long chars = 0;
        var timer = new Stopwatch();
        foreach (string line in File.ReadLines(Option(o, "gold"), Encoding.UTF8))
        {
            if (line.Length == 0)
            {
                continue;
            }

            using JsonDocument json = JsonDocument.Parse(line);
            JsonElement root = json.RootElement;
            string text = root.GetProperty("text").GetString() ?? string.Empty;
            DateTimeOffset? reference = root.TryGetProperty("reference", out JsonElement rf) && rf.GetString() is string rs
                ? new DateTimeOffset(DateTime.Parse(rs, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None), TimeSpan.Zero)
                : null;
            timer.Start();
            IReadOnlyList<Recognition> found = recognizer.Recognize(text, new RecognitionContext(reference) { Region = region });
            timer.Stop();
            chars += text.Length;
            writer.WriteStartObject();
            writer.WriteString("id", root.GetProperty("id").GetString());
            writer.WriteStartArray("items");
            foreach (Recognition r in found)
            {
                (string type, string? value, string? grain, bool approx) = GoldForm(r);
                writer.WriteStartObject();
                writer.WriteNumber("start", r.Start);
                writer.WriteNumber("end", r.End);
                writer.WriteString("type", type);
                if (value is not null)
                {
                    writer.WriteString("value", value);
                }

                if (grain is not null)
                {
                    writer.WriteString("grain", grain);
                }

                if (approx)
                {
                    writer.WriteBoolean("approx", true);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            outFile.WriteByte((byte)'\n');
            writer.Reset(outFile);
        }

        Console.Error.WriteLine($"{chars} chars; recognition time {timer.Elapsed.TotalSeconds:F3} s");
        return 0;
    }

    /// <summary>A result as a gold-file item: its type name, value, grain and approximate flag (docs/recognizer-gold.md).</summary>
    private static (string Type, string? Value, string? Grain, bool Approx) GoldForm(Recognition r)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        switch (r)
        {
            case Quantity q:
            {
                string n = q.Value.ToString(c);
                return q.Kind switch
                {
                    QuantityKind.Cardinal => ("number", n, null, false),
                    QuantityKind.Ordinal => ("ordinal", n, null, false),
                    QuantityKind.Percent => ("percentage", n, null, false),
                    QuantityKind.Money => ("currency", n + " " + q.Currency, null, false),
                    _ when q.Dimension == "temperature" => ("temperature", n + " " + q.Unit, null, false),
                    _ when q.Dimension == "age" => ("age", n + " " + q.Unit, null, false),
                    _ => ("dimension", n + " " + q.Unit, null, false),
                };
            }

            case SequenceMatch s:
                return (s.Kind switch
                {
                    SequenceKind.Email => "email",
                    SequenceKind.Url => "url",
                    SequenceKind.IpAddress => "ip",
                    SequenceKind.PhoneLike => "phone",
                    SequenceKind.Hashtag => "hashtag",
                    SequenceKind.Mention => "mention",
                    _ => "guid",
                }, null, null, false);
            case TemporalMatch t:
                return (t.Kind.ToString().ToLowerInvariant(), t.Value, t.Granularity?.ToString().ToLowerInvariant(), t.Approximate);
            default:
                return (r.Label.ToLowerInvariant(), null, null, false);
        }
    }

    private static void Write(Utf8JsonWriter w, string? id, string text, IReadOnlyList<Recognition> found)
    {
        w.WriteStartObject();
        if (id is not null)
        {
            w.WriteString("id", id);
        }

        w.WriteStartArray("results");
        foreach (Recognition r in found)
        {
            w.WriteStartObject();
            w.WriteNumber("start", r.Start);
            w.WriteNumber("end", r.End);
            w.WriteNumber("cpStart", CodePoints(text, 0, r.Start));
            w.WriteNumber("cpEnd", CodePoints(text, 0, r.End));
            w.WriteString("text", r.Text);
            w.WriteString("label", r.Label);
            w.WriteNumber("confidence", r.Confidence);
            switch (r)
            {
                case Quantity q:
                    w.WriteString("kind", q.Kind.ToString());
                    w.WriteString("value", q.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (q.Unit is not null)
                    {
                        w.WriteString("unit", q.Unit);
                    }

                    if (q.Dimension is not null)
                    {
                        w.WriteString("dimension", q.Dimension);
                    }

                    if (q.Currency is not null)
                    {
                        w.WriteString("currency", q.Currency);
                    }

                    break;
                case SequenceMatch s:
                    w.WriteString("kind", s.Kind.ToString());
                    w.WriteString("normalized", s.Normalized);
                    break;
                case TemporalMatch t:
                    w.WriteString("kind", t.Kind.ToString());
                    w.WriteString("value", t.Value);
                    if (t.Granularity is Granularity g)
                    {
                        w.WriteString("grain", g.ToString().ToLowerInvariant());
                    }

                    w.WriteBoolean("approx", t.Approximate);
                    break;
                case TemporalMatch t:
                    w.WriteString("kind", t.Kind.ToString());
                    w.WriteString("value", t.Value);
                    if (t.Granularity is Granularity g)
                    {
                        w.WriteString("grain", g.ToString().ToLowerInvariant());
                    }

                    w.WriteBoolean("approx", t.Approximate);
                    break;
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static int CodePoints(string text, int from, int to)
    {
        int n = 0;
        for (int i = from; i < to; i++)
        {
            if (!char.IsLowSurrogate(text[i]))
            {
                n++;
            }
        }

        return n;
    }

    private static string Option(Dictionary<string, List<string>> o, string name) =>
        o.TryGetValue(name, out List<string>? v) && v.Count > 0 ? v[0] : throw new ArgumentException($"--{name} is required.");
}
