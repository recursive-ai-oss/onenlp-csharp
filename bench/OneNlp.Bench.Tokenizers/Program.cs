using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.ML.Tokenizers;
using MlTokenizer = Microsoft.ML.Tokenizers.Tokenizer;
using OursTokenizer = RecursiveAI.OneNlp.Tokenizers.Tokenizer;

namespace RecursiveAI.OneNlp.Bench.Tokenizers;

/// <summary>
/// Speed rows of our tokenizers against Microsoft.ML.Tokenizers: for each family, encode-to-ids, token counting and decoding on a
/// short, a medium and a long input. Before a row is timed, both libraries' outputs on that input are compared; a row whose outputs
/// differ is reported as a difference and not timed.
/// </summary>
/// <remarks>
/// Usage: <c>OneNlp.Bench.Tokenizers --models DIR --tatoeba FILE [--filter TEXT] [--out FILE] [--dump-inputs DIR] [--budget-ms N]</c>.
/// <c>--models</c> is a directory with one sub-directory per Hugging Face model and a <c>tiktoken</c> sub-directory;
/// <c>--tatoeba</c> is the tab-separated sentence export (id, language, text).
/// </remarks>
internal static class Program
{
    private static readonly string[] Sizes = ["short", "medium", "long"];

    private static int Main(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            options[args[i]] = args[i + 1];
        }

        if (!options.TryGetValue("--models", out string? models) || !options.TryGetValue("--tatoeba", out string? tatoeba))
        {
            Console.Error.WriteLine("usage: --models DIR --tatoeba FILE [--filter TEXT] [--out FILE] [--dump-inputs DIR] [--budget-ms N]");
            return 2;
        }

        int budgetMs = options.TryGetValue("--budget-ms", out string? b) ? int.Parse(b, CultureInfo.InvariantCulture) : 1000;
        string? filter = options.GetValueOrDefault("--filter");
        Dictionary<string, string> inputs = Corpus.Build(tatoeba);
        if (options.TryGetValue("--dump-inputs", out string? dump))
        {
            Directory.CreateDirectory(dump);
            foreach ((string name, string text) in inputs)
            {
                File.WriteAllText(Path.Combine(dump, name + ".txt"), text, new UTF8Encoding(false));
            }
        }

        var rows = new List<Row>();
        foreach (Family family in Families.All(models))
        {
            if (filter is not null && !family.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Console.WriteLine($"== {family.Name}");
            try
            {
                rows.AddRange(RunFamily(family, inputs, budgetMs));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Console.WriteLine($"   FAILED to run: {ex.GetType().Name}: {ex.Message}");
                rows.Add(new Row(family.Name, "all", "all", null, null, null, null, null, null, $"could not run: {ex.GetType().Name}: {ex.Message}", family.Used));
            }
        }

        string table = Report.Markdown(rows, inputs);
        Console.WriteLine(table);
        if (options.TryGetValue("--out", out string? outFile))
        {
            File.WriteAllText(outFile, table, new UTF8Encoding(false));
        }

        return 0;
    }

    private static List<Row> RunFamily(Family family, Dictionary<string, string> inputs, int budgetMs)
    {
        var rows = new List<Row>();
        long t0 = Stopwatch.GetTimestamp();
        OursTokenizer ours = family.LoadOurs();
        double oursLoadMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        t0 = Stopwatch.GetTimestamp();
        MlTokenizer theirs = family.LoadTheirs();
        double theirsLoadMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        Console.WriteLine($"   load: ours {oursLoadMs:F0} ms ({family.Used}), comparator {theirsLoadMs:F0} ms");
        rows.Add(new Row(family.Name, "load", "-", oursLoadMs * 1e6, theirsLoadMs * 1e6, null, null, null, null, null, family.Used));

        foreach (string size in Sizes)
        {
            string text = inputs[size];
            int[] theirIds = theirs.EncodeToIds(text).ToArray();
            int[] ourIds = new int[(text.Length * 3) + 16];
            ours.EncodeToIds(text, ourIds, out int written, out _, false);
            Array.Resize(ref ourIds, written);

            int firstDiff = FirstDifference(ourIds, theirIds);
            if (firstDiff >= 0)
            {
                string note = firstDiff < Math.Min(ourIds.Length, theirIds.Length)
                    ? $"ids differ: {ourIds.Length} vs {theirIds.Length} tokens, first difference at {firstDiff} (ours {ourIds[firstDiff]}, comparator {theirIds[firstDiff]})"
                    : $"ids differ: {ourIds.Length} vs {theirIds.Length} tokens";
                Console.WriteLine($"   {size}: {note}");
                foreach (string op in new[] { "encode-to-ids", "count", "decode" })
                {
                    rows.Add(new Row(family.Name, op, size, null, null, null, null, null, null, note, family.Used));
                }

                continue;
            }

            int[] buffer = new int[(text.Length * 3) + 16];
            rows.Add(Compare(family, "encode-to-ids", size, budgetMs,
                () => { ours.EncodeToIds(text, buffer, out int w, out _, false); return w; },
                () => theirs.EncodeToIds(text).Count));

            if (ours.CountTokens(text, false) != ourIds.Length || theirs.CountTokens(text) != ourIds.Length)
            {
                string note = $"counts differ: ours {ours.CountTokens(text, false)}, comparator {theirs.CountTokens(text)}, ids {ourIds.Length}";
                Console.WriteLine($"   {size}: {note}");
                rows.Add(new Row(family.Name, "count", size, null, null, null, null, null, null, note, family.Used));
            }
            else
            {
                rows.Add(Compare(family, "count", size, budgetMs, () => ours.CountTokens(text, false), () => theirs.CountTokens(text)));
            }

            string oursText = ours.Decode(ourIds);
            string theirText = theirs.Decode(ourIds);
            if (!string.Equals(oursText, theirText, StringComparison.Ordinal))
            {
                int at = 0;
                while (at < oursText.Length && at < theirText.Length && oursText[at] == theirText[at])
                {
                    at++;
                }

                string note = $"decoded text differs at char {at} (lengths {oursText.Length} vs {theirText.Length})";
                Console.WriteLine($"   {size}: {note}");
                rows.Add(new Row(family.Name, "decode", size, null, null, null, null, null, null, note, family.Used));
            }
            else
            {
                rows.Add(Compare(family, "decode", size, budgetMs, () => ours.Decode(ourIds).Length, () => theirs.Decode(ourIds).Length));
            }
        }

        return rows;
    }

    private static int FirstDifference(int[] a, int[] b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return a.Length == b.Length ? -1 : n;
    }

    private static Row Compare(Family family, string op, string size, int budgetMs, Func<int> ours, Func<int> theirs)
    {
        // Alternate sides so slow drift (thermal, neighbours) hits both alike.
        Measurement o = Timing.Measure(ours, budgetMs);
        Measurement t = Timing.Measure(theirs, budgetMs);
        Row row = new(family.Name, op, size, o.MedianNs, t.MedianNs, o.AllocatedBytes, t.AllocatedBytes, o.PeakExtraBytes, t.PeakExtraBytes, null, family.Used);
        Console.WriteLine($"   {size,-6} {op,-13} ours {Report.Time(o.MedianNs),10} comparator {Report.Time(t.MedianNs),10}  x{t.MedianNs / o.MedianNs:F2}  alloc {o.AllocatedBytes} / {t.AllocatedBytes} B");
        return row;
    }
}

/// <summary>One result: times are medians per operation in nanoseconds; null means not timed (see the note).</summary>
internal sealed record Row(string Family, string Op, string Size, double? OursNs, double? TheirsNs, long? OursAlloc, long? TheirsAlloc, long? OursPeak, long? TheirsPeak, string? Note, string OursSource);

internal readonly record struct Measurement(double MedianNs, long AllocatedBytes, long PeakExtraBytes);

internal static class Timing
{
    /// <summary>
    /// Warms the operation up for about half a second (so tiered compilation has settled), then takes samples of enough repetitions to
    /// last about 40 ms each, for about <paramref name="budgetMs"/> in total (between 5 and 25 samples), and returns the median
    /// per-operation time. Allocation is read from the thread's allocation counter over the last sample. Peak memory is the growth of
    /// the process's resident set high-water mark over the samples.
    /// </summary>
    public static Measurement Measure(Func<int> op, int budgetMs)
    {
        long warmStart = Stopwatch.GetTimestamp();
        int warmRuns = 0;
        long checksum = 0;
        double perOp;
        while (true)
        {
            long s = Stopwatch.GetTimestamp();
            checksum += op();
            perOp = Stopwatch.GetElapsedTime(s).TotalMilliseconds;
            warmRuns++;
            double elapsed = Stopwatch.GetElapsedTime(warmStart).TotalMilliseconds;
            if (elapsed >= 500 || (perOp > 150 && warmRuns >= 3))
            {
                break;
            }
        }

        GC.Collect();
        double estimateMs = Math.Max(perOp, 0.0005);
        // After warm-up the per-op time is lower than the first runs'; re-estimate from a short loop.
        long e0 = Stopwatch.GetTimestamp();
        int probe = 0;
        do
        {
            checksum += op();
            probe++;
        }
        while (Stopwatch.GetElapsedTime(e0).TotalMilliseconds < 20 && probe < 100_000);
        estimateMs = Stopwatch.GetElapsedTime(e0).TotalMilliseconds / probe;

        int inner = Math.Max(1, (int)Math.Ceiling(40.0 / estimateMs));
        int samples = Math.Clamp((int)(budgetMs / (inner * estimateMs)), 5, 25);
        var times = new double[samples];
        long alloc = 0;
        long baselineRss = Memory.ResidentBytes();
        Memory.ResetPeak();
        for (int i = 0; i < samples; i++)
        {
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            long s = Stopwatch.GetTimestamp();
            for (int k = 0; k < inner; k++)
            {
                checksum += op();
            }

            double ms = Stopwatch.GetElapsedTime(s).TotalMilliseconds;
            alloc = (GC.GetAllocatedBytesForCurrentThread() - a0) / inner;
            times[i] = ms / inner * 1e6;
        }

        long peak = Math.Max(0, Memory.PeakBytes() - baselineRss);
        GC.KeepAlive(checksum);
        Array.Sort(times);
        return new Measurement(times[samples / 2], alloc, peak);
    }
}

/// <summary>Process memory on Linux (the high-water mark can be reset there); elsewhere the numbers are 0 and the column reads n/a.</summary>
internal static class Memory
{
    public static long ResidentBytes() => ReadStatusKb("VmRSS:") * 1024;

    public static long PeakBytes() => ReadStatusKb("VmHWM:") * 1024;

    public static void ResetPeak()
    {
        try
        {
            // proc(5): writing 5 to clear_refs resets the peak resident set size.
            File.WriteAllText("/proc/self/clear_refs", "5");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not Linux, or not allowed: the peak then reads as the process's lifetime peak and the report marks it n/a.
        }
    }

    private static long ReadStatusKb(string key)
    {
        try
        {
            foreach (string line in File.ReadLines("/proc/self/status"))
            {
                if (line.StartsWith(key, StringComparison.Ordinal))
                {
                    return long.Parse(line.AsSpan(key.Length).Trim().ToString().Split(' ')[0], CultureInfo.InvariantCulture);
                }
            }
        }
        catch (IOException)
        {
            // no /proc
        }

        return 0;
    }
}

/// <summary>The inputs: one sentence, one paragraph, and about 1 MB of multilingual text, all from the Tatoeba sentence export.</summary>
internal static class Corpus
{
    public static Dictionary<string, string> Build(string tatoebaFile)
    {
        var english = new List<string>();
        var multilingual = new StringBuilder();
        int multilingualBytes = 0;
        foreach (string line in File.ReadLines(tatoebaFile))
        {
            string[] parts = line.Split('\t');
            if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            {
                continue;
            }

            string text = parts[2];
            if (parts[1] == "eng" && text.Length is >= 50 and <= 90 && english.Count < 4000 && id % 97 == 0)
            {
                english.Add(text);
            }

            // Every 600th sentence of the export, up to 1 MB of UTF-8: a spread over all the languages the export holds.
            if (id % 600 == 0 && multilingualBytes < 1_000_000)
            {
                int bytes = Encoding.UTF8.GetByteCount(text) + 1;
                if (multilingualBytes + bytes <= 1_000_000)
                {
                    multilingual.Append(text).Append('\n');
                    multilingualBytes += bytes;
                }
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["short"] = english[1000],
            ["medium"] = string.Join(' ', english.Skip(2000).Take(8)),
            ["long"] = multilingual.ToString(),
        };
    }
}

internal static class Report
{
    public static string Time(double ns) => ns switch
    {
        < 1_000 => $"{ns:F0} ns",
        < 1_000_000 => $"{ns / 1e3:F2} µs",
        < 1_000_000_000 => $"{ns / 1e6:F2} ms",
        _ => $"{ns / 1e9:F2} s",
    };

    private static string Bytes(long? b) => b is null ? "-" : b < 1024 ? $"{b} B" : b < 1024 * 1024 ? $"{b / 1024.0:F1} KB" : $"{b / 1048576.0:F1} MB";

    public static string Markdown(List<Row> rows, Dictionary<string, string> inputs)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Inputs: short {inputs["short"].Length} chars, medium {inputs["medium"].Length} chars, long {Encoding.UTF8.GetByteCount(inputs["long"])} bytes (UTF-8).");
        sb.AppendLine();
        sb.AppendLine("| family | operation | size | ours | comparator | speedup | alloc ours | alloc comparator | peak extra RSS ours | peak extra RSS comparator | note |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (Row r in rows)
        {
            string speed = r.OursNs is { } o && r.TheirsNs is { } t ? $"{t / o:F2}x" : "-";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {r.Family} | {r.Op} | {r.Size} | {(r.OursNs is { } a ? Time(a) : "-")} | {(r.TheirsNs is { } c ? Time(c) : "-")} | {speed} | {Bytes(r.OursAlloc)} | {Bytes(r.TheirsAlloc)} | {Bytes(r.OursPeak)} | {Bytes(r.TheirsPeak)} | {(r.Note ?? (r.Op == "load" ? "load time; ours from " + r.OursSource : ""))} |");
        }

        return sb.ToString();
    }
}

internal sealed record Family(string Name, string OursSource, Func<OursTokenizer> LoadOurs, Func<MlTokenizer> LoadTheirs)
{
    /// <summary>
    /// Describes a family whose tokenizer loads from the first of several files that works (the native format first, then
    /// <c>tokenizer.json</c>), so a file a loader cannot read yet does not stop the row; the file used is named in the table.
    /// </summary>
    public static Family WithFallbacks(string name, string directory, string[] files, Func<MlTokenizer> loadTheirs)
    {
        string used = files[0];
        OursTokenizer Load()
        {
            var errors = new List<string>();
            foreach (string file in files)
            {
                try
                {
                    OursTokenizer t = OursTokenizer.FromFile(Path.Combine(directory, file));
                    used = file;
                    return t;
                }
                catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
                {
                    errors.Add($"{file}: {ex.Message}");
                }
            }

            throw new NotSupportedException(string.Join("; ", errors));
        }

        return new Family(name, files[0], Load, loadTheirs) { _source = () => used };
    }

    private Func<string>? _source;

    /// <summary>The file ours was loaded from (valid after loading).</summary>
    public string Used => _source?.Invoke() ?? OursSource;
}

internal static class Families
{
    public static IEnumerable<Family> All(string models)
    {
        string Dir(string name) => Path.Combine(models, name);

        yield return Family.WithFallbacks("WordPiece (bert-base-uncased)", Dir("google-bert_bert-base-uncased"), ["vocab.txt", "tokenizer.json"],
            () =>
            {
                using FileStream s = File.OpenRead(Path.Combine(Dir("google-bert_bert-base-uncased"), "vocab.txt"));
                return BertTokenizer.Create(s);
            });

        yield return Family.WithFallbacks("Unigram (t5-small)", Dir("google-t5_t5-small"), ["spiece.model", "tokenizer.json"],
            () =>
            {
                using FileStream s = File.OpenRead(Path.Combine(Dir("google-t5_t5-small"), "spiece.model"));
                return SentencePieceTokenizer.Create(s, addBeginningOfSentence: false, addEndOfSentence: false);
            });

        yield return Family.WithFallbacks("SentencePiece BPE, byte fallback (TinyLlama)", Dir("TinyLlama_TinyLlama-1.1B-Chat-v1.0"), ["tokenizer.model", "tokenizer.json"],
            () =>
            {
                using FileStream s = File.OpenRead(Path.Combine(Dir("TinyLlama_TinyLlama-1.1B-Chat-v1.0"), "tokenizer.model"));
                return SentencePieceTokenizer.Create(s, addBeginningOfSentence: false, addEndOfSentence: false);
            });

        // GPT-2's byte-level BPE is the r50k_base encoding: the same ranks and pre-tokenization pattern, so the ranks file is the
        // fallback while the Hugging Face file does not load.
        yield return Family.WithFallbacks("Byte-level BPE (gpt2)", Dir("openai-community_gpt2"), ["tokenizer.json", "../tiktoken/r50k_base.tiktoken"],
            () => TiktokenTokenizer.CreateForModel("gpt2"));

        yield return new Family("Tiktoken cl100k_base", "cl100k_base.tiktoken",
            () => OursTokenizer.FromFile(Path.Combine(Dir("tiktoken"), "cl100k_base.tiktoken")),
            () => TiktokenTokenizer.CreateForModel("gpt-4"));

        yield return new Family("Tiktoken o200k_base", "o200k_base.tiktoken",
            () => OursTokenizer.FromFile(Path.Combine(Dir("tiktoken"), "o200k_base.tiktoken")),
            () => TiktokenTokenizer.CreateForModel("gpt-4o"));
    }
}
