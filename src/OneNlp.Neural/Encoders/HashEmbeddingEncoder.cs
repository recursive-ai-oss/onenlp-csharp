using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// A small trainable encoder: each token's normalized form is hashed into one of a fixed number of buckets, the bucket's embedding
/// row is looked up, and a linear layer with a ReLU maps it to the output width. It sees one token at a time (no context).
/// </summary>
/// <remarks>
/// <para>
/// This is the "hash embedding" idea of Svenstrup, Hansen and Winther 2017 reduced to one hash function, enough to exercise the
/// encoder seam and train the first heads end to end; the per-language fast tier (several attributes, a window encoder) replaces it.
/// </para>
/// <para>
/// The key of a token is its <see cref="TokenColumn.Norm"/> when set, else its text in lower case (invariant culture). The hash is
/// 64-bit FNV-1a over the UTF-16 code units, so a saved model hashes the same way on every platform and runtime.
/// </para>
/// </remarks>
public sealed class HashEmbeddingEncoder : IEncoder
{
    /// <summary>The kind name used by <see cref="EncoderRegistry"/>: <c>hash-embedding</c>.</summary>
    public const string EncoderKind = "hash-embedding";

    private const int MaxBuckets = 1 << 24;
    private const int MaxWidth = 8192;
    private const long MaxTableValues = 1L << 26;
    private readonly HashEmbeddingModule _module;

    /// <summary>Creates an encoder with seeded random weights.</summary>
    /// <param name="width">The length of each output vector, 1 to 8192.</param>
    /// <param name="buckets">The number of hash buckets (embedding rows), 1 to 16,777,216, with buckets times width at most 67,108,864. Default 8192.</param>
    /// <param name="seed">Seed of the initial weights. Default 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is out of range.</exception>
    public HashEmbeddingEncoder(int width, int buckets = 8192, ulong seed = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(buckets, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(buckets, MaxBuckets);
        if ((long)buckets * width > MaxTableValues)
        {
            throw new ArgumentOutOfRangeException(nameof(buckets), buckets, $"The embedding table (buckets x width) may hold at most {MaxTableValues} values.");
        }

        Width = width;
        Buckets = buckets;
        Seed = seed;
        _module = new HashEmbeddingModule(buckets, width, new Generator(seed));
    }

    /// <inheritdoc/>
    public string Kind => EncoderKind;

    /// <inheritdoc/>
    public int Width { get; }

    /// <summary>The number of hash buckets.</summary>
    public int Buckets { get; }

    /// <summary>The seed the initial weights were drawn from.</summary>
    public ulong Seed { get; }

    /// <inheritdoc/>
    public NnModule? Weights => _module;

    /// <inheritdoc/>
    public void Dispose() => _module.Dispose();

    /// <inheritdoc/>
    public EncodedBatch Encode(ReadOnlySpan<Doc> docs)
    {
        var starts = new int[docs.Length + 1];
        for (int d = 0; d < docs.Length; d++)
        {
            starts[d + 1] = starts[d] + docs[d].Length;
        }

        int total = starts[^1];
        if (total == 0)
        {
            Tensor empty = Tensor.Zeros(DType.F32, Shape.Create(0, Width));
            ActivationScope.Track(empty);
            return new EncodedBatch(empty, starts);
        }

        var ids = new int[total];
        int row = 0;
        foreach (Doc doc in docs)
        {
            for (int t = 0; t < doc.Length; t++)
            {
                ids[row++] = (int)(KeyHash(doc, t) % (ulong)Buckets);
            }
        }

        Tensor idTensor = Tensor.FromSpan<int>(ids, Shape.Create(total));
        ActivationScope.Track(idTensor);
        return new EncodedBatch(_module.Forward(idTensor), starts);
    }

    /// <inheritdoc/>
    public void WriteSettings(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumber("width", Width);
        writer.WriteNumber("buckets", Buckets);
        writer.WriteNumber("seed", Seed);
    }

    internal static IEncoder FromSettings(JsonElement settings) =>
        new HashEmbeddingEncoder(
            JsonSettings.Int(settings, "width", 1, MaxWidth),
            JsonSettings.Int(settings, "buckets", 1, MaxBuckets),
            JsonSettings.ULong(settings, "seed"));

    private static ulong KeyHash(Doc doc, int token)
    {
        int norm = doc.GetColumn(TokenColumn.Norm)[token];
        string key = norm != 0 ? doc.Strings[norm] : doc[token].ToString().ToLowerInvariant();

        // FNV-1a, 64 bit, over UTF-16 code units.
        ulong h = 14695981039346656037UL;
        foreach (char c in key)
        {
            h = (h ^ c) * 1099511628211UL;
        }

        return h;
    }

    private sealed class HashEmbeddingModule : NnModule
    {
        private readonly Embedding _embed;
        private readonly Linear _project;

        public HashEmbeddingModule(int buckets, int width, Generator generator)
        {
            _embed = RegisterModule("embed", new Embedding(buckets, width, generator));
            _project = RegisterModule("project", new Linear(width, width, generator));
        }

        // Input: Int32 bucket ids [N]. Output: [N, width].
        public override Tensor Forward(Tensor input) => Ops.Relu(_project.Forward(_embed.Forward(input)));
    }
}
