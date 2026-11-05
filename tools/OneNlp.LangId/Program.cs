using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp.LanguageId;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.LangIdTool;

/// <summary>
/// Language-ID tooling.
///
///   train   --train DIR --languages languages.tsv --out model.bin [--epochs N] [--samples N] [--bits Script=N,...] [--lr X] [--exponent X] [--seed N]
///   predict --model model.bin --eval DIR --set main|tatoeba --out DIR [--name ours]
///           writes DIR/NAME.SET.BUCKET.txt, one predicted code per line, for words, pairs and sentences
///   score   --eval DIR --set NAME --pred DIR --systems a,b,c --ref a [--per-language]
///           accuracy per bucket with bootstrap 95% intervals, paired differences against the reference system(s)
///   bench   --model model.bin --eval DIR --set main|tatoeba
///           single-thread latency per bucket: load time, texts/s, microseconds and bytes allocated per text, peak working set
///   spans   --model model.bin --eval DIR --out FILE
///           mixed-language check on synthetic concatenations
/// </summary>
internal static class Program
{
    private static readonly string[] Buckets = ["words", "pairs", "sentences"];

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: train | predict | score | spans (see source header)");
            return 2;
        }

        var opt = new Dictionary<string, string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                opt[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
            }
        }

        switch (args[0])
        {
            case "train":
                var t = new TrainOptions
                {
                    TrainDir = opt["train"],
                    Languages = opt["languages"],
                    Out = opt["out"],
                };
                if (opt.TryGetValue("epochs", out string? v)) { t.Epochs = int.Parse(v, CultureInfo.InvariantCulture); }
                if (opt.TryGetValue("samples", out v)) { t.SamplesPerLanguage = int.Parse(v, CultureInfo.InvariantCulture); }
                if (opt.TryGetValue("lr", out v)) { t.LearningRate = double.Parse(v, CultureInfo.InvariantCulture); }
                if (opt.TryGetValue("exponent", out v)) { t.Exponent = float.Parse(v, CultureInfo.InvariantCulture); }
                if (opt.TryGetValue("seed", out v)) { t.Seed = int.Parse(v, CultureInfo.InvariantCulture); }
                if (opt.TryGetValue("clip", out v)) { t.ClipPercentile = double.Parse(v, CultureInfo.InvariantCulture); }
                if (opt.TryGetValue("bits", out v))
                {
                    foreach (string kv in v.Split(','))
                    {
                        string[] p = kv.Split('=');
                        t.Bits[p[0]] = int.Parse(p[1], CultureInfo.InvariantCulture);
                    }
                }

                Trainer.Train(t);
                return 0;
            case "predict":
                Predict(opt);
                return 0;
            case "score":
                Scorer.Run(opt);
                return 0;
            case "bench":
                Bench(opt);
                return 0;
            case "spans":
                MixedCheck.Run(opt);
                return 0;
            default:
                Console.Error.WriteLine("unknown command");
                return 2;
        }
    }

    /// <summary>Single-thread latency: load time, then three timed passes per bucket after a warm-up pass, with bytes allocated on the calling thread.</summary>
    private static void Bench(Dictionary<string, string> opt)
    {
        var load = System.Diagnostics.Stopwatch.StartNew();
        using FileStream fs = File.OpenRead(opt["model"]);
        LanguageDetector d = LanguageDetector.Load(fs);
        load.Stop();
        Console.WriteLine($"load {load.Elapsed.TotalMilliseconds:F1} ms");
        foreach (string b in Buckets)
        {
            string[] texts = [.. File.ReadAllLines(Path.Combine(opt["eval"], opt["set"], b + ".tsv"), Encoding.UTF8).Select(l => l[(l.IndexOf('\t', StringComparison.Ordinal) + 1)..])];
            foreach (string t in texts.Take(200))
            {
                _ = d.Detect(t);
            }

            for (int rep = 1; rep <= 3; rep++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                foreach (string t in texts)
                {
                    _ = d.Detect(t);
                }

                sw.Stop();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Console.WriteLine($"{b} rep {rep}: {texts.Length} texts, {sw.Elapsed.TotalMilliseconds:F1} ms, {texts.Length / sw.Elapsed.TotalSeconds:F0} texts/s, {sw.Elapsed.TotalMilliseconds * 1000 / texts.Length:F2} us/text, {allocated / texts.Length} B/text");
            }
        }

        Console.WriteLine($"peak working set {System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0:F0} MB");
    }

    private static void Predict(Dictionary<string, string> opt)
    {
        var load = System.Diagnostics.Stopwatch.StartNew();
        using FileStream fs = File.OpenRead(opt["model"]);
        LanguageDetector d = LanguageDetector.Load(fs);
        load.Stop();
        Console.WriteLine($"load: {load.Elapsed.TotalMilliseconds:F1} ms");
        string name = opt.GetValueOrDefault("name", "ours");
        Directory.CreateDirectory(opt["out"]);
        foreach (string b in Buckets)
        {
            string[] lines = File.ReadAllLines(Path.Combine(opt["eval"], opt["set"], b + ".tsv"), Encoding.UTF8);
            var sb = new StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (string line in lines)
            {
                string text = line[(line.IndexOf('\t', StringComparison.Ordinal) + 1)..];
                sb.Append(d.Detect(text).Code).Append('\n');
            }

            sw.Stop();
            File.WriteAllText(Path.Combine(opt["out"], $"{name}.{opt["set"]}.{b}.txt"), sb.ToString());
            Console.WriteLine($"{b}: {lines.Length} texts, {sw.Elapsed.TotalMilliseconds:F0} ms");
        }
    }
}
