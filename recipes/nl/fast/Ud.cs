using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.Dutch;

// The Universal Dependencies parts of the Dutch recipe: training the tagging heads and the parser, and writing CoNLL-U predictions.
internal static partial class Program
{
    private const ulong EncoderSeed = 1;

    private static readonly Stopwatch Wall = Stopwatch.StartNew();

    // One line per evaluation: the development score after an epoch.
    private static readonly Progress Reporter = new(p =>
    {
        if (p.DevScore is { } score)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev={score:F3} best={p.IsBest} elapsed_s={Wall.Elapsed.TotalSeconds:F1}"));
        }
    });

    // Multiword tokens whose surface form the tokenizer cuts into two pieces, with the words of the treebank. Dutch has none worth a rule:
    // UD Dutch-Alpino has one multiword token in its train and dev files (nogeens), so the table is empty and predictions carry no
    // multiword-token lines. The code path is kept so that the other recipes of the family read the same.
    private static readonly Dictionary<string, string[]> Contractions = new(StringComparer.Ordinal);

    private static Pipeline NewTaggingPipeline(string[] args)
    {
        FastEncoder encoder = MakeEncoder(args, EncoderSeed);
        string heads = Opt(args, "heads", "all");
        Pipeline pipeline = Pipeline.Blank(Language.Dutch).Add("sentencizer", new Dictionary<string, string> { ["language"] = "nl" });
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

    // The development score of the tagging heads: the mean of UPOS, XPOS, UFeats and Lemmas, given gold tokens.
    private static double TaggingDevScore(IReadOnlyList<Example> examples)
    {
        double sum = 0;
        foreach (string metric in new[] { "UPOS", "XPOS", "UFeats", "Lemmas" })
        {
            sum += UdScorer.Score(examples.Select(e => e.Predicted), examples.Select(e => e.Reference), metric).Value;
        }

        return sum / 4;
    }

    private static int TrainTagging(string[] args)
    {
        string outDir = Required(args, "out");
        List<Example> train = [.. ConllUReader.ReadExamples(Required(args, "train"))];
        List<Example> dev = [.. ConllUReader.ReadExamples(Required(args, "dev"))];
        using Pipeline pipeline = NewTaggingPipeline(args);
        int batchWords = Num(args, "batch-words", 1000);
        float lr = Flt(args, "lr", 0.002f);

        // --cosine-epochs N: a fixed budget of N epochs with the rate falling along a half cosine after the warm-up (Loshchilov and Hutter 2017),
        // instead of a constant rate that early stopping has to cut off.
        int cosineEpochs = Num(args, "cosine-epochs", 0);
        long stepsPerEpoch = Math.Max(1, train.Sum(e => (long)e.Predicted.Length) / batchWords);
        RecursiveAI.OneModel.Training.LrSchedule? schedule = cosineEpochs > 0 ? new RecursiveAI.OneModel.Training.WarmupCosineLr(lr, 200, cosineEpochs * stepsPerEpoch) : null;
        return RunTraining(pipeline, train, new TrainOptions
        {
            Epochs = cosineEpochs > 0 ? cosineEpochs : Num(args, "epochs", 40),
            BatchWords = batchWords,
            LearningRate = lr,
            Schedule = schedule,
            WarmupSteps = 200,
            Dropout = Flt(args, "dropout", 0.1f),
            Seed = (ulong)Num(args, "seed", 1),
            DevExamples = dev,
            Score = TaggingDevScore,
            Patience = Num(args, "patience", 6),
            CheckpointDirectory = outDir,
            CheckpointEvery = Num(args, "checkpoint-every", 100),
            Progress = Reporter,
        }, args, outDir);
    }

    private static int TrainParser(string[] args)
    {
        string outDir = Required(args, "out");
        int perDoc = Num(args, "sentences-per-doc", 10);
        List<Example> train = Documents(Required(args, "train"), perDoc);
        List<Example> dev = Documents(Required(args, "dev"), perDoc);
        using Pipeline pipeline = Pipeline.Blank(Language.Dutch).Add(new Parser(
            MakeEncoder(args, EncoderSeed),
            arcSize: Num(args, "arc", 256),
            labelSize: Num(args, "label", 128),
            contextLayers: Num(args, "context", 0),
            recurrentSize: Num(args, "lstm", 192),
            recurrentLayers: Num(args, "lstm-layers", 2),
            labelBilinear: Num(args, "label-bilinear", 1) == 1,
            labelProduct: Num(args, "label-product", 0) == 1)
        { OwnsEncoder = true });
        return RunTraining(pipeline, train, new TrainOptions
        {
            Epochs = Num(args, "epochs", 40),
            BatchWords = Num(args, "batch-words", 1500),
            LearningRate = Flt(args, "lr", 0.002f),
            WarmupSteps = 100,
            Dropout = Flt(args, "dropout", 0.25f),
            Seed = (ulong)Num(args, "seed", 7),
            DevExamples = dev,
            Score = DevScores.Ud("LAS"),
            Patience = Num(args, "patience", 6),
            CheckpointDirectory = outDir,
            CheckpointEvery = Num(args, "checkpoint-every", 25),
            Progress = Reporter,
        }, args, outDir);
    }

    // Trains (or resumes from the checkpoint in outDir) until the epochs are done, early stopping triggers, or --seconds is used up;
    // saves model.onenlp when training has finished.
    private static int RunTraining(Pipeline pipeline, List<Example> train, TrainOptions options, string[] args, string outDir)
    {
        double budget = Opt(args, "seconds", string.Empty) is { Length: > 0 } b ? double.Parse(b, CultureInfo.InvariantCulture) : double.PositiveInfinity;
        using var cts = new CancellationTokenSource();
        if (!double.IsPositiveInfinity(budget))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        var clock = Stopwatch.StartNew();
        TrainResult result = pipeline.Train(train, options, cts.Token);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"completed={result.Completed} steps={result.Steps} epochs={result.Epochs} evaluations={result.Evaluations} best_dev={result.BestScore:F3} best_step={result.BestStep} stopped_early={result.StoppedEarly}"));
        Console.WriteLine($"weights_sha256={result.WeightsSha256}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"chunk_wall_s={clock.Elapsed.TotalSeconds:F1} threads={RecursiveAI.OneModel.Tensors.TensorCompute.Threads}"));
        if (result.Completed)
        {
            string model = Path.Combine(outDir, "model.onenlp");
            PipelineFile.Save(pipeline, model);
            Console.WriteLine($"model={model} bytes={new FileInfo(model).Length}");
        }

        return 0;
    }

    // Groups consecutive sentences into documents, the way running text arrives: a parser that only ever saw single sentences could not learn
    // where one ends.
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

    // ---- loading the parts as one pipeline ----

    // The saved parts in pipeline order. They share the first part's tokenizer and string table, as the packaged pipeline does.
    private static Pipeline LoadParts(string dir, string[] args)
    {
        string[] parts = Opt(args, "parts", "tagging,parser,ner").Split(',');
        var loaded = new List<Pipeline>();
        foreach (string part in parts)
        {
            string file = Path.Combine(dir, part + ".onenlp");
            if (File.Exists(file))
            {
                loaded.Add(PipelineFile.Load(file));
            }
        }

        if (loaded.Count == 0)
        {
            throw new ArgumentException($"No part found in '{dir}'.");
        }

        bool parser = loaded.Exists(p => p.Components.Any(c => c.Name == "parser"));
        Pipeline combined = new(loaded[0].Tokenizer, Language.Dutch, loaded[0].Strings);
        foreach (Pipeline part in loaded)
        {
            foreach (IPipelineComponent component in part.Components)
            {
                // With a parser the parser sets the sentences; without one the saved rule sentencizer stays.
                if (component.Name == "sentencizer" && parser)
                {
                    continue;
                }

                combined.Add(component);
            }
        }

        return combined;
    }

    // ---- prediction ----

    private static int Predict(string[] args)
    {
        bool raw = Required(args, "mode") == "raw";
        using Pipeline pipeline = LoadParts(Required(args, "models"), args);
        var sb = new StringBuilder();
        int tokens = 0;
        if (raw)
        {
            // Ten gold sentences at a time, joined by one space as the text of a running document.
            var chunk = new List<string>();
            foreach (ConllUSentence sentence in ConllUReader.Read(Required(args, "gold")))
            {
                chunk.Add(sentence.Doc.Text);
                if (chunk.Count == 10)
                {
                    tokens += WriteRaw(pipeline, string.Join(' ', chunk), sb);
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
            {
                tokens += WriteRaw(pipeline, string.Join(' ', chunk), sb);
            }
        }
        else
        {
            foreach (ConllUSentence sentence in ConllUReader.Read(Required(args, "gold")))
            {
                // The gold tokens and sentence go in; every column comes out of the pipeline, the multiword tokens and forms are the gold file's.
                Doc predicted = Example.Blank(sentence.Doc);
                predicted.SetSentenceBoundaries(sentence.Doc.GetSentenceBoundaries());
                foreach (IPipelineComponent component in pipeline.Components.Where(c => c is ITrainableComponent))
                {
                    component.Process(predicted);
                }

                tokens += predicted.Length;
                foreach (string comment in sentence.Comments.Where(c => c.StartsWith("# sent_id", StringComparison.Ordinal)))
                {
                    sb.Append(comment).Append('\n');
                }

                var groups = sentence.MultiwordTokens.Select(m => (m.FirstWord, m.LastWord, m.Form)).ToList();
                WriteSentence(sb, predicted, 0, predicted.Length, groups, i => sentence.Forms[i]);
            }
        }

        File.WriteAllText(Required(args, "out"), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"tokens={tokens}");
        return 0;
    }

    private static int WriteRaw(Pipeline pipeline, string text, StringBuilder sb)
    {
        Doc doc = pipeline.Process(text);
        foreach (Span sentence in doc.Sentences)
        {
            WriteRawSentence(sb, doc, sentence.Start, sentence.End);
        }

        return doc.Length;
    }

    // Raw text has no multiword token lines: they are found from the pieces, two neighbouring tokens that together spell a contraction.
    private static void WriteRawSentence(StringBuilder sb, Doc doc, int start, int end)
    {
        var groups = new List<(int First, int Last, string Form)>();
        var forms = new Dictionary<int, string>();
        for (int i = start; i + 1 < end; i++)
        {
            if (doc[i].EndIdx != doc[i + 1].Idx)
            {
                continue;
            }

            string surface = doc[i].ToString() + doc[i + 1].ToString();
            if (Contractions.TryGetValue(surface.ToLowerInvariant(), out string[]? words))
            {
                groups.Add((i - start, i + 1 - start, surface));
                for (int w = 0; w < 2; w++)
                {
                    forms[i - start + w] = CopyCase(words[w], surface, w == 0);
                }

                i++;
            }
        }

        WriteSentence(sb, doc, start, end, groups, i => forms.TryGetValue(i, out string? f) ? f : doc[start + i].ToString());
    }

    // "Zum" gives "Zu dem" and "IM" gives "IN DEM"; a lower-case surface form gives the lower-case words.
    private static string CopyCase(string word, string surface, bool first)
    {
        if (surface.Length > 1 && surface.All(char.IsUpper))
        {
            return word.ToUpperInvariant();
        }

        return first && char.IsUpper(surface[0]) ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    // Writes the words start..end of a document as one CoNLL-U sentence. HEAD and DEPREL are placeholders when no parser ran (the first
    // word is the root, the rest hang off it) so that the scorer accepts the file.
    private static void WriteSentence(StringBuilder sb, Doc doc, int start, int end, List<(int First, int Last, string Form)> groups, Func<int, string> form)
    {
        bool parsed = false;
        for (int k = start; k < end && !parsed; k++)
        {
            parsed = doc[k].Dep.Length > 0;
        }

        int next = 0;
        for (int i = 0; i < end - start; i++)
        {
            if (next < groups.Count && groups[next].First == i)
            {
                sb.Append(groups[next].First + 1).Append('-').Append(groups[next].Last + 1).Append('\t').Append(groups[next].Form).Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                next++;
            }

            Token t = doc[start + i];
            string head = "1";
            string deprel = "dep";
            if (!parsed)
            {
                (head, deprel) = i == 0 ? ("0", "root") : ("1", "dep");
            }
            else if (t.HeadIndex == start + i)
            {
                (head, deprel) = ("0", "root");
            }
            else
            {
                head = (t.HeadIndex - start + 1).ToString(CultureInfo.InvariantCulture);
                deprel = Cell(t.Dep);
            }

            sb.Append(i + 1).Append('\t').Append(form(i)).Append('\t').Append(Cell(t.Lemma)).Append('\t').Append(Cell(t.Pos)).Append('\t')
                .Append(Cell(t.Tag)).Append('\t').Append(Cell(t.Morph)).Append('\t').Append(head).Append('\t').Append(deprel).Append("\t_\t_\n");
        }

        sb.Append('\n');
    }

    private static string Cell(string value) => value.Length == 0 ? "_" : value;

    // Tokens and sentences of the tokenizer and the rule sentencizer alone, for scoring Tokens, Words and Sentences.
    private static int Segment(string[] args)
    {
        using Pipeline pipeline = Pipeline.Blank(Language.Dutch).Add("sentencizer", new Dictionary<string, string> { ["language"] = "nl" });
        var sb = new StringBuilder();
        var chunk = new List<string>();
        int tokens = 0;
        foreach (ConllUSentence sentence in ConllUReader.Read(Required(args, "gold")))
        {
            chunk.Add(sentence.Doc.Text);
            if (chunk.Count == 10)
            {
                tokens += WriteRaw(pipeline, string.Join(' ', chunk), sb);
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
        {
            tokens += WriteRaw(pipeline, string.Join(' ', chunk), sb);
        }

        File.WriteAllText(Required(args, "out"), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"tokens={tokens}");
        return 0;
    }

    // Indicative throughput: raw text through the whole pipeline, in batches, after one untimed pass.
    private static int Speed(string[] args)
    {
        using Pipeline pipeline = LoadParts(Required(args, "models"), args);
        var texts = new List<string>();
        var chunk = new List<string>();
        foreach (ConllUSentence s in ConllUReader.Read(Required(args, "gold")))
        {
            chunk.Add(s.Doc.Text);
            if (chunk.Count == 10)
            {
                texts.Add(string.Join(' ', chunk));
                chunk.Clear();
            }
        }

        long tokens = 0;
        foreach (Doc d in pipeline.ProcessMany(texts, 32))
        {
            tokens += d.Length;
        }

        for (int r = 0; r < Num(args, "repeat", 3); r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            foreach (Doc d in pipeline.ProcessMany(texts, 32))
            {
                _ = d.Length;
            }

            double seconds = clock.Elapsed.TotalSeconds;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"run={r} documents={texts.Count} tokens={tokens} seconds={seconds:F2} tokens_per_second={tokens / seconds:F0} managed_bytes_per_token={(GC.GetAllocatedBytesForCurrentThread() - before) / tokens} threads={RecursiveAI.OneModel.Tensors.TensorCompute.Threads}"));
        }

        return 0;
    }
}
