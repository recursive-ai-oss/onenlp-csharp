using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Recipes.OpenNer;

// Open-type entity recognizer on a frozen transformer encoder (an exported ONNX model).
//
// Data is JSON Lines, one sentence per line: {"tokens": ["Marie", "Curie", ...], "spans": [[0, 2, "physicist"], ...]}
// (token offsets, end exclusive, type named in plain words). A file name may be followed by ":N" to take at most N sentences.
// Every sentence lists the types of its own file (or, if the row has "types": [...], those): the listed types absent from the sentence become its negatives.
//
//   train --onnx M.onnx --tokenizer tokenizer.json --train a.jsonl:40000,b.jsonl --dev d.jsonl:1500 --out DIR
//         [--epochs N] [--seed N] [--threads N] [--budget-seconds S] [--checkpoint-every N] [--lr X] [--batch-words N]
//         [--negatives N] [--hidden N] [--embedding N] [--max-span N] [--eval-every STEPS] [--patience N]
//         trains (or resumes from DIR/last.ckpt) until done or the time budget is used; writes DIR/model.onenlp when done.
//         Run it again with the same arguments to continue.
//   eval  --model M.onenlp --data F.jsonl [--types "a;b;c"] [--threshold X] [--pred OUT.tsv] [--bootstrap N]
//         asks for the file's own types (or --types) and prints span precision, recall and F1, micro and per type.
internal static class Program
{
    private const int MaxTokens = 120;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: train|eval ... (see the comment at the top of Program.cs)");
                return 2;
            }

            Dictionary<string, string> o = Options(args.AsSpan(1));
            if (o.TryGetValue("threads", out string? threads))
            {
                TensorCompute.Threads = int.Parse(threads, CultureInfo.InvariantCulture);
            }

            return args[0] switch
            {
                "train" => Train(o),
                "eval" => Eval(o),
                _ => 2,
            };
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException or JsonException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static int Train(Dictionary<string, string> o)
    {
        string outDir = Required(o, "out");
        int epochs = Int(o, "epochs", 10);
        ulong seed = (ulong)Int(o, "seed", 1);
        double budget = o.TryGetValue("budget-seconds", out string? b) ? double.Parse(b, CultureInfo.InvariantCulture) : double.PositiveInfinity;
        float rate = o.TryGetValue("lr", out string? lr) ? float.Parse(lr, CultureInfo.InvariantCulture) : 0.002f;

        var strings = new StringStore();
        List<Example> train = ReadFiles(Required(o, "train"), strings, (int)seed);
        List<Example> dev = ReadFiles(Required(o, "dev"), strings, (int)seed);
        Console.WriteLine($"train sentences={train.Count} dev sentences={dev.Count}");

        using var encoder = new TransformerEncoder(Required(o, "onnx"), Required(o, "tokenizer"), SubwordPooling.Mean);
        using Pipeline pipeline = Pipeline.Blank()
            .Add(new OpenEntityRecognizer(encoder, context: 1, hidden: Int(o, "hidden", 256), embedding: Int(o, "embedding", 128), maxSpanLength: Int(o, "max-span", 12), negativeTypes: Int(o, "negatives", 12)));
        var head = (OpenEntityRecognizer)pipeline.Components[0];
        using var cts = new CancellationTokenSource();
        if (!double.IsPositiveInfinity(budget))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        var clock = Stopwatch.StartNew();
        int logEvery = Int(o, "log-every", 25);
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } score)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev_f1={score:F3} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
            }
            else if (p.Step % logEvery == 0)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} lr={p.LearningRate:E2} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
            }
        });

        TrainResult result = pipeline.Train(
            train,
            new TrainOptions
            {
                Epochs = epochs,
                BatchWords = Int(o, "batch-words", 1500),
                LearningRate = rate,
                WarmupSteps = Int(o, "warmup", 100),
                Dropout = 0.1f,
                Seed = seed,
                MaxSteps = o.TryGetValue("max-steps", out string? ms) ? long.Parse(ms, CultureInfo.InvariantCulture) : 0,
                EvalEvery = Int(o, "eval-every", 0),
                DevExamples = dev,
                Score = examples => Evaluate(head, examples, null, 0.5f).Micro.F1 * 100,
                Patience = Int(o, "patience", 4),
                CheckpointDirectory = outDir,
                CheckpointEvery = Int(o, "checkpoint-every", 100),
                Progress = progress,
            },
            cts.Token);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"completed={result.Completed} steps={result.Steps} epochs={result.Epochs} evaluations={result.Evaluations} best_dev={result.BestScore:F3} best_step={result.BestStep} stopped_early={result.StoppedEarly}"));
        Console.WriteLine($"weights_sha256={result.WeightsSha256}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"chunk_wall_s={clock.Elapsed.TotalSeconds:F1} threads={TensorCompute.Threads}"));
        if (result.Completed)
        {
            string model = Path.Combine(outDir, "model.onenlp");
            PipelineFile.Save(pipeline, model);
            Console.WriteLine($"model={model} bytes={new FileInfo(model).Length}");
        }

        return 0;
    }

    private static int Eval(Dictionary<string, string> o)
    {
#if ONENLP_PACKAGED
        PackageVerification.Verify(Required(o, "manifest-root"));
        using Pipeline pipeline = RecursiveAI.OneNlp.Models.Multilingual.MultilingualModel.LoadOpenEntities();
#else
        TransformerEncoder.ModelDirectory = o.GetValueOrDefault("model-dir");
        using Pipeline pipeline = PipelineFile.Load(Required(o, "model"));
#endif
        var head = (OpenEntityRecognizer)pipeline.Components.Single(c => c is OpenEntityRecognizer);
#if ONENLP_PACKAGED
        Console.WriteLine($"head_parameters={head.Weights.Parameters().Sum(p => p.Length)} default_threshold={head.Threshold}");
        if (head.Threshold != 0.5f)
        {
            throw new InvalidDataException("The packaged head must use the measured default threshold 0.5.");
        }
#endif
        float threshold = o.TryGetValue("threshold", out string? t) ? float.Parse(t, CultureInfo.InvariantCulture) : head.Threshold;
        string[]? fixedTypes = o.TryGetValue("types", out string? ty) ? ty.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : null;
        List<Example> data = ReadFiles(Required(o, "data"), new StringStore(), 1);
        var predictions = new List<string>();
        Scores scores = Evaluate(head, data, fixedTypes, threshold, predictions);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"sentences={data.Count} threshold={threshold:F2} micro P={scores.Micro.P * 100:F2} R={scores.Micro.R * 100:F2} F1={scores.Micro.F1 * 100:F2}"));
        foreach ((string type, Prf prf) in scores.PerType.OrderByDescending(p => p.Value.Gold).Take(25))
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {type,-28} gold={prf.Gold,6} P={prf.P * 100:F1} R={prf.R * 100:F1} F1={prf.F1 * 100:F1}"));
        }

        if (o.TryGetValue("pred", out string? path))
        {
            File.WriteAllLines(path, predictions);
        }

        if (o.TryGetValue("bootstrap", out string? bs))
        {
            Bootstrap(scores.Counts, int.Parse(bs, CultureInfo.InvariantCulture));
        }

        return 0;
    }

    private readonly record struct Prf(int Tp, int Fp, int Fn)
    {
        public int Gold => Tp + Fn;

        public double P => Tp + Fp == 0 ? 0 : (double)Tp / (Tp + Fp);

        public double R => Tp + Fn == 0 ? 0 : (double)Tp / (Tp + Fn);

        public double F1 => (2.0 * Tp) + Fp + Fn == 0 ? 0 : 2.0 * Tp / ((2.0 * Tp) + Fp + Fn);
    }

    private sealed record Scores(Prf Micro, Dictionary<string, Prf> PerType, List<(int Tp, int Fp, int Fn)> Counts);

    // Strict span match: the same token range and the same type. The types asked for are --types, or those the example lists.
    private static Scores Evaluate(OpenEntityRecognizer head, IReadOnlyList<Example> examples, string[]? types, float threshold, List<string>? lines = null)
    {
        var perType = new Dictionary<string, (int Tp, int Fp, int Fn)>(StringComparer.Ordinal);
        var counts = new List<(int, int, int)>(examples.Count);
        int tpAll = 0, fpAll = 0, fnAll = 0;
        for (int n = 0; n < examples.Count; n++)
        {
            Example e = examples[n];
            string[] asked = types ?? [.. e.Spans!.Keys.Order(StringComparer.Ordinal)];
            Doc doc = Example.Blank(e.Reference);
            head.Recognize(doc, asked, threshold);
            var predicted = doc.Ents.Select(x => (x.Start, x.End, x.Label)).ToHashSet();
            var gold = new HashSet<(int, int, string)>();
            foreach (string type in asked)
            {
                if (e.Spans!.TryGetValue(type, out IReadOnlyList<SpanSpec>? spans))
                {
                    foreach (SpanSpec s in spans)
                    {
                        gold.Add((s.Start, s.End, type));
                    }
                }
            }

            int tp = 0, fp = 0, fn = 0;
            foreach ((int s, int en, string label) in predicted)
            {
                bool hit = gold.Contains((s, en, label));
                (int a, int b, int c) = perType.GetValueOrDefault(label);
                perType[label] = hit ? (a + 1, b, c) : (a, b + 1, c);
                tp += hit ? 1 : 0;
                fp += hit ? 0 : 1;
                lines?.Add(string.Create(CultureInfo.InvariantCulture, $"{n}\t{s}\t{en}\t{label}\t{(hit ? "tp" : "fp")}"));
            }

            foreach ((int s, int en, string label) in gold)
            {
                if (!predicted.Contains((s, en, label)))
                {
                    (int a, int b, int c) = perType.GetValueOrDefault(label);
                    perType[label] = (a, b, c + 1);
                    fn++;
                    lines?.Add(string.Create(CultureInfo.InvariantCulture, $"{n}\t{s}\t{en}\t{label}\tfn"));
                }
            }

            counts.Add((tp, fp, fn));
            tpAll += tp;
            fpAll += fp;
            fnAll += fn;
        }

        return new Scores(new Prf(tpAll, fpAll, fnAll), perType.ToDictionary(p => p.Key, p => new Prf(p.Value.Tp, p.Value.Fp, p.Value.Fn)), counts);
    }

    // Percentile bootstrap of micro F1 over sentences (Efron and Tibshirani 1993), seed 1.
    private static void Bootstrap(List<(int Tp, int Fp, int Fn)> counts, int resamples)
    {
        var random = new Random(1);
        var f1s = new double[resamples];
        for (int r = 0; r < resamples; r++)
        {
            long tp = 0, fp = 0, fn = 0;
            for (int i = 0; i < counts.Count; i++)
            {
                (int a, int b, int c) = counts[random.Next(counts.Count)];
                tp += a;
                fp += b;
                fn += c;
            }

            f1s[r] = 2.0 * tp / ((2.0 * tp) + fp + fn);
        }

        Array.Sort(f1s);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"bootstrap resamples={resamples} seed=1 F1 95% CI [{f1s[(int)(0.025 * resamples)] * 100:F2}, {f1s[(int)(0.975 * resamples)] * 100:F2}]"));
    }

    // "a.jsonl:40000,b.jsonl": at most N sentences of a file, taken at random with the seed.
    private static List<Example> ReadFiles(string spec, StringStore strings, int seed)
    {
        var all = new List<Example>();
        foreach (string part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = part.LastIndexOf(':');
            string path = colon > 1 && int.TryParse(part[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _) ? part[..colon] : part;
            int cap = path.Length == part.Length ? int.MaxValue : int.Parse(part[(colon + 1)..], CultureInfo.InvariantCulture);
            var rows = new List<(string[] Tokens, List<(int, int, string)> Spans, string[]? Types)>();
            var inventory = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string line in File.ReadLines(path))
            {
                using JsonDocument json = JsonDocument.Parse(line);
                string[] tokens = [.. json.RootElement.GetProperty("tokens").EnumerateArray().Select(t => t.GetString() ?? string.Empty)];
                if (tokens.Length == 0 || tokens.Length > MaxTokens || tokens.Any(string.IsNullOrWhiteSpace) || tokens.Any(t => t.Any(char.IsWhiteSpace)))
                {
                    continue;
                }

                var spans = new List<(int, int, string)>();
                foreach (JsonElement s in json.RootElement.GetProperty("spans").EnumerateArray())
                {
                    string type = s[2].GetString() ?? throw new InvalidDataException("A span has no type.");
                    spans.Add((s[0].GetInt32(), s[1].GetInt32(), type));
                    inventory.Add(type);
                }

                // An optional "types" list names the types this row says something about (positives and true negatives); without
                // it every type of the file counts, which suits fully labelled sets.
                string[]? listed = json.RootElement.TryGetProperty("types", out JsonElement ts)
                    ? [.. ts.EnumerateArray().Select(t => t.GetString() ?? string.Empty)]
                    : null;
                rows.Add((tokens, spans, listed));
            }

            if (rows.Count > cap)
            {
                var random = new Random(seed);
                rows = [.. rows.OrderBy(_ => random.Next()).Take(cap)];
            }

            foreach ((string[] tokens, List<(int, int, string)> spans, string[]? listed) in rows)
            {
                var groups = new Dictionary<string, IReadOnlyList<SpanSpec>>(StringComparer.Ordinal);
                foreach (string type in listed ?? [.. inventory])
                {
                    groups[type] = [.. spans.Where(s => s.Item3 == type).Select(s => new SpanSpec(s.Item1, s.Item2, type))];
                }

                all.Add(Example.FromReference(Words(strings, tokens), spans: groups));
            }

            Console.WriteLine($"{path}: {rows.Count} sentences, {inventory.Count} types");
        }

        return all;
    }

    private static Doc Words(StringStore strings, string[] words)
    {
        var tokens = new TokenBoundary[words.Length];
        var text = new StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            if (i > 0)
            {
                text.Append(' ');
            }

            tokens[i] = new TokenBoundary(text.Length, words[i].Length);
            text.Append(words[i]);
        }

        return new Doc(strings, text.ToString(), tokens);
    }

    private static int Int(Dictionary<string, string> o, string name, int fallback) =>
        o.TryGetValue(name, out string? v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

    private static string Required(Dictionary<string, string> o, string name) =>
        o.TryGetValue(name, out string? v) ? v : throw new ArgumentException($"--{name} is required.");

    private static Dictionary<string, string> Options(ReadOnlySpan<string> args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            }

            string name = args[i][2..];
            result[name] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : string.Empty;
        }

        return result;
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
