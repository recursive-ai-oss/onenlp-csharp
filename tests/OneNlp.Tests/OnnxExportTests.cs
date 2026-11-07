using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Tests;

// Fine-tuned weights written back into the exported graph (TrainableTransformerEncoder.ExportOnnx). The fixtures are our random tiny encoders.
public class OnnxExportTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "onenlp-export-" + Guid.NewGuid().ToString("N"));

    public OnnxExportTests() => Directory.CreateDirectory(_work);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private static string Dir => Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");

    private static string Tokenizer => Path.Combine(Dir, "tiny-bert.tokenizer.json");

    private static string ModelOf(string family) => Path.Combine(Dir, family switch { "bert" => "tiny-bert.onnx", "modernbert" => "tiny-modernbert.onnx", _ => "tiny-student.onnx" });

    private static TrainableTransformerEncoder Load(string family, bool freeze = true, SubwordPooling pooling = SubwordPooling.Mean) =>
        TrainableTransformerEncoder.LoadOnnx(
            ModelOf(family),
            Tokenizer,
            family switch
            {
                "modernbert" => new TrainableTransformerOptions { Heads = 2, GlobalEvery = 2, LocalWindow = 8, GlobalRopeTheta = 160000f, LocalRopeTheta = 10000f, FreezeEmbeddings = freeze, MaxLength = 64, Pooling = pooling },
                "bert" => new TrainableTransformerOptions { Heads = 2, PositionOffset = 0, FreezeEmbeddings = freeze, MaxLength = 64, Pooling = pooling },
                _ => new TrainableTransformerOptions { FreezeEmbeddings = freeze, MaxLength = 64, Pooling = pooling },
            });

    // Moves every trainable weight by a deterministic amount, as fine-tuning would.
    private static void Perturb(TrainableTransformerEncoder encoder, int seed)
    {
        var random = new Random(seed);
        foreach ((string _, Tensor t) in encoder.Weights!.StateDict())
        {
            float[] values = t.Detach().Data<float>().ToArray();
            for (int i = 0; i < values.Length; i++)
            {
                values[i] += (float)((random.NextDouble() - 0.5) * 0.1);
            }

            using Tensor next = Tensor.FromSpan<float>(values, t.Shape);
            t.Detach().CopyFrom(next);
        }
    }

    private static Doc Words(string text)
    {
        var tokens = new List<TokenBoundary>();
        for (int i = 0; i < text.Length;)
        {
            if (text[i] == ' ')
            {
                i++;
                continue;
            }

            int j = i;
            while (j < text.Length && text[j] != ' ')
            {
                j++;
            }

            tokens.Add(new TokenBoundary(i, j - i));
            i = j;
        }

        return new Doc(new StringStore(), text, [.. tokens]);
    }

    private static readonly string[] Texts =
    [
        "the cat sat on unable mat",
        "dog ran",
        string.Join(' ', Enumerable.Repeat("the cat sat on a mat and dog ran", 5)),
        "unable cat \u0001 dog",
    ];

    private static float[] Vectors(IEncoder encoder)
    {
        using var scope = new ActivationScope();
        using NoGradScope noGrad = GradMode.NoGrad();
        return encoder.Encode([.. Texts.Select(Words)]).Vectors.Data<float>().ToArray();
    }

    private static double MaxDifference(float[] a, float[] b)
    {
        Assert.Equal(a.Length, b.Length);
        double max = 0;
        for (int i = 0; i < a.Length; i++)
        {
            max = Math.Max(max, Math.Abs(a[i] - b[i]));
        }

        return max;
    }

    [Theory]
    [InlineData("bert")]
    [InlineData("modernbert")]
    [InlineData("student")]
    public void TheWrittenGraphGivesTheTrainedEncodersVectors(string family)
    {
        using TrainableTransformerEncoder encoder = Load(family);
        Perturb(encoder, 11);
        encoder.Weights!.Eval();
        string file = Path.Combine(_work, family + ".onnx");
        string sha = encoder.ExportOnnx(file);
        Assert.Equal(64, sha.Length);

        float[] trained = Vectors(encoder);
        using var graph = new TransformerEncoder(file, Tokenizer, SubwordPooling.Mean, 64);
        float[] fromGraph = Vectors(graph);
        double max = MaxDifference(trained, fromGraph);
        Assert.True(max < 1e-4, $"max abs difference {max}");

        // The weights really changed: the original graph disagrees by far more.
        using var original = new TransformerEncoder(ModelOf(family), Tokenizer, SubwordPooling.Mean, 64);
        Assert.True(MaxDifference(trained, Vectors(original)) > 100 * Math.Max(max, 1e-6));

        // Read back into trainable modules, the written file gives the same weights.
        using TrainableTransformerEncoder again = TrainableTransformerEncoder.LoadOnnx(
            file,
            Tokenizer,
            family switch
            {
                "modernbert" => new TrainableTransformerOptions { Heads = 2, GlobalEvery = 2, LocalWindow = 8, GlobalRopeTheta = 160000f, LocalRopeTheta = 10000f, MaxLength = 64 },
                "bert" => new TrainableTransformerOptions { Heads = 2, PositionOffset = 0, MaxLength = 64 },
                _ => new TrainableTransformerOptions { Heads = 4, MaxLength = 64 },
            });
        again.Weights!.Eval();
        Assert.True(MaxDifference(trained, Vectors(again)) < 1e-5);
    }

    [Theory]
    [InlineData("bert")]
    [InlineData("modernbert")]
    [InlineData("student")]
    public void UntrainedWeightsWriteAGraphThatComputesWhatTheOriginalDoes(string family)
    {
        using TrainableTransformerEncoder encoder = Load(family);
        string file = Path.Combine(_work, family + "-same.onnx");
        encoder.ExportOnnx(file);
        using var written = new TransformerEncoder(file, Tokenizer, SubwordPooling.Mean, 64);
        using var original = new TransformerEncoder(ModelOf(family), Tokenizer, SubwordPooling.Mean, 64);
        Assert.True(MaxDifference(Vectors(original), Vectors(written)) < 1e-6);
    }

    [Fact]
    public void ATrainedEmbeddingTableIsWrittenToo()
    {
        using TrainableTransformerEncoder encoder = Load("bert", freeze: false);
        Perturb(encoder, 5);
        encoder.Weights!.Eval();
        string file = Path.Combine(_work, "table.onnx");
        encoder.ExportOnnx(file);
        using var graph = new TransformerEncoder(file, Tokenizer, SubwordPooling.Mean, 64);
        Assert.True(MaxDifference(Vectors(encoder), Vectors(graph)) < 1e-4);
    }

    [Fact]
    public void TheDestinationMayNotBeTheSourceAndABrokenSourceIsRejected()
    {
        using TrainableTransformerEncoder encoder = Load("bert");
        Assert.Throws<ArgumentException>(() => encoder.ExportOnnx(ModelOf("bert")));

        byte[] good = File.ReadAllBytes(ModelOf("bert"));
        var patch = new WeightPatch("none", [1], () => [0f]);
        string bad = Path.Combine(_work, "bad.onnx");
        string output = Path.Combine(_work, "out.onnx");
        byte[][] hostile = [good[..(good.Length / 2)], good[..5], [], [.. Enumerable.Repeat((byte)0xFF, 64)], [0x3A, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F]];
        foreach (byte[] content in hostile)
        {
            File.WriteAllBytes(bad, content);
            Assert.Throws<InvalidDataException>(() => OnnxWeightPatcher.Patch(bad, output, [patch]));
            Assert.False(File.Exists(output));
        }

        // A name the graph lacks.
        Assert.Throws<InvalidDataException>(() => OnnxWeightPatcher.Patch(ModelOf("bert"), output, [patch]));
    }

    [Theory]
    [InlineData("modernbert")]
    [InlineData("student")]
    public void ASavedPipelineRunsThePairedGraphAndPredictsAsBefore(string family)
    {
        using TrainableTransformerEncoder encoder = Load(family);
        using Pipeline pipeline = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos));
        pipeline.Train(ToyExamples(), new TrainOptions { Epochs = 4, BatchWords = 100, LearningRate = 0.01f, Dropout = 0f, Seed = 5, DevFraction = 0 });
        pipeline.Components.OfType<ITrainableComponent>().First().Encoder.Weights!.Eval();

        string path = Path.Combine(_work, family + ".onenlp");
        PipelineFile.Save(pipeline, path);
        Assert.True(File.Exists(Path.Combine(_work, family + ".encoder0.onnx")));
        Assert.Contains("\"kind\":\"transformer\"", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)), StringComparison.Ordinal);

        string[] before = Predict(pipeline);
        using (Pipeline loaded = PipelineFile.Load(path))
        {
            Assert.IsType<TransformerEncoder>(loaded.Components.OfType<ITrainableComponent>().First().Encoder);
            Assert.Equal(before, Predict(loaded));
        }

        // Keeping the weights in the pipeline file reloads a trainable encoder.
        string inline = Path.Combine(_work, family + "-inline.onenlp");
        PipelineFile.Save(pipeline, inline, exportEncoderGraphs: false);
        Assert.False(File.Exists(Path.Combine(_work, family + "-inline.encoder0.onnx")));
        TransformerEncoder.ModelDirectory = Dir;
        try
        {
            using Pipeline loaded = PipelineFile.Load(inline);
            Assert.IsType<TrainableTransformerEncoder>(loaded.Components.OfType<ITrainableComponent>().First().Encoder);
            Assert.Equal(before, Predict(loaded));
        }
        finally
        {
            TransformerEncoder.ModelDirectory = null;
        }

        // A pipeline saved to a stream cannot hold the graph, and loading one that needs it from a stream says so.
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        Assert.Throws<InvalidDataException>(() => PipelineFile.Load(stream));
    }

    private static List<Example> ToyExamples()
    {
        var random = new Random(1);
        (string Word, string Pos)[] lexicon = [("the", "DET"), ("cat", "NOUN"), ("dog", "NOUN"), ("sat", "VERB"), ("ran", "VERB"), ("on", "ADP")];
        var sb = new System.Text.StringBuilder();
        for (int s = 0; s < 30; s++)
        {
            int n = random.Next(3, 7);
            for (int i = 0; i < n; i++)
            {
                (string w, string pos) = lexicon[random.Next(lexicon.Length)];
                sb.Append(i + 1).Append('\t').Append(w).Append('\t').Append(w).Append('\t').Append(pos).Append("\t_\t_\t").Append(i == 0 ? 0 : 1).Append('\t').Append(i == 0 ? "root" : "dep").Append("\t_\t_\n");
            }

            sb.Append('\n');
        }

        return [.. ConllUReader.Read(new StringReader(sb.ToString())).Select(x => Example.FromReference(x.Doc))];
    }

    private static readonly string[] PredictTexts = ["the cat sat on the dog", "dog ran on the cat sat"];

    private static string[] Predict(Pipeline pipeline) =>
    [
        .. PredictTexts.Select(t =>
        {
            using var scope = new ActivationScope();
            using NoGradScope noGrad = GradMode.NoGrad();
            Doc d = pipeline.Process(t);
            return string.Join(' ', Enumerable.Range(0, d.Length).Select(i => d[i].Pos));
        }),
    ];
}
