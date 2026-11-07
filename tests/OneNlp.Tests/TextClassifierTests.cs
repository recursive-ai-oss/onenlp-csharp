using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Tests;

public class TextClassifierTests : IDisposable
{
    private static readonly string[] Animals = ["cat", "dog", "horse", "mouse"];
    private static readonly string[] Foods = ["bread", "soup", "pasta", "rice"];
    private static readonly string[] Places = ["lake", "hill", "town", "farm"];
    private static readonly string[] Fillers = ["the", "saw", "with", "and", "near", "today", "a", "big", "old"];
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "onenlp-textcat-" + Guid.NewGuid().ToString("N"));

    public TextClassifierTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // Multi-class: the topic is the group of the content words (animals, food or places). Multi-label: each group present is a label.
    private string WriteCorpus(string name, int documents, int seed, bool multi)
    {
        var random = new Random(seed);
        string path = Path.Combine(_dir, name);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        string[][] groups = [Animals, Foods, Places];
        string[] names = ["ANIMAL", "FOOD", "PLACE"];
        for (int i = 0; i < documents; i++)
        {
            var present = new bool[3];
            if (multi)
            {
                for (int g = 0; g < 3; g++)
                {
                    present[g] = random.Next(2) == 0;
                }

                if (!present.Any(x => x))
                {
                    present[random.Next(3)] = true;
                }
            }
            else
            {
                present[random.Next(3)] = true;
            }

            var words = new List<string>();
            for (int g = 0; g < 3; g++)
            {
                if (present[g])
                {
                    words.Add(groups[g][random.Next(4)]);
                    words.Add(groups[g][random.Next(4)]);
                }
            }

            for (int k = 0; k < 4; k++)
            {
                words.Add(Fillers[random.Next(Fillers.Length)]);
            }

            words = [.. words.OrderBy(_ => random.Next())];
            string cats = string.Join(',', Enumerable.Range(0, 3).Select(g => $"\"{names[g]}\":{(present[g] ? 1 : 0)}"));
            writer.WriteLine($"{{\"text\":\"{string.Join(' ', words)}\",\"cats\":{{{cats}}}}}");
        }

        return path;
    }

    private static TrainOptions Options(Func<IReadOnlyList<Example>, double> score, IReadOnlyList<Example> dev) => new()
    {
        Epochs = 25,
        BatchWords = 200,
        LearningRate = 0.01f,
        Dropout = 0.1f,
        Seed = 7,
        DevExamples = dev,
        Score = score,
    };

    private static Pipeline NewPipeline(bool multi, int hidden) =>
        Pipeline.Blank().Add(new TextClassifier(new HashEmbeddingEncoder(width: 32, buckets: 2048, seed: 3), multi, hidden) { OwnsEncoder = true });

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    public void ExclusiveClassesTrainSaveLoadAndPredict(int hidden)
    {
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("train.jsonl", 300, 1, multi: false))];
        List<Example> dev = [.. JsonlReader.Read(WriteCorpus("dev.jsonl", 60, 9, multi: false))];
        List<Example> test = [.. JsonlReader.Read(WriteCorpus("test.jsonl", 100, 2, multi: false))];
        using Pipeline pipeline = NewPipeline(multi: false, hidden);
        TrainResult result = pipeline.Train(train, Options(DevScores.Accuracy(), dev));
        Assert.True(result.Completed);

        double accuracy = Accuracy(pipeline, test);
        Assert.True(accuracy > 95, $"accuracy {accuracy:F1}");

        string model = Path.Combine(_dir, "textcat.onenlp");
        PipelineFile.Save(pipeline, model);
        using Pipeline loaded = PipelineFile.Load(model);
        var head = Assert.IsType<TextClassifier>(Assert.Single(loaded.Components));
        Assert.Equal(["ANIMAL", "FOOD", "PLACE"], head.Labels);
        Assert.False(head.MultiLabel);
        Assert.Equal(hidden, head.HiddenSize);
        foreach (Example e in test.Take(20))
        {
            Doc a = pipeline.Process(e.Reference.Text);
            Doc b = loaded.Process(e.Reference.Text);
            Assert.Equal(a.Cats.OrderBy(c => c.Key), b.Cats.OrderBy(c => c.Key));
            Assert.Equal(1f, a.Cats.Values.Sum(), 3);
        }

        Doc d = loaded.Process("the cat saw a dog near the mouse");
        Assert.Equal("ANIMAL", d.Cats.MaxBy(c => c.Value).Key);
    }

    [Fact]
    public void MultiLabelScoresAreIndependentAndRoundTripThroughTheDoc()
    {
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("mtrain.jsonl", 400, 1, multi: true))];
        List<Example> dev = [.. JsonlReader.Read(WriteCorpus("mdev.jsonl", 60, 9, multi: true))];
        List<Example> test = [.. JsonlReader.Read(WriteCorpus("mtest.jsonl", 100, 2, multi: true))];
        using Pipeline pipeline = NewPipeline(multi: true, hidden: 0);
        pipeline.Train(train, Options(DevScores.CatsMicroF1(), dev));

        var predicted = new List<Example>();
        foreach (Example e in test)
        {
            Doc doc = Example.Blank(e.Reference);
            pipeline.Process(doc);
            predicted.Add(new Example(doc, e.Reference, e.Cats));
        }

        double f1 = DevScores.CatsMicroF1()(predicted);
        Assert.True(f1 > 95, $"micro F1 {f1:F1}");

        Doc d = pipeline.Process("the cat and the bread near a lake");
        Assert.Equal(3, d.Cats.Count(c => c.Value > 0.5f));

        // The scores survive both serializations, with the order the component wrote them in.
        Doc fromBinary = Doc.FromBytes(d.ToBytes());
        Doc fromJson = Doc.FromJson(d.ToJson());
        Assert.Equal(d.Cats.Select(c => (c.Key, c.Value)), fromBinary.Cats.Select(c => (c.Key, c.Value)));
        Assert.Equal(d.Cats.Select(c => (c.Key, c.Value)), fromJson.Cats.Select(c => (c.Key, c.Value)));
    }

    [Fact]
    public void ResumedTrainingReproducesTheUninterruptedRun()
    {
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("r.jsonl", 120, 1, multi: false))];
        TrainOptions Make(string dir, IProgress<TrainProgress>? progress = null) =>
            new() { Epochs = 4, BatchWords = 200, LearningRate = 0.01f, Seed = 4, CheckpointDirectory = dir, CheckpointEvery = 2, Progress = progress };

        using Pipeline whole = NewPipeline(false, 0);
        TrainResult full = whole.Train(train, Make(Path.Combine(_dir, "whole")));

        // Stop the run part way through, then continue it from the checkpoint.
        string killed = Path.Combine(_dir, "killed");
        using var cts = new CancellationTokenSource();
        using (Pipeline first = NewPipeline(false, 0))
        {
            TrainResult partial = first.Train(train, Make(killed, new Sync(p =>
            {
                if (p.Step == 7)
                {
                    cts.Cancel();
                }
            })), cts.Token);
            Assert.False(partial.Completed);
        }

        using Pipeline second = NewPipeline(false, 0);
        TrainResult resumed = second.Train(train, Make(killed));
        Assert.True(resumed.Completed);
        Assert.Equal(full.WeightsSha256, resumed.WeightsSha256);
    }

    private sealed class Sync(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }

    [Fact]
    public void WorksOverATransformerEncoder()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");
        using TrainableTransformerEncoder encoder = TrainableTransformerEncoder.LoadOnnx(
            Path.Combine(dir, "tiny-bert.onnx"),
            Path.Combine(dir, "tiny-bert.tokenizer.json"),
            new TrainableTransformerOptions { Heads = 2, PositionOffset = 0, MaxLength = 32 });
        var head = new TextClassifier(encoder);
        Pipeline pipeline = Pipeline.Blank().Add(head);
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("t.jsonl", 40, 1, multi: false))];
        TrainResult result = pipeline.Train(train, new TrainOptions { Epochs = 2, BatchWords = 100, Seed = 1 });
        Assert.True(result.Completed);
        Doc d = pipeline.Process("the cat saw a dog");
        Assert.Equal(3, d.Cats.Count);
        Assert.Equal(1f, d.Cats.Values.Sum(), 3);
    }

    [Fact]
    public void AnEmptyDocumentGetsNoScoresAndAnUninitializedHeadRefuses()
    {
        using var encoder = new HashEmbeddingEncoder(width: 8, buckets: 64, seed: 1);
        var head = new TextClassifier(encoder);
        Assert.Throws<InvalidOperationException>(() => head.Process(Pipeline.Blank().Process("a b")));
        Assert.Throws<ArgumentException>(() => head.Initialize([Example.FromReference(Pipeline.Blank().Process("a b"))], 1));
    }

    private static double Accuracy(Pipeline pipeline, List<Example> test)
    {
        var predicted = new List<Example>();
        foreach (Example e in test)
        {
            Doc doc = Example.Blank(e.Reference);
            pipeline.Process(doc);
            predicted.Add(new Example(doc, e.Reference, e.Cats));
        }

        return DevScores.Accuracy()(predicted);
    }
}
