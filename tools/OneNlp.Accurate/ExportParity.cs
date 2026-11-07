using System.Globalization;
using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// A fine-tuned encoder written back to ONNX against the trainable modules it came from: the pipeline file that holds the weights inline is loaded,
/// and the hidden states of its modules are compared with OneModel's graph runtime on the written file, on the probe texts. The modules' states can be
/// dumped (float32, rows in text order) for a comparison with ONNX Runtime.
/// </summary>
internal static class ExportParity
{
    public static void Run(Dictionary<string, string> o)
    {
        List<int[]> ids = JsonSerializer.Deserialize(File.ReadAllText(o["ids"]), IdsContext.Default.ListInt32Array)!;
        int limit = o.TryGetValue("texts", out string? t) ? int.Parse(t, CultureInfo.InvariantCulture) : ids.Count;
        int threads = o.TryGetValue("threads", out string? th) ? int.Parse(th, CultureInfo.InvariantCulture) : 8;
        TensorCompute.Threads = threads;
        TransformerEncoder.ModelDirectory = o.GetValueOrDefault("model-dir");

        using Pipeline pipeline = PipelineFile.Load(o["pipeline"]);
        var encoder = (TrainableTransformerEncoder)pipeline.Components.OfType<ITrainableComponent>().First().Encoder;
        NnModule module = encoder.Weights!;
        module.Eval();
        int width = encoder.Width;
        using Session session = Session.Load(o["graph"], new ModelOptions { IntraOpThreads = threads });
        var stats = new TrainableParity.Stats("trainable modules vs written graph (OneModel)");
        using FileStream? dump = o.TryGetValue("dump", out string? d) ? File.Create(d) : null;
        int count = Math.Min(limit, ids.Count);
        for (int text = 0; text < count; text++)
        {
            int n = ids[text].Length;
            long[] x = [.. ids[text].Select(v => (long)v)];
            using var scope = new ActivationScope();
            using NoGradScope noGrad = GradMode.NoGrad();
            float[] mine = TrainableParity.Forward(module, x, 1, n, [n]);
            using Tensor a = Tensor.FromSpan<long>(x, Shape.Create(1, n));
            using Tensor m = Tensor.FromSpan<long>([.. x.Select(_ => 1L)], Shape.Create(1, n));
            using SessionOutputs outs = session.Run(new Dictionary<string, Tensor> { ["input_ids"] = a, ["attention_mask"] = m });
            stats.Add(mine, outs[session.Outputs[0].Name].Data<float>(), width);
            if (dump is not null)
            {
                var bytes = new byte[mine.Length * 4];
                Buffer.BlockCopy(mine, 0, bytes, 0, bytes.Length);
                dump.Write(bytes);
            }
        }

        Console.WriteLine($"family {encoder.Family}; width {width}; texts {count}");
        Console.WriteLine(stats);
    }
}
