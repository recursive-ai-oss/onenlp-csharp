using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.AccurateProbe;

// A head trained on a frozen transformer encoder (an exported ONNX model), to compare encoders with the same recipe.
//
//   upos    --onnx M.onnx --tokenizer T.json --train F.conllu --dev F.conllu --out DIR [--lang en] [--epochs N] [--seed N]
//           [--pooling mean|first] [--threads N] [--patience N] [--lr X] [--batch-words N] [--warmup N] [--dropout X]
//           [--finetune [--train-embeddings] --total-steps N] [--max-steps N] [--eval-every STEPS] [--budget-seconds S] [--checkpoint-every N]
//           With --finetune the encoder trains with the head (TrainableTransformerEncoder; warm-up and cosine decay over --total-steps).
//           --budget-seconds stops the run cleanly after that long; start it again with the same arguments to resume from DIR.
//           UPOS tagger; prints the best development accuracy (the model-selection score).
//   ner     --onnx M.onnx --tokenizer T.json --train F.jsonl --dev F.jsonl --out DIR [same options]
//           Named entity recognizer (BILUO tagging, constrained decoding); prints the best development entity F1.
//   predict-upos --model M.onenlp --gold F.conllu --out P.conllu [--model-dir D]
//           gold tokens in, UPOS out as CoNLL-U for the OneNlp.Eval tool.
//   predict-ner  --model M.onenlp --gold F.jsonl --out PREFIX [--model-dir D]
//           gold tokens in; writes PREFIX.gold.iob2 and PREFIX.pred.iob2 (one token per line, tag last) for 'OneNlp.Eval span'.
//   speed --model M.onenlp --gold F.conllu [--model-dir D] [--threads N] [--reps N] [--batch N]
//           times the trained components over the gold tokens of a file, in batches of --batch (default 32) documents, and prints tokens per second.
//   convert --in F.iob2 --out F.jsonl
//           UNER-style .iob2 to the JSON Lines the ner command reads (gold tokens, character offsets).
//
// Both commands train for at most --epochs (default 20), stop early on the development score, and write DIR/model.onenlp,
// which references the encoder files by name.
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: upos|ner|convert ... (see the comment at the top of Program.cs)");
                return 2;
            }

            Dictionary<string, string> o = Options(args.AsSpan(1));
            if (o.TryGetValue("threads", out string? threads))
            {
                TensorCompute.Threads = int.Parse(threads, CultureInfo.InvariantCulture);
            }

            return args[0] switch
            {
                "upos" => Train(o, ner: false),
                "ner" => Train(o, ner: true),
                "predict-upos" => PredictUpos(o),
                "predict-ner" => PredictNer(o),
                "speed" => Speed(o),
                "convert" => Convert(Required(o, "in"), Required(o, "out")),
                _ => 2,
            };
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static int Train(Dictionary<string, string> o, bool ner)
    {
        string outDir = Required(o, "out");
        int epochs = o.TryGetValue("epochs", out string? e) ? int.Parse(e, CultureInfo.InvariantCulture) : 20;
        ulong seed = o.TryGetValue("seed", out string? s) ? ulong.Parse(s, CultureInfo.InvariantCulture) : 1;
        int patience = o.TryGetValue("patience", out string? p) ? int.Parse(p, CultureInfo.InvariantCulture) : 4;
        Language language = Language.FromCode(o.GetValueOrDefault("lang", "en"));
        List<Example> train = [.. ner ? JsonlReader.Read(Required(o, "train")) : ConllUReader.ReadExamples(Required(o, "train"))];
        List<Example> dev = [.. ner ? JsonlReader.Read(Required(o, "dev")) : ConllUReader.ReadExamples(Required(o, "dev"))];
        SubwordPooling pooling = o.GetValueOrDefault("pooling", "mean") == "first" ? SubwordPooling.FirstPiece : SubwordPooling.Mean;
        bool finetune = o.ContainsKey("finetune");
        using IEncoder encoder = finetune
            ? TrainableTransformerEncoder.LoadOnnx(Required(o, "onnx"), Required(o, "tokenizer"), new TrainableTransformerOptions { Pooling = pooling, FreezeEmbeddings = !o.ContainsKey("train-embeddings") })
            : new TransformerEncoder(Required(o, "onnx"), Required(o, "tokenizer"), pooling);
        float rate = o.TryGetValue("lr", out string? lr) ? float.Parse(lr, CultureInfo.InvariantCulture) : 0.003f;
        int warmup = o.TryGetValue("warmup", out string? wu) ? int.Parse(wu, CultureInfo.InvariantCulture) : 100;
        LrSchedule? schedule = o.TryGetValue("total-steps", out string? ts) ? new WarmupCosineLr(rate, warmup, long.Parse(ts, CultureInfo.InvariantCulture), 0.1f) : null;
        using var cts = new CancellationTokenSource();
        if (o.TryGetValue("budget-seconds", out string? bs))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(double.Parse(bs, CultureInfo.InvariantCulture)));
        }
        Pipeline pipeline = Pipeline.Blank(language);
        pipeline = ner
            ? pipeline.Add(new EntityRecognizer(encoder, NerDecoding.Tagging, 2, 256))
            : pipeline.Add(new Tagger(encoder, TokenColumn.Pos));
        using Pipeline owned = pipeline;

        var clock = Stopwatch.StartNew();
        var progress = new Progress(pr =>
        {
            if (pr.DevScore is { } score)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={pr.Step} epoch={pr.Epoch} loss={pr.Loss:F4} dev={score:F3} best={pr.IsBest} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
            }
        });

        TrainResult result = owned.Train(
            train,
            new TrainOptions
            {
                Epochs = epochs,
                BatchWords = o.TryGetValue("batch-words", out string? bw) ? int.Parse(bw, CultureInfo.InvariantCulture) : 1000,
                LearningRate = rate,
                WarmupSteps = warmup,
                Schedule = schedule,
                MaxSteps = o.TryGetValue("max-steps", out string? ms) ? long.Parse(ms, CultureInfo.InvariantCulture) : 0,
                EvalEvery = o.TryGetValue("eval-every", out string? ee) ? int.Parse(ee, CultureInfo.InvariantCulture) : 0,
                Dropout = o.TryGetValue("dropout", out string? dr) ? float.Parse(dr, CultureInfo.InvariantCulture) : ner ? 0.2f : 0.1f,
                Seed = seed,
                DevExamples = dev,
                Score = ner ? DevScores.Entities() : UposScore,
                Patience = patience,
                CheckpointDirectory = outDir,
                CheckpointEvery = o.TryGetValue("checkpoint-every", out string? ce) ? int.Parse(ce, CultureInfo.InvariantCulture) : 200,
                Progress = progress,
            },
            cts.Token);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"best_dev={result.BestScore:F3} best_step={result.BestStep} steps={result.Steps} epochs={result.Epochs} stopped_early={result.StoppedEarly} completed={result.Completed}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"wall_s={clock.Elapsed.TotalSeconds:F1} threads={TensorCompute.Threads} weights_sha256={result.WeightsSha256}"));
        if (result.Completed)
        {
            PipelineFile.Save(owned, Path.Combine(outDir, "model.onenlp"));
        }

        return 0;
    }

    private static int PredictUpos(Dictionary<string, string> o)
    {
        TransformerEncoder.ModelDirectory = o.GetValueOrDefault("model-dir");
        using Pipeline pipeline = PipelineFile.Load(Required(o, "model"));
        var sb = new StringBuilder();
        foreach (ConllUSentence sentence in ConllUReader.Read(Required(o, "gold")))
        {
            Doc predicted = Example.Blank(sentence.Doc);
            foreach (IPipelineComponent component in pipeline.Components.Where(c => c is ITrainableComponent))
            {
                component.Process(predicted);
            }

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
        return 0;
    }

    private static int Speed(Dictionary<string, string> o)
    {
        TransformerEncoder.ModelDirectory = o.GetValueOrDefault("model-dir");
        int reps = o.TryGetValue("reps", out string? r) ? int.Parse(r, CultureInfo.InvariantCulture) : 3;
        int batch = o.TryGetValue("batch", out string? b) ? int.Parse(b, CultureInfo.InvariantCulture) : 32;
        using Pipeline pipeline = PipelineFile.Load(Required(o, "model"));
        IPipelineComponent[] components = [.. pipeline.Components.Where(c => c is ITrainableComponent)];
        string[] names = [.. components.Select(c => c.GetType().Name)];
        List<Example> examples = [.. ConllUReader.ReadExamples(Required(o, "gold"))];
        long tokens = examples.Sum(e => (long)e.Reference.Length);
        var rates = new List<double>();
        for (int rep = 0; rep <= reps; rep++)
        {
            // Fresh documents each time: the frozen encoder caches by document, which would hide its cost.
            Doc[] docs = [.. examples.Select(e => Example.Blank(e.Reference))];
            var clock = Stopwatch.StartNew();
            using var scope = new ActivationScope();
            for (int i = 0; i < docs.Length; i += batch)
            {
                foreach (IPipelineComponent c in components)
                {
                    c.ProcessBatch(docs.AsSpan(i, Math.Min(batch, docs.Length - i)));
                }
            }

            if (rep > 0)
            {
                rates.Add(tokens / clock.Elapsed.TotalSeconds);
            }
        }

        rates.Sort();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"components={string.Join('+', names)} tokens={tokens} threads={TensorCompute.Threads} batch={batch} tok_per_s_median={rates[rates.Count / 2]:F0} min={rates[0]:F0} max={rates[^1]:F0}"));
        return 0;
    }

    private static int PredictNer(Dictionary<string, string> o)
    {
        TransformerEncoder.ModelDirectory = o.GetValueOrDefault("model-dir");
        using Pipeline pipeline = PipelineFile.Load(Required(o, "model"));
        string prefix = Required(o, "out");
        using var gold = new StreamWriter(prefix + ".gold.iob2", false, new UTF8Encoding(false)) { NewLine = "\n" };
        using var pred = new StreamWriter(prefix + ".pred.iob2", false, new UTF8Encoding(false)) { NewLine = "\n" };
        foreach (Example e in JsonlReader.Read(Required(o, "gold")))
        {
            Doc blank = Example.Blank(e.Reference);
            pipeline.Process(blank);
            List<string> g = IobTags(e.Reference);
            List<string> p = IobTags(blank);
            for (int t = 0; t < blank.Length; t++)
            {
                gold.WriteLine(blank[t].ToString() + '\t' + g[t]);
                pred.WriteLine(blank[t].ToString() + '\t' + p[t]);
            }

            gold.WriteLine();
            pred.WriteLine();
        }

        return 0;
    }

    private static List<string> IobTags(Doc doc)
    {
        var tags = new List<string>(doc.Length);
        foreach (Span e in doc.Ents)
        {
            while (tags.Count < e.Start)
            {
                tags.Add("O");
            }

            tags.Add("B-" + e.Label);
            for (int t = e.Start + 1; t < e.End; t++)
            {
                tags.Add("I-" + e.Label);
            }
        }

        while (tags.Count < doc.Length)
        {
            tags.Add("O");
        }

        return tags;
    }

    private static double UposScore(IReadOnlyList<Example> examples) =>
        UdScorer.Score(examples.Select(x => x.Predicted), examples.Select(x => x.Reference), "UPOS").Value;

    // UNER .iob2: '# text = ...' comment, then one token per line (id, form, BIO tag, ...), a blank line between sentences.
    private static int Convert(string input, string output)
    {
        using var stream = new FileStream(output, FileMode.Create);
        int sentences = 0;
        string? text = null;
        var forms = new List<string>();
        var tags = new List<string>();

        void Flush()
        {
            if (forms.Count == 0)
            {
                return;
            }

            WriteLine(stream, text, forms, tags);
            sentences++;
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

    private static void WriteLine(Stream stream, string? text, List<string> forms, List<string> tags)
    {
        // Token offsets come from walking the sentence text; if a form is not found there the forms are joined with spaces instead.
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
        w.WriteStartArray("entities");
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
        w.Flush();
        stream.WriteByte((byte)'\n');
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
