// ModelBench cold                         time from process start to the first Nlp.Analyze result, and the second call
// ModelBench speed <gold.conllu> <runs>   single-thread throughput of parts of the English pipeline on the raw document texts
using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Models.English;

namespace RecursiveAI.OneNlp.ModelBench;

internal static class Program
{
    private static int Main(string[] args)
    {
        // The package's registration file is compiled into projects that reference it as a package; here it is a project reference.
        EnglishModel.Register();
        if (args.Length == 1 && args[0] == "cold")
        {
            return Cold();
        }

        if (args.Length == 3 && args[0] == "speed")
        {
            return Speed(args[1], int.Parse(args[2], CultureInfo.InvariantCulture));
        }

        Console.Error.WriteLine("usage: ModelBench cold | speed <gold.conllu> <runs>");
        return 2;
    }

    private static int Cold()
    {
        double sinceStart = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        const string text = "Ada Lovelace worked with Charles Babbage in London. She wrote the first program!";
        long a0 = Stopwatch.GetTimestamp();
        Doc doc = Nlp.Analyze(text);
        double first = Stopwatch.GetElapsedTime(a0).TotalMilliseconds;
        long b0 = Stopwatch.GetTimestamp();
        doc = Nlp.Analyze(text);
        double second = Stopwatch.GetElapsedTime(b0).TotalMilliseconds;
        long c0 = Stopwatch.GetTimestamp();
        for (int i = 0; i < 100; i++)
        {
            doc = Nlp.Analyze(text);
        }

        double steady = Stopwatch.GetElapsedTime(c0).TotalMilliseconds / 100;
        Console.WriteLine($"process start to Main: {sinceStart:F0} ms; first Nlp.Analyze (loads weights): {first:F0} ms; second: {second:F2} ms; mean of next 100: {steady:F2} ms; {doc.Length} tokens; working set {Environment.WorkingSet / 1048576} MB");
        return 0;
    }

    private static int Speed(string gold, int runs)
    {
        List<string> texts = ReadDocuments(gold);
        long chars = texts.Sum(t => (long)t.Length);
        Pipeline full = ModelPackages.Get(Language.English);
        Console.WriteLine($"{texts.Count} documents, {chars} characters");

        // Same work as Catalyst's default pipeline: sentences, tokens, UPOS (the morphologizer) and lemmas.
        Measure("sentencizer + UPOS/UFeats + lemmas", Sub(full, true, "morphologizer", "lemmatizer"), texts, chars, runs);
        Measure("sentencizer + UPOS/UFeats + XPOS + lemmas", Sub(full, true, "tagger", "morphologizer", "lemmatizer"), texts, chars, runs);
        Measure("sentencizer + entities", Sub(full, true, "ner"), texts, chars, runs);
        Measure("dependency parser (predicts sentences)", Sub(full, false, "parser"), texts, chars, runs);
        Measure("full pipeline", full, texts, chars, runs);
        return 0;
    }

    // A view over the full pipeline's components, so the weights are loaded once.
    private static Pipeline Sub(Pipeline full, bool sentencizer, params string[] names)
    {
        var p = new Pipeline(full.Tokenizer, Language.English, full.Strings);
        if (sentencizer)
        {
            p.Add("sentencizer", new Dictionary<string, string> { ["data"] = "english" });
        }

        foreach (string name in names)
        {
            p.Add(full.Components.First(c => c.Name == name));
        }

        return p;
    }

    private static void Measure(string name, Pipeline pipeline, List<string> texts, long chars, int runs)
    {
        long tokens = Pass(pipeline, texts);
        for (int r = 1; r <= runs; r++)
        {
            long alloc = GC.GetAllocatedBytesForCurrentThread();
            long t0 = Stopwatch.GetTimestamp();
            tokens = Pass(pipeline, texts);
            double s = Stopwatch.GetElapsedTime(t0).TotalSeconds;
            Console.WriteLine($"{name}: run {r}: {tokens / s:F0} tokens/s, {chars / s / 1e6:F2} M chars/s, {(GC.GetAllocatedBytesForCurrentThread() - alloc) / (double)tokens:F0} B/token ({tokens} tokens, {s:F2} s)");
        }
    }

    private static long Pass(Pipeline pipeline, List<string> texts)
    {
        long tokens = 0;
        foreach (string text in texts)
        {
            tokens += pipeline.Process(text).Length;
        }

        return tokens;
    }

    // Documents of a CoNLL-U file as raw text, rebuilt from the forms and SpaceAfter (the same rule the Catalyst oracle uses).
    private static List<string> ReadDocuments(string path)
    {
        var docs = new List<string>();
        var sb = new StringBuilder();
        int skipTo = 0;
        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith("# newdoc", StringComparison.Ordinal) && sb.Length > 0)
            {
                docs.Add(sb.ToString().TrimEnd());
                sb.Clear();
            }

            if (line.Length == 0)
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }

                skipTo = 0;
                continue;
            }

            if (line[0] == '#')
            {
                continue;
            }

            string[] c = line.Split('\t');
            if (c[0].Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            if (c[0].Contains('-', StringComparison.Ordinal))
            {
                skipTo = int.Parse(c[0].Split('-')[1], CultureInfo.InvariantCulture);
            }
            else if (int.Parse(c[0], CultureInfo.InvariantCulture) <= skipTo)
            {
                continue;
            }

            sb.Append(c[1]);
            if (!c[9].Contains("SpaceAfter=No", StringComparison.Ordinal))
            {
                sb.Append(' ');
            }
        }

        if (sb.Length > 0)
        {
            docs.Add(sb.ToString().TrimEnd());
        }

        return docs;
    }
}
