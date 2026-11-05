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
    [InlineData("\"hashes\":\"two\"")]
    public void OutOfRangeOptionalSettingsAreRejected(string extra)
    {
        string json = "{\"width\":16,\"depth\":1,\"normRows\":100,\"prefixRows\":100,\"suffixRows\":100,\"shapeRows\":100,\"seed\":0," + extra + "}";
        Assert.Throws<InvalidDataException>(() => Create(json));
    }

    [Fact]
    public void TableSizeLimitCountsTheEmbeddingWidthAndTheCasedTable()
    {
        string json = "{\"width\":16,\"depth\":1,\"normRows\":4194304,\"prefixRows\":4194304,\"suffixRows\":4194304,\"shapeRows\":4194304,\"seed\":0,\"embedWidth\":16,\"casedRows\":4194304}";
        Assert.ThrowsAny<ArgumentException>(() => Create(json));
    }
}
