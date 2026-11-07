using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.EnSpancat;

// English span categorizer over the fast encoder: every n-gram of the chosen sizes is scored for every label, and the spans that clear the
// threshold go into a span group. The whole recipe: JSON Lines in, a saved pipeline out.
//
//   convert <in.iob2> <out.jsonl>                          turns a UNER-style .iob2 file into JSON Lines whose entity mentions are the gold
//                                                          spans of group "sc" (gold tokens, character offsets)
//   train   <train.jsonl> <dev.jsonl> <out-dir> [options]  trains, or resumes from <out-dir>/last.ckpt; writes <out-dir>/model.onenlp when done
//   predict <model.onenlp> <in.jsonl> <out.jsonl>          one {"spans": [[start, end, label, score], ...]} line per input line, character
//                                                          offsets, for OneNlp.Eval spangroup. Lines with "tokens" are given to the model as is.
//
// Data lines: {"text": "...", "tokens": [[s, e], ...], "spans": {"sc": [[s, e, "LABEL"], ...]}}. Options (defaults are the recipe):
//   --sizes 1,2,3 --threshold 0.5 --hidden 256 --epochs 40 --seed 7 --lr 0.003 --dropout 0.2 --batch-words 1000 --patience 6 --threads N
//   --checkpoint-every 0 --budget-seconds S
internal static class Program
{
    private const string Group = "sc";

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
                "convert" when args.Length == 3 => Convert(args[1], args[2]),
                "train" when args.Length >= 4 => Train(args),
                "predict" when args.Length >= 4 => Predict(args),
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
        Console.Error.WriteLine("usage: convert <in.iob2> <out.jsonl> | train <train.jsonl> <dev.jsonl> <out-dir> [options] | predict <model> <in.jsonl> <out.jsonl>  (see Program.cs)");
        return 2;
    }

    private static string Opt(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, "--" + name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static int Num(string[] args, string name, int fallback) => int.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static float Flt(string[] args, string name, float fallback) => float.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static int Convert(string input, string output)
    {
        using var stream = new FileStream(output, FileMode.Create);
        int sentences = 0;
        string? text = null;
        var forms = new List<string>();
        var tags = new List<string>();

        void Flush()
        {
            if (forms.Count > 0)
            {
                WriteLine(stream, text, forms, tags);
                sentences++;
            }

            text = null;
            forms.Clear();
            tags.Clear();
        }

        foreach (string line in File.ReadLines(input))
        {
            if (line.StartsWith("# text = ", StringComparison.Ordinal))
            {
                text = line["# text = ".Length..];
            }
            else if (line.StartsWith('#'))
            {
                continue;
            }
            else if (line.Length == 0)
            {
                Flush();
            }
            else
            {
                string[] cols = line.Split('\t');
                forms.Add(cols[1]);
                tags.Add(cols[2]);
            }
        }

        Flush();
        Console.WriteLine($"{input}: {sentences} sentences -> {output}");
        return 0;
    }

    // Token offsets come from walking the sentence text; when a form is not found there, the forms are joined with spaces instead.
    private static void WriteLine(Stream stream, string? text, List<string> forms, List<string> tags)
    {
        var starts = new int[forms.Count];
        bool ok = text is not null;
        int pos = 0;
        for (int i = 0; ok && i < forms.Count; i++)
        {
            int at = text!.IndexOf(forms[i], pos, StringComparison.Ordinal);
            if (at < 0)
            {
                ok = false;
                break;
            }

            starts[i] = at;
            pos = at + forms[i].Length;
        }

        if (!ok)
        {
            text = string.Join(' ', forms);
            pos = 0;
            for (int i = 0; i < forms.Count; i++)
            {
                starts[i] = pos;
                pos += forms[i].Length + 1;
            }
        }

        using var w = new Utf8JsonWriter(stream);
        w.WriteStartObject();
        w.WriteString("text", text);
        w.WriteStartArray("tokens");
        for (int i = 0; i < forms.Count; i++)
        {
            w.WriteStartArray();
            w.WriteNumberValue(starts[i]);
            w.WriteNumberValue(starts[i] + forms[i].Length);
            w.WriteEndArray();
        }

        w.WriteEndArray();
        w.WriteStartObject("spans");
        w.WriteStartArray(Group);
        foreach (LabeledSpan s in SpanScorer.ExtractIob2(tags))
        {
            w.WriteStartArray();
            w.WriteNumberValue(starts[s.Start]);
            w.WriteNumberValue(starts[s.End - 1] + forms[s.End - 1].Length);
            w.WriteStringValue(s.Label);
            w.WriteEndArray();
        }

        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();
        w.Flush();
        stream.WriteByte((byte)'\n');
    }

    private static int Train(string[] args)
    {
        List<Example> train = [.. JsonlReader.Read(args[1])];
        List<Example> dev = [.. JsonlReader.Read(args[2])];
        string outDir = args[3];
        ulong seed = (ulong)Num(args, "seed", 7);
        int[] sizes = [.. Opt(args, "sizes", "1,2,3").Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture))];
        using Pipeline pipeline = Pipeline.Blank(Language.English)
            .Add(new SpanCategorizer(FastEncoder.English(seed), Group, sizes, Num(args, "hidden", 256), Flt(args, "threshold", 0.5f)) { OwnsEncoder = true });

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
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev_span_f1={score:F3} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
            }
        });

        TrainResult result = pipeline.Train(
            train,
            new TrainOptions
            {
                Epochs = Num(args, "epochs", 40),
                BatchWords = Num(args, "batch-words", 1000),
                LearningRate = Flt(args, "lr", 0.003f),
                WarmupSteps = Num(args, "warmup", 100),
                Dropout = Flt(args, "dropout", 0.2f),
                Seed = seed,
                DevExamples = dev,
                Score = DevScores.SpanGroup(Group),
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

    private static int Predict(string[] args)
    {
        using Pipeline pipeline = PipelineFile.Load(args[1]);
        using var writer = new StreamWriter(args[3], false, new UTF8Encoding(false)) { NewLine = "\n" };
        int n = 0;
        // The examples carry the gold tokens, so the model sees exactly the tokens the spans were written on.
        foreach (Example example in JsonlReader.Read(args[2]))
        {
            Doc doc = example.Predicted;
            foreach (IPipelineComponent component in pipeline.Components)
            {
                component.Process(doc);
            }

            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteStartArray("spans");
                if (doc.Spans.TryGetValue(Group, out IReadOnlyList<Span>? spans))
                {
                    foreach (Span s in spans)
                    {
                        w.WriteStartArray();
                        w.WriteNumberValue(s.StartChar);
                        w.WriteNumberValue(s.EndChar);
                        w.WriteStringValue(s.Label);
                        w.WriteNumberValue(s.Confidence);
                        w.WriteEndArray();
                    }
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            writer.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
            n++;
        }

        Console.WriteLine($"documents={n}");
        return 0;
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
