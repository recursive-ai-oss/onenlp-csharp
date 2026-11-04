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
        string input = Option(o, "in");
        string output = Option(o, "out");
        Language language = o.TryGetValue("lang", out List<string>? l) && l.Count > 0 ? Language.FromCode(l[0]) : Language.English;
        Recognizer recognizer = o.TryGetValue("rules", out List<string>? files) && files.Count > 0
            ? Recognizer.FromFiles([.. files])
            : Recognizer.For(language);

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
            if (line[0] == '{')
            {
                using JsonDocument json = JsonDocument.Parse(line);
                text = json.RootElement.GetProperty("text").GetString() ?? string.Empty;
                id = json.RootElement.TryGetProperty("id", out JsonElement e) ? e.ToString() : null;
            }

            timer.Start();
            IReadOnlyList<Recognition> found = recognizer.Recognize(text);
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
