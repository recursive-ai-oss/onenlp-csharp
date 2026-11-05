using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>Reads the weights of an exported encoder (ONNX, written by the PyTorch exporter without dynamo) into trainable modules.</summary>
/// <remarks>
/// The exporter names norm scales and biases after the PyTorch parameters but leaves a linear layer's weight as an unnamed constant of the
/// <c>MatMul</c> node whose name is the module path (<c>/model/layers.0/attn/Wqkv/MatMul</c>), stored [in, out]. The loader finds each weight through that node
/// and transposes it to [out, in]. A graph written another way (for example by the dynamo exporter) is rejected with a message, never guessed at.
/// </remarks>
internal sealed class OnnxEncoderWeights
{
    private readonly Dictionary<string, TensorData> _data;
    private readonly Dictionary<string, string> _matmul = new(StringComparer.Ordinal);
    private readonly IrGraph _graph;

    public OnnxEncoderWeights(IrGraph graph)
    {
        _graph = graph;
        _data = graph.Initializers.ToDictionary(t => t.Name, StringComparer.Ordinal);
        // The exporter stores equal tensors once (the zero biases of a freshly made model) and gives the other parameters' names to Identity nodes of it.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (IrNode n in graph.Nodes)
            {
                if (n.OpType == "Identity" && n.InputNames.Length == 1 && n.OutputNames.Length == 1 && _data.TryGetValue(n.InputNames[0], out TensorData? source))
                {
                    _data.TryAdd(n.OutputNames[0], source);
                }
            }
        }

        foreach (IrNode n in graph.Nodes)
        {
            if (n.OpType == "MatMul" && n.InputNames.Length == 2 && _data.ContainsKey(n.InputNames[1]) && n.Name.EndsWith("/MatMul", StringComparison.Ordinal))
            {
                _matmul[n.Name.TrimStart('/').Replace('/', '.')[..^".MatMul".Length]] = n.InputNames[1];
            }
        }
    }

    /// <summary>Tells the family from the names of the initializers; <see langword="null"/> when neither is present.</summary>
    public TransformerFamily? DetectFamily()
    {
        foreach (string name in _data.Keys)
        {
            if (name.EndsWith("embeddings.tok_embeddings.weight", StringComparison.Ordinal))
            {
                return TransformerFamily.ModernBert;
            }

            if (name.EndsWith("embeddings.word_embeddings.weight", StringComparison.Ordinal))
            {
                return TransformerFamily.PostNorm;
            }
        }

        return null;
    }

    /// <summary>The ε of the first layer normalization in the graph, or <see langword="null"/>.</summary>
    public float? NormEpsilon()
    {
        foreach (IrNode n in _graph.Nodes)
        {
            if (n.OpType == "LayerNormalization" && n.FindAttribute("epsilon") is { } attribute)
            {
                return attribute.Real;
            }
        }

        return null;
    }

    /// <summary>
    /// The distinct rotary bases found in the graph's rotary table constants, each rounded to three significant digits. A table holds
    /// <c>θ^(−2i/d)</c> for <c>i</c> below d/2, so θ = table[1]^(−d/2). Which table belongs to the global layers is not recorded in the graph's names,
    /// so a graph with more than one base needs them given explicitly.
    /// </summary>
    public List<float> RopeThetas(int headDim)
    {
        var found = new List<float>();
        foreach (IrNode n in _graph.Nodes)
        {
            if (n.OpType != "Constant" || !n.Name.Contains("rotary", StringComparison.Ordinal) ||
                n.FindAttribute("value") is not { Type: IrAttributeType.Tensor } attribute)
            {
                continue;
            }

            TensorData t = attribute.Tensor;
            if (t.DataType == 1 && headDim >= 4 && t.ElementCount == headDim / 2 && t.RawLength == 4 * t.ElementCount)
            {
                double inverse = BitConverter.ToSingle(t.RawData[4..8]);
                if (inverse > 0 && inverse < 1)
                {
                    double theta = Math.Pow(inverse, -headDim / 2.0);
                    double scale = Math.Pow(10, Math.Floor(Math.Log10(theta)) - 2);
                    float rounded = (float)(Math.Round(theta / scale) * scale);
                    if (!found.Contains(rounded))
                    {
                        found.Add(rounded);
                    }
                }
            }
        }

        return found;
    }

    public (ModernBertModule Module, ModernBertConfig Config) LoadModernBert(ModernBertConfig defaults, TrainableTransformerOptions o, bool freezeEmbeddings, ulong seed)
    {
        string p = PrefixOf("embeddings.tok_embeddings.weight");
        TensorData table = Initializer(p + "embeddings.tok_embeddings.weight");
        int layers = 0;
        while (_matmul.ContainsKey($"{p}layers.{layers}.attn.Wqkv"))
        {
            layers++;
        }

        if (layers == 0)
        {
            throw new InvalidDataException("The graph has no 'layers.N.attn.Wqkv' MatMul nodes; only graphs written by the non-dynamo PyTorch exporter are supported.");
        }

        int width = (int)table.Dims[1];
        int heads = o.Heads > 0 ? o.Heads : width / 64;
        int headDim = heads > 0 ? width / heads : 0;
        ModernBertConfig config = defaults with
        {
            VocabularySize = (int)table.Dims[0],
            Width = width,
            Layers = layers,
            Heads = heads,
            FeedForwardWidth = (int)(Weight($"{p}layers.0.mlp.Wi").Dims[1] / 2),
            GlobalEvery = o.GlobalEvery,
            LocalWindow = o.LocalWindow,
            NormEpsilon = o.NormEpsilon > 0 ? o.NormEpsilon : NormEpsilon() ?? defaults.NormEpsilon,
        };
        if (heads < 1)
        {
            throw new InvalidDataException("The head count cannot be inferred for this width; set Heads.");
        }

        List<float> thetas = o.GlobalRopeTheta > 0 && o.LocalRopeTheta > 0 ? [] : RopeThetas(headDim);
        if (thetas.Count > 1)
        {
            throw new InvalidDataException($"The graph has {thetas.Count} rotary bases ({string.Join(", ", thetas)}); set GlobalRopeTheta and LocalRopeTheta.");
        }

        float? found = thetas.Count == 1 ? thetas[0] : null;
        float global = o.GlobalRopeTheta > 0 ? o.GlobalRopeTheta : found ?? defaults.GlobalRopeTheta;
        config = config with
        {
            GlobalRopeTheta = global,
            LocalRopeTheta = o.LocalRopeTheta > 0 ? o.LocalRopeTheta : found ?? defaults.LocalRopeTheta,
        };

        Tensor tokens = table.ToTensor();
        var state = new List<(string, Tensor)>();
        ModernBertModule module;
        try
        {
            module = new ModernBertModule(config, seed, freezeEmbeddings ? tokens : default);
        }
        catch
        {
            tokens.Dispose();
            throw;
        }

        try
        {
            if (!freezeEmbeddings)
            {
                state.Add(("tokens.weight", tokens));
            }

            state.Add(("embedding_norm.weight", Floats(p + "embeddings.norm.weight")));
            for (int i = 0; i < layers; i++)
            {
                string l = $"{p}layers.{i}.";
                if (i > 0)
                {
                    state.Add(($"layers{i}.attn_norm.weight", Floats(l + "attn_norm.weight")));
                }

                state.Add(($"layers{i}.wqkv.weight", Transposed(Weight(l + "attn.Wqkv"))));
                state.Add(($"layers{i}.wo.weight", Transposed(Weight(l + "attn.Wo"))));
                state.Add(($"layers{i}.mlp_norm.weight", Floats(l + "mlp_norm.weight")));
                state.Add(($"layers{i}.wi.weight", Transposed(Weight(l + "mlp.Wi"))));
                state.Add(($"layers{i}.mlp_wo.weight", Transposed(Weight(l + "mlp.Wo"))));
            }

            state.Add(("final_norm.weight", Floats(p + "final_norm.weight")));
            module.LoadStateDict(state);
        }
        catch
        {
            module.Dispose();
            throw;
        }
        finally
        {
            foreach ((string name, Tensor t) in state)
            {
                if (!(name == "tokens.weight"))
                {
                    t.Dispose();
                }
            }

            if (!freezeEmbeddings)
            {
                tokens.Dispose();
            }
        }

        return (module, config);
    }

    /// <summary>
    /// Reads a BERT-style encoder. Two weight layouts are understood: the PyTorch exporter's (each linear weight is the constant of a named <c>MatMul</c>
    /// node) and the distilled student's (<c>docs/accurate-encoder.md</c>: every weight a named initializer, stored [in, out], with a factorised
    /// embedding). <paramref name="configHeads"/> is the head count from the model's <c>.config.json</c>, or 0; the student layout needs it
    /// because the head count is not in the weights.
    /// </summary>
    public (PostNormModule Module, PostNormConfig Config) LoadPostNorm(PostNormConfig defaults, TrainableTransformerOptions o, bool freezeEmbeddings, ulong seed, int configHeads = 0)
    {
        string p = PrefixOf("embeddings.word_embeddings.weight");
        bool student = _data.ContainsKey(p + "embeddings.projection.weight");
        TensorData table = Initializer(p + "embeddings.word_embeddings.weight");
        TensorData positions = Initializer(p + "embeddings.position_embeddings.weight");
        int layers = 0;
        while (student
            ? _data.ContainsKey($"{p}encoder.layer.{layers}.attention.query.weight")
            : _matmul.ContainsKey($"{p}encoder.layer.{layers}.attention.self.query"))
        {
            layers++;
        }

        if (layers == 0)
        {
            throw new InvalidDataException("The graph has no 'encoder.layer.N.attention.self.query' MatMul nodes; only graphs written by the non-dynamo PyTorch exporter are supported.");
        }

        int embedWidth = (int)table.Dims[1];
        int width = student ? (int)Initializer(p + "embeddings.projection.weight").Dims[1] : embedWidth;
        int heads = o.Heads > 0 ? o.Heads : configHeads > 0 ? configHeads : student ? 0 : width / 64;
        if (heads < 1 || width % heads != 0)
        {
            throw new InvalidDataException("The head count is not in the weights: set Heads, or put <model>.config.json with a 'heads' value beside the model.");
        }

        // The student has no padding-index offset on its positions.
        int positionOffset = student ? 0 : o.PositionOffset;
        string ffName = student ? "intermediate.weight" : "intermediate.dense";
        PostNormConfig config = defaults with
        {
            VocabularySize = (int)table.Dims[0],
            Width = width,
            EmbeddingWidth = embedWidth == width ? 0 : embedWidth,
            Layers = layers,
            Heads = heads,
            FeedForwardWidth = (int)(student ? Initializer($"{p}encoder.layer.0.{ffName}") : Weight($"{p}encoder.layer.0.{ffName}")).Dims[1],
            MaxPositions = (int)positions.Dims[0],
            PositionOffset = positionOffset,
            NormEpsilon = o.NormEpsilon > 0 ? o.NormEpsilon : NormEpsilon() ?? defaults.NormEpsilon,
        };

        Tensor tokens = table.ToTensor();
        var state = new List<(string, Tensor)>();
        PostNormModule module;
        try
        {
            module = new PostNormModule(config, seed, freezeEmbeddings ? tokens : default);
        }
        catch
        {
            tokens.Dispose();
            throw;
        }

        try
        {
            if (!freezeEmbeddings)
            {
                state.Add(("tokens.weight", tokens));
            }

            if (student)
            {
                state.Add(("projection.weight", Transposed(Initializer(p + "embeddings.projection.weight"))));
                state.Add(("projection.bias", Floats(p + "embeddings.projection.bias")));
                state.Add(("positions.weight", Floats(p + "embeddings.position_embeddings.weight")));
            }
            else
            {
                // The single token-type vector is added to every position row.
                using Tensor positionTable = positions.ToTensor();
                using Tensor type = Initializer(p + "embeddings.token_type_embeddings.weight").ToTensor();
                float[] merged = positionTable.Data<float>().ToArray();
                ReadOnlySpan<float> t0 = type.Data<float>()[..width];
                for (int r = 0; r < positions.Dims[0]; r++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        merged[(r * width) + j] += t0[j];
                    }
                }

                state.Add(("positions.weight", Tensor.FromSpan<float>(merged, positionTable.Shape)));
            }

            state.Add(("embedding_norm.weight", Floats(p + "embeddings.LayerNorm.weight")));
            state.Add(("embedding_norm.bias", Floats(p + "embeddings.LayerNorm.bias")));
            for (int i = 0; i < layers; i++)
            {
                string l = $"{p}encoder.layer.{i}.";
                void Linear(string mine, string theirs)
                {
                    state.Add(($"layers{i}.{mine}.weight", Transposed(student ? Initializer(l + theirs + ".weight") : Weight(l + theirs))));
                    state.Add(($"layers{i}.{mine}.bias", Floats(l + theirs + ".bias")));
                }

                void Norm(string mine, string theirs)
                {
                    state.Add(($"layers{i}.{mine}.weight", Floats(l + theirs + ".weight")));
                    state.Add(($"layers{i}.{mine}.bias", Floats(l + theirs + ".bias")));
                }

                if (student)
                {
                    Linear("attn.q", "attention.query");
                    Linear("attn.k", "attention.key");
                    Linear("attn.v", "attention.value");
                    Linear("attn.o", "attention.output");
                    Norm("norm1", "attn_norm");
                    Linear("ff1", "intermediate");
                    Linear("ff2", "output");
                    Norm("norm2", "output_norm");
                }
                else
                {
                    Linear("attn.q", "attention.self.query");
                    Linear("attn.k", "attention.self.key");
                    Linear("attn.v", "attention.self.value");
                    Linear("attn.o", "attention.output.dense");
                    Norm("norm1", "attention.output.LayerNorm");
                    Linear("ff1", "intermediate.dense");
                    Linear("ff2", "output.dense");
                    Norm("norm2", "output.LayerNorm");
                }
            }

            module.LoadStateDict(state);
        }
        catch
        {
            module.Dispose();
            throw;
        }
        finally
        {
            foreach ((string name, Tensor t) in state)
            {
                if (!(name == "tokens.weight"))
                {
                    t.Dispose();
                }
            }

            if (!freezeEmbeddings)
            {
                tokens.Dispose();
            }
        }

        return (module, config);
    }

    private string PrefixOf(string suffix)
    {
        foreach (string name in _data.Keys)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return name[..^suffix.Length];
            }
        }

        throw new InvalidDataException($"The graph has no initializer ending in '{suffix}'.");
    }

    private TensorData Initializer(string name) =>
        _data.TryGetValue(name, out TensorData? t) ? t : throw new InvalidDataException($"The graph has no initializer '{name}'.");

    private TensorData Weight(string path) =>
        _matmul.TryGetValue(path, out string? name) ? _data[name] : throw new InvalidDataException($"The graph has no MatMul node '{path}' with a constant weight.");

    private Tensor Floats(string name)
    {
        TensorData t = Initializer(name);
        if (t.DataType != 1)
        {
            throw new InvalidDataException($"The initializer '{name}' is not float32.");
        }

        return t.ToTensor();
    }

    private static Tensor Transposed(TensorData d)
    {
        if (d.DataType != 1 || d.Dims.Length != 2)
        {
            throw new InvalidDataException($"The weight '{d.Name}' is not a float32 matrix.");
        }

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
}
