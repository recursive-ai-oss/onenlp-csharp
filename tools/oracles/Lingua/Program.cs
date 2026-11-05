// Runs SearchPioneer.Lingua as a black box over an evaluation file and writes one predicted language code per line.
//
//   LinguaOracle <high|low> <languages.tsv> <eval.tsv> <out.txt> [--single]
//
// languages.tsv is the 75-language list (code, name); the detector is built from exactly those languages so the comparison
// with our detector is like for like. eval.tsv lines are "<gold code>\t<text>". The output has the predicted code, or an
// empty line when the library answers Unknown. With --single the texts run on one thread and the elapsed time is printed.
// The calls follow the package README (LanguageDetectorBuilder.FromLanguages, WithLowAccuracyMode, DetectLanguageOf).
using System.Diagnostics;
using Lingua;

namespace OneNlp.Tools.LinguaOracle;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 4 && args[0] == "bench")
        {
            return Bench(args[1], args[2], args[3].Split(','));
        }

        if (args.Length < 4 || args[0] is not ("high" or "low"))
        {
            Console.Error.WriteLine("usage: LinguaOracle high|low <languages.tsv> <eval.tsv> <out.txt> [--single] | bench high|low <languages.tsv> <eval.tsv>[,<eval.tsv>...]");
            return 2;
        }

        (LanguageDetector detector, Dictionary<Language, string> byCode) = Build(args[0], args[1]);
        string[] texts = [.. File.ReadLines(args[2]).Select(l => l[(l.IndexOf('\t', StringComparison.Ordinal) + 1)..])];
        bool single = args.Length > 4 && args[4] == "--single";

        string Predict(string t) => byCode.TryGetValue(detector.DetectLanguageOf(t), out string? code) ? code : string.Empty;

        var sw = Stopwatch.StartNew();
        string[] result = single
            ? [.. texts.Select(Predict)]
            : [.. texts.AsParallel().AsOrdered().Select(Predict)];
        sw.Stop();
        File.WriteAllLines(args[3], result);
        Console.Error.WriteLine($"{texts.Length} texts, {sw.Elapsed.TotalMilliseconds:F0} ms, {(single ? "1 thread" : "parallel")}");
        return 0;
    }

    private static (LanguageDetector Detector, Dictionary<Language, string> ByCode) Build(string mode, string languagesTsv)
    {
        // Our list names the languages the way the README does; map them to the enum members.
        var byCode = new Dictionary<Language, string>();
        foreach (string line in File.ReadLines(languagesTsv))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            string[] f = line.Split('\t');
            string name = f[1].Replace("Norwegian ", string.Empty, StringComparison.Ordinal);
            byCode[Enum.Parse<Language>(name.Replace(" ", string.Empty, StringComparison.Ordinal), ignoreCase: true)] = f[0];
        }

        LanguageDetectorBuilder builder = LanguageDetectorBuilder.FromLanguages([.. byCode.Keys]);
        if (mode == "low")
        {
            builder = builder.WithLowAccuracyMode();
        }

        return (builder.WithPreloadedLanguageModels().Build(), byCode);
    }

    /// <summary>Single-thread latency: build time, then three timed passes per file after a warm-up pass, with bytes allocated on the calling thread.</summary>
    private static int Bench(string mode, string languagesTsv, string[] files)
    {
        var load = Stopwatch.StartNew();
        (LanguageDetector detector, _) = Build(mode, languagesTsv);
        load.Stop();
        Console.WriteLine($"load {load.Elapsed.TotalMilliseconds:F1} ms");
        foreach (string file in files)
        {
            string[] texts = [.. File.ReadLines(file).Select(l => l[(l.IndexOf('\t', StringComparison.Ordinal) + 1)..])];
            foreach (string t in texts.Take(200))
            {
                _ = detector.DetectLanguageOf(t);
            }

            for (int rep = 1; rep <= 3; rep++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                foreach (string t in texts)
                {
                    _ = detector.DetectLanguageOf(t);
                }

                sw.Stop();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Console.WriteLine($"{Path.GetFileNameWithoutExtension(file)} rep {rep}: {texts.Length} texts, {sw.Elapsed.TotalMilliseconds:F1} ms, {texts.Length / sw.Elapsed.TotalSeconds:F0} texts/s, {sw.Elapsed.TotalMilliseconds * 1000 / texts.Length:F2} us/text, {allocated / texts.Length} B/text");
            }
        }

        Console.WriteLine($"peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0:F0} MB");
        return 0;
    }
}
