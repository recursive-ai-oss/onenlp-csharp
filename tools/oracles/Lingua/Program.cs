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
        if (args.Length < 4 || args[0] is not ("high" or "low"))
        {
            Console.Error.WriteLine("usage: LinguaOracle high|low <languages.tsv> <eval.tsv> <out.txt> [--single]");
            return 2;
        }

        // Our list names the languages the way the README does; map them to the enum members.
        var byCode = new Dictionary<Language, string>();
        foreach (string line in File.ReadLines(args[1]))
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
        if (args[0] == "low")
        {
            builder = builder.WithLowAccuracyMode();
        }

        LanguageDetector detector = builder.WithPreloadedLanguageModels().Build();
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
}
