using System.Runtime.CompilerServices;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// The fast-tier encoder: hash embeddings of four attributes of each token (normalized form, prefix, suffix, shape) mixed by a
/// linear layer, then a stack of residual window layers, each of which looks at the vector of the token on either side.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hash embeddings.</b> Svenstrup, Hansen and Winther 2017, "Hash Embeddings for Efficient Word Representations": a token is
/// hashed with <see cref="HashesPerToken"/> hash functions into the rows of one small table and its vector is the sum of those
/// rows, so a vocabulary of any size fits a fixed table and rare words collide with different partners under each function.
/// (The paper also learns a weight per hash function per word; with no vocabulary to hold them, the weights here are equal.)
/// Each attribute has its own table. The attributes are the lower-cased text (each UTF-16 unit through
/// <see cref="char.ToLowerInvariant(char)"/>), the first <see cref="PrefixLength"/> characters, the last
/// <see cref="SuffixLength"/> characters (both of the original text), and the shape: upper case letters as <c>X</c>, lower case as
/// <c>x</c>, digits as <c>d</c>, anything else as itself, with a run of the same shape character cut after 4. The hash is 64-bit
/// FNV-1a over the UTF-16 units, seeded per attribute and finished with a SplitMix64 mix; hash 0 uses the low half and hash 1 the
/// high half, each modulo the table's row count. The result is the same on every platform.
/// </para>
/// <para>
/// <b>Window layers.</b> Collobert et al. 2011 §3.3 (window approach) with the residual form and layer normalization of the
/// published spaCy tok2vec description: <c>x ← x + LayerNorm(Gelu(Linear([x[t-1], x[t], x[t+1]])))</c>. A document's edges are padded
/// with zero vectors and nothing crosses from one document to the next, so a document's vectors do not depend on its batch.
/// After <see cref="Depth"/> layers a token sees <see cref="Depth"/> tokens either side.
/// </para>
/// <para>
/// <b>One pass for many heads.</b> Heads that share one encoder instance share one encoding: in training the trainer encodes the batch
/// once and sums the heads' gradients into the encoder; at inference (no gradient recording, evaluation mode) the output of each
/// document is kept with the document, so the second and later heads of a pipeline find it there. The cache assumes the encoder's weights do not change
/// while a document is alive, and is not used while the weights are in training mode.
/// </para>
/// </remarks>
public sealed class FastEncoder : IEncoder
{
    /// <summary>The kind name used by <see cref="EncoderRegistry"/>: <c>fast</c>.</summary>
    public const string EncoderKind = "fast";

    /// <summary>The default number of hash functions per attribute.</summary>
    public const int HashesPerToken = 2;

    /// <summary>The number of leading characters hashed as the prefix attribute.</summary>
    public const int PrefixLength = 1;

    /// <summary>The number of trailing characters hashed as the suffix attribute.</summary>
    public const int SuffixLength = 3;

    private const int BaseAttributes = 4;
    private const int MaxAffix = 8;
    private const int MaxHashes = 4;
    private const int MaxWidth = 2048;
    private const int MaxDepth = 32;
    private const int MaxRows = 1 << 22;
    private const long MaxTableValues = 1L << 25;
    private const int ShapeRun = 4;
    private static readonly ulong[] AttributeSeeds = [0x9E3779B97F4A7C15UL, 0xC2B2AE3D27D4EB4FUL, 0x165667B19E3779F9UL, 0x27D4EB2F165667C5UL, 0x85EBCA77C2B2AE63UL];

    private readonly int[] _rows;
    private readonly int _attributes;
    private readonly FastModule _module;
    private readonly ConditionalWeakTable<Doc, CachedRows> _cache = [];

    /// <summary>Creates an encoder with seeded random weights.</summary>
    /// <param name="width">The length of each output vector, 8 to 2048. Default 96.</param>
    /// <param name="depth">The number of window layers, 0 to 32. Default 4.</param>
    /// <param name="normRows">Rows of the table for the normalized form. Default 20,000.</param>
    /// <param name="prefixRows">Rows of the table for the prefix. Default 2,000.</param>
    /// <param name="suffixRows">Rows of the table for the suffix. Default 5,000.</param>
    /// <param name="shapeRows">Rows of the table for the shape. Default 2,500.</param>
    /// <param name="seed">Seed of the initial weights. Default 0.</param>
    /// <param name="prefixLength">Characters hashed as the prefix, 1 to 8. Default <see cref="PrefixLength"/>.</param>
    /// <param name="suffixLength">Characters hashed as the suffix, 1 to 8. Default <see cref="SuffixLength"/>.</param>
    /// <param name="casedRows">Rows of a fifth table for the case-preserving text; 0 (the default) leaves that attribute out.</param>
    /// <param name="embedWidth">Width of the embedding rows, 8 to 2048; 0 (the default) means <paramref name="width"/>. The attributes are joined and a linear layer maps them to <paramref name="width"/>.</param>
    /// <param name="hashes">Hash functions per attribute, 1 to 4: a token's vector for an attribute is the sum of that many rows. Default <see cref="HashesPerToken"/>.</param>
    /// <param name="plain">Join the attributes with a ReLU and no layer normalization, as the original hash-embedding recipes do; the window layers keep theirs. Default false (GELU and layer normalization).</param>
    /// <param name="turkicCase">Lower-case the norm attribute with the Turkish and Azeri rules (<c>I</c> to <c>ı</c>, <c>İ</c> to <c>i</c>) instead of the invariant ones. Default false; a file records the setting only when it is on, so other languages' files do not change.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is out of range; each table has 1 to 4,194,304 rows and the tables hold at most 33,554,432 values together.</exception>
    public FastEncoder(int width = 96, int depth = 4, int normRows = 20000, int prefixRows = 2000, int suffixRows = 5000, int shapeRows = 2500, ulong seed = 0, int prefixLength = PrefixLength, int suffixLength = SuffixLength, int casedRows = 0, int embedWidth = 0, bool plain = false, int hashes = HashesPerToken, bool turkicCase = false)
    {
        TurkicCase = turkicCase;
        ArgumentOutOfRangeException.ThrowIfLessThan(hashes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hashes, MaxHashes);
        Hashes = hashes;
        ArgumentOutOfRangeException.ThrowIfLessThan(prefixLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(prefixLength, MaxAffix);
        ArgumentOutOfRangeException.ThrowIfLessThan(suffixLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(suffixLength, MaxAffix);
        ArgumentOutOfRangeException.ThrowIfNegative(casedRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(casedRows, MaxRows);
        if (embedWidth == 0)
        {
            embedWidth = width;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(embedWidth, 8);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(embedWidth, MaxWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 8);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, MaxDepth);
        _rows = casedRows > 0 ? [normRows, prefixRows, suffixRows, shapeRows, casedRows] : [normRows, prefixRows, suffixRows, shapeRows];
        _attributes = _rows.Length;
        long values = 0;
        foreach (int rows in _rows)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxRows);
            values += (long)rows * embedWidth;
        }

        // A settings object read from a file must not be able to ask for gigabytes before the weights are looked at.
        if (values > MaxTableValues)
        {
            throw new ArgumentOutOfRangeException(nameof(normRows), values, $"The four embedding tables (rows x width) may hold at most {MaxTableValues} values together.");
        }

        Width = width;
        Depth = depth;
        Seed = seed;
        PrefixChars = prefixLength;
        SuffixChars = suffixLength;
        EmbedWidth = embedWidth;
        Plain = plain;
        _module = new FastModule(width, embedWidth, depth, plain, hashes, _rows, new Generator(seed));
    }

    /// <summary>
    /// The configuration of the English fast tier, one encoder for every head: 64-wide hash embeddings (one hash function, 32,768 rows
    /// per attribute) of the lower-cased form, the first three and last three characters and the shape, joined and mixed to 256, then
    /// two window layers. Chosen on the development sets of UNER English-EWT (entities) and the tagging recipe, where a narrow,
    /// low-collision hash table with three-character affixes did better than the first configuration (96 wide, one-character prefix,
    /// small tables, four window layers).
    /// </summary>
    /// <param name="seed">Seed of the initial weights. Default 0.</param>
    /// <returns>A new encoder.</returns>
    public static FastEncoder English(ulong seed = 0) =>
        new(width: 256, depth: 2, normRows: 32768, prefixRows: 32768, suffixRows: 32768, shapeRows: 32768, seed: seed, prefixLength: 3, suffixLength: 3, casedRows: 0, embedWidth: 64, plain: false, hashes: 1);

    /// <inheritdoc/>
    public string Kind => EncoderKind;

    /// <inheritdoc/>
    public int Width { get; }

    /// <summary>The number of window layers.</summary>
    public int Depth { get; }

    /// <summary>The number of hash functions per attribute.</summary>
    public int Hashes { get; }

    /// <summary>Whether the attributes are joined by a ReLU without layer normalization.</summary>
    public bool Plain { get; }

    /// <summary>Characters hashed as the prefix.</summary>
    public int PrefixChars { get; }

    /// <summary>Characters hashed as the suffix.</summary>
    public int SuffixChars { get; }

    /// <summary>Whether the norm attribute is lower-cased with the Turkish rules (<c>I</c> to <c>ı</c>, <c>İ</c> to <c>i</c>).</summary>
    public bool TurkicCase { get; }

    /// <summary>Width of the embedding rows before the joining layer.</summary>
    public int EmbedWidth { get; }

    /// <summary>The seed the initial weights were drawn from.</summary>
    public ulong Seed { get; }

    /// <summary>The rows of the norm, prefix, suffix and shape tables, then the cased table when there is one.</summary>
    public IReadOnlyList<int> TableRows => _rows;

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

        if (starts[^1] == 0)
        {
            Tensor empty = Tensor.Zeros(DType.F32, Shape.Create(0, Width));
            ActivationScope.Track(empty);
            return new EncodedBatch(empty, starts);
        }

        if (GradMode.IsEnabled || _module.IsTraining)
        {
            return new EncodedBatch(Compute(docs, starts), starts);
        }

        return EncodeCached(docs, starts);
    }

    /// <inheritdoc/>
    public void WriteSettings(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumber("width", Width);
        writer.WriteNumber("depth", Depth);
        writer.WriteNumber("normRows", _rows[0]);
        writer.WriteNumber("prefixRows", _rows[1]);
        writer.WriteNumber("suffixRows", _rows[2]);
        writer.WriteNumber("shapeRows", _rows[3]);
        writer.WriteNumber("seed", Seed);

        // Written only when they differ from the first file version, so an unchanged configuration saves the same bytes as before.
        if (PrefixChars != PrefixLength)
        {
            writer.WriteNumber("prefixLength", PrefixChars);
        }

        if (SuffixChars != SuffixLength)
        {
            writer.WriteNumber("suffixLength", SuffixChars);
        }

        if (_attributes > BaseAttributes)
        {
            writer.WriteNumber("casedRows", _rows[4]);
        }

        if (Plain)
        {
            writer.WriteNumber("plain", 1);
        }

        if (Hashes != HashesPerToken)
        {
            writer.WriteNumber("hashes", Hashes);
        }

        if (EmbedWidth != Width)
        {
            writer.WriteNumber("embedWidth", EmbedWidth);
        }

        if (TurkicCase)
        {
            writer.WriteNumber("turkicCase", 1);
        }
    }

    internal static IEncoder FromSettings(JsonElement s)
    {
        // 0 means "the same as width"; otherwise the same lower bound as width.
        int embedWidth = JsonSettings.OptionalInt(s, "embedWidth", 0, MaxWidth, 0);
        if (embedWidth is > 0 and < 8)
        {
            throw new InvalidDataException("'embedWidth' must be 0 or an integer from 8 to " + MaxWidth + ".");
        }

        return new FastEncoder(
            JsonSettings.Int(s, "width", 8, MaxWidth),
            JsonSettings.Int(s, "depth", 0, MaxDepth),
            JsonSettings.Int(s, "normRows", 1, MaxRows),
            JsonSettings.Int(s, "prefixRows", 1, MaxRows),
            JsonSettings.Int(s, "suffixRows", 1, MaxRows),
            JsonSettings.Int(s, "shapeRows", 1, MaxRows),
            JsonSettings.ULong(s, "seed"),
            JsonSettings.OptionalInt(s, "prefixLength", 1, MaxAffix, PrefixLength),
            JsonSettings.OptionalInt(s, "suffixLength", 1, MaxAffix, SuffixLength),
            JsonSettings.OptionalInt(s, "casedRows", 0, MaxRows, 0),
            embedWidth,
            JsonSettings.OptionalInt(s, "plain", 0, 1, 0) == 1,
            JsonSettings.OptionalInt(s, "hashes", 1, MaxHashes, HashesPerToken),
            JsonSettings.OptionalInt(s, "turkicCase", 0, 1, 0) == 1);
    }

    private static ulong Mix(ulong h)
    {
        // SplitMix64 finalizer: FNV-1a leaves the low bits of short keys poorly spread.
        h += 0x9E3779B97F4A7C15UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        return h ^ (h >> 31);
    }

    private static ulong Fnv(ulong h, char c) => (h ^ c) * 1099511628211UL;

    private ulong NormHash(ReadOnlySpan<char> text)
    {
        ulong h = 14695981039346656037UL ^ AttributeSeeds[0];
        foreach (char c in text)
        {
            // Turkish and Azeri keep the dotted and dotless i apart: I is ı and İ is i (Unicode SpecialCasing, tr and az).
            h = Fnv(h, TurkicCase ? (c == 'I' ? '\u0131' : c == '\u0130' ? 'i' : char.ToLowerInvariant(c)) : char.ToLowerInvariant(c));
        }

        return Mix(h);
    }

    private static ulong SliceHash(ReadOnlySpan<char> text, int attribute)
    {
        ulong h = 14695981039346656037UL ^ AttributeSeeds[attribute];
        foreach (char c in text)
        {
            h = Fnv(h, c);
        }

        return Mix(h);
    }

    private static ulong ShapeHash(ReadOnlySpan<char> text)
    {
        ulong h = 14695981039346656037UL ^ AttributeSeeds[3];
        char last = '\0';
        int run = 0;
        foreach (char c in text)
        {
            char shape = char.IsUpper(c) ? 'X' : char.IsLower(c) ? 'x' : char.IsDigit(c) ? 'd' : c;
            run = shape == last ? run + 1 : 1;
            last = shape;
            if (run <= ShapeRun)
            {
                h = Fnv(h, shape);
            }
        }

        return Mix(h);
    }

    private ReadOnlySpan<char> Prefix(ReadOnlySpan<char> t) => t.Length <= PrefixChars ? t : t[..PrefixChars];

    private ReadOnlySpan<char> Suffix(ReadOnlySpan<char> t) => t.Length <= SuffixChars ? t : t[^SuffixChars..];

    private Tensor Compute(ReadOnlySpan<Doc> docs, int[] starts)
    {
        int n = starts[^1];

        // ids[(attribute * Hashes + hash) * n + row]
        var ids = new int[_attributes * Hashes * n];
        var prev = new int[n];
        var next = new int[n];
        int row = 0;
        foreach (Doc doc in docs)
        {
            for (int t = 0; t < doc.Length; t++, row++)
            {
                ReadOnlySpan<char> text = doc[t].Text;
                Put(ids, n, 0, row, NormHash(text));
                Put(ids, n, 1, row, SliceHash(Prefix(text), 1));
                Put(ids, n, 2, row, SliceHash(Suffix(text), 2));
                Put(ids, n, 3, row, ShapeHash(text));
                if (_attributes > BaseAttributes)
                {
                    Put(ids, n, 4, row, SliceHash(text, 4));
                }

                prev[row] = t == 0 ? n : row - 1;
                next[row] = t == doc.Length - 1 ? n : row + 1;
            }

        }

        var idTensors = new Tensor[_attributes * Hashes];
        for (int k = 0; k < idTensors.Length; k++)
        {
            idTensors[k] = Tensor.FromSpan<int>(ids.AsSpan(k * n, n), Shape.Create(n));
            ActivationScope.Track(idTensors[k]);
        }

        Tensor prevTensor = Tensor.FromSpan<int>(prev, Shape.Create(n));
        Tensor nextTensor = Tensor.FromSpan<int>(next, Shape.Create(n));
        Tensor zero = Tensor.Zeros(DType.F32, Shape.Create(1, Width));
        ActivationScope.Track(prevTensor);
        ActivationScope.Track(nextTensor);
        ActivationScope.Track(zero);
        return _module.Run(idTensors, prevTensor, nextTensor, zero);
    }

    private void Put(int[] ids, int n, int attribute, int row, ulong hash)
    {
        uint rows = (uint)_rows[attribute];
        for (int h = 0; h < Hashes; h++)
        {
            // Hashes 0 and 1 are the low and high halves of the 64-bit hash; further ones come from a re-mixed hash.
            ulong source = h < 2 ? hash : Mix(hash + (ulong)h);
            uint part = h % 2 == 0 ? (uint)source : (uint)(source >> 32);
            ids[(((attribute * Hashes) + h) * n) + row] = (int)(part % rows);
        }
    }

    private EncodedBatch EncodeCached(ReadOnlySpan<Doc> docs, int[] starts)
    {
        var missing = new List<Doc>();
        foreach (Doc doc in docs)
        {
            if (doc.Length > 0 && !(_cache.TryGetValue(doc, out CachedRows? c) && c.Length == doc.Length) && !missing.Contains(doc))
            {
                missing.Add(doc);
            }
        }

        if (missing.Count > 0)
        {
            var missingStarts = new int[missing.Count + 1];
            for (int i = 0; i < missing.Count; i++)
            {
                missingStarts[i + 1] = missingStarts[i] + missing[i].Length;
            }

            using var scope = new ActivationScope();
            Tensor computed = Compute([.. missing], missingStarts);
            ReadOnlySpan<float> data = computed.Data<float>();
            for (int i = 0; i < missing.Count; i++)
            {
                float[] copy = data.Slice(missingStarts[i] * Width, missing[i].Length * Width).ToArray();
                _cache.AddOrUpdate(missing[i], new CachedRows(missing[i].Length, copy));
            }
        }

        var all = new float[starts[^1] * Width];
        for (int d = 0; d < docs.Length; d++)
        {
            if (docs[d].Length > 0)
            {
                _cache.TryGetValue(docs[d], out CachedRows? c);
                c!.Rows.CopyTo(all.AsSpan(starts[d] * Width));
            }
        }

        Tensor result = Tensor.FromSpan<float>(all, Shape.Create(starts[^1], Width));
        ActivationScope.Track(result);
        return new EncodedBatch(result, starts);
    }

    private sealed record CachedRows(int Length, float[] Rows);

    private sealed class FastModule : NnModule
    {
        private readonly Embedding[] _tables;
        private readonly Linear _mix;
        private readonly LayerNorm? _mixNorm;
        private readonly bool _plain;
        private readonly int _hashes;
        private readonly Linear[] _windows;
        private readonly LayerNorm[] _norms;

        public FastModule(int width, int embedWidth, int depth, bool plain, int hashes, int[] rows, Generator generator)
        {
            _plain = plain;
            _hashes = hashes;
            _tables = new Embedding[rows.Length];
            string[] names = ["norm", "prefix", "suffix", "shape", "cased"];
            for (int a = 0; a < rows.Length; a++)
            {
                _tables[a] = RegisterModule("embed_" + names[a], new Embedding(rows[a], embedWidth, generator));
            }

            _mix = RegisterModule("mix", new Linear(rows.Length * embedWidth, width, generator));
            _mixNorm = plain ? null : RegisterModule("mix_norm", new LayerNorm(width));
            _windows = new Linear[depth];
            _norms = new LayerNorm[depth];
            for (int i = 0; i < depth; i++)
            {
                _windows[i] = RegisterModule("window" + i, new Linear(3 * width, width, generator));
                _norms[i] = RegisterModule("window_norm" + i, new LayerNorm(width));
            }
        }

        public override Tensor Forward(Tensor input) => throw new NotSupportedException("Call Run.");

        // ids: Int32 [n] per (attribute, hash); prev/next: Int32 [n] row of the neighbour, or n for the zero row.
        public Tensor Run(Tensor[] ids, Tensor prev, Tensor next, Tensor zero)
        {
            var parts = new Tensor[_tables.Length];
            for (int a = 0; a < parts.Length; a++)
            {
                Tensor sum = _tables[a].Forward(ids[a * _hashes]);
                for (int h = 1; h < _hashes; h++)
                {
                    sum = Ops.Add(sum, _tables[a].Forward(ids[(a * _hashes) + h]));
                }

                parts[a] = sum;
            }

            Tensor joined = _mix.Forward(Ops.Concat(parts, 1));
            Tensor x = _plain ? Ops.Relu(joined) : _mixNorm!.Forward(Ops.Gelu(joined));
            for (int i = 0; i < _windows.Length; i++)
            {
                Tensor padded = Ops.Concat([x, zero], 0);
                Tensor left = Ops.Embedding(padded, prev);
                Tensor right = Ops.Embedding(padded, next);
                Tensor window = Ops.Concat([left, x, right], 1);
                x = Ops.Add(x, _norms[i].Forward(Ops.Gelu(_windows[i].Forward(window))));
            }

            return x;
        }
    }
}
