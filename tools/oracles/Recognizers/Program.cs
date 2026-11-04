// Runs Microsoft.Recognizers.Text (English) as a black box over a recognizer gold file.
//
//   RecognizersOracle raw   <in.jsonl> <out.jsonl>   every model's ModelResult as the package returns it (type, text, offsets, resolution)
//   RecognizersOracle probe <reference ISO> <text>   the same for one text, printed
//
// The calls follow the package's published README usage (the per-model Recognize methods of each recognizer class, with a culture and,
// for date/time, a reference time). The package contents were not unpacked or read.
using System.Text.Json;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;
using Microsoft.Recognizers.Text.NumberWithUnit;
using Microsoft.Recognizers.Text.Sequence;

namespace OneNlp.Tools.RecognizersOracle;

internal static class Program
{
    private static readonly string Culture = Microsoft.Recognizers.Text.Culture.English;

    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "probe")
        {
            DateTime reference = DateTime.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
            using var w = new Utf8JsonWriter(Console.OpenStandardOutput(), new JsonWriterOptions { Indented = true });
            WriteModels(w, args[2], reference);
            w.Flush();
            Console.WriteLine();
            return 0;
        }

        if (args.Length == 3 && args[0] == "raw")
        {
            using var output = new StreamWriter(args[2], false, new System.Text.UTF8Encoding(false)) { NewLine = "\n" };
            foreach (string line in File.ReadLines(args[1]))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                using JsonDocument doc = JsonDocument.Parse(line);
                string id = doc.RootElement.GetProperty("id").GetString()!;
                string text = doc.RootElement.GetProperty("text").GetString()!;
                DateTime reference = DateTime.Parse(doc.RootElement.GetProperty("reference").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                using var ms = new MemoryStream();
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    w.WriteString("id", id);
                    w.WritePropertyName("results");
                    WriteModels(w, text, reference);
                    w.WriteEndObject();
                }

                output.WriteLine(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
            }

            return 0;
        }

        if (args.Length is 3 or 4 && args[0] == "bench")
        {
            return Bench(args[1], args[2], args.Length == 4 && args[3] == "with-datetime");
        }

        Console.Error.WriteLine("usage: RecognizersOracle raw <in.jsonl> <out.jsonl> | probe <reference> <text> | bench <lines.txt> <reference> [with-datetime]");
        return 2;
    }

    /// <summary>
    /// Times the models over a file of text lines, after a warm-up pass over the first lines that builds the models. By default it runs only
    /// the number, unit, currency and sequence models (the work our recognizers do); with-datetime adds the date/time model.
    /// </summary>
    private static int Bench(string path, string referenceText, bool withDateTime)
    {
        string[] lines = File.ReadAllLines(path);
        DateTime reference = DateTime.Parse(referenceText, System.Globalization.CultureInfo.InvariantCulture);
        for (int i = 0; i < Math.Min(200, lines.Length); i++)
        {
            RunModels(lines[i], reference, withDateTime);
        }

        long chars = 0;
        long results = 0;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        foreach (string line in lines)
        {
            results += RunModels(line, reference, withDateTime);
            chars += line.Length;
        }

        timer.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        double seconds = timer.Elapsed.TotalSeconds;
        Console.WriteLine($"{lines.Length} texts, {chars} chars, {results} results; recognition time {seconds:F3} s ({chars / seconds / 1e6:F2} M chars/s); allocated {allocated / 1e6:F1} MB");
        return 0;
    }

    private static int RunModels(string text, DateTime reference, bool withDateTime)
    {
        int n = NumberRecognizer.RecognizeNumber(text, Culture).Count;
        n += NumberRecognizer.RecognizeOrdinal(text, Culture).Count;
        n += NumberRecognizer.RecognizePercentage(text, Culture).Count;
        n += NumberWithUnitRecognizer.RecognizeCurrency(text, Culture).Count;
        n += NumberWithUnitRecognizer.RecognizeDimension(text, Culture).Count;
        n += NumberWithUnitRecognizer.RecognizeTemperature(text, Culture).Count;
        n += NumberWithUnitRecognizer.RecognizeAge(text, Culture).Count;
        n += SequenceRecognizer.RecognizeEmail(text, Culture).Count;
        n += SequenceRecognizer.RecognizeURL(text, Culture).Count;
        n += SequenceRecognizer.RecognizeIpAddress(text, Culture).Count;
        n += SequenceRecognizer.RecognizePhoneNumber(text, Culture).Count;
        n += SequenceRecognizer.RecognizeHashtag(text, Culture).Count;
        n += SequenceRecognizer.RecognizeMention(text, Culture).Count;
        n += SequenceRecognizer.RecognizeGUID(text, Culture).Count;
        if (withDateTime)
        {
            n += DateTimeRecognizer.RecognizeDateTime(text, Culture, DateTimeOptions.None, reference).Count;
        }

        return n;
    }

    private static void WriteModels(Utf8JsonWriter w, string text, DateTime reference)
    {
        w.WriteStartArray();
        Write(w, "number", NumberRecognizer.RecognizeNumber(text, Culture));
        Write(w, "ordinal", NumberRecognizer.RecognizeOrdinal(text, Culture));
        Write(w, "percentage", NumberRecognizer.RecognizePercentage(text, Culture));
        Write(w, "currency", NumberWithUnitRecognizer.RecognizeCurrency(text, Culture));
        Write(w, "dimension", NumberWithUnitRecognizer.RecognizeDimension(text, Culture));
        Write(w, "temperature", NumberWithUnitRecognizer.RecognizeTemperature(text, Culture));
        Write(w, "age", NumberWithUnitRecognizer.RecognizeAge(text, Culture));
        Write(w, "email", SequenceRecognizer.RecognizeEmail(text, Culture));
        Write(w, "url", SequenceRecognizer.RecognizeURL(text, Culture));
        Write(w, "ip", SequenceRecognizer.RecognizeIpAddress(text, Culture));
        Write(w, "phone", SequenceRecognizer.RecognizePhoneNumber(text, Culture));
        Write(w, "hashtag", SequenceRecognizer.RecognizeHashtag(text, Culture));
        Write(w, "mention", SequenceRecognizer.RecognizeMention(text, Culture));
        Write(w, "guid", SequenceRecognizer.RecognizeGUID(text, Culture));
        Write(w, "datetime", DateTimeRecognizer.RecognizeDateTime(text, Culture, DateTimeOptions.None, reference));
        w.WriteEndArray();
    }

    private static void Write(Utf8JsonWriter w, string model, List<ModelResult> results)
    {
        foreach (ModelResult r in results)
        {
            w.WriteStartObject();
            w.WriteString("model", model);
            w.WriteString("typeName", r.TypeName);
            w.WriteString("text", r.Text);
            w.WriteNumber("start", r.Start);
            w.WriteNumber("end", r.End);
            w.WritePropertyName("resolution");
            w.WriteStartObject();
            foreach (KeyValuePair<string, object> kv in r.Resolution ?? new SortedDictionary<string, object>())
            {
                w.WritePropertyName(kv.Key);
                WriteValue(w, kv.Value);
            }

            w.WriteEndObject();
            w.WriteEndObject();
        }
    }

    private static void WriteValue(Utf8JsonWriter w, object? v)
    {
        switch (v)
        {
            case null:
                w.WriteNullValue();
                break;
            case string s:
                w.WriteStringValue(s);
                break;
            case System.Collections.IDictionary d:
                w.WriteStartObject();
                foreach (System.Collections.DictionaryEntry e in d)
                {
                    w.WritePropertyName(e.Key.ToString()!);
                    WriteValue(w, e.Value);
                }

                w.WriteEndObject();
                break;
            case System.Collections.IEnumerable list:
                w.WriteStartArray();
                foreach (object? item in list)
                {
                    WriteValue(w, item);
                }

                w.WriteEndArray();
                break;
            default:
                w.WriteStringValue(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                break;
        }
    }
}
