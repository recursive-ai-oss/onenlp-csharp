using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Recipes.EnTextcat;

// English text classifier (one category per document) over the fast encoder. The whole recipe: JSON Lines in, a saved pipeline out.
//
//   train   <train.jsonl> <dev.jsonl> <out-dir> [options]   trains, or resumes from <out-dir>/last.ckpt; writes <out-dir>/model.onenlp when done
//   predict <model.onenlp> <in.jsonl> <out.jsonl>           one {"cats": {label: score}} line per input line, for OneNlp.Eval cats
//   bench   <model.onenlp> <in.jsonl>                       documents and tokens per second on one thread
//
// Data lines: {"text": "...", "cats": {"label_a": 1, "label_b": 0, ...}}. Options (defaults are the recipe):
//   --epochs 40 --seed 7 --lr 0.003 --dropout 0.2 --batch-words 2000 --patience 6 --hidden 0 --width 256 --depth 2 --threads N
//   --checkpoint-every 0 --budget-seconds S (stop after S seconds; run the same command again to continue)
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (Opt(args, "threads", string.Empty).Length > 0)
            {
                TensorCompute.Threads = Num(args, "threads", 1);
            }

            return (args.Length > 0 ? args[0] : string.Empty) switch
            {
                "train" when args.Length >= 4 => Train(args),
                "predict" when args.Length >= 4 => Predict(args),
                "bench" when args.Length >= 3 => Bench(args),
                _ => Usage(),
            };
        }
        catch (Exception e) when (e is DataFormatException or IOException or ArgumentException or InvalidDataException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: train <train.jsonl> <dev.jsonl> <out-dir> [options] | predict <model> <in.jsonl> <out.jsonl> | bench <model> <in.jsonl>  (see Program.cs)");
        return 2;
    }

    private static string Opt(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, "--" + name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static int Num(string[] args, string name, int fallback) => int.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static float Flt(string[] args, string name, float fallback) => float.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static int Train(string[] args)
    {
        string outDir = args[3];
        ulong seed = (ulong)Num(args, "seed", 7);
        FastEncoder encoder = FastEncoderFor(args, seed);
        using Pipeline pipeline = Pipeline.Blank(Language.English)
            .Add(new TextClassifier(encoder, multiLabel: false, hiddenSize: Num(args, "hidden", 0)) { OwnsEncoder = true });

        // The documents are split by the pipeline's own tokenizer, so training sees the tokens that prediction will.
        var options = new JsonlOptions { Tokenizer = pipeline.Tokenizer };
        List<Example> train = [.. JsonlReader.Read(args[1], null, options)];
        List<Example> dev = [.. JsonlReader.Read(args[2], null, options)];

        using var cts = new CancellationTokenSource();
        double budget = double.Parse(Opt(args, "budget-seconds", "0"), CultureInfo.InvariantCulture);
        if (budget > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        var clock = Stopwatch.StartNew();
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } score)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev_accuracy={score:F3} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
            }
        });

        TrainResult result = pipeline.Train(
            train,
            new TrainOptions
            {
                Epochs = Num(args, "epochs", 40),
                BatchWords = Num(args, "batch-words", 2000),
                LearningRate = Flt(args, "lr", 0.003f),
                WarmupSteps = Num(args, "warmup", 100),
                Dropout = Flt(args, "dropout", 0.2f),
                Seed = seed,
                DevExamples = dev,
                Score = DevScores.Accuracy(),
                Patience = Num(args, "patience", 6),
                CheckpointDirectory = outDir,
                CheckpointEvery = Num(args, "checkpoint-every", 0),
                Progress = progress,
            },
            cts.Token);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"completed={result.Completed} steps={result.Steps} epochs={result.Epochs} best_dev={result.BestScore:F3} best_step={result.BestStep} stopped_early={result.StoppedEarly} weights_sha256={result.WeightsSha256} chunk_wall_s={clock.Elapsed.TotalSeconds:F1} threads={TensorCompute.Threads}"));
        if (result.Completed)
        {
            string model = Path.Combine(outDir, "model.onenlp");
            PipelineFile.Save(pipeline, model);
            Console.WriteLine($"model={model} bytes={new FileInfo(model).Length}");
        }

        return 0;
    }

    // The fast-tier English encoder, unless a shape is given.
    private static FastEncoder FastEncoderFor(string[] args, ulong seed) =>
        args.Contains("--width") || args.Contains("--depth")
            ? new FastEncoder(Num(args, "width", 256), Num(args, "depth", 2), normRows: 32768, prefixRows: 32768, suffixRows: 32768, shapeRows: 32768, seed: seed, prefixLength: 3, suffixLength: 3, embedWidth: 64, hashes: 1)
            : FastEncoder.English(seed);

    private static int Predict(string[] args)
    {
        using Pipeline pipeline = PipelineFile.Load(args[1]);
        using var writer = new StreamWriter(args[3], false, new UTF8Encoding(false)) { NewLine = "\n" };
        int n = 0;
        foreach (string text in ReadTexts(args[2]))
        {
            Doc doc = pipeline.Process(text);
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteStartObject("cats");
                foreach (KeyValuePair<string, float> c in doc.Cats)
                {
                    w.WriteNumber(c.Key, c.Value);
                }

                w.WriteEndObject();
                w.WriteEndObject();
            }

            writer.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
            n++;
        }

        Console.WriteLine($"documents={n}");
        return 0;
    }

    private static int Bench(string[] args)
    {
        using Pipeline pipeline = PipelineFile.Load(args[1]);
        string[] texts = [.. ReadTexts(args[2])];
        int tokens = 0;
        foreach (string t in texts.Take(50))
        {
            tokens += pipeline.Process(t).Length;
        }

        var clock = Stopwatch.StartNew();
        tokens = 0;
        foreach (Doc d in pipeline.ProcessMany(texts))
        {
            tokens += d.Length;
        }

        double s = clock.Elapsed.TotalSeconds;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"threads={TensorCompute.Threads} documents={texts.Length} tokens={tokens} seconds={s:F2} docs_per_s={texts.Length / s:F0} tokens_per_s={tokens / s:F0}"));
        return 0;
    }

    private static IEnumerable<string> ReadTexts(string path)
    {
        foreach (string line in File.ReadLines(path))
        {
            if (line.AsSpan().Trim().Length == 0)
            {
                continue;
            }

            using JsonDocument json = JsonDocument.Parse(line);
            yield return json.RootElement.GetProperty("text").GetString() ?? string.Empty;
        }
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
