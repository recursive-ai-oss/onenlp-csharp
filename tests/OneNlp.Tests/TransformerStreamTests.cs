using System.Text.Json;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

// The model packages embed their encoders, so the transformer encoder must load from streams and give the vectors it gives from files;
// the shared encoder and word-form expansion are what the accurate-tier package relies on for many languages and multiword tokens.
public class TransformerStreamTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");

    private static Doc Plain(string text, StringStore strings)
    {
        var tokens = new List<TokenBoundary>();
        for (int i = 0; i < text.Length;)
        {
            if (text[i] == ' ')
            {
                i++;
                continue;
            }

            int j = text.IndexOf(' ', i);
            j = j < 0 ? text.Length : j;
            tokens.Add(new TokenBoundary(i, j - i));
            i = j;
        }

        return new Doc(strings, text, [.. tokens]);
    }

    [Fact]
    public void AnEncoderLoadedFromStreamsGivesTheVectorsOfOneLoadedFromFiles()
    {
        string model = Path.Combine(Dir, "tiny-bert.onnx");
        string tokenizer = Path.Combine(Dir, "tiny-bert.tokenizer.json");
        using var fromFiles = new TransformerEncoder(model, tokenizer);
        using FileStream onnx = File.OpenRead(model);
        using FileStream json = File.OpenRead(tokenizer);
        using TransformerEncoder fromStreams = TransformerEncoder.FromStreams(onnx, json);
        var strings = new StringStore();
        Doc[] docs = [Plain("the cat sat on the mat", strings), Plain("a dog", strings)];
        Assert.Equal(fromFiles.Encode(docs).Vectors.Data<float>().ToArray(), fromStreams.Encode(docs).Vectors.Data<float>().ToArray());
        Assert.Equal(fromFiles.Width, fromStreams.Width);
    }

    [Fact]
    public void ExpandedWordFormsGiveTheVectorsOfTheTextWrittenWithSpaces()
    {
        string model = Path.Combine(Dir, "tiny-bert.onnx");
        string tokenizer = Path.Combine(Dir, "tiny-bert.tokenizer.json");
        using var expanding = new TransformerEncoder(model, tokenizer, expandWordForms: true);
        var strings = new StringStore();
        var written = new Doc(strings, "thecat", [new TokenBoundary(0, 3), new TokenBoundary(3, 3)]);
        written.SetAnnotation(TokenColumn.Norm, 0, "the");
        written.SetAnnotation(TokenColumn.Norm, 1, "cat");
        Doc spaced = Plain("the cat", strings);
        Assert.Equal(expanding.Encode([spaced]).Vectors.Data<float>().ToArray(), expanding.Encode([written]).Vectors.Data<float>().ToArray());

        // The setting is saved with the encoder and read back.
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            expanding.WriteSettings(w);
            w.WriteEndObject();
        }

        using JsonDocument settings = JsonDocument.Parse(ms.ToArray());
        Assert.True(settings.RootElement.GetProperty("expandForms").GetBoolean());
    }

    [Fact]
    public void ASharedEncoderRegistersOnceAndSavesNoWeights()
    {
        string model = Path.Combine(Dir, "tiny-bert.onnx");
        string tokenizer = Path.Combine(Dir, "tiny-bert.tokenizer.json");
        using var real = new TransformerEncoder(model, tokenizer);
        var shared = new SharedEncoder(real, "test-transformer-stream");
        Assert.Null(shared.Weights);
        Assert.Equal(real.Width, shared.Width);
        SharedEncoder.Register("test-transformer-stream", real);
        try
        {
            Assert.Throws<InvalidOperationException>(() => SharedEncoder.Register("test-transformer-stream", real));
            IEncoder made = EncoderRegistry.Create(SharedEncoder.EncoderKind, JsonDocument.Parse("""{"name":"test-transformer-stream"}""").RootElement);
            Assert.Equal(real.Width, made.Width);
        }
        finally
        {
            Assert.True(SharedEncoder.Unregister("test-transformer-stream"));
        }

        Assert.Throws<InvalidDataException>(() => EncoderRegistry.Create(SharedEncoder.EncoderKind, JsonDocument.Parse("""{"name":"test-transformer-stream"}""").RootElement));
    }
}
