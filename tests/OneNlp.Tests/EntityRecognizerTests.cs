using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Matching;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Tests;

public class EntityRecognizerTests : IDisposable
{
    private static readonly string[] Widgets = ["Zorp", "Blix", "Quan", "Mepo", "Trin"];
    private static readonly string[] Gadgets = ["Vexa", "Nolo", "Hux"];
    private static readonly string[] Fillers = ["the", "saw", "with", "and", "near", "today", "bought", "a", "red"];
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "onenlp-ner-" + Guid.NewGuid().ToString("N"));

    public EntityRecognizerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // A toy corpus with custom labels: "Grand <widget>" is one two-token WIDGET, a bare widget is a WIDGET, a gadget is a GADGET.
    // Written as JSON Lines the way a user would supply their own data (characters offsets, no tokens: the reader tokenizes).
    private string WriteCorpus(string name, int sentences, int seed)
    {
        var random = new Random(seed);
        string path = Path.Combine(_dir, name);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        for (int s = 0; s < sentences; s++)
        {
            var text = new StringBuilder();
            var entities = new List<string>();
            int words = random.Next(5, 10);
            for (int i = 0; i < words; i++)
            {
                if (text.Length > 0)
                {
                    text.Append(' ');
                }

                int start = text.Length;
                int kind = random.Next(5);
                if (kind == 0)
                {
                    text.Append("Grand ").Append(Widgets[random.Next(Widgets.Length)]);
                    entities.Add($"[{start},{text.Length},\"WIDGET\"]");
                }
                else if (kind == 1)
                {
                    text.Append(Widgets[random.Next(Widgets.Length)]);
                    entities.Add($"[{start},{text.Length},\"WIDGET\"]");
                }
                else if (kind == 2)
                {
                    text.Append(Gadgets[random.Next(Gadgets.Length)]);
                    entities.Add($"[{start},{text.Length},\"GADGET\"]");
                }
                else
                {
                    text.Append(Fillers[random.Next(Fillers.Length)]);
                }
            }

            writer.WriteLine($"{{\"text\":\"{text}\",\"entities\":[{string.Join(',', entities)}]}}");
        }

        return path;
    }

    private static Pipeline NewPipeline(NerDecoding decoding, IPipelineComponent? first = null)
    {
        var encoder = new HashEmbeddingEncoder(width: 32, buckets: 2048, seed: 3);
        Pipeline pipeline = Pipeline.Blank();
        if (first is not null)
        {
            pipeline.Add(first);
        }

        return pipeline.Add(new EntityRecognizer(encoder, decoding, context: 2, hidden: 48, maxSpanLength: 4) { OwnsEncoder = true });
    }

    private static TrainOptions Options() => new()
    {
        Epochs = 25,
        BatchWords = 150,
        LearningRate = 0.01f,
        Dropout = 0.1f,
        Seed = 5,
    };

    private static double F1(Pipeline pipeline, List<Example> gold)
    {
        var predicted = new List<Doc>();
        var reference = new List<Doc>();
        foreach (Example e in gold)
        {
            Doc doc = Example.Blank(e.Reference);
            pipeline.Process(doc);
            predicted.Add(doc);
            reference.Add(e.Reference);
        }

        return SpanScorer.EntityScore(predicted, reference).Value;
    }

    [Theory]
    [InlineData(NerDecoding.Tagging)]
    [InlineData(NerDecoding.Spans)]
    public void CustomLabelsTrainFromJsonlSaveLoadAndPredict(NerDecoding decoding)
    {
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("train.jsonl", 300, 1))];
        List<Example> test = [.. JsonlReader.Read(WriteCorpus("test.jsonl", 80, 2))];
        using Pipeline pipeline = NewPipeline(decoding);
        TrainResult result = pipeline.Train(train, Options());
        Assert.True(result.Completed);

        double f1 = F1(pipeline, test);
        Assert.True(f1 > 90, $"entity F1 {f1:F1} with {decoding}");

        string model = Path.Combine(_dir, "ner-" + decoding + ".onenlp");
        PipelineFile.Save(pipeline, model);
        using Pipeline loaded = PipelineFile.Load(model);
        var head = Assert.IsType<EntityRecognizer>(Assert.Single(loaded.Components));
        Assert.Equal(["GADGET", "WIDGET"], head.Labels);
        Assert.Equal(decoding, head.Decoding);

        // The loaded pipeline gives the same entities, labels and confidences as the one that was trained.
        foreach (Example e in test.Take(20))
        {
            Doc a = pipeline.Process(e.Reference.Text);
            Doc b = loaded.Process(e.Reference.Text);
            Assert.Equal(a.Ents.Select(x => (x.Start, x.End, x.Label, x.Confidence)), b.Ents.Select(x => (x.Start, x.End, x.Label, x.Confidence)));
        }

        Doc d = loaded.Process("Grand Zorp saw Vexa near the Hux");
        Assert.Equal(["Grand Zorp:WIDGET", "Vexa:GADGET", "Hux:GADGET"], d.Ents.Select(x => x.Text + ":" + x.Label));
        Assert.All(d.Ents, x => Assert.InRange(x.Confidence, 0.5f, 1f));
    }

    [Fact]
    public void TaggingDecodingNeverWritesAMalformedSequence()
    {
        // Untrained-ish weights give arbitrary scores; the constrained search must still produce ordered, non-empty, non-overlapping spans.
        List<Example> train = [.. JsonlReader.Read(WriteCorpus("t.jsonl", 40, 3))];
        using Pipeline pipeline = NewPipeline(NerDecoding.Tagging);
        pipeline.Train(train, new TrainOptions { Epochs = 1, BatchWords = 100, Seed = 1 });
        foreach (Example e in train)
        {
            Doc d = pipeline.Process(e.Reference.Text);
            int end = 0;
            foreach (Span s in d.Ents)
            {
                Assert.True(s.Start >= end && s.End > s.Start);
                end = s.End;
            }
        }
    }

    [Theory]
    [InlineData(NerDecoding.Tagging)]
    [InlineData(NerDecoding.Spans)]
    public void RuleEntitiesAfterTheModelFollowOverwriteEnts(NerDecoding decoding)
    {
        using Pipeline model = NewPipeline(decoding);
        model.Train([.. JsonlReader.Read(WriteCorpus("a.jsonl", 300, 1))], Options());
        var head = (EntityRecognizer)model.Components[0];

        // A fresh pipeline around the same trained head: model first, then rules.
        foreach (bool overwrite in new[] { false, true })
        {
            var ruler = new EntityRuler(overwriteEnts: overwrite);
            ruler.AddPhrase("PLACE", "Zorp");
            ruler.AddPhrase("DATE", "today");
            Pipeline pipeline = Pipeline.Blank().Add(head).Add(ruler); // the head belongs to `model`, which disposes it
            Doc d = pipeline.Process("Vexa saw Zorp today");
            string[] ents = [.. d.Ents.Select(x => x.Text + ":" + x.Label)];

            // The model does not know "today" as an entity, so the rule adds it. Zorp is a model WIDGET: kept unless the rules overwrite.
            Assert.Contains("today:DATE", ents);
            Assert.Contains("Vexa:GADGET", ents);
            Assert.Contains(overwrite ? "Zorp:PLACE" : "Zorp:WIDGET", ents);
            Assert.Equal(3, ents.Length);
        }
    }

    [Theory]
    [InlineData(NerDecoding.Tagging)]
    [InlineData(NerDecoding.Spans)]
    public void ModelPredictsAroundRuleEntitiesSetBeforeIt(NerDecoding decoding)
    {
        using Pipeline trained = NewPipeline(decoding);
        trained.Train([.. JsonlReader.Read(WriteCorpus("b.jsonl", 300, 1))], Options());
        var head = (EntityRecognizer)trained.Components[0];

        var ruler = new EntityRuler();
        ruler.AddPhrase("PLACE", "Zorp");
        ruler.AddPhrase("DATE", "today");
        Pipeline pipeline = Pipeline.Blank().Add(ruler).Add(head); // the head belongs to `trained`, which disposes it
        Doc d = pipeline.Process("Vexa saw Zorp today and Grand Quan");
        string[] ents = [.. d.Ents.Select(x => x.Text + ":" + x.Label)];

        // The rule entities stay as the rules set them (including Zorp, which the model alone would call a WIDGET); the model fills the rest.
        Assert.Equal(["Vexa:GADGET", "Zorp:PLACE", "today:DATE", "Grand Quan:WIDGET"], ents);
        Assert.Equal(1f, d.Ents[1].Confidence);
    }
}
