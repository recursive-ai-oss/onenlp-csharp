using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Parity;

internal static class Program
{
    private const string Usage = """
        OneNlp.Parity: compare tokenizers with golden files from the reference libraries.

          OneNlp.Parity --families families.json --models DIR --goldens DIR --corpus DIR --out DIR
                        [--family ID]... [--load KEY] [--adhoc ID=PATH]... [--max-examples N] [--max-records N] [--require-supported]

        --load picks which file of a family to load (default: primary). Exit code 0 when every family matched or has no loader yet,
        1 when anything mismatched or failed to load, 2 on a usage error. --require-supported makes "not supported" exit 1.
        """;

    private static int Main(string[] args)
    {
        var opts = new Dictionary<string, List<string>>();
        var flags = new HashSet<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            string key = args[i];
            if (key == "--require-supported")
            {
                flags.Add(key);
                continue;
            }

            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            if (!opts.TryGetValue(key, out List<string>? list))
            {
                opts[key] = list = [];
            }

            list.Add(args[++i]);
        }

        string? One(string k) => opts.TryGetValue(k, out List<string>? v) ? v[^1] : null;
        string? goldens = One("--goldens"), corpus = One("--corpus"), outDir = One("--out");
        if (goldens is null || corpus is null || outDir is null || (One("--families") is null && !opts.ContainsKey("--adhoc")))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        Directory.CreateDirectory(outDir);
        var options = new ParityOptions
        {
            MaxExamples = int.Parse(One("--max-examples") ?? "20", System.Globalization.CultureInfo.InvariantCulture),
            MaxRecords = int.Parse(One("--max-records") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
        };
        string load = One("--load") ?? "primary";
        var only = opts.GetValueOrDefault("--family");

        var jobs = new List<(string Id, string Path)>();
        if (One("--families") is { } familiesPath)
        {
            string models = One("--models") ?? throw new ArgumentException("--models is required with --families");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(familiesPath));
            foreach (JsonElement f in doc.RootElement.GetProperty("families").EnumerateArray())
            {
                string id = f.GetProperty("id").GetString()!;
                if (only is not null && !only.Contains(id))
                {
                    continue;
                }

                JsonElement files = f.GetProperty("files");
                string file = files.TryGetProperty(load, out JsonElement p) ? p.GetString()! : files.GetProperty("primary").GetString()!;
                jobs.Add((id, Path.Combine(models, f.GetProperty("dir").GetString()!, file)));
            }
        }

        foreach (string a in opts.GetValueOrDefault("--adhoc") ?? [])
        {
            int eq = a.IndexOf('=', StringComparison.Ordinal);
            jobs.Add((a[..eq], a[(eq + 1)..]));
        }

        int exit = 0;
        var table = new StringBuilder();
        foreach ((string id, string path) in jobs)
        {
            FamilyResult r = RunOne(id, path, Path.Combine(goldens, id + ".golden.jsonl.gz"), corpus, options);
            Write(outDir, r);
            string line = $"{id,-30} {r.Status,-14} records {r.Records,7} pairs {r.Pairs,4} comparisons {r.Comparisons,9} mismatches {r.MismatchCount,8}  {r.Seconds,7:F1}s  {r.Oracle}  {r.Message}";
            Console.WriteLine(line);
            table.AppendLine(line);
            if (r.Status is "mismatches" or "load failed" or "no golden" || (r.Status == "not supported" && flags.Contains("--require-supported")))
            {
                exit = 1;
            }
        }

        File.WriteAllText(Path.Combine(outDir, "summary.txt"), table.ToString());
        return exit;
    }

    private static FamilyResult RunOne(string id, string tokenizerPath, string golden, string corpus, ParityOptions options)
    {
        var result = new FamilyResult { Family = id };
        if (!File.Exists(golden))
        {
            result.Status = "no golden";
            result.Message = golden;
            return result;
        }

        Tokenizer tokenizer;
        try
        {
            tokenizer = TokenizerFiles.Load(tokenizerPath);
        }
        catch (NotSupportedException ex)
        {
            // The golden file is still read, so the plumbing and the file are checked and the work waiting for the loader is counted.
            result.Status = "not supported";
            (string oracle, int records, int pairs) = Peek(golden);
            result.Oracle = oracle;
            result.Message = $"{ex.Message} (golden: {records} records, {pairs} pairs)";
            return result;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FormatException or JsonException)
        {
            result.Status = "load failed";
            result.Message = ex.GetType().Name + ": " + ex.Message;
            return result;
        }

        return ParityChecker.Run(id, tokenizer, golden, corpus, options);
    }

    private static (string Oracle, int Records, int Pairs) Peek(string golden)
    {
        using var gz = new System.IO.Compression.GZipStream(File.OpenRead(golden), System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        using JsonDocument header = JsonDocument.Parse(reader.ReadLine() ?? throw new InvalidDataException("Empty golden file."));
        JsonElement h = header.RootElement.GetProperty("header");
        int records = 0, pairs = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith("{\"pair\"", StringComparison.Ordinal))
            {
                pairs++;
            }
            else
            {
                records++;
            }
        }

        return (h.GetProperty("oracle").GetString() + " " + h.GetProperty("oracle_version").GetString(), records, pairs);
    }

    private static void Write(string outDir, FamilyResult r)
    {
        using (FileStream fs = File.Create(Path.Combine(outDir, r.Family + ".summary.json")))
        using (var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("family", r.Family);
            w.WriteString("status", r.Status);
            w.WriteString("message", r.Message);
            w.WriteString("oracle", r.Oracle);
            w.WriteNumber("records", r.Records);
            w.WriteNumber("pairs", r.Pairs);
            w.WriteNumber("comparisons", r.Comparisons);
            w.WriteNumber("mismatches", r.MismatchCount);
            w.WriteNumber("seconds", r.Seconds);
            w.WriteStartObject("by_field");
            foreach (var kv in r.ByField)
            {
                w.WriteNumber(kv.Key, kv.Value);
            }

            w.WriteEndObject();
            w.WriteStartObject("by_class");
            foreach (var kv in r.ByClass)
            {
                w.WriteNumber(kv.Key, kv.Value);
            }

            w.WriteEndObject();
            w.WriteStartObject("by_field_and_class");
            foreach (var kv in r.ByFieldAndClass)
            {
                w.WriteStartObject(kv.Key);
                foreach (var c in kv.Value)
                {
                    w.WriteNumber(c.Key, c.Value);
                }

                w.WriteEndObject();
            }

            w.WriteEndObject();
            w.WriteStartArray("examples");
            foreach (Mismatch m in r.Examples)
            {
                w.WriteStartObject();
                w.WriteString("check", m.Check);
                w.WriteString("field", m.Field);
                w.WriteNumber("record", m.RecordId);
                w.WriteString("class", m.Class);
                w.WriteNumber("input_length", m.InputLength);
                w.WriteString("detail", m.Detail);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        string testsPath = Path.Combine(outDir, r.Family + ".mismatch-tests.cs");
        if (r.TestCases.Count == 0)
        {
            File.Delete(testsPath);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// Generated by OneNlp.Parity from the shortest failing inputs. Each test names the oracle's expected value.");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using RecursiveAI.OneNlp.Tokenizers;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine("namespace RecursiveAI.OneNlp.Tests.Parity;");
        sb.AppendLine();
        sb.AppendLine($"public class {Identifier(r.Family)}MismatchTests");
        sb.AppendLine("{");
        int n = 0;
        foreach (Mismatch m in r.TestCases)
        {
            sb.AppendLine($"    // {m.Check} {m.Field}, corpus record {m.RecordId} ({m.Class}): {m.Detail.ReplaceLineEndings(" ")}");
            sb.AppendLine("    [Fact]");
            sb.AppendLine($"    public void Case{++n}_{Identifier(m.Check)}_{Identifier(m.Field)}()");
            sb.AppendLine("    {");
            foreach (string l in m.TestBody!.Split('\n'))
            {
                sb.AppendLine("        " + l);
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        File.WriteAllText(testsPath, sb.ToString());
    }

    private static string Identifier(string s)
    {
        var sb = new StringBuilder();
        bool upper = true;
        foreach (char c in s)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            else
            {
                upper = true;
            }
        }

        return sb.ToString();
    }
}
