using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

public class OpenEntityRecognizerTests : IDisposable
{
    private static readonly string[] Widgets = ["Zorp", "Blix", "Quan", "Mepo", "Trin"];
    private static readonly string[] Gadgets = ["Vexa", "Nolo", "Hux"];
    private static readonly string[] Places = ["Aldor", "Brenn", "Cyth"];
    private static readonly string[] Fillers = ["the", "saw", "with", "and", "near", "today", "bought", "a", "red"];
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "onenlp-openner-" + Guid.NewGuid().ToString("N"));

    public OpenEntityRecognizerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // A toy corpus: a widget (one or two tokens, "Grand <widget>"), a gadget or a place is an entity named by a plain-word type. A widget
    // is also a "thing" and so is a gadget: spans overlap across types, as a person is also an actor.
    private static List<Example> Corpus(int sentences, int seed)
    {
        var random = new Random(seed);
        var examples = new List<Example>();
        var strings = new StringStore();
        for (int s = 0; s < sentences; s++)
        {
            var words = new List<string>();
            var groups = new Dictionary<string, List<SpanSpec>> { ["widget"] = [], ["gadget"] = [], ["place"] = [], ["thing"] = [] };
            int n = random.Next(5, 10);
            for (int i = 0; i < n; i++)
            {
                int kind = random.Next(5);
                int at = words.Count;
                if (kind == 0)
                {
                    words.Add("Grand");
                    words.Add(Widgets[random.Next(Widgets.Length)]);
                    groups["widget"].Add(new SpanSpec(at, at + 2, "widget"));
                    groups["thing"].Add(new SpanSpec(at, at + 2, "thing"));
                }
                else if (kind == 1)
                {
                    words.Add(Widgets[random.Next(Widgets.Length)]);
                    groups["widget"].Add(new SpanSpec(at, at + 1, "widget"));
                    groups["thing"].Add(new SpanSpec(at, at + 1, "thing"));
                }
                else if (kind == 2)
                {
                    words.Add(Gadgets[random.Next(Gadgets.Length)]);
                    groups["gadget"].Add(new SpanSpec(at, at + 1, "gadget"));
                    groups["thing"].Add(new SpanSpec(at, at + 1, "thing"));
                }
                else if (kind == 3)
                {
                    words.Add(Places[random.Next(Places.Length)]);
                    groups["place"].Add(new SpanSpec(at, at + 1, "place"));
                }
                else
                {
                    words.Add(Fillers[random.Next(Fillers.Length)]);
                }
            }

            examples.Add(Example.FromReference(Words(strings, words), spans: groups.ToDictionary(g => g.Key, g => (IReadOnlyList<SpanSpec>)g.Value)));
        }

        return examples;
    }

    private static Doc Words(StringStore strings, List<string> words)
    {
        string text = string.Join(' ', words);
        var tokens = new List<TokenBoundary>();
        int p = 0;
        foreach (string w in words)
        {
            tokens.Add(new TokenBoundary(p, w.Length));
            p += w.Length + 1;
        }

        return new Doc(strings, text, [.. tokens]);
    }

    private static OpenEntityRecognizer NewHead() =>
        new(new HashEmbeddingEncoder(width: 32, buckets: 2048, seed: 3), context: 1, hidden: 48, embedding: 24, maxSpanLength: 4, negativeTypes: 3) { OwnsEncoder = true };

    private static TrainOptions Options(string? dir = null, int checkpointEvery = 0, IProgress<TrainProgress>? progress = null) => new()
    {
        Epochs = 20,
        BatchWords = 150,
        LearningRate = 0.01f,
        Dropout = 0.1f,
        Seed = 5,
        CheckpointDirectory = dir,
        CheckpointEvery = checkpointEvery,
        Progress = progress,
    };

    private static string[] Found(Doc d) => [.. d.Ents.Select(x => x.Text + ":" + x.Label)];

    [Fact]
    public void LearnsTypesFromNamesAndFindsOnlyTheTypesAsked()
    {
        using Pipeline pipeline = Pipeline.Blank().Add(NewHead());
        var head = (OpenEntityRecognizer)pipeline.Components[0];
        pipeline.Train(Corpus(300, 1), Options());

        // Held-out sentences: every gold span of the asked types is found, with the label as it was asked.
        int tp = 0, fp = 0, fn = 0;
        foreach (Example e in Corpus(60, 2))
        {
            Doc doc = Example.Blank(e.Reference);
            head.Recognize(doc, ["widget", "gadget", "place"]);
            var predicted = doc.Ents.Select(x => (x.Start, x.End, x.Label)).ToHashSet();
            var gold = new HashSet<(int, int, string)>();
            foreach (string t in new[] { "widget", "gadget", "place" })
            {
                foreach (SpanSpec s in e.Spans![t])
                {
                    gold.Add((s.Start, s.End, t));
                }
            }

            tp += predicted.Count(gold.Contains);
            fp += predicted.Count(p => !gold.Contains(p));
            fn += gold.Count(g => !predicted.Contains(g));
        }

        double f1 = 2.0 * tp / ((2.0 * tp) + fp + fn);
        Assert.True(f1 > 0.9, $"F1 {f1:F3} (tp {tp}, fp {fp}, fn {fn})");

        // Asking for one type reports only that type, and asking for none of the present types reports nothing.
        Doc one = pipeline.Process("Grand Zorp saw Vexa near Aldor");
        Assert.Empty(one.Ents);
        head.Recognize(one, ["gadget"]);
        Assert.Equal(["Vexa:gadget"], Found(one));
        Doc none = pipeline.Process("the saw with and");
        head.Recognize(none, ["widget", "gadget"]);
        Assert.Empty(none.Ents);

        // The component form: Types is what Process looks for.
        head.Types = ["place", "widget"];
        Assert.Equal(["Grand Zorp:widget", "Aldor:place"], Found(pipeline.Process("Grand Zorp saw Vexa near Aldor")));
    }

    [Fact]
    public void RecognizeValidatesTypes()
    {
        using Pipeline pipeline = Pipeline.Blank().Add(NewHead());
        var head = (OpenEntityRecognizer)pipeline.Components[0];
        pipeline.Train(Corpus(20, 1), new TrainOptions { Epochs = 1, Seed = 1 });
        Doc doc = pipeline.Process("Zorp");
        Assert.Throws<ArgumentException>(() => head.Recognize(doc, []));
        Assert.Throws<ArgumentException>(() => head.Recognize(doc, ["a", "a"]));
        Assert.Throws<ArgumentException>(() => head.Recognize(doc, [" "]));
        Assert.Throws<ArgumentException>(() => head.Recognize(doc, [new string('x', 201)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => head.Recognize(doc, ["a"], 1.5f));
    }

    [Fact]
    public void SavedPipelinesPredictTheSame()
    {
        using Pipeline trained = Pipeline.Blank().Add(NewHead());
        trained.Train(Corpus(150, 1), Options());
        string path = Path.Combine(_dir, "open.onenlp");
        PipelineFile.Save(trained, path);
        using Pipeline loaded = PipelineFile.Load(path);
        var a = (OpenEntityRecognizer)trained.Components[0];
        var b = Assert.IsType<OpenEntityRecognizer>(Assert.Single(loaded.Components));
        Assert.Equal((a.Context, a.Hidden, a.MatchWidth, a.MaxSpanLength, a.NegativeTypes, a.Threshold), (b.Context, b.Hidden, b.MatchWidth, b.MaxSpanLength, b.NegativeTypes, b.Threshold));
        foreach (Example e in Corpus(15, 7))
        {
            Doc x = trained.Process(e.Reference.Text);
            Doc y = loaded.Process(e.Reference.Text);
            a.Recognize(x, ["widget", "thing", "a type never seen"]);
            b.Recognize(y, ["widget", "thing", "a type never seen"]);
            Assert.Equal(x.Ents.Select(s => (s.Start, s.End, s.Label, s.Confidence)), y.Ents.Select(s => (s.Start, s.End, s.Label, s.Confidence)));
        }
    }

    [Fact]
    public void ResumingAKilledRunGivesTheUninterruptedWeights()
    {
        List<Example> train = Corpus(60, 3);
        TrainOptions Short(string dir, IProgress<TrainProgress>? progress = null) => new()
        {
            Epochs = 5, BatchWords = 120, LearningRate = 0.01f, Seed = 7, CheckpointDirectory = dir, CheckpointEvery = 3, Progress = progress,
        };

        using Pipeline reference = Pipeline.Blank().Add(NewHead());
        TrainResult whole = reference.Train(train, Short(Path.Combine(_dir, "whole")));

        using var cts = new CancellationTokenSource();
        using Pipeline first = Pipeline.Blank().Add(NewHead());
        TrainResult partial = first.Train(train, Short(Path.Combine(_dir, "killed"), new SyncProgress<TrainProgress>(p =>
        {
            if (p.Step == 8)
            {
                cts.Cancel();
            }
        })), cts.Token);
        Assert.False(partial.Completed);

        using Pipeline second = Pipeline.Blank().Add(NewHead());
        TrainResult resumed = second.Train(train, Short(Path.Combine(_dir, "killed")));
        Assert.True(resumed.Completed);
        Assert.Equal(whole.Steps, resumed.Steps);
        Assert.Equal(whole.WeightsSha256, resumed.WeightsSha256);
    }

    [Fact]
    public void LossGradientsMatchFiniteDifferences()
    {
        // The head's own weights, with the encoder and the batch fixed. Sizes are small so every element is checked.
        List<Example> batch = Corpus(4, 11);
        using var encoder = new HashEmbeddingEncoder(width: 8, buckets: 64, seed: 2);
        using var head = new OpenEntityRecognizer(encoder, context: 1, hidden: 6, embedding: 5, maxSpanLength: 3, negativeTypes: 2);
        head.Initialize(batch, seed: 4);
        Tensor[] parameters = head.Weights.Parameters();
        Doc[] docs = [.. batch.Select(e => e.Predicted)];

        // Float32 rounding limits small steps (the loss is a sum over many pairs) and the ReLU kink limits large ones, so the best of a few
        // steps decides, per parameter. A wrong backward pass is off by order one, not by a few percent.
        double[] best = new double[parameters.Length];
        Array.Fill(best, double.MaxValue);
        foreach (double step in new[] { 5e-3, 1e-2, 2e-2, 4e-2 })
        {
            GradientCheckResult[] results = GradientCheck.Run(_ => head.Loss(encoder.Encode(docs), batch), parameters, step);
            for (int i = 0; i < results.Length; i++)
            {
                best[i] = Math.Min(best[i], results[i].ScaledError);
            }
        }

        for (int i = 0; i < best.Length; i++)
        {
            Assert.True(best[i] < 0.1, $"parameter {i}: scaled error {best[i]:G3}");
        }
    }

    [Fact]
    public void TheHeadWithoutGoldRefusesToInitialize()
    {
        using var head = NewHead();
        var strings = new StringStore();
        Example plain = Example.FromReference(Words(strings, ["no", "gold"]));
        Assert.Throws<ArgumentException>(() => head.Initialize([plain], 1));
        Assert.Throws<InvalidOperationException>(() => head.Recognize(plain.Predicted, ["x"]));
    }
}
