// ModelBench cold <lang>                          time from process start to the first Nlp.Analyze result for a sentence of the language, the second call, peak resident memory
// ModelBench speed <lang> <text-file> <runs>      single-thread throughput of the full pipeline on the lines of a text file (one document per line)
using System.Diagnostics;
using System.Globalization;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Models.Multilingual;

namespace RecursiveAI.OneNlp.ModelBench.Multilingual;

internal static class Program
{
    private static int Main(string[] args)
    {
        // The package's registration file is compiled into projects that reference it as a package; here it is a project reference.
        MultilingualModel.Register();
        // Parallelism is across documents (one worker thread each, as an application would); inside an operation there is one thread.
        TensorCompute.Threads = 1;
        if (args.Length == 2 && args[0] == "cold")
        {
            return Cold(args[1]);
        }

        if (args.Length == 4 && args[0] == "speed")
        {
            return Speed(args[1], args[2], int.Parse(args[3], CultureInfo.InvariantCulture));
        }

        Console.Error.WriteLine("usage: cold <lang> | speed <lang> <text-file> <runs>");
        return 2;
    }

    private static int Cold(string language)
    {
        string text = File.ReadLines(Path.GetFullPath(Environment.GetEnvironmentVariable("ONENLP_BENCH_TEXT") ?? "bench.txt")).First();
        _ = language;
        var clock = Stopwatch.StartNew();
        Doc first = Nlp.Analyze(text);
        double firstMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        Doc second = Nlp.Analyze(text);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"first Analyze {firstMs:F0} ms (process start to result {(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds:F0} ms), second {clock.Elapsed.TotalMilliseconds:F1} ms, {first.Length} tokens {second.Length}, peak RSS {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0:F0} MB"));
        return 0;
    }

    private static int Speed(string language, string file, int runs)
    {
        Pipeline pipeline = ModelPackages.Get(Language.FromCode(language));
        string[] texts = [.. File.ReadAllLines(file).Where(l => l.Length > 0)];
        long tokens = 0;
        foreach (string t in texts.Take(20))
        {
            tokens += pipeline.Process(t).Length; // warm-up
        }

        double best = double.MaxValue;
        for (int r = 0; r < runs; r++)
        {
            tokens = 0;
            var clock = Stopwatch.StartNew();
            foreach (string t in texts)
            {
                tokens += pipeline.Process(t).Length;
            }

            best = Math.Min(best, clock.Elapsed.TotalSeconds);
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{language}: {texts.Length} documents, {tokens} tokens, best of {runs}: {best:F2} s, {tokens / best:F0} tokens/s, peak RSS {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0:F0} MB"));
        return 0;
    }
}
