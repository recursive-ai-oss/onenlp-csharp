using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>The shape of a ModernBERT-family encoder (Warner et al. 2024, arXiv 2412.13663, §2; mmBERT, Marone et al. 2025).</summary>
/// <remarks>
/// The defaults are those of mmBERT-base: 22 layers of width 768 with 12 heads of 64, a gated feed-forward of inner width 1152,
/// full attention in every third layer and a 128-token sliding window in the rest, rotary base 160,000 in both kinds of layer.
/// </remarks>
public sealed record ModernBertConfig
{
    /// <summary>Number of rows of the token embedding table.</summary>
    public int VocabularySize { get; init; } = 256000;

    /// <summary>Hidden width. Default 768.</summary>
    public int Width { get; init; } = 768;

    /// <summary>Number of transformer layers. Default 22.</summary>
    public int Layers { get; init; } = 22;

    /// <summary>Number of attention heads; <see cref="Width"/> must be divisible by it. Default 12.</summary>
    public int Heads { get; init; } = 12;

    /// <summary>Inner width of each of the two halves of the gated feed-forward. Default 1152.</summary>
    public int FeedForwardWidth { get; init; } = 1152;

    /// <summary>Layer <c>i</c> attends over the whole sequence when <c>i % GlobalEvery == 0</c> (0, 3, 6, …) and over a sliding window otherwise. Default 3.</summary>
    public int GlobalEvery { get; init; } = 3;

    /// <summary>Total width of a local layer's window: a token sees the tokens at most <c>LocalWindow / 2</c> positions away on either side. Default 128.</summary>
    public int LocalWindow { get; init; } = 128;

    /// <summary>Rotary base of the global layers (Su et al. 2021, §3.4.3 uses 10,000). Default 160,000.</summary>
    public float GlobalRopeTheta { get; init; } = 160000f;

    /// <summary>Rotary base of the local layers. Default 160,000.</summary>
    public float LocalRopeTheta { get; init; } = 160000f;

    /// <summary>The ε of every layer normalization. Default 1e-5.</summary>
    public float NormEpsilon { get; init; } = 1e-5f;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(VocabularySize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Width, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(Layers, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Heads, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(FeedForwardWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(GlobalEvery, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(LocalWindow, 2);
        if (Width % Heads != 0 || (Width / Heads) % 2 != 0)
        {
            throw new ArgumentException($"Width {Width} must split into {Heads} heads of even size.");
        }

        if (!(GlobalRopeTheta > 1f) || !(LocalRopeTheta > 1f) || !(NormEpsilon > 0f))
        {
            throw new ArgumentException("The rotary bases must exceed 1 and the epsilon must be positive.");
        }
    }
}

/// <summary>
/// A ModernBERT-family encoder (token embedding, embedding norm, pre-norm layers with rotary attention and a GeGLU feed-forward,
/// final norm) written from OneModel's tensor ops, so its weights train with autodiff.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layer</b> (Warner et al. 2024 §2): <c>x = x + Wo(attention(rotary(Wqkv(norm(x)))))</c>, then
/// <c>x = x + Wo2(gelu(a) · b)</c> where <c>[a, b] = Wi(norm(x))</c> (GeGLU, Shazeer 2020 §2). The first layer skips its attention norm
/// because the embedding norm precedes it. No layer has a bias and the norms have no shift.
/// </para>
/// <para>
/// <b>Attention.</b> Heads of equal size, scale <c>1/√d</c>, rotary position embeddings on queries and keys with the rotate-half pairing
/// of coordinate <c>i</c> with <c>i + d/2</c> (Su et al. 2021, Eq. 34) and frequencies <c>θ^(−2i/d)</c>. Global layers see every real token; local layers
/// see the tokens within <see cref="ModernBertConfig.LocalWindow"/>/2 positions. Padding keys are always hidden.
/// </para>
/// <para>
/// <b>Embedding table.</b> With <c>freezeEmbeddings</c> the token table is a plain tensor outside the module's state, not trained and
/// not saved (a 256,000 × 768 table would be 786 MB of weights, gradients and two optimizer moments); it must be supplied again when the module is rebuilt.
/// </para>
/// A module is not thread-safe.
/// </remarks>
public sealed class ModernBertModule : NnModule
{
    private readonly ModernBertConfig _config;
    private readonly Tensor _frozenTable;
    private readonly Embedding? _tokens;
    private readonly BiasFreeLayerNorm _embeddingNorm;
    private readonly Layer[] _layers;
    private readonly BiasFreeLayerNorm _finalNorm;

    /// <summary>Creates the module with random weights.</summary>
    /// <param name="config">The shape.</param>
    /// <param name="seed">Seed of the initial values.</param>
    /// <param name="frozenTable">The token embedding table [VocabularySize, Width] to use without training it, or an empty tensor for a trainable table of the module's own. The module takes ownership.</param>
    /// <exception cref="ArgumentException">The configuration or the table's shape is invalid.</exception>
    public ModernBertModule(ModernBertConfig config, ulong seed, Tensor frozenTable = default)
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

        _embeddingNorm = RegisterModule("embedding_norm", new BiasFreeLayerNorm(config.Width, config.NormEpsilon));
        _layers = new Layer[config.Layers];
        for (int i = 0; i < _layers.Length; i++)
        {
            _layers[i] = RegisterModule("layers" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), new Layer(config, i, generator));
        }

        _finalNorm = RegisterModule("final_norm", new BiasFreeLayerNorm(config.Width, config.NormEpsilon));
    }

    /// <summary>The module's shape.</summary>
    public ModernBertConfig Config => _config;

    /// <summary>True when the token table is outside the module's trainable state.</summary>
    public bool HasFrozenTable => !_frozenTable.IsEmpty;

    /// <summary>Token ids [batch, length] (Int64 or Int32), all positions real; the result is [batch, length, width].</summary>
    /// <param name="input">The ids.</param>
    public override Tensor Forward(Tensor input) => Forward(input, default);

    /// <summary>Runs the encoder over right-padded sequences.</summary>
    /// <param name="ids">Token ids [batch, length], Int64 or Int32.</param>
    /// <param name="lengths">The number of real tokens in each row (at least 1, at most length); empty means every row is full. Padding keys are hidden from attention; the output at padding positions is unspecified.</param>
    /// <returns>Hidden states [batch, length, width].</returns>
    public Tensor Forward(Tensor ids, ReadOnlySpan<int> lengths)
    {
        if (ids.Rank != 2)
        {
            throw new TensorShapeException($"The encoder needs ids [batch, length], not {ids.Shape}.");
        }

        int b = ids.Shape[0], n = ids.Shape[1];
        CheckLengths(lengths, b, n);
        Tensor table = _frozenTable.IsEmpty ? _tokens!.Weight : _frozenTable;
        Tensor h = _embeddingNorm.Forward(Ops.Embedding(table, ids));

        int headDim = _config.Width / _config.Heads;
        (Tensor cosGlobal, Tensor sinGlobal) = RotaryTable(n, headDim, _config.GlobalRopeTheta);
        (Tensor cosLocal, Tensor sinLocal) = _config.LocalRopeTheta == _config.GlobalRopeTheta ? (cosGlobal, sinGlobal) : RotaryTable(n, headDim, _config.LocalRopeTheta);
        Tensor globalMask = AttentionMask(b, n, lengths, window: 0);
        Tensor localMask = AttentionMask(b, n, lengths, _config.LocalWindow / 2);
        for (int i = 0; i < _layers.Length; i++)
        {
            bool global = i % _config.GlobalEvery == 0;
            h = _layers[i].Forward(h, global ? globalMask : localMask, global ? cosGlobal : cosLocal, global ? sinGlobal : sinLocal);
        }

        return _finalNorm.Forward(h);
    }

    internal static void CheckLengths(ReadOnlySpan<int> lengths, int batch, int length)
    {
        if (lengths.IsEmpty)
        {
            return;
        }

        if (lengths.Length != batch)
        {
            throw new ArgumentException($"There are {lengths.Length} lengths for {batch} rows.", nameof(lengths));
        }

        foreach (int l in lengths)
        {
            if (l < 1 || l > length)
            {
                throw new ArgumentOutOfRangeException(nameof(lengths), $"A row length {l} is outside 1 to {length}.");
            }
        }
    }

    /// <summary>
    /// The additive attention mask, or an empty tensor when nothing is hidden. Hidden entries are −1e30 rather than −∞ so a row whose
    /// every key is hidden (a padding query) softmaxes to a uniform row instead of NaN.
    /// </summary>
    internal static Tensor AttentionMask(int batch, int n, ReadOnlySpan<int> lengths, int window)
    {
        bool padded = false;
        foreach (int l in lengths)
        {
            padded |= l < n;
        }

        bool windowed = window > 0 && n > window + 1;
        if (!padded && !windowed)
        {
            return default;
        }

        int rows = padded ? batch : 1;
        Tensor mask = Tensor.Create(DType.F32, padded ? Shape.Create(batch, n, n) : Shape.Create(n, n));
        Span<float> m = mask.MutableData<float>();
        for (int r = 0; r < rows; r++)
        {
            int real = padded ? lengths[r] : n;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    bool visible = j < real && (!windowed || Math.Abs(i - j) <= window);
                    m[(((r * n) + i) * n) + j] = visible ? 0f : -1e30f;
                }
            }
        }

        ActivationScope.Track(mask);
        return mask;
    }

    /// <summary>Rotary cosine and sine tables [n, headDim/2]: the angle of position <c>p</c> and pair <c>i</c> is <c>p · θ^(−2i/headDim)</c> (Su et al. 2021 §3.2.2).</summary>
    internal static (Tensor Cos, Tensor Sin) RotaryTable(int n, int headDim, float theta)
    {
        int half = headDim / 2;
        Tensor cos = Tensor.Create(DType.F32, Shape.Create(n, half));
        Tensor sin = Tensor.Create(DType.F32, Shape.Create(n, half));
        Span<float> c = cos.MutableData<float>();
        Span<float> s = sin.MutableData<float>();
        var inverse = new float[half];
        for (int i = 0; i < half; i++)
        {
            inverse[i] = (float)Math.Pow(theta, -2.0 * i / headDim);
        }

        for (int p = 0; p < n; p++)
        {
            for (int i = 0; i < half; i++)
            {
                float angle = p * inverse[i];
                c[(p * half) + i] = MathF.Cos(angle);
                s[(p * half) + i] = MathF.Sin(angle);
            }
        }

        ActivationScope.Track(cos);
        ActivationScope.Track(sin);
        return (cos, sin);
    }

    /// <summary>Rotates the two halves of each head vector by the position's angle: <c>x·cos + rotateHalf(x)·sin</c> with <c>rotateHalf([x1, x2]) = [−x2, x1]</c>.</summary>
    internal static Tensor ApplyRotary(Tensor x, Tensor cos, Tensor sin)
    {
        int d = x.Shape[3];
        int half = d / 2;
        Tensor x1 = x.Slice(3, 0, half);
        Tensor x2 = x.Slice(3, half, d);
        Tensor first = Ops.Sub(Ops.Mul(x1, cos), Ops.Mul(x2, sin));
        Tensor second = Ops.Add(Ops.Mul(x2, cos), Ops.Mul(x1, sin));
        return Ops.Concat([first, second], 3);
    }

    private sealed class Layer : NnModule
    {
        private readonly int _heads;
        private readonly int _inner;
        private readonly BiasFreeLayerNorm? _attentionNorm;
        private readonly Linear _qkv;
        private readonly Linear _attentionOut;
        private readonly BiasFreeLayerNorm _mlpNorm;
        private readonly Linear _wi;
        private readonly Linear _mlpOut;

        public Layer(ModernBertConfig c, int index, Generator generator)
        {
            _heads = c.Heads;
            _inner = c.FeedForwardWidth;
            if (index > 0)
            {
                _attentionNorm = RegisterModule("attn_norm", new BiasFreeLayerNorm(c.Width, c.NormEpsilon));
            }

            _qkv = RegisterModule("wqkv", new Linear(c.Width, 3 * c.Width, generator, bias: false));
            _attentionOut = RegisterModule("wo", new Linear(c.Width, c.Width, generator, bias: false));
            _mlpNorm = RegisterModule("mlp_norm", new BiasFreeLayerNorm(c.Width, c.NormEpsilon));
            _wi = RegisterModule("wi", new Linear(c.Width, 2 * c.FeedForwardWidth, generator, bias: false));
            _mlpOut = RegisterModule("mlp_wo", new Linear(c.FeedForwardWidth, c.Width, generator, bias: false));
        }

        public override Tensor Forward(Tensor input) => throw new NotSupportedException("Use the overload with a mask and rotary tables.");

        public Tensor Forward(Tensor x, Tensor mask, Tensor cos, Tensor sin)
        {
            int b = x.Shape[0], n = x.Shape[1], width = x.Shape[2], d = width / _heads;
            Tensor a = _attentionNorm is null ? x : _attentionNorm.Forward(x);

            // Wqkv's output is laid out [3, heads, d] (observed in the exported graph of mmBERT-base: reshape to [B, L, 3, H, D], then split on axis 2).
            Tensor qkv = _qkv.Forward(a).Reshape(b, n, 3, _heads, d);
            Tensor q = ApplyRotary(qkv.Select(2, 0).Permute(0, 2, 1, 3), cos, sin);
            Tensor k = ApplyRotary(qkv.Select(2, 1).Permute(0, 2, 1, 3), cos, sin);
            Tensor v = qkv.Select(2, 2).Permute(0, 2, 1, 3);
            Tensor context = Ops.Attention(q, k, v, mask).Permute(0, 2, 1, 3).Reshape(b, n, width);
            x = Ops.Add(x, _attentionOut.Forward(context));

            // GeGLU (Shazeer 2020 §2): the first half goes through GELU and gates the second half (observed in the same graph).
            Tensor gated = _wi.Forward(_mlpNorm.Forward(x));
            Tensor activated = Ops.Mul(Ops.Gelu(gated.Slice(2, 0, _inner)), gated.Slice(2, _inner, 2 * _inner));
            return Ops.Add(x, _mlpOut.Forward(activated));
        }
    }
}

/// <summary>Layer normalization with a scale and no shift (ModernBERT's norms have no bias).</summary>
internal sealed class BiasFreeLayerNorm : NnModule
{
    private readonly float _epsilon;

    public BiasFreeLayerNorm(int features, float epsilon)
    {
        _epsilon = epsilon;
        Tensor ones = Tensor.Create(DType.F32, Shape.Create(features));
        ones.Fill(1f);
        Weight = RegisterParameter("weight", ones);
    }

    public Tensor Weight { get; }

    public override Tensor Forward(Tensor input)
    {
        // A zero shift that never trains: the fused op takes both vectors, so the missing bias is a constant of zeros.
        Tensor zero = Tensor.Zeros(DType.F32, Weight.Shape);
        ActivationScope.Track(zero);
        return Ops.LayerNorm(input, Weight, zero, _epsilon);
    }
}
