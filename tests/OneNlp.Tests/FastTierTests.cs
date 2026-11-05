using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Tests;

public class FastTierTests
{
    [Theory]
    [InlineData("walking", "walk")]
    [InlineData("went", "go")]
    [InlineData("studies", "study")]
    [InlineData("", "")]
    [InlineData("a", "")]
    [InlineData("", "ab")]
    [InlineData("unhappier", "happy")]
    [InlineData("n't", "not")]
    public void EditTreeReproducesItsLemma(string form, string lemma)
    {
        EditTree tree = EditTree.Build(form, lemma);
        Assert.Equal(lemma, tree.Apply(form));
        EditTree again = EditTree.Parse(tree.Serialize());
        Assert.Equal(tree.Serialize(), again.Serialize());
        Assert.Equal(lemma, again.Apply(form));
    }

    [Fact]
    public void EditTreeGeneralizesAcrossWords()
    {
        EditTree tree = EditTree.Build("walking", "walk");
        Assert.Equal("talk", tree.Apply("talking"));
        Assert.Null(tree.Apply("ab"));
        EditTree went = EditTree.Build("went", "go");
        Assert.Null(went.Apply("sent"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("M1,1(S0:0:)")]
    [InlineData("S5:ab0:")]
    [InlineData("X")]
    [InlineData("S1:a1:bS")]
    public void EditTreeRejectsMalformedText(string text) => Assert.Throws<InvalidDataException>(() => EditTree.Parse(text));

    [Fact]
    public void EditTreeRejectsDeepNesting()
    {
        string deep = string.Concat(Enumerable.Repeat("M0,0(", 200)) + "S0:0:" + string.Concat(Enumerable.Repeat(")(S0:0:)", 200));
        Assert.Throws<InvalidDataException>(() => EditTree.Parse(deep));
    }

    // A toy language with inflection: stems carry a tag, and suffixes -s / -ing change the lemma by a rule.
    private static List<Example> Toy(int sentences, int seed)
    {
        var random = new Random(seed);
        string[] stems = ["walk", "talk", "jump", "play", "look", "cook", "push", "pull", "kick", "call"];
        string[] suffixes = ["", "s", "ing", "ed"];
        var sb = new System.Text.StringBuilder();
        for (int s = 0; s < sentences; s++)
        {
            int n = random.Next(3, 8);
            for (int i = 0; i < n; i++)
            {
                string stem = stems[random.Next(stems.Length)];
                int k = random.Next(suffixes.Length);
                string cap = i == 0 ? char.ToUpperInvariant(stem[0]) + stem[1..] : stem;
                sb.Append(i + 1).Append('\t').Append(cap).Append(suffixes[k]).Append('\t').Append(stem).Append("\tVERB\tV").Append(k)
                  .Append('\t').Append(k == 1 ? "Number=Sing" : "_").Append('\t').Append(i == 0 ? 0 : 1).Append('\t').Append(i == 0 ? "root" : "dep").Append("\t_\t_\n");
            }

            sb.Append('\n');
        }

        return [.. ConllUReader.Read(new StringReader(sb.ToString())).Select(x => Example.FromReference(x.Doc))];
    }

    private static Pipeline NewPipeline()
    {
        var encoder = new FastEncoder(width: 24, depth: 2, normRows: 512, prefixRows: 64, suffixRows: 256, shapeRows: 64, seed: 3);
        return Pipeline.Blank()
            .Add(new Tagger(encoder, TokenColumn.Tag) { OwnsEncoder = true })
            .Add(new Morphologizer(encoder))
            .Add(new EditTreeLemmatizer(encoder));
    }

    [Fact]
    public void ThreeHeadsShareOneEncoderTrainSaveAndLoad()
    {
        List<Example> train = Toy(200, 1);
        List<Example> test = Toy(40, 2);
        using Pipeline pipeline = NewPipeline();
        TrainResult result = pipeline.Train(train, new TrainOptions { Epochs = 12, BatchWords = 200, LearningRate = 0.01f, Dropout = 0f, Seed = 5, DevFraction = 0.1 });
        Assert.True(result.Completed);

        (double tag, double pos, double morph, double lemma) = Score(pipeline, test);
        Assert.True(tag > 0.98 && pos > 0.98 && morph > 0.98 && lemma > 0.98, $"{tag} {pos} {morph} {lemma}");

        string path = Path.Combine(Path.GetTempPath(), "onenlp-fast-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            PipelineFile.Save(pipeline, path);
            using Pipeline loaded = PipelineFile.Load(path);
            Assert.Equal(Score(pipeline, test), Score(loaded, test));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CachedInferenceEqualsFreshEncoding()
    {
        using var encoder = new FastEncoder(width: 16, depth: 2, normRows: 64, prefixRows: 16, suffixRows: 32, shapeRows: 16, seed: 9);
        encoder.Weights!.Eval();
        List<Example> docs = Toy(6, 4);
        Doc[] batch = [.. docs.Select(e => e.Predicted)];
        using NoGradScope noGrad = GradMode.NoGrad();
        using var scope = new ActivationScope();
        EncodedBatch first = encoder.Encode(batch);
        float[] a = first.Vectors.Data<float>().ToArray();

        // A different batch composition: each document alone, then reversed. Rows must not depend on the batch.
        var parts = new List<float>();
        foreach (Doc d in batch)
        {
            parts.AddRange(encoder.Encode([d]).Vectors.Data<float>().ToArray());
        }

        Assert.Equal(a, parts.ToArray());

        // And without the cache (recording on) the values agree to float rounding.
        using var fresh = new FastEncoder(width: 16, depth: 2, normRows: 64, prefixRows: 16, suffixRows: 32, shapeRows: 16, seed: 9);
        fresh.Weights!.Train(true); // training mode skips the cache
        Doc[] other = [.. Toy(6, 4).Select(e => e.Predicted)];
        float[] b = fresh.Encode(other).Vectors.Data<float>().ToArray();

        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.True(Math.Abs(a[i] - b[i]) < 1e-5f, $"row value {i}: {a[i]} vs {b[i]}");
        }
    }

    [Fact]
    public void WindowGatherHasCorrectGradients()
    {
        // The encoder's neighbour lookup: rows gathered from [x; 0] by index. Checked against finite differences.
        var x = Tensor.FromSpan<float>([0.5f, -1f, 2f, 0.25f, 1f, -0.5f, 0.75f, 1.5f], Shape.Create(4, 2)).WithRequiresGrad();
        var zero = Tensor.Zeros(DType.F32, Shape.Create(1, 2));
        var prev = Tensor.FromSpan<int>([4, 0, 1, 4], Shape.Create(4));
        GradientCheckResult[] results = GradientCheck.Run(
            t =>
            {
                Tensor padded = Ops.Concat([t[0], zero], 0);
                Tensor window = Ops.Concat([Ops.Embedding(padded, prev), t[0]], 1);
                return Ops.Sum(Ops.Mul(window, window));
            },
            [x]);
        Assert.All(results, r => Assert.True(r.ScaledError < 1e-2, $"{r.ScaledError}"));
    }

    private static string Here([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    // Found by fuzzing pipeline files: weights with NaN values gave NaN probabilities, and Process threw on the confidence.
    [Fact]
    public void PipelineWithNaNWeightsStillPredicts()
    {
        string path = Path.Combine(Path.GetDirectoryName(Here())!, "..", "..", "testdata", "neural", "pipeline-nan-weights.bin");
        using Pipeline pipeline = PipelineFile.Load(path);
        Doc doc = pipeline.Process("Walking talks, 42 times.");
        Assert.True(doc.Length > 0);
        for (int i = 0; i < doc.Length; i++)
        {
            Assert.InRange(doc[i].Confidence, 0f, 1f);
        }
    }

    [Theory]
    [InlineData("""{"width":2048,"depth":0,"normRows":4194304,"prefixRows":1,"suffixRows":1,"shapeRows":1,"seed":0}""")]
    [InlineData("""{"width":2048,"depth":32,"normRows":5000,"prefixRows":5000,"suffixRows":5000,"shapeRows":5000,"seed":0}""")]
    public void FastEncoderRefusesHugeTables(string settings)
    {
        using var document = System.Text.Json.JsonDocument.Parse(settings);
        Assert.Throws<ArgumentOutOfRangeException>(() => FastEncoder.FromSettings(document.RootElement));
    }

    [Fact]
    public void TwoLevelOutputLayersTrainSaveAndLoad()
    {
        List<Example> train = Toy(200, 1);
        List<Example> test = Toy(40, 2);
        var encoder = new FastEncoder(width: 24, depth: 1, normRows: 512, prefixRows: 64, suffixRows: 256, shapeRows: 64, seed: 3, relu: true, neighbourRows: 256);
        using Pipeline pipeline = Pipeline.Blank()
            .Add(new Tagger(encoder, TokenColumn.Tag) { OwnsEncoder = true })
            .Add(new Morphologizer(encoder, headClasses: 1))
            .Add(new EditTreeLemmatizer(encoder, minClassCount: 2, headClasses: 3));
        pipeline.Train(train, new TrainOptions { Epochs = 14, BatchWords = 200, LearningRate = 0.01f, Dropout = 0f, Seed = 5, DevFraction = 0.1 });
        var lemmatizer = (EditTreeLemmatizer)pipeline.Components[2];
        Assert.True(lemmatizer.Labels.Count > 3, "the test needs a tail");
        (double tag, double pos, double morph, double lemma) = Score(pipeline, test);
        Assert.True(tag > 0.97 && pos > 0.97 && morph > 0.97 && lemma > 0.97, $"{tag} {pos} {morph} {lemma}");

        string path = Path.Combine(Path.GetTempPath(), "onenlp-two-level-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            PipelineFile.Save(pipeline, path);
            using Pipeline loaded = PipelineFile.Load(path);
            Assert.Equal(Score(pipeline, test), Score(loaded, test));
            Assert.Equal(lemmatizer.Labels, ((EditTreeLemmatizer)loaded.Components[2]).Labels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SharedInferenceBatchIsReusedOnlyForTheSameDocuments()
    {
        using var encoder = new FastEncoder(width: 16, depth: 1, normRows: 64, prefixRows: 16, suffixRows: 32, shapeRows: 16, seed: 9, relu: true, neighbourRows: 32, bigramRows: 32);
        encoder.Weights!.Eval();
        Doc[] docs = [.. Toy(4, 4).Select(e => e.Predicted)];
        using NoGradScope noGrad = GradMode.NoGrad();
        using var scope = new ActivationScope();
        EncodedBatch first = encoder.Encode(docs);
        Assert.Same(first, encoder.Encode([.. docs]));

        // One document alone gives the rows it has in the batch: nothing leaks across a document edge, with neighbours and word pairs too.
        float[] all = first.Vectors.Data<float>().ToArray();
        int width = encoder.Width;
        int row = 0;
        foreach (Doc d in docs)
        {
            float[] alone = encoder.Encode([d]).Vectors.Data<float>().ToArray();
            Assert.Equal(all.AsSpan(row * width, d.Length * width).ToArray(), alone);
            row += d.Length;
        }

        // After training-mode work the kept batch is out of date.
        EncodedBatch kept = encoder.Encode(docs);
        encoder.Weights.Train(true);
        _ = encoder.Encode(docs);

        encoder.Weights.Eval();
        Assert.NotSame(kept, encoder.Encode(docs));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void TableLookupInferenceMatchesTheTensorPath(bool plain, int hashes)
    {
        // The ReLU inference path folds the joining layer into the embedding tables and the window layers into one product each; it must
        // give what the tensor operations give, also after the weights change.
        using var encoder = new FastEncoder(width: 24, depth: 2, normRows: 64, prefixRows: 16, suffixRows: 32, shapeRows: 16, seed: 3, embedWidth: 12, plain: plain, hashes: hashes, relu: true, neighbourRows: 40, contextRows: 24);
        Doc[] docs = [.. Toy(5, 6).Select(e => e.Predicted)];
        for (int round = 0; round < 2; round++)
        {
            encoder.Weights!.Eval();
            float[] fast;
            using (var scope = new ActivationScope())
            using (GradMode.NoGrad())
            {
                fast = encoder.Encode(docs).Vectors.Data<float>().ToArray();
            }

            encoder.Weights.Train(true);
            float[] reference;
            using (var scope = new ActivationScope())
            {
                reference = encoder.Encode(docs).Vectors.Data<float>().ToArray();
            }

            Assert.Equal(reference.Length, fast.Length);
            for (int i = 0; i < fast.Length; i++)
            {
                Assert.True(Math.Abs(fast[i] - reference[i]) < 1e-4f, $"round {round} value {i}: {fast[i]} vs {reference[i]}");
            }

            // change a weight in place: the derived tables must follow
            Span<float> w = encoder.Weights.Parameters()[0].MutableData<float>();
            for (int i = 0; i < w.Length; i += 3)
            {
                w[i] += 0.05f;
            }
        }
    }

    private static (double Tag, double Pos, double Morph, double Lemma) Score(Pipeline pipeline, List<Example> test)
    {
        int tag = 0, pos = 0, morph = 0, lemma = 0, total = 0;
        foreach (Example e in test)
        {
            Doc d = pipeline.Process(e.Reference.Text);
            Assert.Equal(e.Reference.Length, d.Length);
            for (int i = 0; i < d.Length; i++)
            {
                total++;
                tag += d[i].Tag == e.Reference[i].Tag ? 1 : 0;
                pos += d[i].Pos == e.Reference[i].Pos ? 1 : 0;
                morph += d[i].Morph == e.Reference[i].Morph ? 1 : 0;
                lemma += d[i].Lemma == e.Reference[i].Lemma ? 1 : 0;
            }
        }

        return ((double)tag / total, (double)pos / total, (double)morph / total, (double)lemma / total);
    }
}
