using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.AotSmoke;

/// <summary>Loads the tiny exported encoder (ONNX import, tokenizer.json, session run) and encodes under NativeAOT.</summary>
internal static class TransformerSmoke
{
    public static void Run(Action<string, bool> check)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");
        string model = Path.Combine(dir, "tiny-bert.onnx");
        string tokenizer = Path.Combine(dir, "tiny-bert.tokenizer.json");
        if (!File.Exists(model))
        {
            return;
        }

        var doc = new Doc(new StringStore(), "the cat sat on unable", [new(0, 3), new(4, 3), new(8, 3), new(12, 2), new(15, 6)]);
        using var encoder = new TransformerEncoder(model, tokenizer, SubwordPooling.Mean, maxLength: 16);
        using var scope = new ActivationScope();
        float[] v = encoder.Encode([doc]).Vectors.Data<float>().ToArray();
        check("transformer encode", v.Length == 5 * encoder.Width && v.All(float.IsFinite) && v.Any(x => x != 0f));

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            encoder.WriteSettings(w);
            w.WriteEndObject();
        }

        using JsonDocument settings = JsonDocument.Parse(stream.ToArray());
        TransformerEncoder.ModelDirectory = null;
        using IEncoder again = EncoderRegistry.Create("transformer", settings.RootElement);
        check("transformer registry", again.Width == encoder.Width && again.Weights is null);
    }
}
