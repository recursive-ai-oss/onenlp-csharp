using System.Globalization;
using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// Forward parity of the trainable modules: the same probe texts (already tokenized, special tokens included) through the trainable module,
/// through OneModel's graph runtime on the exported ONNX file (the inference path of <see cref="TransformerEncoder"/>), and against the
/// hidden states PyTorch wrote for them. Also runs right-padded batches and compares them to the unpadded rows, which checks the masks.
/// </summary>
internal static class TrainableParity
{
    public static void Run(Dictionary<string, string> o)
    {
        List<int[]> ids = JsonSerializer.Deserialize(File.ReadAllText(o["ids"]), IdsContext.Default.ListInt32Array)!;
        byte[] raw = File.ReadAllBytes(o["ref"]);
        var reference = new float[raw.Length / 4];
        Buffer.BlockCopy(raw, 0, reference, 0, raw.Length);
        int limit = o.TryGetValue("texts", out string? t) ? int.Parse(t, CultureInfo.InvariantCulture) : ids.Count;
        int batch = o.TryGetValue("batch", out string? b) ? int.Parse(b, CultureInfo.InvariantCulture) : 8;
        string family = o.GetValueOrDefault("family", "auto");
        int threads = o.TryGetValue("threads", out string? th) ? int.Parse(th, CultureInfo.InvariantCulture) : 8;
        TensorCompute.Threads = threads;

        using TrainableTransformerEncoder encoder = TrainableTransformerEncoder.LoadOnnx(
            o["model"],
            o["tokenizer"],
            new TrainableTransformerOptions
            {
                Family = family switch { "modernbert" => TransformerFamily.ModernBert, "postnorm" => TransformerFamily.PostNorm, _ => TransformerFamily.Auto },
                PositionOffset = o.TryGetValue("position-offset", out string? po) ? int.Parse(po, CultureInfo.InvariantCulture) : 2,
            });
        NnModule module = encoder.Weights!;
        module.Eval();
        int width = encoder.Width;
        Console.WriteLine($"family {encoder.Family}; width {width}; parameters {module.Parameters().Sum(p => (long)p.Data<float>().Length):N0}; texts {Math.Min(limit, ids.Count)}");

        using Session session = Session.Load(o["model"], new ModelOptions { IntraOpThreads = threads });
        var stats = new Stats("trainable vs PyTorch");
        var graph = new Stats("trainable vs graph runtime");
        var runtime = new Stats("graph runtime vs PyTorch");
        var padded = new Stats($"padded batch of {batch} vs unpadded");
        var rows = new List<float[]>();
        int row = 0;
        for (int text = 0; text < Math.Min(limit, ids.Count); text++)
        {
            int n = ids[text].Length;
            long[] x = [.. ids[text].Select(v => (long)v)];
            using var scope = new ActivationScope();
            using NoGradScope noGrad = GradMode.NoGrad();
            float[] mine = Forward(module, x, 1, n, [n]);
            rows.Add(mine);

            using Tensor a = Tensor.FromSpan<long>(x, Shape.Create(1, n));
            using Tensor m = Tensor.FromSpan<long>([.. x.Select(_ => 1L)], Shape.Create(1, n));
            using SessionOutputs outs = session.Run(new Dictionary<string, Tensor> { ["input_ids"] = a, ["attention_mask"] = m });
            ReadOnlySpan<float> viaGraph = outs[session.Outputs[0].Name].Data<float>();
            ReadOnlySpan<float> expected = reference.AsSpan(row * width, n * width);
            stats.Add(mine, expected, width);
            graph.Add(mine, viaGraph, width);
            runtime.Add(viaGraph, expected, width);
            row += n;
        }

        for (int start = 0; start + 1 < rows.Count; start += batch)
        {
            int count = Math.Min(batch, rows.Count - start);
            int longest = 0;
            for (int i = 0; i < count; i++)
            {
                longest = Math.Max(longest, ids[start + i].Length);
            }

            var x = new long[count * longest];
            var lengths = new int[count];
            for (int i = 0; i < count; i++)
            {
                lengths[i] = ids[start + i].Length;
                for (int j = 0; j < lengths[i]; j++)
                {
                    x[(i * longest) + j] = ids[start + i][j];
                }
            }

            using var scope = new ActivationScope();
            using NoGradScope noGrad = GradMode.NoGrad();
            float[] batched = Forward(module, x, count, longest, lengths);
            for (int i = 0; i < count; i++)
            {
                padded.Add(batched.AsSpan(i * longest * width, lengths[i] * width), rows[start + i], width);
            }
        }

        foreach (Stats s in new[] { stats, graph, runtime, padded })
        {
            Console.WriteLine(s);
        }
    }

    internal static float[] Forward(NnModule module, long[] x, int rows, int length, int[] lengths)
    {
        using Tensor ids = Tensor.FromSpan<long>(x, Shape.Create(rows, length));
        Tensor h = module switch
        {
            ModernBertModule m => m.Forward(ids, lengths),
            PostNormModule m => m.Forward(ids, lengths),
            _ => throw new InvalidOperationException("Not a trainable transformer module."),
        };
        return h.Data<float>().ToArray();
    }

    internal sealed class Stats(string name)
    {
        private double _maxAbs;
        private double _maxRef;
        private double _sumAbs;
        private double _minCos = 1;
        private long _count;
        private long _tokens;

        public void Add(ReadOnlySpan<float> got, ReadOnlySpan<float> expected, int width)
        {
            for (int r = 0; r < expected.Length / width; r++)
            {
                double dot = 0, na = 0, nb = 0;
                for (int j = 0; j < width; j++)
                {
                    double g = got[(r * width) + j], e = expected[(r * width) + j];
                    double d = Math.Abs(g - e);
                    _maxAbs = Math.Max(_maxAbs, d);
                    _maxRef = Math.Max(_maxRef, Math.Abs(e));
                    _sumAbs += d;
                    dot += g * e;
                    na += g * g;
                    nb += e * e;
                }

                _minCos = Math.Min(_minCos, dot / Math.Sqrt(na * nb));
                _count += width;
                _tokens++;
            }
        }

        public override string ToString() => string.Create(
            CultureInfo.InvariantCulture,
            $"{name}: tokens {_tokens} maxAbs {_maxAbs:E3} (largest |value| {_maxRef:F1}, relative {_maxAbs / _maxRef:E2}) meanAbs {_sumAbs / Math.Max(1, _count):E3} minCos {_minCos:F8}");
    }
}
