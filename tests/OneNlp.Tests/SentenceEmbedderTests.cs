using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Neural.Embeddings;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests;

// Uses the random 2-layer BERT-style fixture (width 16) from testdata/accurate. The expected vectors are computed here from the raw
// hidden states of the same graph, so these tests check pooling, normalization, batching, truncation and ordering, not the model.
public class SentenceEmbedderTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");

    private static SentenceEmbedder Make(PoolingMode mode, bool normalize, int maxLength = 512, string prefix = "")
    {
        Tokenizer tok = Tokenizer.FromFile(Path.Combine(Dir, "tiny-bert.tokenizer.json"));
        Session session = Session.Load(Path.Combine(Dir, "tiny-bert.onnx"));
        return new SentenceEmbedder(session, tok, new EmbeddingConfig(mode, normalize, maxLength), padId: 0, prefix);
    }

    private static float[][] Hidden(int[] ids)
    {
        using Session session = Session.Load(Path.Combine(Dir, "tiny-bert.onnx"));
        long[] x = [.. ids.Select(i => (long)i)];
        long[] m = [.. ids.Select(_ => 1L)];
        using Tensor a = Tensor.FromSpan<long>(x, Shape.Create(1, ids.Length));
        using Tensor b = Tensor.FromSpan<long>(m, Shape.Create(1, ids.Length));
        using SessionOutputs o = session.Run(new Dictionary<string, Tensor> { ["input_ids"] = a, ["attention_mask"] = b }, [session.Outputs[0].Name]);
        float[] h = o[session.Outputs[0].Name].Data<float>().ToArray();
        int w = h.Length / ids.Length;
        return [.. Enumerable.Range(0, ids.Length).Select(t => h.AsSpan(t * w, w).ToArray())];
    }

    private static float[] Expected(float[][] h, PoolingMode mode, bool normalize)
    {
        int w = h[0].Length;
        var v = new float[w];
        for (int j = 0; j < w; j++)
        {
            v[j] = mode switch
            {
                PoolingMode.Cls => h[0][j],
                PoolingMode.Max => h.Max(r => r[j]),
                PoolingMode.MeanSqrtLength => h.Sum(r => r[j]) / MathF.Sqrt(h.Length),
                _ => h.Sum(r => r[j]) / h.Length,
            };
        }

        if (normalize)
        {
            float n = MathF.Sqrt(v.Sum(x => x * x));
            for (int j = 0; j < w; j++)
            {
                v[j] /= n;
            }
        }

        return v;
    }

    private static void Close(float[] expected, float[] actual, float tol = 1e-5f)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.InRange(MathF.Abs(expected[i] - actual[i]), 0, tol);
        }
    }

    [Theory]
    [InlineData(PoolingMode.Cls, false)]
    [InlineData(PoolingMode.Cls, true)]
    [InlineData(PoolingMode.Mean, true)]
    [InlineData(PoolingMode.Max, false)]
    [InlineData(PoolingMode.MeanSqrtLength, false)]
    public void PoolsTheEncoderOutput(PoolingMode mode, bool normalize)
    {
        using SentenceEmbedder e = Make(mode, normalize);
        int[] ids = e.TokenIds("hello world 42");
        Assert.Equal(2, ids[0]);
        Assert.Equal(3, ids[^1]);
        Close(Expected(Hidden(ids), mode, normalize), e.Embed("hello world 42"));
        Assert.Equal(16, e.Dimensions);
    }

    [Fact]
    public void BatchMatchesSinglesAndKeepsOrder()
    {
        using SentenceEmbedder e = Make(PoolingMode.Mean, true);
        e.BatchSize = 3;
        string[] texts = ["a", "the quick brown fox jumps over the lazy dog", "", "b c", "zebra", "one two three four five six seven", "x"];
        float[][] batch = e.EmbedBatch(texts);
        Assert.Equal(texts.Length, batch.Length);
        for (int i = 0; i < texts.Length; i++)
        {
            Close(e.Embed(texts[i]), batch[i], 1e-4f);
        }

        float[][] many = [.. e.EmbedMany(texts)];
        for (int i = 0; i < texts.Length; i++)
        {
            Close(batch[i], many[i], 1e-5f);
        }
    }

    [Fact]
    public void TruncatesToTheMaximumLengthKeepingTheClosingToken()
    {
        using SentenceEmbedder e = Make(PoolingMode.Mean, false, maxLength: 6);
        int[] ids = e.TokenIds("a b c d e f g h i j");
        Assert.Equal(6, ids.Length);
        Assert.Equal(2, ids[0]);
        Assert.Equal(3, ids[^1]);
        Close(Expected(Hidden(ids), PoolingMode.Mean, false), e.Embed("a b c d e f g h i j"));
    }

    [Fact]
    public void PrefixIsPartOfTheInput()
    {
        using SentenceEmbedder withPrefix = Make(PoolingMode.Mean, false, prefix: "q ");
        using SentenceEmbedder plain = Make(PoolingMode.Mean, false);
        Assert.Equal(plain.TokenIds("q hello"), withPrefix.TokenIds("hello"));
    }

    [Fact]
    public void EmptyInputsAndBadArguments()
    {
        using SentenceEmbedder e = Make(PoolingMode.Mean, true);
        Assert.Empty(e.EmbedBatch([]));
        Assert.Equal(e.Dimensions, e.Embed("").Length);
        Assert.Throws<ArgumentNullException>(() => e.Embed(null!));
        Assert.Throws<ArgumentNullException>(() => e.EmbedBatch(["a", null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() => e.BatchSize = 0);
    }

    [Fact]
    public void NormalizedVectorsHaveUnitLength()
    {
        using SentenceEmbedder e = Make(PoolingMode.Cls, true);
        foreach (float[] v in e.EmbedBatch(["abc", "def ghi", ""]))
        {
            Assert.InRange(v.Sum(x => x * x), 0.9999f, 1.0001f);
        }
    }

    [Fact]
    public void MissingPackageErrorNamesThePackage()
    {
        if (EmbedderPackages.IsRegistered)
        {
            return;
        }

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Nlp.Embed("x"));
        Assert.Contains("RecursiveAI.OneNlp.Models.Embeddings", ex.Message);
        Assert.Throws<InvalidOperationException>(() => Nlp.EmbedMany(["x"]));
    }

    [Fact]
    public void ReadsSentenceTransformersConfiguration()
    {
        const string modules = """[{"idx":0,"name":"0","path":"","type":"sentence_transformers.models.Transformer"},{"idx":1,"name":"1","path":"1_Pooling","type":"sentence_transformers.models.Pooling"},{"idx":2,"name":"2","path":"2_Normalize","type":"sentence_transformers.models.Normalize"}]""";
        const string cls = """{"word_embedding_dimension":384,"pooling_mode_cls_token":true,"pooling_mode_mean_tokens":false,"pooling_mode_max_tokens":false,"pooling_mode_mean_sqrt_len_tokens":false}""";
        EmbeddingConfig c = EmbeddingConfig.Parse(modules, cls, """{"max_seq_length":256,"do_lower_case":true}""");
        Assert.Equal(new EmbeddingConfig(PoolingMode.Cls, true, 256), c);
        EmbeddingConfig bare = EmbeddingConfig.Parse(null, null, null);
        Assert.Equal(new EmbeddingConfig(PoolingMode.Mean, false, 512), bare);
    }

    [Theory]
    [InlineData("""[{"type":"sentence_transformers.models.Dense"}]""", null, null)]
    [InlineData("""{"not":"an array"}""", null, null)]
    [InlineData("""[{"nope":1}]""", null, null)]
    [InlineData("not json", null, null)]
    [InlineData(null, """{"pooling_mode_cls_token":true,"pooling_mode_mean_tokens":true}""", null)]
    [InlineData(null, """{"pooling_mode_cls_token":false}""", null)]
    [InlineData(null, null, """{"max_seq_length":1}""")]
    [InlineData(null, null, """{"max_seq_length":99999999999}""")]
    public void RejectsUnsupportedOrDamagedConfigurations(string? modules, string? pooling, string? bert)
    {
        Exception? ex = Record.Exception(() => EmbeddingConfig.Parse(modules, pooling, bert));
        Assert.True(ex is InvalidDataException or NotSupportedException, ex?.GetType().Name ?? "no exception");
    }

    [Fact]
    public void RefusesAPoolingPathOutsideTheModelDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "onenlp-emb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "modules.json"), """[{"type":"x.Pooling","path":"../evil"}]""");
            Assert.Throws<InvalidDataException>(() => EmbeddingConfig.Load(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
