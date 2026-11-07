using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.EnglishFastTagging;

// English fast-tier tagger, morphologizer and edit-tree lemmatizer, trained jointly on Universal Dependencies English-EWT.
//
//   train   --train F.conllu --dev F.conllu --out DIR [--epochs N] [--seed N] [--threads N] [--budget-seconds S] [--checkpoint-every N]
//           trains (or resumes from DIR/last.ckpt) until done or the time budget is used; writes DIR/model.onenlp when done.
//           Run it again with the same arguments to continue: the result is the same as one uninterrupted run.
//   predict --model M.onenlp --gold F.conllu --mode raw|gold [--mwt] --out P.conllu
//           writes CoNLL-U predictions for scoring with the OneNlp.Eval tool. "raw" gives the model only the text (its word
//           tokenizer and sentence splitter find the tokens); "gold" gives it the gold tokens and sentences.
internal static class Program
{
    // The recipe. Everything that shapes the model is here, so that a model can be rebuilt from this file and the data.
    private const ulong EncoderSeed = 1;
    private const int BatchWords = 1000;
    private const float LearningRate = 0.002f;
    private const int WarmupSteps = 200;
    private const float Dropout = 0.1f;
    private const int Patience = 6;

    private static readonly string[] Clitics =
        ["n't", "n\u2019t", "'s", "\u2019s", "'re", "\u2019re", "'ve", "\u2019ve", "'ll", "\u2019ll", "'d", "\u2019d", "'m", "\u2019m"];

    private static readonly string[] Fused = ["cannot", "gonna", "gotta", "wanna", "gimme", "lemme"];

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: train|predict ... (see the comment at the top of Program.cs)");
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
                "bench" => Bench(o),
                _ => 2,
            };
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    // heads: all | tagger | morphologizer | lemmatizer (one head alone, for timing; the recipe's model has all three).
    private static Pipeline NewPipeline(string heads = "all")
    {
        var encoder = FastEncoder.English(EncoderSeed);
        Pipeline pipeline = Pipeline.Blank(Language.English).Add("sentencizer", new Dictionary<string, string> { ["language"] = "en" });
        if (heads is "all" or "tagger")
        {
            pipeline.Add(new Tagger(encoder, TokenColumn.Tag) { OwnsEncoder = true });
        }

        if (heads is "all" or "morphologizer")
        {
            pipeline.Add(new Morphologizer(encoder) { OwnsEncoder = heads == "morphologizer" });
        }

        if (heads is "all" or "lemmatizer")
        {
            pipeline.Add(new EditTreeLemmatizer(encoder) { OwnsEncoder = heads == "lemmatizer" });
        }

        return pipeline;
    }

    // The development score: the mean of UPOS, XPOS, UFeats and Lemmas on the development set, given gold tokens.
    private static double DevScore(IReadOnlyList<Example> examples)
    {
        double sum = 0;
        foreach (string metric in new[] { "UPOS", "XPOS", "UFeats", "Lemmas" })
        {
            sum += UdScorer.Score(examples.Select(e => e.Predicted), examples.Select(e => e.Reference), metric).Value;
        }

        return sum / 4;
    }

    private static int Train(Dictionary<string, string> o)
    {
        string outDir = Required(o, "out");
        int epochs = o.TryGetValue("epochs", out string? e) ? int.Parse(e, CultureInfo.InvariantCulture) : 30;
        ulong seed = o.TryGetValue("seed", out string? s) ? ulong.Parse(s, CultureInfo.InvariantCulture) : 1;
        int every = o.TryGetValue("checkpoint-every", out string? c) ? int.Parse(c, CultureInfo.InvariantCulture) : 100;
        double budget = o.TryGetValue("budget-seconds", out string? b) ? double.Parse(b, CultureInfo.InvariantCulture) : double.PositiveInfinity;

        List<Example> train = [.. ConllUReader.ReadExamples(Required(o, "train"))];
        List<Example> dev = [.. ConllUReader.ReadExamples(Required(o, "dev"))];
        using Pipeline pipeline = NewPipeline(o.GetValueOrDefault("heads", "all"));
        using var cts = new CancellationTokenSource();
        if (!double.IsPositiveInfinity(budget))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        var clock = Stopwatch.StartNew();
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } score)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev={score:F3} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
            }
        });

        TrainResult result = pipeline.Train(
            train,
            new TrainOptions
            {
                Epochs = epochs,
                BatchWords = BatchWords,
                LearningRate = LearningRate,
                WarmupSteps = WarmupSteps,
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
        bool raw = Required(o, "mode") == "raw";
        bool mwt = o.ContainsKey("mwt");
        using Pipeline pipeline = PipelineFile.Load(Required(o, "model"));
        var sb = new StringBuilder();
        int tokens = 0;
        if (raw)
        {
            // Ten gold sentences at a time, joined by one space as the text of a running document.
            var chunk = new List<string>();
            foreach (ConllUSentence sentence in ConllUReader.Read(Required(o, "gold")))
            {
                chunk.Add(sentence.Doc.Text);
                if (chunk.Count == 10)
                {
                    tokens += WriteRaw(pipeline, string.Join(' ', chunk), mwt, sb);
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
            {
                tokens += WriteRaw(pipeline, string.Join(' ', chunk), mwt, sb);
            }
        }
        else
        {
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

                    WriteWord(sb, predicted, i, i, sentence.Forms[i], i == 0);
                }

                sb.Append('\n');
            }
        }

        File.WriteAllText(Required(o, "out"), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"tokens={tokens}");
        return 0;
    }

    // Inference throughput of the three heads on the gold tokens of a file, and of raw text end to end. Documents are rebuilt for every
    // repetition because an encoded document is kept with it. Prints the median of the repetitions.
    private static int Bench(Dictionary<string, string> o)
    {
        int reps = o.TryGetValue("reps", out string? r) ? int.Parse(r, CultureInfo.InvariantCulture) : 3;
        using Pipeline pipeline = PipelineFile.Load(Required(o, "model"));
        List<ConllUSentence> gold = [.. ConllUReader.Read(Required(o, "gold"))];
        IPipelineComponent[] heads = [.. pipeline.Components.Where(c => c is ITrainableComponent)];
        string[] texts = [.. gold.Select(g => g.Doc.Text)];
        int tokens = gold.Sum(g => g.Doc.Length);
        var headSeconds = new List<double>();
        var rawSeconds = new List<double>();
        for (int rep = 0; rep < reps + 1; rep++)
        {
            Doc[] docs = [.. gold.Select(g => Example.Blank(g.Doc))];
            var clock = Stopwatch.StartNew();
            for (int start = 0; start < docs.Length; start += 64)
            {
                int n = Math.Min(64, docs.Length - start);
                foreach (IPipelineComponent head in heads)
                {
                    head.ProcessBatch(docs.AsSpan(start, n));
                }
            }

            double h = clock.Elapsed.TotalSeconds;
            clock.Restart();
            int rawTokens = 0;
            foreach (Doc d in pipeline.ProcessMany(texts))
            {
                rawTokens += d.Length;
            }

            double w = clock.Elapsed.TotalSeconds;
            if (rep > 0)
            {
                headSeconds.Add(h);
                rawSeconds.Add(w);
            }

            _ = rawTokens;
        }

        headSeconds.Sort();
        rawSeconds.Sort();
        double hm = headSeconds[headSeconds.Count / 2];
        double wm = rawSeconds[rawSeconds.Count / 2];
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"threads={TensorCompute.Threads} tokens={tokens} sentences={gold.Count} heads_median_s={hm:F3} heads_tokens_per_s={tokens / hm:F0} raw_end_to_end_median_s={wm:F3} raw_tokens_per_s={tokens / wm:F0} reps={reps}"));
        return 0;
    }

    private static int WriteRaw(Pipeline pipeline, string text, bool mwt, StringBuilder sb)
    {
        Doc doc = pipeline.Process(text);
        foreach (Span sentence in doc.Sentences)
        {
            var ids = new List<int>();
            foreach (Token token in sentence)
            {
                ids.Add(token.Index);
            }

            for (int k = 0; k < ids.Count; k++)
            {
                int groupEnd = k;
                if (mwt && k + 1 < ids.Count && doc[ids[k]].EndIdx == doc[ids[k + 1]].Idx)
                {
                    string second = doc[ids[k + 1]].ToString();
                    string whole = doc[ids[k]].ToString() + second;
                    if (Array.Exists(Clitics, c => string.Equals(c, second, StringComparison.OrdinalIgnoreCase))
                        || Array.Exists(Fused, f => string.Equals(f, whole, StringComparison.OrdinalIgnoreCase)))
                    {
                        groupEnd = k + 1;
                    }
                }

                if (groupEnd > k)
                {
                    sb.Append(k + 1).Append('-').Append(groupEnd + 1).Append('\t')
                        .Append(text, doc[ids[k]].Idx, doc[ids[groupEnd]].EndIdx - doc[ids[k]].Idx)
                        .Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                }

                for (int j = k; j <= groupEnd; j++)
                {
                    WriteWord(sb, doc, ids[j], j, doc[ids[j]].ToString(), j == 0);
                }

                k = groupEnd;
            }

            sb.Append('\n');
        }

        return doc.Length;
    }

    // HEAD and DEPREL are placeholders (the first word is the root, the rest hang off it) so that the scorer accepts the file.
    private static void WriteWord(StringBuilder sb, Doc doc, int tokenIndex, int position, string form, bool root)
    {
        Token t = doc[tokenIndex];
        sb.Append(position + 1).Append('\t').Append(form).Append('\t').Append(Cell(t.Lemma)).Append('\t').Append(Cell(t.Pos)).Append('\t')
            .Append(Cell(t.Tag)).Append('\t').Append(Cell(t.Morph)).Append('\t').Append(root ? "0\troot" : "1\tdep").Append("\t_\t_\n");
    }

    private static string Cell(string value) => value.Length == 0 ? "_" : value;

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
