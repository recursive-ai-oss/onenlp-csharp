using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Label extension on training, error messages, JSON offsets, default views, and ProcessMany cancellation.</summary>
public class ApiFollowUpTests
{
    private static readonly string[] Names = ["Zorp", "Blix", "Quan", "Mepo"];
    private static readonly string[] Codes = ["Alpha", "Beta", "Gamma"];
    private static readonly string[] Fillers = ["the", "saw", "with", "near", "today", "bought"];

    private static Pipeline NewPipeline(NerDecoding decoding)
    {
        var encoder = new HashEmbeddingEncoder(width: 32, buckets: 2048, seed: 3);
        return Pipeline.Blank().Add(new EntityRecognizer(encoder, decoding, context: 2, hidden: 48, maxSpanLength: 4) { OwnsEncoder = true });
    }

    private static List<Example> Examples(Pipeline nlp, string[] words, string label, int count, int seed)
    {
        var random = new Random(seed);
        var result = new List<Example>();
        for (int i = 0; i < count; i++)
        {
            string a = Fillers[random.Next(Fillers.Length)];
            string w = words[random.Next(words.Length)];
            string b = Fillers[random.Next(Fillers.Length)];
            string text = $"{a} {w} {b}";
            result.Add(Example.Create(nlp, text, [(a.Length + 1, a.Length + 1 + w.Length, label)]));
        }

        return result;
    }

    [Theory]
    [InlineData(NerDecoding.Tagging)]
    [InlineData(NerDecoding.Spans)]
    public void TrainingAPackagedRecognizerOnANewLabelAddsTheLabelAndKeepsTheOldWeights(NerDecoding decoding)
    {
        using Pipeline nlp = NewPipeline(decoding);
        var options = new TrainOptions { Epochs = 30, BatchWords = 100, LearningRate = 0.01f, Seed = 5 };
        nlp.Train(Examples(nlp, Names, "WIDGET", 300, 1), options);
        var head = (EntityRecognizer)nlp.Components[0];
        Assert.Equal(["WIDGET"], head.Labels);

        // Extending alone appends the label and leaves every earlier weight where it was.
        Dictionary<string, float[]> before = head.Weights.NamedParameters().ToDictionary(p => p.Name, p => p.Tensor.Data<float>().ToArray());
        List<Example> both = [.. Examples(nlp, Names, "WIDGET", 150, 2), .. Examples(nlp, Codes, "ORDER", 150, 3)];
        IReadOnlyList<string> added = head.ExtendLabels(both, seed: 9);
        Assert.Equal(["ORDER"], added);
        Assert.Equal(["WIDGET", "ORDER"], head.Labels);
        Assert.Empty(head.ExtendLabels(both, seed: 9));
        foreach ((string name, Tensor tensor) in head.Weights.NamedParameters())
        {
            float[] old = before[name];
            Assert.True(tensor.Data<float>().Length >= old.Length);
            Assert.True(tensor.Data<float>()[..old.Length].SequenceEqual(old), name);
        }

        // Training then learns it.
        TrainResult result = nlp.Train(both, new TrainOptions { Epochs = 30, BatchWords = 100, LearningRate = 0.01f, Seed = 5 });
        Assert.True(result.Completed);

        // Sentences shaped like the training data (a toy window encoder reads only a few neighbours).
        Assert.Contains("Gamma:ORDER", nlp.Process("the Gamma saw").Ents.Select(e => e.Text + ":" + e.Label));
        Assert.Contains("Zorp:WIDGET", nlp.Process("near Zorp with").Ents.Select(e => e.Text + ":" + e.Label));
    }

    [Fact]
    public void ATaggerRefusesLabelsItDoesNotHave()
    {
        var encoder = new HashEmbeddingEncoder(width: 16, buckets: 512, seed: 1);
        using Pipeline nlp = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos) { OwnsEncoder = true });
        Example Make(string pos) => Example.FromReference(Tagged(nlp, "a b", pos));
        nlp.Train([Make("NOUN"), Make("VERB")], new TrainOptions { Epochs = 1, Seed = 1 });
        ArgumentException ex = Assert.Throws<ArgumentException>(() => nlp.Train([Make("ADJ")], new TrainOptions { Epochs = 1, Seed = 1 }));
        Assert.Contains("ADJ", ex.Message, StringComparison.Ordinal);
    }

    private static Doc Tagged(Pipeline nlp, string text, string pos)
    {
        Doc doc = nlp.Tokenizer.Tokenize(text, nlp.Strings);
        for (int i = 0; i < doc.Length; i++)
        {
            doc.SetAnnotation(TokenColumn.Pos, i, pos);
        }

        return doc;
    }

    [Fact]
    public void ExampleCreateSaysWhenAnOffsetIsPastTheEndOfTheText()
    {
        using Pipeline nlp = Pipeline.Blank();
        const string text = "Order 12 shipped";
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => Example.Create(nlp, text, [(6, 99, "ORDER")]));
        Assert.Contains($"{text.Length} characters", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => Example.Create(nlp, text, [(-1, 5, "ORDER")]));
        ArgumentException misaligned = Assert.Throws<ArgumentException>(() => Example.Create(nlp, text, [(1, 4, "ORDER")]));
        Assert.IsNotType<ArgumentOutOfRangeException>(misaligned);
        Assert.Contains("token boundaries", misaligned.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpanAndTokenJsonUseTheCodePointOffsetsOfTheDocForm()
    {
        using Pipeline nlp = Pipeline.Blank();
        Doc doc = nlp.Process("\U0001F642\U0001F642 Acme paid.");
        int acme = Array.FindIndex([.. doc], t => t.Text == "Acme");
        doc.SetEntities([new SpanSpec(acme, acme + 1, "ORG")]);

        using JsonDocument whole = JsonDocument.Parse(doc.ToJson());
        JsonElement fromDoc = whole.RootElement.GetProperty("ents")[0];
        using JsonDocument spanJson = JsonDocument.Parse(JsonSerializer.Serialize(doc.Ents[0]));
        Assert.Equal(fromDoc.GetProperty("start").GetInt32(), spanJson.RootElement.GetProperty("start").GetInt32());
        Assert.Equal(fromDoc.GetProperty("end").GetInt32(), spanJson.RootElement.GetProperty("end").GetInt32());
        Assert.NotEqual(doc.Ents[0].StartChar, spanJson.RootElement.GetProperty("start").GetInt32());

        using JsonDocument tokenJson = JsonDocument.Parse(JsonSerializer.Serialize(doc[acme]));
        Assert.Equal(fromDoc.GetProperty("start").GetInt32(), tokenJson.RootElement.GetProperty("start").GetInt32());

        // Without astral characters nothing changes.
        Doc plain = nlp.Process("Acme paid.");
        plain.SetEntities([new SpanSpec(0, 1, "ORG")]);
        using JsonDocument plainJson = JsonDocument.Parse(JsonSerializer.Serialize(plain.Ents[0]));
        Assert.Equal(0, plainJson.RootElement.GetProperty("start").GetInt32());
        Assert.Equal(4, plainJson.RootElement.GetProperty("end").GetInt32());
    }

    [Fact]
    public void DefaultViewsThrowInvalidOperationNotNullReference()
    {
        Assert.Throws<InvalidOperationException>(() => default(Token).Text);
        Assert.Throws<InvalidOperationException>(() => default(Token).ToString());
        Assert.Throws<InvalidOperationException>(() => default(Token).Lemma);
        Assert.Throws<InvalidOperationException>(() => default(Span).Text);
        Assert.Throws<InvalidOperationException>(() => default(Span).Label);
        Assert.Throws<InvalidOperationException>(() => default(Span).ToString());
        Assert.True(default(Token) == default);
        Assert.Equal(default, default(Span));
    }

    [Fact]
    public void FromJsonErrorsDoNotMentionInternalParameterNames()
    {
        const string json = """{"text":"a b","tokens":[{"id":0,"start":0,"end":1,"head":7},{"id":1,"start":2,"end":3,"head":0,"dep":"x"}]}""";
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => Doc.FromJson(json));
        Assert.Contains("Head 7", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Parameter", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessManyStopsWhenCancelled()
    {
        using Pipeline nlp = Pipeline.Blank();
        using var cts = new CancellationTokenSource();
        IEnumerable<string> Texts()
        {
            for (int i = 0; i < 100; i++)
            {
                if (i == 3)
                {
                    cts.Cancel();
                }

                yield return "text " + i;
            }
        }

        Assert.ThrowsAny<OperationCanceledException>(() => nlp.ProcessMany(Texts(), batchSize: 2, cts.Token).ToList());
        Assert.Equal(100, nlp.ProcessMany(Texts().Take(0).Concat(Enumerable.Range(0, 100).Select(i => "t" + i))).Count());
    }

    [Fact]
    public void MissingLanguageNamesOnlyPackagesThatWouldHelp()
    {
        // Basque: no published package covers it, so there is nothing to add.
        ModelNotAvailableException none = ModelPackages.Missing(Language.FromCode("eu"), Tier.Fast);
        Assert.Empty(none.PackageIds);
        Assert.DoesNotContain("Add the NuGet package", none.Message, StringComparison.Ordinal);
        Assert.Contains("'eu'", none.Message, StringComparison.Ordinal);

        // A covered language still gets the package named.
        Assert.Empty(ModelPackages.Missing(Language.FromCode("eu"), Tier.Accurate).PackageIds);
        ModelNotAvailableException turkish = ModelPackages.Missing(Language.FromCode("tr"), Tier.Accurate);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", turkish.PackageIds);
    }
}
