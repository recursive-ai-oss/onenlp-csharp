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

        Console.Error.WriteLine("usage: RecognizersOracle raw <in.jsonl> <out.jsonl> | probe <reference> <text>");
        return 2;
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
