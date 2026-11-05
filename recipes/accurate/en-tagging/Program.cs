using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.AccurateEnglishTagging;

// UPOS tagger on a frozen transformer encoder (an exported ONNX model), trained on Universal Dependencies English-EWT.
//
//   train   --onnx M.onnx --tokenizer tokenizer.json --train F.conllu --dev F.conllu --out DIR
//           [--pooling mean|first] [--epochs N] [--seed N] [--threads N] [--budget-seconds S] [--checkpoint-every N]
//           [--finetune] [--max-steps N] [--eval-every STEPS] [--log-every STEPS] [--lr X] [--batch-words N] [--warmup N] [--total-steps N] [--train-embeddings]
//           With --finetune the encoder trains with the head (TrainableTransformerEncoder; learning rate 5e-5 by default, token
//           embedding table frozen unless --train-embeddings); --total-steps sets a warm-up and cosine-decay schedule.
//           trains (or resumes from DIR/last.ckpt) until done or the time budget is used; writes DIR/model.onenlp when done.
//           Run it again with the same arguments to continue.
//   predict --model M.onenlp --gold F.conllu --out P.conllu
//           gold tokens and sentences in, UPOS out, as CoNLL-U for the OneNlp.Eval tool.
//   encode  --onnx M.onnx --tokenizer tokenizer.json --gold F.conllu [--pooling mean|first]
//           runs the encoder over a file once and prints the time (the cost of one frozen-encoder epoch without the cache).
internal static class Program
{
    private const int BatchWords = 1000;
    private const float LearningRate = 0.003f;
    private const int WarmupSteps = 100;
    private const float Dropout = 0.1f;
    private const int Patience = 4;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: train|predict|encode ... (see the comment at the top of Program.cs)");
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
                "predict" => Predict(o),
                "encode" => Encode(o),
                _ => 2,
            };
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static TransformerEncoder NewEncoder(Dictionary<string, string> o) =>
        new(Required(o, "onnx"), Required(o, "tokenizer"), Pooling(o));

    private static SubwordPooling Pooling(Dictionary<string, string> o) =>
        o.GetValueOrDefault("pooling", "mean") == "first" ? SubwordPooling.FirstPiece : SubwordPooling.Mean;

    private static IEncoder NewTrainEncoder(Dictionary<string, string> o) =>
        o.ContainsKey("finetune")
            ? TrainableTransformerEncoder.LoadOnnx(
                Required(o, "onnx"),
                Required(o, "tokenizer"),
                new TrainableTransformerOptions { Pooling = Pooling(o), FreezeEmbeddings = !o.ContainsKey("train-embeddings") })
            : NewEncoder(o);

    private static double DevScore(IReadOnlyList<Example> examples) =>
        UdScorer.Score(examples.Select(e => e.Predicted), examples.Select(e => e.Reference), "UPOS").Value;

    private static int Train(Dictionary<string, string> o)
    {
        string outDir = Required(o, "out");
        int epochs = o.TryGetValue("epochs", out string? e) ? int.Parse(e, CultureInfo.InvariantCulture) : 20;
        ulong seed = o.TryGetValue("seed", out string? s) ? ulong.Parse(s, CultureInfo.InvariantCulture) : 1;
        int every = o.TryGetValue("checkpoint-every", out string? c) ? int.Parse(c, CultureInfo.InvariantCulture) : 100;
        double budget = o.TryGetValue("budget-seconds", out string? b) ? double.Parse(b, CultureInfo.InvariantCulture) : double.PositiveInfinity;
        bool finetune = o.ContainsKey("finetune");
        float rate = o.TryGetValue("lr", out string? lr) ? float.Parse(lr, CultureInfo.InvariantCulture) : (finetune ? 5e-5f : LearningRate);
        int batchWords = o.TryGetValue("batch-words", out string? bw) ? int.Parse(bw, CultureInfo.InvariantCulture) : BatchWords;
        int warmup = o.TryGetValue("warmup", out string? wu) ? int.Parse(wu, CultureInfo.InvariantCulture) : WarmupSteps;
        LrSchedule? schedule = o.TryGetValue("total-steps", out string? ts) ? new WarmupCosineLr(rate, warmup, long.Parse(ts, CultureInfo.InvariantCulture), 0.1f) : null;

        List<Example> train = [.. ConllUReader.ReadExamples(Required(o, "train"))];
        List<Example> dev = [.. ConllUReader.ReadExamples(Required(o, "dev"))];
        using IEncoder encoder = NewTrainEncoder(o);
        using Pipeline pipeline = Pipeline.Blank(Language.English)
            .Add("sentencizer", new Dictionary<string, string> { ["language"] = "en" })
            .Add(new Tagger(encoder, TokenColumn.Pos));
        using var cts = new CancellationTokenSource();
        if (!double.IsPositiveInfinity(budget))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        int logEvery = o.TryGetValue("log-every", out string? le) ? int.Parse(le, CultureInfo.InvariantCulture) : 25;
        var clock = Stopwatch.StartNew();
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } score)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev_upos={score:F3} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
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
                BatchWords = batchWords,
                LearningRate = rate,
                WarmupSteps = warmup,
                Schedule = schedule,
                MaxSteps = o.TryGetValue("max-steps", out string? ms) ? long.Parse(ms, CultureInfo.InvariantCulture) : 0,
                EvalEvery = o.TryGetValue("eval-every", out string? ee) ? int.Parse(ee, CultureInfo.InvariantCulture) : 0,
                Dropout = Dropout,
                Seed = seed,
                DevExamples = dev,
                Score = DevScore,
                Patience = o.TryGetValue("patience", out string? pat) ? int.Parse(pat, CultureInfo.InvariantCulture) : Patience,
                CheckpointDirectory = outDir,
                CheckpointEvery = every,
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

    private static int Predict(Dictionary<string, string> o)
    {
        TransformerEncoder.ModelDirectory = o.GetValueOrDefault("model-dir");
        using Pipeline pipeline = PipelineFile.Load(Required(o, "model"));
        var sb = new StringBuilder();
        int tokens = 0;
        foreach (ConllUSentence sentence in ConllUReader.Read(Required(o, "gold")))
        {
            Doc predicted = Example.Blank(sentence.Doc);
            foreach (IPipelineComponent component in pipeline.Components.Where(c => c is ITrainableComponent))
            {
                component.Process(predicted);
            }

            tokens += predicted.Length;
            foreach (string comment in sentence.Comments.Where(c => c.StartsWith("# sent_id", StringComparison.Ordinal)))
            {
                sb.Append(comment).Append('\n');
            }

            int next = 0;
            for (int i = 0; i < predicted.Length; i++)
            {
                if (next < sentence.MultiwordTokens.Count && sentence.MultiwordTokens[next].FirstWord == i)
                {
                    MultiwordToken m = sentence.MultiwordTokens[next++];
                    sb.Append(m.FirstWord + 1).Append('-').Append(m.LastWord + 1).Append('\t').Append(m.Form).Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                }

                string pos = predicted[i].Pos;
                sb.Append(i + 1).Append('\t').Append(sentence.Forms[i]).Append("\t_\t").Append(pos.Length == 0 ? "_" : pos)
                    .Append("\t_\t_\t").Append(i == 0 ? "0\troot" : "1\tdep").Append("\t_\t_\n");
            }

            sb.Append('\n');
        }

        File.WriteAllText(Required(o, "out"), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"tokens={tokens}");
        return 0;
    }

    private static int Encode(Dictionary<string, string> o)
    {
        List<Example> examples = [.. ConllUReader.ReadExamples(Required(o, "gold"))];
        using TransformerEncoder encoder = NewEncoder(o);
        Doc[] docs = [.. examples.Select(e => e.Predicted)];
        long tokens = docs.Sum(d => d.Length);
        var clock = Stopwatch.StartNew();
        using var scope = new ActivationScope();
        for (int i = 0; i < docs.Length; i += 64)
        {
            encoder.Encode(docs.AsSpan(i, Math.Min(64, docs.Length - i)));
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"docs={docs.Length} tokens={tokens} encode_s={clock.Elapsed.TotalSeconds:F1} tok_per_s={tokens / clock.Elapsed.TotalSeconds:F0} threads={TensorCompute.Threads}"));
        return 0;
    }

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
