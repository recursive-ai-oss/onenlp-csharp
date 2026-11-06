using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Tests;

// The fixtures are random tiny encoders exported from PyTorch (tools/accurate/make_tiny_fixture.py and make_tiny_modernbert_fixture.py),
// with PyTorch's hidden states for fixed ids; nothing here is trained or taken from a released model.
public class TrainableTransformerTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");

    private static string Tokenizer => Path.Combine(Dir, "tiny-bert.tokenizer.json");

    private static TrainableTransformerOptions ModernOptions(bool freeze = true, int maxLength = 64, SubwordPooling pooling = SubwordPooling.Mean) => new()
    {
        Heads = 2,
        GlobalEvery = 2,
        LocalWindow = 8,
        GlobalRopeTheta = 160000f,
        LocalRopeTheta = 10000f,
        FreezeEmbeddings = freeze,
        MaxLength = maxLength,
        Pooling = pooling,
    };

    private static TrainableTransformerEncoder Modern(TrainableTransformerOptions? o = null) =>
        TrainableTransformerEncoder.LoadOnnx(Path.Combine(Dir, "tiny-modernbert.onnx"), Tokenizer, o ?? ModernOptions());

    private static TrainableTransformerEncoder Bert(TrainableTransformerOptions? o = null) =>
        TrainableTransformerEncoder.LoadOnnx(
            Path.Combine(Dir, "tiny-bert.onnx"),
            Tokenizer,
            o ?? new TrainableTransformerOptions { Heads = 2, PositionOffset = 0, MaxLength = 64 });

    private static (long[] Ids, int[] Lengths, float[][][] Hidden, int Rows, int Length) Reference(string name)
    {
        using JsonDocument reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, name)));
        JsonElement ids = reference.RootElement.GetProperty("input_ids");
        JsonElement mask = reference.RootElement.GetProperty("attention_mask");
        int rows = ids.GetArrayLength(), length = ids[0].GetArrayLength();
        long[] x = [.. ids.EnumerateArray().SelectMany(r => r.EnumerateArray().Select(v => v.GetInt64()))];
        int[] lengths = [.. mask.EnumerateArray().Select(r => r.EnumerateArray().Sum(v => v.GetInt32()))];
        float[][][] hidden = [.. reference.RootElement.GetProperty("hidden").EnumerateArray().Select(r => r.EnumerateArray().Select(t => t.EnumerateArray().Select(v => v.GetSingle()).ToArray()).ToArray())];
        return (x, lengths, hidden, rows, length);
    }

    private static double MaxDifference(NnModule module, string referenceFile)
    {
        (long[] x, int[] lengths, float[][][] hidden, int rows, int length) = Reference(referenceFile);
        using var scope = new ActivationScope();
        using NoGradScope noGrad = GradMode.NoGrad();
        using Tensor ids = Tensor.FromSpan<long>(x, Shape.Create(rows, length));
        Tensor h = module switch
        {
            ModernBertModule m => m.Forward(ids, lengths),
            PostNormModule m => m.Forward(ids, lengths),
            _ => throw new InvalidOperationException(),
        };
        ReadOnlySpan<float> got = h.Data<float>();
        int width = hidden[0][0].Length;
        double max = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int t = 0; t < lengths[r]; t++)
            {
                for (int j = 0; j < width; j++)
                {
                    max = Math.Max(max, Math.Abs(got[(((r * length) + t) * width) + j] - hidden[r][t][j]));
                }
            }
        }

        return max;
    }

    [Fact]
    public void TheModernBertModuleReproducesPyTorchWithPaddingAndLocalWindows()
    {
        using TrainableTransformerEncoder encoder = Modern();
        Assert.Equal(TransformerFamily.ModernBert, encoder.Family);
        encoder.Weights!.Eval();
        double max = MaxDifference(encoder.Weights, "tiny-modernbert.reference.json");
        Assert.True(max < 2e-5, $"max abs difference {max}");
    }

    [Fact]
    public void TheBertModuleReproducesPyTorch()
    {
        using TrainableTransformerEncoder encoder = Bert();
        Assert.Equal(TransformerFamily.PostNorm, encoder.Family);
        encoder.Weights!.Eval();
        double max = MaxDifference(encoder.Weights, "tiny-bert.reference.json");
        Assert.True(max < 2e-5, $"max abs difference {max}");
    }

    [Fact]
    public void AGraphWithTwoRotaryBasesNeedsThemGivenAndAnUnknownGraphIsRejected()
    {
        var o = new TrainableTransformerOptions { Heads = 2, GlobalEvery = 2, LocalWindow = 8 };
        InvalidDataException e = Assert.Throws<InvalidDataException>(() => Modern(o));
        Assert.Contains("rotary", e.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => TrainableTransformerEncoder.LoadOnnx(Path.Combine(Dir, "tiny-bert.onnx"), Tokenizer, new TrainableTransformerOptions { Family = TransformerFamily.ModernBert }));
    }

    [Fact]
    public void ThePaddedBatchOfABertModuleGivesTheRowsOfEachSequenceAlone()
    {
        using TrainableTransformerEncoder encoder = Bert();
        var module = (PostNormModule)encoder.Weights!;
        module.Eval();
        using var scope = new ActivationScope();
        using NoGradScope noGrad = GradMode.NoGrad();
        long[] a = [2, 9, 10, 11, 12, 13, 3];
        long[] b = [2, 20, 21, 3];
        using Tensor both = Tensor.FromSpan<long>([.. a, .. b, 0, 0, 0], Shape.Create(2, 7));
        float[] batched = module.Forward(both, [7, 4]).Data<float>().ToArray();
        using Tensor alone = Tensor.FromSpan<long>(b, Shape.Create(1, 4));
        float[] single = module.Forward(alone).Data<float>().ToArray();
        for (int i = 0; i < 4 * 16; i++)
        {
            Assert.Equal(single[i], batched[(7 * 16) + i], 1e-5f);
        }
    }

    private static Doc Words(string text)
    {
        var tokens = new List<TokenBoundary>();
        int i = 0;
        while (i < text.Length)
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

    [Theory]
    [InlineData(SubwordPooling.Mean, 64)]
    [InlineData(SubwordPooling.FirstPiece, 64)]
    [InlineData(SubwordPooling.Mean, 12)]
    public void TheTrainableEncoderGivesTheSameTokenVectorsAsTheFrozenOnnxEncoder(SubwordPooling pooling, int maxLength)
    {
        string[] texts = ["the cat sat on unable mat", "dog ran", string.Join(' ', Enumerable.Repeat("the cat sat on a mat and dog ran", 4)), "unable cat \u0001 dog"];
        Doc[] docs = [.. texts.Select(Words)];
        using TrainableTransformerEncoder mine = Bert(new TrainableTransformerOptions { Heads = 2, PositionOffset = 0, MaxLength = maxLength, Pooling = pooling });
        mine.Weights!.Eval();
        using var frozen = new TransformerEncoder(Path.Combine(Dir, "tiny-bert.onnx"), Tokenizer, pooling, maxLength);
        using var scope = new ActivationScope();
        using NoGradScope noGrad = GradMode.NoGrad();
        EncodedBatch got = mine.Encode(docs);
        EncodedBatch expected = frozen.Encode(docs);
        Assert.Equal(expected.TokenCount, got.TokenCount);
        float[] a = got.Vectors.Data<float>().ToArray();
        float[] b = expected.Vectors.Data<float>().ToArray();
        for (int i = 0; i < a.Length; i++)
        {
            Assert.True(Math.Abs(a[i] - b[i]) < 1e-4f, $"element {i}: {a[i]} vs {b[i]}");
        }
    }

    [Fact]
    public void GradientsMatchFiniteDifferencesThroughRotaryGatedFeedForwardAndLocalWindows()
    {
        var config = new ModernBertConfig { VocabularySize = 20, Width = 8, Layers = 3, Heads = 2, FeedForwardWidth = 6, GlobalEvery = 2, LocalWindow = 4, GlobalRopeTheta = 100f, LocalRopeTheta = 20f };
        using var module = new ModernBertModule(config, seed: 3);
        long[] x = [1, 5, 7, 2, 9, 4, 11, 3, 6, 8, 2, 1, 4, 4, 0, 0];
        int[] lengths = [8, 6];
        var named = module.NamedParameters().ToDictionary(p => p.Name, p => p.Tensor);
        string[] checkedNames = ["tokens.weight", "layers0.wqkv.weight", "layers1.wi.weight", "layers1.attn_norm.weight", "layers2.mlp_wo.weight", "final_norm.weight"];
        Tensor[] inputs = [.. checkedNames.Select(n => named[n])];
        GradientCheckResult[] results = GradientCheck.Run(
            _ =>
            {
                Tensor ids = Tensor.FromSpan<long>(x, Shape.Create(2, 8));
                ActivationScope.Track(ids);
                return module.Forward(ids, lengths);
            },
            inputs,
            step: 1e-2);
        for (int i = 0; i < results.Length; i++)
        {
            Assert.True(results[i].ScaledError < 0.03, $"{checkedNames[i]}: scaled error {results[i].ScaledError} over {results[i].Elements} elements");
        }
    }

    private static List<Example> Toy(int sentences, int seed)
    {
        var random = new Random(seed);
        (string Word, string Pos)[] lexicon = [("the", "DET"), ("a", "DET"), ("cat", "NOUN"), ("dog", "NOUN"), ("mat", "NOUN"), ("sat", "VERB"), ("ran", "VERB"), ("on", "ADP"), ("of", "ADP")];
        var sb = new System.Text.StringBuilder();
        for (int s = 0; s < sentences; s++)
        {
            int n = random.Next(3, 8);
            for (int i = 0; i < n; i++)
            {
                (string w, string pos) = lexicon[random.Next(lexicon.Length)];
                sb.Append(i + 1).Append('\t').Append(w).Append('\t').Append(w).Append('\t').Append(pos).Append("\t_\t_\t")
                  .Append(i == 0 ? 0 : 1).Append('\t').Append(i == 0 ? "root" : "dep").Append("\t_\t_\n");
            }

            sb.Append('\n');
        }

        return [.. ConllUReader.Read(new StringReader(sb.ToString())).Select(x => Example.FromReference(x.Doc))];
    }

    private static double Accuracy(Pipeline pipeline, List<Example> examples)
    {
        int right = 0, total = 0;
        foreach (Example e in examples)
        {
            Doc d = Example.Blank(e.Reference);
            foreach (IPipelineComponent c in pipeline.Components.Where(c => c is ITrainableComponent))
            {
                c.Process(d);
            }

            for (int i = 0; i < d.Length; i++, total++)
            {
                right += d[i].Pos == e.Reference[i].Pos ? 1 : 0;
            }
        }

        return (double)right / total;
    }

    [Theory]
    [InlineData("modernbert")]
    [InlineData("bert")]
    public void TheEncoderFineTunesThroughTrainAndTheSavedPipelineReloadsIt(string family)
    {
        List<Example> train = Toy(120, 1);
        List<Example> test = Toy(30, 2);
        using TrainableTransformerEncoder encoder = family == "bert" ? Bert() : Modern();
        float[] before = [.. encoder.Weights!.Parameters()[0].Data<float>()];
        using Pipeline pipeline = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos));
        TrainResult result = pipeline.Train(train, new TrainOptions { Epochs = 30, BatchWords = 200, LearningRate = 0.01f, Dropout = 0f, Seed = 5, DevFraction = 0.1 });
        Assert.True(result.Completed);
        Assert.NotEqual(before, encoder.Weights.Parameters()[0].Data<float>().ToArray()); // the encoder's own weights moved
        double accuracy = Accuracy(pipeline, test);
        Assert.True(accuracy > 0.95, $"UPOS {accuracy}");

        string path = Path.Combine(Path.GetTempPath(), "onenlp-trainable-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            PipelineFile.Save(pipeline, path);
            TransformerEncoder.ModelDirectory = Dir;
            try
            {
                using Pipeline loaded = PipelineFile.Load(path);
                Assert.Equal(accuracy, Accuracy(loaded, test));
            }
            finally
            {
                TransformerEncoder.ModelDirectory = null;
            }
        }
        finally
        {
            File.Delete(path);
            File.Delete(Path.ChangeExtension(path, null) + ".encoder0.onnx");
        }
    }

    private static float[] Fingerprint(TrainableTransformerEncoder encoder) =>
        [.. encoder.Weights!.NamedParameters().SelectMany(p => p.Tensor.Data<float>().ToArray())];

    [Fact]
    public void ResumedFineTuningReproducesAnUninterruptedRunBitForBit()
    {
        List<Example> train = Toy(40, 3);
        string dir = Path.Combine(Path.GetTempPath(), "onenlp-resume-" + Guid.NewGuid().ToString("N"));
        try
        {
            TrainOptions Options(string? checkpoints, IProgress<TrainProgress>? progress = null) => new()
            {
                Epochs = 10,
                MaxSteps = 8,
                BatchWords = 40,
                LearningRate = 0.01f,
                Dropout = 0.1f,
                Seed = 9,
                DevFraction = 0,
                CheckpointDirectory = checkpoints,
                CheckpointEvery = 1,
                Progress = progress,
            };

            float[] whole;
            using (TrainableTransformerEncoder encoder = Modern())
            using (Pipeline pipeline = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos)))
            {
                Assert.True(pipeline.Train(train, Options(null)).Completed);
                whole = Fingerprint(encoder);
            }

            using (var cts = new CancellationTokenSource())
            {
                using TrainableTransformerEncoder encoder = Modern();
                using Pipeline pipeline = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos));
                Assert.False(pipeline.Train(train, Options(dir, new Immediate(p => { if (p.Step >= 3) { cts.Cancel(); } })), cts.Token).Completed);
            }

            float[] resumed;
            using (TrainableTransformerEncoder encoder = Modern())
            using (Pipeline pipeline = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos)))
            {
                Assert.True(pipeline.Train(train, Options(dir)).Completed);
                resumed = Fingerprint(encoder);
            }

            Assert.Equal(whole, resumed);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // Progress<T> posts to the thread pool; this one calls back on the training thread so the cancel lands before the next step.
    private sealed class Immediate(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }

    [Fact]
    public void ATrainableEmbeddingTableCannotBeSavedButTrainsAndTheFrozenOneStaysOutOfTheState()
    {
        using TrainableTransformerEncoder frozen = Modern();
        using TrainableTransformerEncoder open = Modern(ModernOptions(freeze: false));
        Assert.DoesNotContain(frozen.Weights!.NamedParameters(), p => p.Name == "tokens.weight");
        Assert.Contains(open.Weights!.NamedParameters(), p => p.Name == "tokens.weight");
        using var stream = new MemoryStream();
        using var w = new Utf8JsonWriter(stream);
        w.WriteStartObject();
        Assert.Throws<NotSupportedException>(() => open.WriteSettings(w));
        frozen.WriteSettings(w);
    }

    [Fact]
    public void TheEncoderKindIsRegisteredAndRebuildsFromItsSettings()
    {
        using TrainableTransformerEncoder encoder = Modern(ModernOptions(maxLength: 48, pooling: SubwordPooling.FirstPiece));
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            encoder.WriteSettings(w);
            w.WriteEndObject();
        }

        TransformerEncoder.ModelDirectory = Dir;
        try
        {
            using JsonDocument settings = JsonDocument.Parse(stream.ToArray());
            Assert.Equal("modernbert", settings.RootElement.GetProperty("family").GetString());
            using IEncoder again = EncoderRegistry.Create("transformer-trainable", settings.RootElement);
            var rebuilt = Assert.IsType<TrainableTransformerEncoder>(again);
            Assert.Equal(48, rebuilt.MaxLength);
            Assert.Equal(SubwordPooling.FirstPiece, rebuilt.Pooling);
            Assert.Equal(16, rebuilt.Width);
        }
        finally
        {
            TransformerEncoder.ModelDirectory = null;
        }
    }
}
