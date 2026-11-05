using System.Diagnostics;
using System.Globalization;
using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Eval;

/// <summary>
/// The <c>recognize-bench</c> command: single-thread throughput of the recognizers over a file of text lines, in the same shape as the
/// comparator's <c>bench</c> mode. The rules are built and one text recognized first (the load row), then 200 lines warm up, then each
/// repetition recognizes every line. Prints the time, characters per second and bytes allocated on the calling thread per repetition.
/// </summary>
internal static class RecognizeBenchCommand
{
    internal static int Run(Dictionary<string, List<string>> o)
    {
        string input = o.TryGetValue("in", out List<string>? i) && i.Count > 0 ? i[0] : throw new ArgumentException("--in is required.");
        DateTimeOffset reference = o.TryGetValue("reference", out List<string>? r) && r.Count > 0
            ? DateTimeOffset.Parse(r[0], CultureInfo.InvariantCulture)
            : new DateTimeOffset(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);
        int reps = o.TryGetValue("reps", out List<string>? n) && n.Count > 0 ? int.Parse(n[0], CultureInfo.InvariantCulture) : 3;
        string[] lines = File.ReadAllLines(input);
        var context = new RecognitionContext(reference);

        var load = Stopwatch.StartNew();
        Recognizer recognizer = o.TryGetValue("rules", out List<string>? files) && files.Count > 0 ? Recognizer.FromFiles([.. files]) : Recognizer.English;
        _ = recognizer.Recognize(lines[0], context);
        load.Stop();

        for (int k = 0; k < Math.Min(200, lines.Length); k++)
        {
            _ = recognizer.Recognize(lines[k], context);
        }

        Console.WriteLine($"load {load.Elapsed.TotalMilliseconds:F1} ms");
        for (int rep = 1; rep <= reps; rep++)
        {
            long chars = 0;
            long results = 0;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            foreach (string line in lines)
            {
                results += recognizer.Recognize(line, context).Count;
                chars += line.Length;
            }

            timer.Stop();
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            double seconds = timer.Elapsed.TotalSeconds;
            Console.WriteLine($"rep {rep}: {lines.Length} texts, {chars} chars, {results} results; recognition time {seconds:F3} s ({chars / seconds / 1e6:F2} M chars/s); allocated {allocated / 1e6:F1} MB");
        }

        Console.WriteLine($"peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0:F0} MB");
        return 0;
    }
}
