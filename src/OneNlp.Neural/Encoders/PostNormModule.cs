using System.Globalization;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>The shape of a BERT-style (post-norm, learned absolute positions) encoder: BERT, RoBERTa, XLM-R.</summary>
public sealed record PostNormConfig
{
    /// <summary>Number of rows of the token embedding table.</summary>
    public int VocabularySize { get; init; } = 250002;

    /// <summary>Hidden width. Default 768.</summary>
    public int Width { get; init; } = 768;

    /// <summary>Number of layers. Default 12.</summary>
    public int Layers { get; init; } = 12;

    /// <summary>Number of attention heads. Default 12.</summary>
    public int Heads { get; init; } = 12;

    /// <summary>Inner width of the feed-forward. Default 3072.</summary>
    public int FeedForwardWidth { get; init; } = 3072;

    /// <summary>Number of rows of the position table. Default 514 (XLM-R).</summary>
    public int MaxPositions { get; init; } = 514;

    /// <summary>The position of the first token: 2 for XLM-R and RoBERTa (positions start after the padding index), 0 for BERT. Default 2.</summary>
    public int PositionOffset { get; init; } = 2;

    /// <summary>The ε of every layer normalization: 1e-5 for XLM-R, 1e-12 for BERT. Default 1e-5.</summary>
    public float NormEpsilon { get; init; } = 1e-5f;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(VocabularySize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Width, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(Layers, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Heads, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(FeedForwardWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxPositions, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(PositionOffset, 0);
        if (Width % Heads != 0 || (Width / Heads) % 2 != 0)
        {
            throw new ArgumentException($"Width {Width} must split into {Heads} heads of even size.");
        }

        if (!(NormEpsilon > 0f))
        {
            throw new ArgumentException("The epsilon must be positive.");
        }
    }
}

/// <summary>
/// A BERT-style encoder (Devlin et al. 2019 §3; Vaswani et al. 2017 §3) from OneModel's tensor ops: token + position embeddings,
/// a layer norm, then post-norm layers <c>x = norm(x + attention(x)); x = norm(x + ff(x))</c> with exact GELU.
/// </summary>
/// <remarks>
/// The token-type embedding of the original BERT has a single row in the exported models, so it is added into every position row
/// when the weights are loaded. With <c>frozenTable</c> the token table is outside the module's state (see <see cref="ModernBertModule"/>).
/// A module is not thread-safe.
/// </remarks>
public sealed class PostNormModule : NnModule
{
    private readonly PostNormConfig _config;
    private readonly Tensor _frozenTable;
    private readonly Embedding? _tokens;
    private readonly Embedding _positions;
    private readonly EpsilonLayerNorm _embeddingNorm;
    private readonly Layer[] _layers;

    /// <summary>Creates the module with random weights.</summary>
    /// <param name="config">The shape.</param>
    /// <param name="seed">Seed of the initial values.</param>
    /// <param name="frozenTable">The token table [VocabularySize, Width] to use without training it, or an empty tensor for a trainable table. The module takes ownership.</param>
    /// <exception cref="ArgumentException">The configuration or the table's shape is invalid.</exception>
    public PostNormModule(PostNormConfig config, ulong seed, Tensor frozenTable = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        _config = config;
        var generator = new Generator(seed);
        if (frozenTable.IsEmpty)
        {
            _tokens = RegisterModule("tokens", new Embedding(config.VocabularySize, config.Width, generator));
        }
        else
        {
            if (frozenTable.Rank != 2 || frozenTable.Shape[0] != config.VocabularySize || frozenTable.Shape[1] != config.Width || frozenTable.DType != DType.F32)
            {
                throw new ArgumentException($"The token table must be float32 [{config.VocabularySize}, {config.Width}], not {frozenTable.Shape}.", nameof(frozenTable));
            }

            _frozenTable = frozenTable;
        }

        _positions = RegisterModule("positions", new Embedding(config.MaxPositions, config.Width, generator));
        _embeddingNorm = RegisterModule("embedding_norm", new EpsilonLayerNorm(config.Width, config.NormEpsilon));
        _layers = new Layer[config.Layers];
        for (int i = 0; i < _layers.Length; i++)
        {
            _layers[i] = RegisterModule("layers" + i.ToString(CultureInfo.InvariantCulture), new Layer(config, generator));
        }
    }

    /// <summary>The module's shape.</summary>
    public PostNormConfig Config => _config;

    /// <summary>True when the token table is outside the module's trainable state.</summary>
    public bool HasFrozenTable => !_frozenTable.IsEmpty;

    /// <summary>Token ids [batch, length], all positions real; the result is [batch, length, width].</summary>
    /// <param name="input">The ids, Int64 or Int32.</param>
    public override Tensor Forward(Tensor input) => Forward(input, default);

    /// <summary>Runs the encoder over right-padded sequences.</summary>
    /// <param name="ids">Token ids [batch, length], Int64 or Int32.</param>
    /// <param name="lengths">The number of real tokens in each row; empty means every row is full. Padding keys are hidden from attention; the output at padding positions is unspecified.</param>
    /// <returns>Hidden states [batch, length, width].</returns>
    public Tensor Forward(Tensor ids, ReadOnlySpan<int> lengths)
    {
        if (ids.Rank != 2)
        {
            throw new TensorShapeException($"The encoder needs ids [batch, length], not {ids.Shape}.");
        }

        int b = ids.Shape[0], n = ids.Shape[1];
        ModernBertModule.CheckLengths(lengths, b, n);
        if (n + _config.PositionOffset > _config.MaxPositions)
        {
            throw new ArgumentException($"A sequence of {n} tokens needs {n + _config.PositionOffset} positions; the model has {_config.MaxPositions}.");
        }

        var positionIds = new long[n];
        for (int i = 0; i < n; i++)
        {
            positionIds[i] = i + _config.PositionOffset;
        }

        Tensor positionTensor = Tensor.FromSpan<long>(positionIds, Shape.Create(1, n));
        ActivationScope.Track(positionTensor);
        Tensor table = _frozenTable.IsEmpty ? _tokens!.Weight : _frozenTable;
        Tensor h = _embeddingNorm.Forward(Ops.Add(Ops.Embedding(table, ids), _positions.Forward(positionTensor)));
        Tensor mask = ModernBertModule.AttentionMask(b, n, lengths, window: 0);
        foreach (Layer layer in _layers)
        {
            h = layer.Forward(h, mask);
        }

        return h;
    }

    private sealed class Layer : NnModule
    {
        private readonly MultiHeadAttention _attention;
        private readonly EpsilonLayerNorm _norm1;
        private readonly Linear _ff1;
        private readonly Linear _ff2;
        private readonly EpsilonLayerNorm _norm2;

        public Layer(PostNormConfig c, Generator generator)
        {
            _attention = RegisterModule("attn", new MultiHeadAttention(c.Width, c.Heads, generator));
            _norm1 = RegisterModule("norm1", new EpsilonLayerNorm(c.Width, c.NormEpsilon));
            _ff1 = RegisterModule("ff1", new Linear(c.Width, c.FeedForwardWidth, generator));
            _ff2 = RegisterModule("ff2", new Linear(c.FeedForwardWidth, c.Width, generator));
            _norm2 = RegisterModule("norm2", new EpsilonLayerNorm(c.Width, c.NormEpsilon));
        }

        public override Tensor Forward(Tensor input) => Forward(input, default);

        public Tensor Forward(Tensor x, Tensor mask)
        {
            Tensor x1 = _norm1.Forward(Ops.Add(x, _attention.Forward(x, mask)));
            return _norm2.Forward(Ops.Add(x1, _ff2.Forward(Ops.Gelu(_ff1.Forward(x1)))));
        }
    }
}

/// <summary>Layer normalization with scale, shift and a chosen ε (the training library's fixes ε at 1e-5, and BERT uses 1e-12).</summary>
internal sealed class EpsilonLayerNorm : NnModule
{
    private readonly float _epsilon;

    public EpsilonLayerNorm(int features, float epsilon)
    {
        _epsilon = epsilon;
        Tensor ones = Tensor.Create(DType.F32, Shape.Create(features));
        ones.Fill(1f);
        Weight = RegisterParameter("weight", ones);
        Bias = RegisterParameter("bias", Tensor.Zeros(DType.F32, Shape.Create(features)));
    }

    public Tensor Weight { get; }

    public Tensor Bias { get; }

    public override Tensor Forward(Tensor input) => Ops.LayerNorm(input, Weight, Bias, _epsilon);
}
