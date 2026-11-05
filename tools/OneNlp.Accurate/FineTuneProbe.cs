using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Onnx;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

/// <summary>
/// Can an imported encoder be fine-tuned? Part A: the graph runtime's outputs carry no autodiff graph. Part B: the same weights,
/// read from the ONNX initializers into OneModel.Training's own modules (<see cref="TransformerEncoderLayer"/> is a post-norm BERT/XLM-R
/// layer), reproduce the graph's output and train for a few steps.
/// </summary>
internal static class FineTuneProbe
{
    public static void Run(Dictionary<string, string> o)
    {
        string path = o["model"];
        List<int[]> ids = JsonSerializer.Deserialize(File.ReadAllText(o["ids"]), IdsContext.Default.ListInt32Array)!;
        int[] text = ids[int.Parse(o.GetValueOrDefault("text", "3"), CultureInfo.InvariantCulture)];
        int steps = int.Parse(o.GetValueOrDefault("steps", "5"), CultureInfo.InvariantCulture);
        long[] x = [.. text.Select(v => (long)v)];

        // Part A.
        using Session session = Session.Load(path);
        using Tensor ids64 = Tensor.FromSpan<long>(x, Shape.Create(1, x.Length));
        using Tensor mask = Tensor.FromSpan<long>([.. x.Select(_ => 1L)], Shape.Create(1, x.Length));
        using SessionOutputs outs = session.Run(new Dictionary<string, Tensor> { ["input_ids"] = ids64, ["attention_mask"] = mask });
        Tensor hidden = outs["last_hidden_state"];
        Console.WriteLine($"A: graph output RequiresGrad={hidden.RequiresGrad}");
        float[] expected = hidden.Data<float>().ToArray();

        // Part B.
        using IrModel model = OnnxImporter.Load(path);
        var clock = Stopwatch.StartNew();
        using var encoder = new BertStyleEncoder(model.Graph);
        Console.WriteLine($"B: modules built from {model.Graph.Initializers.Length} initializers in {clock.Elapsed.TotalSeconds:F1} s; parameters {encoder.Parameters().Sum(p => (long)p.Data<float>().Length):N0}");
        encoder.Eval();
        using (NoGradScope _ = GradMode.NoGrad())
        using (new ActivationScope())
        {
            float[] got = encoder.Forward(Tensor.FromSpan<long>(x, Shape.Create(1, x.Length))).Data<float>().ToArray();
            double max = 0;
            for (int i = 0; i < got.Length; i++)
            {
                max = Math.Max(max, Math.Abs(got[i] - expected[i]));
            }

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"B: modules vs graph runtime, max abs difference {max:E2} over {got.Length} values"));
        }

        encoder.Train(true);
        var generator = new Generator(7);
        using var head = new Linear(encoder.Width, 8, generator);
        Tensor[] parameters = [.. encoder.Parameters(), .. head.Parameters()];
        using var optimizer = new AdamW(parameters, learningRate: 2e-5f);
        long[] labels = [.. x.Select(v => v % 8)];
        for (int step = 0; step < steps; step++)
        {
            clock.Restart();
            using var scope = new ActivationScope();
            optimizer.ZeroGrad();
            Tensor h = encoder.Forward(Tensor.FromSpan<long>(x, Shape.Create(1, x.Length)));
            Tensor logits = head.Forward(h.Reshape(x.Length, encoder.Width));
            Tensor loss = Losses.CrossEntropy(logits, Tensor.FromSpan<long>(labels, Shape.Create(labels.Length)));
            loss.Backward();
            Tensor g0 = encoder.FirstLayerQueryWeight.Grad;
            double gnorm = 0;
            foreach (float v in g0.Data<float>())
            {
                gnorm += (double)v * v;
            }

            optimizer.Step();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"B: step {step} loss {loss.Item<float>():F4} |grad layer0.q| {Math.Sqrt(gnorm):E2} step_s {clock.Elapsed.TotalSeconds:F2}"));
        }
    }
}

/// <summary>An XLM-R / BERT-style encoder in OneModel.Training modules, filled from the initializers of an exported graph.</summary>
internal sealed class BertStyleEncoder : NnModule
{
    private readonly Embedding _word;
    private readonly Tensor _positionPlusType;
    private readonly LayerNorm _norm;
    private readonly TransformerEncoderLayer[] _layers;

    public BertStyleEncoder(IrGraph graph)
    {
        var data = graph.Initializers.ToDictionary(t => t.Name, StringComparer.Ordinal);
        Tensor Floats(string name) => data[name].ToTensor();

        // The exporter names the bias and norm initializers after the PyTorch parameters; a linear layer's weight is the unnamed
        // initializer of the MatMul node whose path matches (node "/model/encoder/layer.0/attention/self/query/MatMul").
        var matmul = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (IrNode n in graph.Nodes.Where(n => n.OpType == "MatMul" && n.InputNames.Length == 2 && data.ContainsKey(n.InputNames[1])))
        {
            matmul[n.Name.TrimStart('/').Replace('/', '.')[..^".MatMul".Length]] = n.InputNames[1];
        }

        Tensor wordTable = Floats("model.embeddings.word_embeddings.weight");
        Width = wordTable.Shape[1];
        var generator = new Generator(1);
        _word = RegisterModule("word", new Embedding(wordTable.Shape[0], Width, generator));
        _norm = RegisterModule("norm", new LayerNorm(Width));
        int layerCount = matmul.Keys.Count(k => k.EndsWith("attention.self.query", StringComparison.Ordinal));
        int ff = data[matmul["model.encoder.layer.0.intermediate.dense"]].Dims[1] is long f ? (int)f : 0;
        _layers = new TransformerEncoderLayer[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            _layers[i] = RegisterModule($"layer{i}", new TransformerEncoderLayer(Width, 12, ff, generator));
        }

        // Positions start after the padding index (XLM-R: 2); the single token-type vector is added to every position.
        Tensor positions = Floats("model.embeddings.position_embeddings.weight");
        Tensor type = Floats("model.embeddings.token_type_embeddings.weight");
        float[] p = positions.Data<float>().ToArray();
        ReadOnlySpan<float> t0 = type.Data<float>()[..Width];
        for (int r = 0; r < positions.Shape[0]; r++)
        {
            for (int j = 0; j < Width; j++)
            {
                p[(r * Width) + j] += t0[j];
            }
        }

        _positionPlusType = Tensor.FromSpan<float>(p, positions.Shape);

        var state = new List<(string, Tensor)>
        {
            ("word.weight", wordTable),
            ("norm.weight", Floats("model.embeddings.LayerNorm.weight")),
            ("norm.bias", Floats("model.embeddings.LayerNorm.bias")),
        };
        for (int i = 0; i < layerCount; i++)
        {
            string L = $"model.encoder.layer.{i}.";
            void Linear(string mine, string theirs)
            {
                state.Add(($"layer{i}.{mine}.weight", Transposed(data[matmul[L + theirs]])));
                state.Add(($"layer{i}.{mine}.bias", Floats(L + theirs + ".bias")));
            }

            void Norm(string mine, string theirs)
            {
                state.Add(($"layer{i}.{mine}.weight", Floats(L + theirs + ".weight")));
                state.Add(($"layer{i}.{mine}.bias", Floats(L + theirs + ".bias")));
            }

            Linear("attn.q", "attention.self.query");
            Linear("attn.k", "attention.self.key");
            Linear("attn.v", "attention.self.value");
            Linear("attn.o", "attention.output.dense");
            Norm("norm1", "attention.output.LayerNorm");
            Linear("ff1", "intermediate.dense");
            Linear("ff2", "output.dense");
            Norm("norm2", "output.LayerNorm");
        }

        LoadStateDict(state);
        foreach ((_, Tensor t) in state)
        {
            t.Dispose();
        }
    }

    public int Width { get; }

    public Tensor FirstLayerQueryWeight => _layers[0].StateDict().First(s => s.Name == "attn.q.weight").Tensor;

    private static Tensor Transposed(TensorData d)
    {
        using Tensor t = d.ToTensor();
        int rows = t.Shape[0], cols = t.Shape[1];
        ReadOnlySpan<float> s = t.Data<float>();
        var r = new float[s.Length];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                r[(j * rows) + i] = s[(i * cols) + j];
            }
        }

        return Tensor.FromSpan<float>(r, Shape.Create(cols, rows));
    }

    /// <summary>Token ids [1, n] (Int64) to hidden states [1, n, width]. One sequence, no padding.</summary>
    public override Tensor Forward(Tensor input)
    {
        int n = input.Shape[1];
        var pos = new long[n];
        for (int i = 0; i < n; i++)
        {
            pos[i] = i + 2;
        }

        using Tensor posIds = Tensor.FromSpan<long>(pos, Shape.Create(1, n));
        Tensor e = Ops.Add(_word.Forward(input), Ops.Embedding(_positionPlusType, posIds));
        Tensor h = _norm.Forward(e);
        foreach (TransformerEncoderLayer layer in _layers)
        {
            h = layer.Forward(h);
        }

        return h;
    }
}
