using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Recipes.EnParser;

/// <summary>
/// The English dependency-parser recipe: trains a pipeline (word tokenizer, encoder, parser) on a Universal Dependencies treebank with
/// the public training API, and writes CoNLL-U predictions for scoring.
/// </summary>
/// <remarks>
/// Commands (options as <c>--name value</c>):
/// <c>train --train T.conllu --dev D.conllu --out DIR [--epochs 40] [--seconds 1500] [--threads 8] [--seed 7] [--sentences-per-doc 10]</c>
/// trains or resumes from <c>DIR/last.ckpt</c> and stops cleanly after about <c>--seconds</c>; when training has finished it saves <c>DIR/model.onenlp</c>.
/// <c>predict --model M.onenlp --gold G.conllu --mode raw|raw-sentencizer|gold --out P.conllu</c> writes predictions.
/// </remarks>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: train|predict [options]; see the remarks of Program");
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
                "speed" => Speed(o),
                _ => 2,
            };
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidDataException or FormatException or KeyNotFoundException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> Options(ReadOnlySpan<string> args)
    {
        var o = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            o[args[i].TrimStart('-')] = args[i + 1];
        }

        return o;
    }

    private static int Int(Dictionary<string, string> o, string name, int fallback) =>
        o.TryGetValue(name, out string? v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

    // The fast encoder of the English tagging recipe (hashed lower-case form, prefix, suffix and shape, four window layers).
    // --encoder hash swaps in the one-token hashed embedding, which the PyTorch timing script reproduces exactly.
    private static Pipeline NewPipeline(Dictionary<string, string> o) =>
        Pipeline.Blank(Language.English).Add(new Parser(
            o.GetValueOrDefault("encoder", "fast") == "hash"
                ? new HashEmbeddingEncoder(128, 32768, seed: 1)
                : new FastEncoder(Int(o, "width", 96), Int(o, "depth", 4), normRows: 20000, prefixRows: 2000, suffixRows: 5000, shapeRows: 2500, seed: 1),
            arcSize: Int(o, "arc", 256),
            labelSize: Int(o, "label", 128),
            contextLayers: Int(o, "context", 0),
            recurrentSize: Int(o, "lstm", 192),
            recurrentLayers: Int(o, "lstm-layers", 2))
        { OwnsEncoder = true });

    private static int Train(Dictionary<string, string> o)
    {
        int perDoc = Int(o, "sentences-per-doc", 10);
        string outDir = o["out"];
        List<Example> train = Documents(o["train"], perDoc);
        List<Example> dev = Documents(o["dev"], perDoc);
        if (o.TryGetValue("dev-docs", out string? devDocs))
        {
            // Timing runs keep the dev set tiny so that an epoch's wall time is training time.
            dev = dev.Take(int.Parse(devDocs, CultureInfo.InvariantCulture)).ToList();
        }

        using Pipeline pipeline = NewPipeline(o);
        var clock = Stopwatch.StartNew();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(Int(o, "seconds", 1500)));
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } score || p.Step % 25 == 0)
            {
                Console.WriteLine($"step={p.Step} epoch={p.Epoch} loss={p.Loss.ToString("F4", CultureInfo.InvariantCulture)} dev_las={p.DevScore?.ToString("F3", CultureInfo.InvariantCulture)} best={p.IsBest} wall_s={clock.Elapsed.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)}");
            }
        });

        TrainResult result = pipeline.Train(train, new TrainOptions
        {
            Epochs = Int(o, "epochs", 40),
            BatchWords = Int(o, "batch-words", 1500),
            LearningRate = float.Parse(o.GetValueOrDefault("lr", "0.002"), CultureInfo.InvariantCulture),
            WarmupSteps = 100,
            Dropout = float.Parse(o.GetValueOrDefault("dropout", "0.25"), CultureInfo.InvariantCulture),
            Seed = ulong.Parse(o.GetValueOrDefault("seed", "7"), CultureInfo.InvariantCulture),
            DevExamples = dev,
            Score = DevScores.Ud("LAS"),
            Patience = Int(o, "patience", 6),
            CheckpointDirectory = outDir,
            CheckpointEvery = 25,
            Progress = progress,
        }, budget.Token);

        Console.WriteLine($"steps={result.Steps} epochs={result.Epochs} completed={result.Completed} stopped_early={result.StoppedEarly} best_dev_las={result.BestScore.ToString("F3", CultureInfo.InvariantCulture)} best_step={result.BestStep}");
        Console.WriteLine($"weights_sha256={result.WeightsSha256} chunk_wall_s={clock.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} threads={TensorCompute.Threads}");
        if (result.Completed)
        {
            string model = Path.Combine(outDir, "model.onenlp");
            PipelineFile.Save(pipeline, model);
            Console.WriteLine($"saved {model} bytes={new FileInfo(model).Length}");
        }

        return 0;
    }

    // Groups consecutive sentences into documents, the way running text arrives: a parser that only ever saw single sentences could not learn where one ends.
    private static List<Example> Documents(string path, int perDoc)
    {
        var result = new List<Example>();
        var strings = new StringStore();
        var group = new List<ConllUSentence>();
        foreach (ConllUSentence s in ConllUReader.Read(path, strings))
        {
            group.Add(s);
            if (group.Count == perDoc)
            {
                result.Add(Example.FromReference(Merge(group, strings)));
                group.Clear();
            }
        }

        if (group.Count > 0)
        {
            result.Add(Example.FromReference(Merge(group, strings)));
        }

        return result;
    }

    private static Doc Merge(List<ConllUSentence> sentences, StringStore strings)
    {
        var text = new StringBuilder();
        var tokens = new List<TokenBoundary>();
        var heads = new List<int>();
        var deps = new List<int>();
        var marks = new List<SentenceBoundary>();
        foreach (ConllUSentence s in sentences)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            int charOffset = text.Length;
            int tokenOffset = tokens.Count;
            Doc d = s.Doc;
            text.Append(d.Text);
            for (int t = 0; t < d.Length; t++)
            {
                tokens.Add(new TokenBoundary(charOffset + d.GetStarts()[t], d.GetLengths()[t]));
                heads.Add(tokenOffset + d.GetHeads()[t]);
                deps.Add(d.GetColumn(TokenColumn.Dep)[t]);
                marks.Add(t == 0 ? SentenceBoundary.Start : SentenceBoundary.Inside);
            }
        }

        var doc = new Doc(strings, text.ToString(), tokens.ToArray());
        doc.SetHeads(heads.ToArray());
        doc.SetColumn(TokenColumn.Dep, deps.ToArray());
        doc.SetSentenceBoundaries(marks.ToArray());
        return doc;
    }

    private static int Predict(Dictionary<string, string> o)
    {
        string mode = o["mode"];
        using Pipeline pipeline = PipelineFile.Load(o["model"]);
        if (mode == "raw-sentencizer")
        {
            Pipeline withSentences = Pipeline.Blank(Language.English).Add(SentencizerComponent.For(Language.English));
            foreach (IPipelineComponent c in pipeline.Components)
            {
                withSentences.Add(c);
            }

            return Write(withSentences, o, mode);
        }

        return Write(pipeline, o, mode);
    }

    private static int Write(Pipeline pipeline, Dictionary<string, string> o, string mode)
    {
        using var writer = new StreamWriter(o["out"], false, new UTF8Encoding(false)) { NewLine = "\n" };
        int sentences = 0;
        int tokens = 0;
        if (mode == "gold")
        {
            foreach (ConllUSentence gold in ConllUReader.Read(o["gold"]))
            {
                // The gold tokens and sentence go in; only heads and relations come out. Everything else in the line is the gold file's.
                Doc input = Example.Blank(gold.Doc);
                input.SetSentenceBoundaries(gold.Doc.GetSentenceBoundaries());
                foreach (IPipelineComponent c in pipeline.Components)
                {
                    c.Process(input);
                }

                gold.Doc.SetHeads(input.GetHeads());
                gold.Doc.SetColumn(TokenColumn.Dep, input.GetColumn(TokenColumn.Dep));
                ConllUWriter.Write(writer, gold);
                sentences++;
                tokens += input.Length;
            }
        }
        else
        {
            foreach (string text in RawDocuments(o["gold"]))
            {
                Doc doc = pipeline.Process(text);
                foreach (Span sentence in doc.Sentences)
                {
                    WriteSentence(writer, doc, sentence.Start, sentence.End);
                    sentences++;
                }

                tokens += doc.Length;
            }
        }

        Console.WriteLine($"{o["out"]}: {sentences} sentences, {tokens} tokens");
        return 0;
    }

    // Indicative throughput: raw text through the tokenizer and the parser, in batches, after one untimed warm-up pass.
    private static int Speed(Dictionary<string, string> o)
    {
        using Pipeline pipeline = PipelineFile.Load(o["model"]);
        List<string> texts = [.. RawDocuments(o["gold"])];
        int repeats = Int(o, "repeat", 3);
        long tokens = 0;
        foreach (Doc d in pipeline.ProcessMany(texts, 32))
        {
            tokens += d.Length;
        }

        for (int r = 0; r < repeats; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            foreach (Doc d in pipeline.ProcessMany(texts, 32))
            {
                _ = d.Length;
            }

            double seconds = clock.Elapsed.TotalSeconds;
            Console.WriteLine($"run={r} documents={texts.Count} tokens={tokens} seconds={seconds.ToString("F2", CultureInfo.InvariantCulture)} tokens_per_second={(tokens / seconds).ToString("F0", CultureInfo.InvariantCulture)} managed_bytes_per_token={((GC.GetAllocatedBytesForCurrentThread() - before) / tokens).ToString(CultureInfo.InvariantCulture)} threads={TensorCompute.Threads}");
        }

        return 0;
    }

    // The text of each document of a gold file, rebuilt from the gold forms and SpaceAfter, sentences joined by a space.
    private static IEnumerable<string> RawDocuments(string path)
    {
        var text = new StringBuilder();
        foreach (ConllUSentence s in ConllUReader.Read(path))
        {
            if (s.Comments.Any(c => c.StartsWith("# newdoc", StringComparison.Ordinal)) && text.Length > 0)
            {
                yield return text.ToString();
                text.Clear();
            }

            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(s.Doc.Text);
        }

        if (text.Length > 0)
        {
            yield return text.ToString();
        }
    }

    private static void WriteSentence(StreamWriter writer, Doc doc, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            Token t = doc[i];
            int head = t.HeadIndex == i ? 0 : t.HeadIndex - start + 1;
            string deprel = head == 0 ? "root" : t.Dep;
            string misc = i + 1 < end && !t.HasSpaceAfter ? "SpaceAfter=No" : "_";
            writer.Write(string.Create(CultureInfo.InvariantCulture, $"{i - start + 1}\t{t}\t_\t_\t_\t_\t{head}\t{deprel}\t_\t{misc}\n"));
        }

        writer.Write('\n');
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
