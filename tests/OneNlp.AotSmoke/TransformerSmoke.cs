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

        // The trainable twins: load both families from their exported graphs, encode with the autodiff graph on, and backpropagate.
        foreach ((string file, TrainableTransformerOptions options) in new[]
        {
            ("tiny-bert.onnx", new TrainableTransformerOptions { Heads = 2, PositionOffset = 0, MaxLength = 16 }),
            ("tiny-modernbert.onnx", new TrainableTransformerOptions { Heads = 2, GlobalEvery = 2, LocalWindow = 8, GlobalRopeTheta = 160000f, LocalRopeTheta = 10000f, MaxLength = 16 }),
        })
        {
            using TrainableTransformerEncoder trainable = TrainableTransformerEncoder.LoadOnnx(Path.Combine(dir, file), tokenizer, options);
            using var trainScope = new ActivationScope();
            Tensor vectors = trainable.Encode([doc]).Vectors;
            check(file + " trainable encode", vectors.Shape[0] == 5 && vectors.RequiresGrad);
            Ops.Sum(vectors).Backward();
            check(file + " trainable gradients", trainable.Weights!.Parameters().Any(p => !p.Grad.IsEmpty));
            using var settingsStream = new MemoryStream();
            using (var w = new Utf8JsonWriter(settingsStream))
            {
                w.WriteStartObject();
                trainable.WriteSettings(w);
                w.WriteEndObject();
            }

            using JsonDocument saved = JsonDocument.Parse(settingsStream.ToArray());
            using IEncoder rebuilt = EncoderRegistry.Create(TrainableTransformerEncoder.EncoderKind, saved.RootElement);
            check(file + " trainable registry", rebuilt.Width == trainable.Width && rebuilt.Weights is not null);

            // The fine-tuned weights written back to ONNX run on the graph runtime.
            string written = Path.Combine(Path.GetTempPath(), "aot-smoke-" + Guid.NewGuid().ToString("N") + ".onnx");
            try
            {
                trainable.ExportOnnx(written);
                using var graph = new TransformerEncoder(written, tokenizer, SubwordPooling.Mean, maxLength: 16);
                using var graphScope = new ActivationScope();
                float[] a = graph.Encode([doc]).Vectors.Data<float>().ToArray();
                check(file + " written graph", a.Length == 5 * graph.Width && a.All(float.IsFinite));
            }
            finally
            {
                File.Delete(written);
            }
        }
    }
}
