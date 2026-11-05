using System.Globalization;
using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Onnx;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// A BERT-style transformer encoder as OneModel.Training modules, so it can be trained and fine-tuned: a token table of width
/// <c>embedRank</c> lifted to the model width by a linear projection (the factorised embedding of Lan et al. 2020, §3.1), learned
/// absolute positions (Devlin et al. 2019, §3), a layer norm, and post-norm <see cref="TransformerEncoderLayer"/>s (Vaswani et al.
/// 2017, §3.1).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FromOnnx"/> fills the modules from the initializers of a graph exported from the distilled student
/// (<c>docs/accurate-encoder.md</c> lists the names). It reproduces the exported graph's output to float32 rounding, which the tests
/// check against the inference runtime. The module computes the same function the inference runtime runs; it is only slower,
/// because it records the autodiff graph.
/// </para>
/// <para>State names, in order: <c>word</c>, <c>projection</c>, <c>positions</c>, <c>norm</c>, then <c>layer0</c> … with the children of
/// <see cref="TransformerEncoderLayer"/>.</para>
/// </remarks>
public sealed class BertStyleEncoder : NnModule
{
    private const int MaxLayers = 128;
    private const float MaskedScore = -1e9f;

    private readonly Embedding _word;
    private readonly Linear _projection;
    private readonly Embedding _positions;
    private readonly LayerNorm _norm;
    private readonly TransformerEncoderLayer[] _layers;

    /// <summary>Creates an encoder with freshly initialised weights.</summary>
    /// <param name="vocabulary">Number of token ids, at least 1.</param>
    /// <param name="embedRank">Width of the token table, at least 1.</param>
    /// <param name="width">Model width, divisible by <paramref name="heads"/>.</param>
    /// <param name="layers">Number of transformer layers, 1 to 128.</param>
    /// <param name="heads">Attention heads.</param>
    /// <param name="feedForwardWidth">Hidden width of each feed-forward block.</param>
    /// <param name="maxPositions">Number of position vectors, at least 1.</param>
    /// <param name="generator">Source of the initial values.</param>
    public BertStyleEncoder(int vocabulary, int embedRank, int width, int layers, int heads, int feedForwardWidth, int maxPositions, Generator generator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPositions, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(layers, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(layers, MaxLayers);
        ArgumentNullException.ThrowIfNull(generator);
        Width = width;
        Heads = heads;
        MaxPositions = maxPositions;
        _word = RegisterModule("word", new Embedding(vocabulary, embedRank, generator));
        _projection = RegisterModule("projection", new Linear(embedRank, width, generator));
        _positions = RegisterModule("positions", new Embedding(maxPositions, width, generator));
        _norm = RegisterModule("norm", new LayerNorm(width));
        _layers = new TransformerEncoderLayer[layers];
        for (int i = 0; i < layers; i++)
        {
            _layers[i] = RegisterModule($"layer{i.ToString(CultureInfo.InvariantCulture)}", new TransformerEncoderLayer(width, heads, feedForwardWidth, generator));
        }
    }

    /// <summary>The model width (length of each output vector).</summary>
    public int Width { get; }

    /// <summary>The number of attention heads.</summary>
    public int Heads { get; }

    /// <summary>The longest sequence the position table covers.</summary>
    public int MaxPositions { get; }

    /// <summary>The number of transformer layers.</summary>
    public int LayerCount => _layers.Length;

    /// <summary>
    /// Reads the weights of an exported student graph. The head count comes from <paramref name="heads"/>, or else from the
    /// <c>.config.json</c> file written beside the model (<c>model.onnx</c> → <c>model.config.json</c>, property <c>heads</c>).
    /// </summary>
    /// <param name="path">The ONNX file.</param>
    /// <param name="heads">The attention head count, or <see langword="null"/> to read it from the config file.</param>
    /// <returns>The encoder, in evaluation mode.</returns>
    /// <exception cref="InvalidDataException">The graph is not a student-layout encoder, a weight has the wrong shape, or the head count is unknown or does not divide the width.</exception>
    public static BertStyleEncoder FromOnnx(string path, int? heads = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        int headCount = heads ?? ReadHeads(path);
        using IrModel model = OnnxImporter.Load(path);
        var data = new Dictionary<string, TensorData>(StringComparer.Ordinal);
        foreach (TensorData t in model.Graph.Initializers)
        {
            data[t.Name] = t;
        }

        TensorData Get(string name) =>
            data.TryGetValue(name, out TensorData? t) ? t : throw new InvalidDataException($"The graph has no initializer '{name}'; it is not a distilled-student encoder.");

        const string E = "model.embeddings.";
        long[] wordDims = Dims(Get(E + "word_embeddings.weight"), 2);
        long[] projDims = Dims(Get(E + "projection.weight"), 2);
        long[] posDims = Dims(Get(E + "position_embeddings.weight"), 2);
        int vocabulary = Checked(wordDims[0]), rank = Checked(wordDims[1]), width = Checked(projDims[1]), maxPositions = Checked(posDims[0]);
        if (projDims[0] != rank || posDims[1] != width)
        {
            throw new InvalidDataException("The embedding weights do not fit together.");
        }

        if (headCount < 1 || width % headCount != 0)
        {
            throw new InvalidDataException($"The head count {headCount} does not divide the width {width}.");
        }

        int layers = 0;
        while (layers < MaxLayers && data.ContainsKey($"model.encoder.layer.{layers.ToString(CultureInfo.InvariantCulture)}.attention.query.weight"))
        {
            layers++;
        }

        if (layers == 0)
        {
            throw new InvalidDataException("The graph has no transformer layers.");
        }

        int ffn = Checked(Dims(Get("model.encoder.layer.0.intermediate.weight"), 2)[1]);
        var encoder = new BertStyleEncoder(vocabulary, rank, width, layers, headCount, ffn, maxPositions, new Generator(1));
        var state = new List<(string, Tensor)>
        {
            ("word.weight", Get(E + "word_embeddings.weight").ToTensor()),
            ("projection.weight", Transposed(Get(E + "projection.weight"))),
            ("projection.bias", Get(E + "projection.bias").ToTensor()),
            ("positions.weight", Get(E + "position_embeddings.weight").ToTensor()),
            ("norm.weight", Get(E + "LayerNorm.weight").ToTensor()),
            ("norm.bias", Get(E + "LayerNorm.bias").ToTensor()),
        };
        for (int i = 0; i < layers; i++)
        {
            string l = $"model.encoder.layer.{i.ToString(CultureInfo.InvariantCulture)}.";
            string mine = $"layer{i.ToString(CultureInfo.InvariantCulture)}.";
            void Linear(string m, string theirs)
            {
                state.Add((mine + m + ".weight", Transposed(Get(l + theirs + ".weight"))));
                state.Add((mine + m + ".bias", Get(l + theirs + ".bias").ToTensor()));
            }

            void Norm(string m, string theirs)
            {
                state.Add((mine + m + ".weight", Get(l + theirs + ".weight").ToTensor()));
                state.Add((mine + m + ".bias", Get(l + theirs + ".bias").ToTensor()));
            }

            Linear("attn.q", "attention.query");
            Linear("attn.k", "attention.key");
            Linear("attn.v", "attention.value");
            Linear("attn.o", "attention.output");
            Norm("norm1", "attn_norm");
            Linear("ff1", "intermediate");
            Linear("ff2", "output");
            Norm("norm2", "output_norm");
        }

        try
        {
            encoder.LoadStateDict(state);
        }
        catch (Exception e) when (e is ArgumentException or TensorShapeException)
        {
            encoder.Dispose();
            throw new InvalidDataException("A weight in the graph has a shape the encoder does not expect: " + e.Message, e);
        }
        finally
        {
            foreach ((_, Tensor t) in state)
            {
                t.Dispose();
            }
        }

        encoder.Eval();
        return encoder;
    }

    /// <summary>Token ids [batch, length] (Int32 or Int64) to hidden states [batch, length, width], attending to every position.</summary>
    /// <param name="input">Token ids; the length may not exceed <see cref="MaxPositions"/>.</param>
    public override Tensor Forward(Tensor input) => Forward(input, default);

    /// <summary>Token ids to hidden states with an additive attention mask.</summary>
    /// <param name="ids">Token ids [batch, length]; the length may not exceed <see cref="MaxPositions"/>.</param>
    /// <param name="mask">Additive score mask [length, length] or [batch, length, length] (0 keeps a key, a large negative value hides it; see <see cref="PaddingMask"/>), or an empty tensor for none.</param>
    /// <exception cref="TensorShapeException"><paramref name="ids"/> is not 2-D or is longer than the position table.</exception>
    public Tensor Forward(Tensor ids, Tensor mask)
    {
        if (ids.Rank != 2 || ids.Shape[1] > MaxPositions)
        {
            throw new TensorShapeException($"BertStyleEncoder needs ids [batch, length] with length at most {MaxPositions}, not {ids.Shape}.");
        }

        int n = ids.Shape[1];
        var positions = new long[n];
        for (int i = 0; i < n; i++)
        {
            positions[i] = i;
        }

        // The backward pass reads the ids, so the tensor lives as long as the surrounding activation scope.
        Tensor positionIds = Tensor.FromSpan<long>(positions, Shape.Create(1, n));
        ActivationScope.Track(positionIds);
        Tensor e = Ops.Add(_projection.Forward(_word.Forward(ids)), Ops.Embedding(_positions.Weight, positionIds));
        Tensor h = _norm.Forward(e);
        foreach (TransformerEncoderLayer layer in _layers)
        {
            h = layer.Forward(h, mask);
        }

        return h;
    }

    /// <summary>Builds the additive attention mask [batch, length, length] for 0/1 key flags: every query row hides the keys flagged 0.</summary>
    /// <param name="keep">Row-major 0/1 flags, <c>rows * length</c> values.</param>
    /// <param name="rows">Batch size.</param>
    /// <param name="length">Sequence length.</param>
    public static Tensor PaddingMask(ReadOnlySpan<long> keep, int rows, int length)
    {
        if (keep.Length != rows * length)
        {
            throw new ArgumentException("The mask has the wrong number of values.", nameof(keep));
        }

        var values = new float[rows * length * length];
        for (int r = 0; r < rows; r++)
        {
            for (int q = 0; q < length; q++)
            {
                for (int k = 0; k < length; k++)
                {
                    values[(((r * length) + q) * length) + k] = keep[(r * length) + k] != 0 ? 0f : MaskedScore;
                }
            }
        }

        return Tensor.FromSpan<float>(values, Shape.Create(rows, length, length));
    }

    private static int ReadHeads(string path)
    {
        string config = Path.ChangeExtension(path, ".config.json");
        if (!File.Exists(config))
        {
            throw new InvalidDataException($"The head count is not stored in the graph: pass it, or put {Path.GetFileName(config)} beside the model.");
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(config));
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("heads", out JsonElement h) || !h.TryGetInt32(out int heads))
        {
            throw new InvalidDataException($"{Path.GetFileName(config)} has no integer 'heads'.");
        }

        return heads;
    }

    private static long[] Dims(TensorData t, int rank) =>
        t.Dims.Length == rank ? [.. t.Dims] : throw new InvalidDataException($"Initializer '{t.Name}' has rank {t.Dims.Length}, expected {rank}.");

    private static int Checked(long v) =>
        v is >= 1 and <= int.MaxValue ? (int)v : throw new InvalidDataException("An initializer dimension is out of range.");

    // The exported weights are [in, out]; Linear stores [out, in].
    private static Tensor Transposed(TensorData d)
    {
        using Tensor t = d.ToTensor();
        if (t.Rank != 2)
        {
            throw new InvalidDataException($"Initializer '{d.Name}' is not a matrix.");
        }

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
