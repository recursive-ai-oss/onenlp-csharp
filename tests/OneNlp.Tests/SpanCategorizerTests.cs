using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

public class SpanCategorizerTests : IDisposable
{
    private static readonly string[] Widgets = ["Zorp", "Blix", "Quan", "Mepo", "Trin"];
    private static readonly string[] Fillers = ["the", "saw", "with", "and", "near", "today", "bought", "a", "red"];
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "onenlp-spancat-" + Guid.NewGuid().ToString("N"));

    public SpanCategorizerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // "Grand <widget>" is a PRODUCT, and the widget inside it is a PART as well: the two gold spans nest. A bare widget is a PART.
    private string WriteCorpus(string name, int sentences, int seed)
    {
        var random = new Random(seed);
        string path = Path.Combine(_dir, name);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        for (int s = 0; s < sentences; s++)
        {
            var text = new StringBuilder();
            var spans = new List<string>();
            int words = random.Next(5, 10);
            for (int i = 0; i < words; i++)
            {
                if (text.Length > 0)
                {
                    text.Append(' ');
                }

                int kind = random.Next(4);
                if (kind == 0)
                {
                    int start = text.Length;
                    text.Append("Grand ");
                    int inner = text.Length;
                    text.Append(Widgets[random.Next(Widgets.Length)]);
                    spans.Add($"[{start},{text.Length},\"PRODUCT\"]");
                    spans.Add($"[{inner},{text.Length},\"PART\"]");
                }
                else if (kind == 1)
                {
                    int start = text.Length;
                    text.Append(Widgets[random.Next(Widgets.Length)]);
                    spans.Add($"[{start},{text.Length},\"PART\"]");
                }
                else
                {
                    text.Append(Fillers[random.Next(Fillers.Length)]);
                }
            }

            writer.WriteLine($"{{\"text\":\"{text}\",\"spans\":{{\"sc\":[{string.Join(',', spans)}]}}}}");
        }

        return path;
    }

    private static Pipeline NewPipeline(int hidden) =>
        Pipeline.Blank().Add(new SpanCategorizer(new HashEmbeddingEncoder(width: 32, buckets: 2048, seed: 3), "sc", [1, 2, 3], hidden) { OwnsEncoder = true });

    private static double F1(Pipeline pipeline, List<Example> gold)
    {
        var predicted = new List<Example>();
        foreach (Example e in gold)
        {
            Doc doc = Example.Blank(e.Reference);
            pipeline.Process(doc);
            predicted.Add(new Example(doc, e.Reference, null, e.Spans));
        }

        return DevScores.SpanGroup("sc")(predicted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(48)]
    public void OverlappingSpansTrainSaveLoadAndPredict(int hidden)
    {
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("train.jsonl", 300, 1))];
        List<Example> dev = [.. JsonlReader.Read(WriteCorpus("dev.jsonl", 50, 9))];
        List<Example> test = [.. JsonlReader.Read(WriteCorpus("test.jsonl", 80, 2))];
        using Pipeline pipeline = NewPipeline(hidden);
        TrainResult result = pipeline.Train(train, new TrainOptions
        {
            Epochs = 30,
            BatchWords = 150,
            LearningRate = 0.01f,
            Dropout = 0.1f,
            Seed = 5,
            DevExamples = dev,
            Score = DevScores.SpanGroup("sc"),
        });
        Assert.True(result.Completed);
        double f1 = F1(pipeline, test);
        Assert.True(f1 > 90, $"span F1 {f1:F1}");

        string model = Path.Combine(_dir, "spancat.onenlp");
        PipelineFile.Save(pipeline, model);
        using Pipeline loaded = PipelineFile.Load(model);
        var head = Assert.IsType<SpanCategorizer>(Assert.Single(loaded.Components));
        Assert.Equal(["PART", "PRODUCT"], head.Labels);
        Assert.Equal([1, 2, 3], head.Sizes);
        Assert.Equal("sc", head.SpansKey);
        foreach (Example e in test.Take(20))
        {
            Doc a = pipeline.Process(e.Reference.Text);
            Doc b = loaded.Process(e.Reference.Text);
            Assert.Equal(
                a.Spans["sc"].Select(x => (x.Start, x.End, x.Label, x.Confidence)),
                b.Spans["sc"].Select(x => (x.Start, x.End, x.Label, x.Confidence)));
        }

        // The nested pair is found: both the two-token PRODUCT and the PART inside it, which entities could not hold together.
        Doc d = loaded.Process("the Grand Zorp saw Blix");
        string[] spans = [.. d.Spans["sc"].Select(x => x.Text + ":" + x.Label)];
        Assert.Contains("Grand Zorp:PRODUCT", spans);
        Assert.Contains("Zorp:PART", spans);
        Assert.Contains("Blix:PART", spans);

        // Overlapping groups survive both serializations.
        Doc fromBinary = Doc.FromBytes(d.ToBytes());
        Doc fromJson = Doc.FromJson(d.ToJson());
        var expected = d.Spans["sc"].Select(x => (x.Start, x.End, x.Label, x.Confidence)).ToList();
        Assert.Equal(expected, fromBinary.Spans["sc"].Select(x => (x.Start, x.End, x.Label, x.Confidence)));
        Assert.Equal(expected, fromJson.Spans["sc"].Select(x => (x.Start, x.End, x.Label, x.Confidence)));
    }

    [Fact]
    public void SizesBoundWhatCanBeProposed()
    {
        using var encoder = new HashEmbeddingEncoder(width: 8, buckets: 64, seed: 1);
        Assert.Equal([1, 2, 3], new SpanCategorizer(encoder).Sizes);
        Assert.Equal([2, 5], new SpanCategorizer(encoder, sizes: [5, 2]).Sizes);
        Assert.Throws<ArgumentException>(() => new SpanCategorizer(encoder, sizes: [2, 2]));
        Assert.Throws<ArgumentException>(() => new SpanCategorizer(encoder, sizes: [0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpanCategorizer(encoder, threshold: 0f));
        var head = new SpanCategorizer(encoder);
        Assert.Equal(0.8f, head.Threshold);
        head.Threshold = 0.3f;
        Assert.Equal(0.3f, head.Threshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => head.Threshold = 1.5f);
    }

    [Fact]
    public void GoldSpansLongerThanTheLargestSizeAreNotTrainedOnAndNeverPredicted()
    {
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("s.jsonl", 80, 1))];
        using var encoder = new HashEmbeddingEncoder(width: 16, buckets: 256, seed: 1);
        var head = new SpanCategorizer(encoder, sizes: [1]);
        Pipeline pipeline = Pipeline.Blank().Add(head);
        pipeline.Train(train, new TrainOptions { Epochs = 3, BatchWords = 100, Seed = 1 });
        Doc d = pipeline.Process("the Grand Zorp saw Blix");
        Assert.All(d.Spans["sc"], s => Assert.Equal(1, s.Length));
    }
}
