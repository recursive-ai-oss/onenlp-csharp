// ModelBench cold                         time from process start to the first Nlp.Analyze result, and the second call
// ModelBench speed <gold.conllu> <runs>   single-thread throughput of parts of the English pipeline on the raw document texts
using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Models.English;

namespace RecursiveAI.OneNlp.ModelBench;

internal static class Program
{
    private static int Main(string[] args)
    {
        // The package's registration file is compiled into projects that reference it as a package; here it is a project reference.
        EnglishModel.Register();
        // Parallelism is across documents (one worker thread each, as an application would); inside an operation there is one thread.
        TensorCompute.Threads = 1;
        if (args.Length == 1 && args[0] == "cold")
        {
            return Cold();
        }

        if (args.Length == 3 && args[0] == "ops")
        {
            return Ops(int.Parse(args[1], CultureInfo.InvariantCulture), int.Parse(args[2], CultureInfo.InvariantCulture));
        }

        if (args.Length == 5 && args[0] == "profile")
        {
            return Profile(args[1], int.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3], CultureInfo.InvariantCulture), int.Parse(args[4], CultureInfo.InvariantCulture));
        }

        if (args.Length == 3 && args[0] == "speed")
        {
            return Speed(args[1], int.Parse(args[2], CultureInfo.InvariantCulture));
        }

        Console.Error.WriteLine("usage: ModelBench cold | speed <gold.conllu> <runs>");
        return 2;
    }

    // The packaged English pipeline, or the saved pipeline named by ONENLP_BENCH_MODEL (for trying a model before it is packed).
    private static Pipeline Load() =>
        Environment.GetEnvironmentVariable("ONENLP_BENCH_MODEL") is { Length: > 0 } path ? PipelineFile.Load(path) : ModelPackages.Get(Language.English);

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

    // Where the time of the English pipeline goes, per stage, on the raw document texts: the word tokenizer, the sentencizer, each distinct encoder,
    // then each head on documents whose encoding is already kept. Single document per call when batch is 1.
    private static int Profile(string gold, int runs, int threads, int batch)
    {
        TensorCompute.Threads = threads;
        List<string> texts = ReadDocuments(gold);
        Pipeline full = Load();
        var sentencizer = new Pipeline(full.Tokenizer, Language.English, full.Strings).Add("sentencizer", new Dictionary<string, string> { ["data"] = "english" });
        var encoders = new List<IEncoder>();
        foreach (IPipelineComponent c in full.Components)
        {
            if (c is ITrainableComponent t && !encoders.Contains(t.Encoder))
            {
                encoders.Add(t.Encoder);
            }
        }

        var sums = new Dictionary<string, double>();
        long tokens = 0;
        for (int run = 0; run <= runs; run++)
        {
            var times = new Dictionary<string, double>();
            var allocs = new Dictionary<string, long>();
            void Time(string name, Action action)
            {
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                long t0 = Stopwatch.GetTimestamp();
                action();
                times[name] = times.GetValueOrDefault(name) + Stopwatch.GetElapsedTime(t0).TotalSeconds;
                allocs[name] = allocs.GetValueOrDefault(name) + GC.GetAllocatedBytesForCurrentThread() - a0;
            }

            tokens = 0;
            for (int start = 0; start < texts.Count; start += batch)
            {
                int n = Math.Min(batch, texts.Count - start);
                var docs = new Doc[n];
                Time("1 tokenizer", () =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        docs[i] = full.Tokenizer.Tokenize(texts[start + i], full.Strings);
                    }
                });
                foreach (Doc d in docs)
                {
                    tokens += d.Length;
                }

                Time("2 sentencizer", () => sentencizer.Components[0].ProcessBatch(docs));
                for (int e = 0; e < encoders.Count; e++)
                {
                    IEncoder enc = encoders[e];
                    Time("3 encoder " + e, () =>
                    {
                        enc.Weights?.Eval();
                        using NoGradScope ng = GradMode.NoGrad();
                        using var scope = new ActivationScope();
                        _ = enc.Encode(docs);
                    });
                }

                foreach (IPipelineComponent c in full.Components)
                {
                    if (c.Name != "sentencizer")
                    {
                        Time("4 head " + c.Name, () => c.ProcessBatch(docs));
                    }
                }
            }

            if (run > 0)
            {
                foreach (KeyValuePair<string, double> kv in times)
                {
                    sums[kv.Key] = sums.GetValueOrDefault(kv.Key) + kv.Value;
                }

                Console.WriteLine($"run {run}");
                foreach (KeyValuePair<string, double> kv in times.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    Console.WriteLine($"  {kv.Key,-24} {kv.Value * 1e6 / tokens,8:F2} us/token {allocs[kv.Key] / (double)tokens,8:F0} B/token");
                }
            }
        }

        Console.WriteLine($"threads={threads} batch={batch} tokens/run={tokens}");
        return 0;
    }

    // Cost per token of the operations inside the window encoder, for a batch of n rows at the given width (embedding width = width / 4).
    private static int Ops(int n, int width)
    {
        int ew = width / 4;
        var g = new Generator(1);
        var table = new Embedding(32768, ew, g);
        var mix = new Linear(4 * ew, width, g);
        var norm = new LayerNorm(width);
        var win = new Linear(3 * width, width, g);
        var ids = new int[n];
        var prev = new int[n];
        var next = new int[n];
        for (int i = 0; i < n; i++)
        {
            ids[i] = (i * 7919) % 32768;
            prev[i] = Math.Max(i - 1, 0);
            next[i] = Math.Min(i + 1, n - 1);
        }

        Tensor idT = Tensor.FromSpan<int>(ids, Shape.Create(n));
        Tensor prevT = Tensor.FromSpan<int>(prev, Shape.Create(n));
        Tensor nextT = Tensor.FromSpan<int>(next, Shape.Create(n));
        using NoGradScope ng = GradMode.NoGrad();
        table.Eval();
        var t = new Dictionary<string, double>();
        for (int rep = 0; rep < 12; rep++)
        {
            using var scope = new ActivationScope();
            long a0 = Stopwatch.GetTimestamp();
            double Lap(string name)
            {
                long a1 = Stopwatch.GetTimestamp();
                double d = Stopwatch.GetElapsedTime(a0, a1).TotalSeconds * 1e6 / n;
                a0 = a1;
                if (rep >= 2)
                {
                    t[name] = t.GetValueOrDefault(name) + d / 10;
                }

                return d;
            }

            Tensor e = table.Forward(idT);
            Lap("gather (x1)");
            Tensor cat = RecursiveAI.OneModel.Tensors.Ops.Concat([e, e, e, e], 1);
            Lap("concat 4");
            Tensor joined = mix.Forward(cat);
            Lap("mix linear");
            Tensor act = RecursiveAI.OneModel.Tensors.Ops.Gelu(joined);
            Lap("gelu");
            Tensor act2 = RecursiveAI.OneModel.Tensors.Ops.Gelu(joined);
            Lap("gelu again");
            Tensor act3 = RecursiveAI.OneModel.Tensors.Ops.Gelu(joined, true);
            Lap("gelu tanh");
            Tensor act4 = RecursiveAI.OneModel.Tensors.Ops.Relu(joined);
            Lap("relu");
            Tensor x = norm.Forward(act);
            Lap("layernorm");
            Tensor left = RecursiveAI.OneModel.Tensors.Ops.Embedding(x, prevT);
            Tensor right = RecursiveAI.OneModel.Tensors.Ops.Embedding(x, nextT);
            Lap("neighbour gather");
            Tensor w = RecursiveAI.OneModel.Tensors.Ops.Concat([left, x, right], 1);
            Lap("window concat");
            Tensor wl = win.Forward(w);
            Lap("window linear");
            Tensor sum = RecursiveAI.OneModel.Tensors.Ops.Add(x, wl);
            Lap("add");
        }

        // Output layers: matrix product, softmax and arg-max over the classes, per token.
        foreach (int classes in new[] { 50, 250, 900 })
        {
            var head = new Linear(width, classes, g);
            Tensor input = Tensor.Zeros(DType.F32, Shape.Create(n, width));
            for (int rep = 0; rep < 8; rep++)
            {
                using var scope = new ActivationScope();
                long b0 = Stopwatch.GetTimestamp();
                Tensor logits = head.Forward(input);
                long b1 = Stopwatch.GetTimestamp();
                Tensor probs = RecursiveAI.OneModel.Tensors.Ops.Softmax(logits);
                long b2 = Stopwatch.GetTimestamp();
                ReadOnlySpan<float> data = logits.Data<float>();
                int sink = 0;
                for (int r = 0; r < n; r++)
                {
                    ReadOnlySpan<float> row = data.Slice(r * classes, classes);
                    int best = 0;
                    for (int c = 1; c < classes; c++)
                    {
                        if (row[c] > row[best])
                        {
                            best = c;
                        }
                    }

                    sink += best;
                }

                long b3 = Stopwatch.GetTimestamp();
                if (rep == 7)
                {
                    Console.WriteLine($"  classes={classes,4}: linear {Stopwatch.GetElapsedTime(b0, b1).TotalSeconds * 1e6 / n:F3} softmax {Stopwatch.GetElapsedTime(b1, b2).TotalSeconds * 1e6 / n:F3} argmax {Stopwatch.GetElapsedTime(b2, b3).TotalSeconds * 1e6 / n:F3} us/token ({sink})");
                }
            }
        }

        Console.WriteLine($"n={n} width={width} embed={ew} (us per token, one thread)");
        foreach (KeyValuePair<string, double> kv in t)
        {
            Console.WriteLine($"  {kv.Key,-20} {kv.Value,7:F3}");
        }

        return 0;
    }

    private static int Speed(string gold, int runs)
    {
        List<string> texts = ReadDocuments(gold);
        int copies = int.Parse(Environment.GetEnvironmentVariable("ONENLP_BENCH_REPEAT") ?? "1", CultureInfo.InvariantCulture);
        if (copies > 1)
        {
            // Many threads finish one pass over the file in milliseconds; repeating it gives a pass worth timing.
            texts = [.. Enumerable.Repeat(texts, copies).SelectMany(x => x)];
        }

        long chars = texts.Sum(t => (long)t.Length);
        Pipeline full = Load();
        Console.WriteLine($"{texts.Count} documents, {chars} characters");

        // Same work as Catalyst's default pipeline: sentences, tokens, UPOS (the morphologizer) and lemmas.
        Measure("sentencizer + UPOS/UFeats + lemmas", Sub(full, true, "morphologizer", "lemmatizer"), texts, chars, runs);
        Measure("sentencizer + UPOS/UFeats + XPOS + lemmas", Sub(full, true, "tagger", "morphologizer", "lemmatizer"), texts, chars, runs);
        Measure("sentencizer + entities", Sub(full, true, "ner"), texts, chars, runs);
        Measure("dependency parser (predicts sentences)", Sub(full, false, "parser"), texts, chars, runs);
        Measure("full pipeline", full, texts, chars, runs);
        Console.WriteLine($"peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576} MB");
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

    private static int Workers() => int.Parse(Environment.GetEnvironmentVariable("ONENLP_BENCH_THREADS") ?? "1", CultureInfo.InvariantCulture);

    private static void Measure(string name, Pipeline pipeline, List<string> texts, long chars, int runs)
    {
        int workers = Workers();
        long tokens = Pass(pipeline, texts, workers);
        for (int r = 1; r <= runs; r++)
        {
            long alloc = GC.GetTotalAllocatedBytes(precise: true);
            long t0 = Stopwatch.GetTimestamp();
            tokens = Pass(pipeline, texts, workers);
            double s = Stopwatch.GetElapsedTime(t0).TotalSeconds;
            Console.WriteLine($"{name}: threads {workers} run {r}: {tokens / s:F0} tokens/s, {chars / s / 1e6:F2} M chars/s, {(GC.GetTotalAllocatedBytes(precise: true) - alloc) / (double)tokens:F0} B/token ({tokens} tokens, {s:F2} s)");
        }
    }

    // Documents go to worker threads round-robin; every worker runs the same pipeline (Process is safe to call from several threads).
    private static long Pass(Pipeline pipeline, List<string> texts, int workers)
    {
        long total = 0;
        var threads = new Thread[workers];
        for (int w = 0; w < workers; w++)
        {
            int worker = w;
            threads[w] = new Thread(() =>
            {
                long tokens = 0;
                for (int i = worker; i < texts.Count; i += workers)
                {
                    tokens += pipeline.Process(texts[i]).Length;
                }

                Interlocked.Add(ref total, tokens);
            });
            threads[w].Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        return total;
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
