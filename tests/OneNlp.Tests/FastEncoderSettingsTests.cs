using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

public class FastEncoderSettingsTests
{
    private static string Settings(IEncoder encoder)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            encoder.WriteSettings(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static IEncoder Create(string json) => EncoderRegistry.Create("fast", JsonDocument.Parse(json).RootElement);

    [Fact]
    public void FirstVersionSettingsLoadAndSaveUnchanged()
    {
        const string V1 = "{\"width\":96,\"depth\":4,\"normRows\":20000,\"prefixRows\":2000,\"suffixRows\":5000,\"shapeRows\":2500,\"seed\":1}";
        using IEncoder encoder = Create(V1);
        Assert.Equal(V1, Settings(encoder));
        var fast = Assert.IsType<FastEncoder>(encoder);
        Assert.Equal(1, fast.PrefixChars);
        Assert.Equal(3, fast.SuffixChars);
        Assert.Equal(96, fast.EmbedWidth);
        Assert.Equal(2, fast.Hashes);
        Assert.False(fast.Plain);
    }

    [Fact]
    public void ReluNeighbourAndWordPairSettingsRoundTrip()
    {
        using var encoder = new FastEncoder(width: 16, depth: 1, normRows: 64, prefixRows: 16, suffixRows: 32, shapeRows: 16, seed: 4, relu: true, neighbourRows: 48, bigramRows: 24);
        string json = Settings(encoder);
        Assert.Contains("\"relu\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"neighbourRows\":48", json, StringComparison.Ordinal);
        Assert.Contains("\"bigramRows\":24", json, StringComparison.Ordinal);
        using IEncoder again = Create(json);
        Assert.Equal(json, Settings(again));
        var fast = Assert.IsType<FastEncoder>(again);
        Assert.True(fast.Relu);
        Assert.Equal(48, fast.NeighbourRows);
        Assert.Equal(24, fast.BigramRows);
        int[] expectedRows = [64, 16, 32, 16, 48, 48, 48, 48, 24, 24];
        Assert.Equal(expectedRows, fast.TableRows);
    }

    [Fact]
    public void TheEnglishConfigurationRoundTripsAndEncodesTheSame()
    {
        using FastEncoder english = FastEncoder.English(5);
        string json = Settings(english);
        using IEncoder again = Create(json);
        Assert.Equal(json, Settings(again));

        Pipeline pipeline = Pipeline.Blank(Language.English);
        Doc a = pipeline.Process("Ada Lovelace met Bob in Paris .");
        Doc b = pipeline.Process("Ada Lovelace met Bob in Paris .");
        english.Weights!.Eval();
        again.Weights!.Eval();
        using var scope = new ActivationScope();
        float[] x = english.Encode([a]).Vectors.Data<float>().ToArray();
        float[] y = again.Encode([b]).Vectors.Data<float>().ToArray();
        Assert.Equal(x, y);
        Assert.Equal(256, english.Width);
    }

    [Theory]
    [InlineData("\"hashes\":0")]
    [InlineData("\"hashes\":5")]
    [InlineData("\"prefixLength\":0")]
    [InlineData("\"suffixLength\":9")]
    [InlineData("\"casedRows\":-1")]
    [InlineData("\"embedWidth\":7")]
    [InlineData("\"plain\":2")]
    [InlineData("\"turkicCase\":2")]
    [InlineData("\"hashes\":\"two\"")]
    public void OutOfRangeOptionalSettingsAreRejected(string extra)
    {
        string json = "{\"width\":16,\"depth\":1,\"normRows\":100,\"prefixRows\":100,\"suffixRows\":100,\"shapeRows\":100,\"seed\":0," + extra + "}";
        Assert.Throws<InvalidDataException>(() => Create(json));
    }

    [Fact]
    public void TurkishCaseIsRecordedOnlyWhenOn()
    {
        // Files of other languages must not change: the key is absent unless the setting is on.
        using FastEncoder plain = FastEncoder.English(3);
        Assert.False(plain.TurkicCase);
        Assert.DoesNotContain("turkicCase", Settings(plain), StringComparison.Ordinal);

        using var turkish = new FastEncoder(width: 256, depth: 2, normRows: 32768, prefixRows: 32768, suffixRows: 32768, shapeRows: 32768, seed: 3, prefixLength: 3, suffixLength: 3, casedRows: 0, embedWidth: 64, plain: false, hashes: 1, turkicCase: true);
        string json = Settings(turkish);
        Assert.EndsWith(",\"turkicCase\":1}", json, StringComparison.Ordinal);
        using IEncoder again = Create(json);
        Assert.True(Assert.IsType<FastEncoder>(again).TurkicCase);
        Assert.Equal(json, Settings(again));
    }

    [Fact]
    public void TableSizeLimitCountsTheEmbeddingWidthAndTheCasedTable()
    {
        string json = "{\"width\":16,\"depth\":1,\"normRows\":4194304,\"prefixRows\":4194304,\"suffixRows\":4194304,\"shapeRows\":4194304,\"seed\":0,\"embedWidth\":16,\"casedRows\":4194304}";
        Assert.ThrowsAny<ArgumentException>(() => Create(json));
    }

    [Fact]
    public void RecurrentLayersRoundTripAndAFirstVersionFileHasNone()
    {
        using var recurrent = new FastEncoder(width: 16, depth: 1, normRows: 100, prefixRows: 100, suffixRows: 100, shapeRows: 100, seed: 3, recurrentLayers: 2);
        string json = Settings(recurrent);
        Assert.Contains("\"recurrentLayers\":2", json, StringComparison.Ordinal);
        using IEncoder again = Create(json);
        Assert.Equal(json, Settings(again));
        Assert.Equal(2, Assert.IsType<FastEncoder>(again).RecurrentLayers);
        using FastEncoder plain = FastEncoder.English(1);
        Assert.Equal(0, plain.RecurrentLayers);
        Assert.DoesNotContain("recurrentLayers", Settings(plain), StringComparison.Ordinal);
    }

    [Fact]
    public void RecurrentLayersNeedAnEvenWidthAndStayInRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastEncoder(width: 15, depth: 1, normRows: 10, prefixRows: 10, suffixRows: 10, shapeRows: 10, recurrentLayers: 1));
        Assert.Throws<InvalidDataException>(() => Create("{\"width\":16,\"depth\":1,\"normRows\":100,\"prefixRows\":100,\"suffixRows\":100,\"shapeRows\":100,\"seed\":0,\"recurrentLayers\":5}"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADocumentEncodesTheSameAloneOrBatchedWithOthersUnderRecurrence(bool relu)
    {
        // relu: true takes the table-lookup inference path, false the graph.
        using var encoder = new FastEncoder(width: 16, depth: 1, normRows: 100, prefixRows: 100, suffixRows: 100, shapeRows: 100, seed: 4, relu: relu, recurrentLayers: 1, recurrentSize: 6);
        encoder.Weights!.Eval();
        Pipeline pipeline = Pipeline.Blank(Language.English);
        Doc a = pipeline.Process("Ada Lovelace met Bob in Paris .");
        Doc b = pipeline.Process("Short one");
        Doc c = pipeline.Process("Ada Lovelace met Bob in Paris .");
        using var scope = new ActivationScope();
        float[] alone = encoder.Encode([a]).Vectors.Data<float>().ToArray();
        float[] batched = encoder.Encode([b, c]).Vectors.Data<float>().Slice(b.Length * 16, c.Length * 16).ToArray();
        Assert.Equal(alone.Length, batched.Length);
        for (int i = 0; i < alone.Length; i++)
        {
            Assert.Equal(alone[i], batched[i], 1e-4f);
        }
    }

    [Fact]
    public void TheTableLookupPathMatchesTheGraphWithRecurrentLayers()
    {
        using var encoder = new FastEncoder(width: 16, depth: 1, normRows: 100, prefixRows: 100, suffixRows: 100, shapeRows: 100, seed: 5, relu: true, recurrentLayers: 1, recurrentSize: 6);
        encoder.Weights!.Eval();
        Doc doc = Pipeline.Blank(Language.English).Process("Ada Lovelace met Bob in Paris .");
        using var scope = new ActivationScope();
        float[] graph = encoder.Encode([doc]).Vectors.Data<float>().ToArray(); // recording on: the graph
        float[] lookup;
        using (GradMode.NoGrad())
        {
            lookup = encoder.Encode([doc]).Vectors.Data<float>().ToArray();
        }

        Assert.Equal(graph.Length, lookup.Length);
        for (int i = 0; i < graph.Length; i++)
        {
            Assert.Equal(graph[i], lookup[i], 1e-3f);
        }
    }
}
