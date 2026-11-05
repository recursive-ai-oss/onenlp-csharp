using System.Buffers;
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
    private const int MaxRecurrentLayers = 4;
    private const int MaxRows = 1 << 22;
    private const long MaxTableValues = 1L << 25;
    private const int ShapeRun = 4;
    private static readonly ulong[] AttributeSeeds = [0x9E3779B97F4A7C15UL, 0xC2B2AE3D27D4EB4FUL, 0x165667B19E3779F9UL, 0x27D4EB2F165667C5UL, 0x85EBCA77C2B2AE63UL, 0xD6E8FEB86659FD93UL, 0xA0761D6478BD642FUL, 0xE7037ED1A0B428DBUL, 0x8EBC6AF09C88C6E3UL];
    private const ulong EdgeHash = 0x5851F42D4C957F2DUL;

    private readonly int[] _rows;
    private readonly int _attributes;
    private readonly int _neighbourStart;
    private readonly int _contextStart;
    private readonly int _bigramStart;
    private readonly FastModule _module;
    private readonly ThreadLocal<InferenceSlot?> _slot = new();
    private long _epoch;

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
    /// <param name="relu">Use ReLU instead of GELU in the joining layer and the window layers. ReLU is a comparison; GELU needs an error function per value and costs several times more than the matrix products around it on small widths. Default false.</param>
    /// <param name="bigramRows">Rows of two more tables that hash the lower-cased text of a token together with the previous token, and with the next token: word pairs, the features a perceptron tagger is built on, at the price of a table lookup each. 0 (the default) leaves them out.</param>
    /// <param name="neighbourRows">Rows of four extra tables that hash the lower-cased text and the last <paramref name="suffixLength"/> characters of the previous and of the next token (the document edge hashes to a fixed row). They give every token its neighbours' words for the price of table lookups, where a window layer pays a matrix product. 0 (the default) leaves them out.</param>
    /// <param name="contextRows">Rows of four more tables that hash the lower-cased text of the token two before and two after, and the shape of the previous and of the next token: wider context for the price of table lookups. 0 (the default) leaves them out.</param>
    /// <param name="plain">Join the attributes with a ReLU and no layer normalization, as the original hash-embedding recipes do; the window layers keep theirs. Default false (GELU and layer normalization).</param>
    /// <param name="turkicCase">Lower-case the norm attribute with the Turkish and Azeri rules (<c>I</c> to <c>ı</c>, <c>İ</c> to <c>i</c>) instead of the invariant ones. Default false; a file records the setting only when it is on, so other languages' files do not change.</param>
    /// <param name="recurrentLayers">Layers of a bidirectional LSTM run over each document after the window layers, 0 to 4; 0 (the default) means none. Its output is added to the window output, so every token sees the whole document; a window layer only sees its neighbours. A file records the setting only when it is on.</param>
    /// <param name="recurrentSize">Hidden size of each LSTM direction, 0 to 2048; 0 (the default) means half the width. A smaller size is cheaper (the cost grows with its square and with the width) and a linear layer maps the two directions back to the width.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is out of range; each table has 1 to 4,194,304 rows and the tables hold at most 33,554,432 values together.</exception>
    public FastEncoder(int width = 96, int depth = 4, int normRows = 20000, int prefixRows = 2000, int suffixRows = 5000, int shapeRows = 2500, ulong seed = 0, int prefixLength = PrefixLength, int suffixLength = SuffixLength, int casedRows = 0, int embedWidth = 0, bool plain = false, int hashes = HashesPerToken, bool relu = false, int neighbourRows = 0, int bigramRows = 0, int contextRows = 0, bool turkicCase = false, int recurrentLayers = 0, int recurrentSize = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(recurrentLayers, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(recurrentLayers, MaxRecurrentLayers);
        ArgumentOutOfRangeException.ThrowIfLessThan(recurrentSize, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(recurrentSize, MaxWidth);
        if (recurrentLayers > 0 && recurrentSize == 0 && width % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A recurrent layer splits the width between its two directions, so the width must be even.");
        }

        RecurrentLayers = recurrentLayers;
        RecurrentSize = recurrentSize;
        TurkicCase = turkicCase;
        ArgumentOutOfRangeException.ThrowIfNegative(contextRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(contextRows, MaxRows);
        ArgumentOutOfRangeException.ThrowIfNegative(neighbourRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(neighbourRows, MaxRows);
        ArgumentOutOfRangeException.ThrowIfNegative(bigramRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bigramRows, MaxRows);
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
        var rowList = new List<int> { normRows, prefixRows, suffixRows, shapeRows };
        if (casedRows > 0)
        {
            rowList.Add(casedRows);
        }

        _neighbourStart = rowList.Count;
        if (neighbourRows > 0)
        {
            // previous norm, next norm, previous suffix, next suffix
            rowList.AddRange([neighbourRows, neighbourRows, neighbourRows, neighbourRows]);
        }

        _contextStart = rowList.Count;
        if (contextRows > 0)
        {
            // lower-cased text two tokens before and two after, and the shape of the previous and of the next token
            rowList.AddRange([contextRows, contextRows, contextRows, contextRows]);
        }

        _bigramStart = rowList.Count;
        if (bigramRows > 0)
        {
            // previous+current, current+next
            rowList.AddRange([bigramRows, bigramRows]);
        }

        _rows = [.. rowList];
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
        Relu = relu;
        NeighbourRows = neighbourRows;
        BigramRows = bigramRows;
        ContextRows = contextRows;
        _module = new FastModule(width, embedWidth, depth, plain, relu, hashes, _rows, casedRows > 0, neighbourRows > 0, contextRows > 0, bigramRows > 0, recurrentLayers, recurrentSize, new Generator(seed));
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

    /// <summary>Whether the joining and window layers use ReLU instead of GELU.</summary>
    public bool Relu { get; }

    /// <summary>Rows of each of the four neighbour tables; 0 when there are none.</summary>
    public int NeighbourRows { get; }

    /// <summary>Rows of each of the four wider-context tables (text two tokens away, shape of the neighbours); 0 when there are none.</summary>
    public int ContextRows { get; }

    /// <summary>Rows of each of the two word-pair tables; 0 when there are none.</summary>
    public int BigramRows { get; }

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

    /// <summary>The number of bidirectional LSTM layers (Hochreiter and Schmidhuber 1997) run over each document after the window layers; 0 when there are none. Their output is added to the window output, so a token sees the whole document and not only its neighbours.</summary>
    public int RecurrentLayers { get; }

    /// <summary>The hidden size per LSTM direction as given; 0 means half the width. A smaller size is cheaper, and a linear layer maps the two directions back to the width.</summary>
    public int RecurrentSize { get; }

    /// <inheritdoc/>
    public NnModule? Weights => _module;

    /// <inheritdoc/>
    public void Dispose()
    {
        _slot.Dispose();
        _module.Dispose();
    }

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
            // The weights are about to change, so whatever was kept for inference is out of date.
            Interlocked.Increment(ref _epoch);
            return new EncodedBatch(Compute(docs, starts, false), starts);
        }

        return EncodeShared(docs, starts);
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

        if (_neighbourStart > BaseAttributes)
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

        if (Relu)
        {
            writer.WriteNumber("relu", 1);
        }

        if (NeighbourRows > 0)
        {
            writer.WriteNumber("neighbourRows", NeighbourRows);
        }

        if (BigramRows > 0)
        {
            writer.WriteNumber("bigramRows", BigramRows);
        }

        if (ContextRows > 0)
        {
            writer.WriteNumber("contextRows", ContextRows);
        }

        if (TurkicCase)
        {
            writer.WriteNumber("turkicCase", 1);
        }

        if (RecurrentLayers > 0)
        {
            writer.WriteNumber("recurrentLayers", RecurrentLayers);
            if (RecurrentSize > 0)
            {
                writer.WriteNumber("recurrentSize", RecurrentSize);
            }
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
            JsonSettings.OptionalInt(s, "relu", 0, 1, 0) == 1,
            JsonSettings.OptionalInt(s, "neighbourRows", 0, MaxRows, 0),
            JsonSettings.OptionalInt(s, "bigramRows", 0, MaxRows, 0),
            JsonSettings.OptionalInt(s, "contextRows", 0, MaxRows, 0),
            JsonSettings.OptionalInt(s, "turkicCase", 0, 1, 0) == 1,
            JsonSettings.OptionalInt(s, "recurrentLayers", 0, MaxRecurrentLayers, 0),
            JsonSettings.OptionalInt(s, "recurrentSize", 0, MaxWidth, 0));
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

    // inference: return a tensor the caller owns, computed by the table-lookup path when the encoder uses ReLU (see FastModule.RunInference).
    private Tensor Compute(ReadOnlySpan<Doc> docs, int[] starts, bool inference)
    {
        int n = starts[^1];

        // ids[(attribute * Hashes + hash) * n + row]. The scratch arrays come from the shared pool (the tensors copy what they need),
        // so a call allocates no per-token garbage of its own.
        int[] ids = ArrayPool<int>.Shared.Rent(_attributes * Hashes * n);
        int[] prev = ArrayPool<int>.Shared.Rent(n);
        int[] next = ArrayPool<int>.Shared.Rent(n);
        bool neighbours = NeighbourRows > 0;
        bool bigrams = BigramRows > 0;
        ulong[]? normHashes = neighbours || bigrams ? ArrayPool<ulong>.Shared.Rent(n) : null;
        ulong[]? suffixHashes = neighbours ? ArrayPool<ulong>.Shared.Rent(n) : null;
        bool context = ContextRows > 0;
        ulong[]? shapeHashes = context ? ArrayPool<ulong>.Shared.Rent(n) : null;
        if (context && normHashes is null)
        {
            normHashes = ArrayPool<ulong>.Shared.Rent(n);
        }
        try
        {
            int row = 0;
            foreach (Doc doc in docs)
            {
                for (int t = 0; t < doc.Length; t++, row++)
                {
                    ReadOnlySpan<char> text = doc[t].Text;
                    ulong norm = NormHash(text);
                    ulong suffix = SliceHash(Suffix(text), 2);
                    Put(ids, n, 0, row, norm);
                    Put(ids, n, 1, row, SliceHash(Prefix(text), 1));
                    Put(ids, n, 2, row, suffix);
                    ulong shape = ShapeHash(text);
                    Put(ids, n, 3, row, shape);
                    if (shapeHashes is not null)
                    {
                        shapeHashes[row] = shape;
                    }
                    if (_neighbourStart > BaseAttributes)
                    {
                        Put(ids, n, 4, row, SliceHash(text, 4));
                    }

                    if (normHashes is not null)
                    {
                        normHashes[row] = norm;
                    }

                    if (suffixHashes is not null)
                    {
                        suffixHashes[row] = suffix;
                    }

                    prev[row] = t == 0 ? n : row - 1;
                    next[row] = t == doc.Length - 1 ? n : row + 1;
                }
            }

            if (neighbours)
            {
                // Attributes _neighbourStart + 0..3: previous norm, next norm, previous suffix, next suffix. A document's first token
                // has no previous one and its last no next one; those hash to EdgeHash, which no text hashes to except by chance.
                for (int r = 0; r < n; r++)
                {
                    bool first = prev[r] == n;
                    bool last = next[r] == n;
                    Put(ids, n, _neighbourStart, r, first ? EdgeHash : normHashes![r - 1]);
                    Put(ids, n, _neighbourStart + 1, r, last ? EdgeHash : normHashes![r + 1]);
                    Put(ids, n, _neighbourStart + 2, r, first ? EdgeHash : suffixHashes![r - 1]);
                    Put(ids, n, _neighbourStart + 3, r, last ? EdgeHash : suffixHashes![r + 1]);
                }
            }

            if (context)
            {
                // Attributes _contextStart + 0..3: text two before, text two after, shape before, shape after. A neighbour beyond the
                // document's edge hashes to EdgeHash.
                for (int r = 0; r < n; r++)
                {
                    bool first = prev[r] == n;
                    bool last = next[r] == n;
                    bool first2 = first || prev[r - 1] == n;
                    bool last2 = last || next[r + 1] == n;
                    Put(ids, n, _contextStart, r, first2 ? EdgeHash : normHashes![r - 2]);
                    Put(ids, n, _contextStart + 1, r, last2 ? EdgeHash : normHashes![r + 2]);
                    Put(ids, n, _contextStart + 2, r, first ? EdgeHash : shapeHashes![r - 1]);
                    Put(ids, n, _contextStart + 3, r, last ? EdgeHash : shapeHashes![r + 1]);
                }
            }

            if (bigrams)
            {
                // Attributes _bigramStart + 0, 1: previous+current and current+next word.
                for (int r = 0; r < n; r++)
                {
                    ulong before = prev[r] == n ? EdgeHash : normHashes![r - 1];
                    ulong after = next[r] == n ? EdgeHash : normHashes![r + 1];
                    Put(ids, n, _bigramStart, r, Mix((before * 0x9E3779B97F4A7C15UL) ^ normHashes![r]));
                    Put(ids, n, _bigramStart + 1, r, Mix((normHashes[r] * 0xC2B2AE3D27D4EB4FUL) ^ after));
                }
            }

            if (inference && _module.CanRunInference)
            {
                return _module.RunInference(ids, n, prev, next, starts);
            }

            var idTensors = new Tensor[_attributes * Hashes];
            for (int k = 0; k < idTensors.Length; k++)
            {
                idTensors[k] = Tensor.FromSpan<int>(ids.AsSpan(k * n, n), Shape.Create(n));
                ActivationScope.Track(idTensors[k]);
            }

            Tensor prevTensor = Tensor.FromSpan<int>(prev.AsSpan(0, n), Shape.Create(n));
            Tensor nextTensor = Tensor.FromSpan<int>(next.AsSpan(0, n), Shape.Create(n));
            Tensor zero = Tensor.Zeros(DType.F32, Shape.Create(1, Width));
            ActivationScope.Track(prevTensor);
            ActivationScope.Track(nextTensor);
            ActivationScope.Track(zero);
            return _module.Run(idTensors, prevTensor, nextTensor, zero, starts);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(ids);
            ArrayPool<int>.Shared.Return(prev);
            ArrayPool<int>.Shared.Return(next);
            if (normHashes is not null)
            {
                ArrayPool<ulong>.Shared.Return(normHashes);
            }

            if (suffixHashes is not null)
            {
                ArrayPool<ulong>.Shared.Return(suffixHashes);
            }

            if (shapeHashes is not null)
            {
                ArrayPool<ulong>.Shared.Return(shapeHashes);
            }
        }
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

    // Inference: heads that read this encoder one after another are handed the same documents each time, so the matrix of the last
    // batch each thread encoded is kept and returned again when the very same documents come back (a pipeline of five heads encodes
    // once). The matrix is a copy that belongs to the slot, because the caller's activation scope releases its own tensors.
    private EncodedBatch EncodeShared(ReadOnlySpan<Doc> docs, int[] starts)
    {
        long epoch = Interlocked.Read(ref _epoch);
        InferenceSlot? slot = _slot.Value;
        if (slot is not null && slot.Epoch == epoch && !slot.Batch.Vectors.IsReleased && slot.Matches(docs))
        {
            return slot.Batch;
        }

        Tensor owned;
        if (_module.CanRunInference)
        {
            owned = Compute(docs, starts, true);
        }
        else
        {
            using var scope = new ActivationScope();
            owned = Compute(docs, starts, false).Clone();
        }

        if (slot is not null && !slot.Batch.Vectors.IsReleased)
        {
            slot.Batch.Vectors.Dispose();
        }

        var batch = new EncodedBatch(owned, starts);
        _slot.Value = new InferenceSlot(epoch, docs.ToArray(), batch);
        return batch;
    }

    private sealed record InferenceSlot(long Epoch, Doc[] Docs, EncodedBatch Batch)
    {
        public bool Matches(ReadOnlySpan<Doc> docs)
        {
            if (docs.Length != Docs.Length)
            {
                return false;
            }

            for (int i = 0; i < docs.Length; i++)
            {
                if (!ReferenceEquals(docs[i], Docs[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class FastModule : NnModule
    {
        private readonly Embedding[] _tables;
        private readonly Linear _mix;
        private readonly LayerNorm? _mixNorm;
        private readonly bool _plain;
        private readonly bool _relu;
        private readonly int _hashes;
        private readonly Linear[] _windows;
        private readonly LayerNorm[] _norms;
        private readonly int _width;
        private readonly int _embedWidth;
        private readonly int _hidden;
        private readonly Recurrent? _lstm;
        private readonly Linear? _back;
        private Tensor[]? _watched;
        private volatile Derived? _derived;
        private readonly object _deriveLock = new();

        public FastModule(int width, int embedWidth, int depth, bool plain, bool relu, int hashes, int[] rows, bool cased, bool neighbours, bool context, bool bigrams, int recurrentLayers, int recurrentSize, Generator generator)
        {
            _plain = plain;
            _relu = relu;
            _hashes = hashes;
            _width = width;
            _embedWidth = embedWidth;
            _tables = new Embedding[rows.Length];
            var names = new List<string> { "norm", "prefix", "suffix", "shape" };
            if (cased)
            {
                names.Add("cased");
            }

            if (neighbours)
            {
                names.AddRange(["prev_norm", "next_norm", "prev_suffix", "next_suffix"]);
            }

            if (context)
            {
                names.AddRange(["prev2_norm", "next2_norm", "prev_shape", "next_shape"]);
            }

            if (bigrams)
            {
                names.AddRange(["bigram_prev", "bigram_next"]);
            }

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

            // Registered last, so the state order of a file without it is unchanged.
            if (recurrentLayers > 0)
            {
                _hidden = recurrentSize > 0 ? recurrentSize : width / 2;
                _lstm = RegisterModule("lstm", new Recurrent(gru: false, width, _hidden, generator, recurrentLayers, bidirectional: true));
                if (2 * _hidden != width)
                {
                    _back = RegisterModule("lstm_out", new Linear(2 * _hidden, width, generator));
                }
            }
        }

        public override Tensor Forward(Tensor input) => throw new NotSupportedException("Call Run.");

        // The table-lookup inference path needs ReLU: the exact GELU is an error function per value.
        public bool CanRunInference => _relu;


        private Tensor Activate(Tensor x) => _relu ? Ops.Relu(x) : Ops.Gelu(x);

        // ids: Int32 [n] per (attribute, hash); prev/next: Int32 [n] row of the neighbour, or n for the zero row.
        public Tensor Run(Tensor[] ids, Tensor prev, Tensor next, Tensor zero, int[] starts)
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
            Tensor x = _plain ? Ops.Relu(joined) : _mixNorm!.Forward(Activate(joined));
            for (int i = 0; i < _windows.Length; i++)
            {
                Tensor padded = Ops.Concat([x, zero], 0);
                Tensor left = Ops.Embedding(padded, prev);
                Tensor right = Ops.Embedding(padded, next);
                Tensor window = Ops.Concat([left, x, right], 1);
                x = Ops.Add(x, _norms[i].Forward(Activate(_windows[i].Forward(window))));
            }

            return _lstm is null ? x : Ops.Add(x, Recurrence(x, zero, starts));
        }

        // The documents form one padded LSTM batch with their lengths, so a document's result does not depend on which others share the
        // batch (packed-sequence semantics of Ops.Lstm: the reverse direction starts at the document's own end).
        private Tensor Recurrence(Tensor x, Tensor zero, int[] starts)
        {
            int n = x.Shape[0];
            int docs = starts.Length - 1;
            int longest = 0;
            var lengths = new int[docs];
            for (int d = 0; d < docs; d++)
            {
                lengths[d] = starts[d + 1] - starts[d];
                longest = Math.Max(longest, lengths[d]);
            }

            var gather = new int[longest * docs]; // [time, batch] -> row of x, or n for padding
            var scatter = new int[n]; // row of x -> [time, batch] position
            Array.Fill(gather, n);
            for (int d = 0; d < docs; d++)
            {
                for (int t = 0; t < lengths[d]; t++)
                {
                    gather[(t * docs) + d] = starts[d] + t;
                    scatter[starts[d] + t] = (t * docs) + d;
                }
            }

            Tensor gatherIds = Tensor.FromSpan<int>(gather, Shape.Create(gather.Length));
            Tensor scatterIds = Tensor.FromSpan<int>(scatter, Shape.Create(n));
            ActivationScope.Track(gatherIds);
            ActivationScope.Track(scatterIds);
            Tensor padded = Ops.Embedding(Ops.Concat([x, zero], 0), gatherIds).Reshape(longest, docs, _width);
            Tensor outputs = _lstm!.Forward(padded, lengths).Reshape(longest * docs, 2 * _hidden);
            Tensor rows = Ops.Embedding(outputs, scatterIds);
            return _back is null ? rows : _back.Forward(rows);
        }

        private sealed class Derived(long stamp, float[][] tables, Tensor[] windows)
        {
            public readonly long Stamp = stamp;
            public readonly float[][] Tables = tables;
            public readonly Tensor[] Windows = windows;
        }

        private long Stamp()
        {
            Tensor[] watched = _watched ??= Parameters();
            long sum = 0;
            foreach (Tensor t in watched)
            {
                sum += t.Version;
            }

            return sum;
        }

        // Inference tables. The joining layer is linear in the concatenated embedding rows, so  mix([e_a1, ..., e_aK]) = sum_a (e_a W_a)
        // and each table can be multiplied by its slice of the joining weights once: a token then costs one row lookup per attribute
        // and a sum, where the product of a K*embedWidth vector by the joining matrix was the largest cost of the encoder. Likewise a
        // window layer applies one weight block to each of the three vectors, so it is one product x * [B_left | B_self | B_right]
        // and a shifted sum. Rebuilt when any weight changes (the storage version counters tell).
        private Derived GetDerived()
        {
            long stamp = Stamp();
            Derived? d = _derived;
            if (d is not null && d.Stamp == stamp)
            {
                return d;
            }

            lock (_deriveLock)
            {
                d = _derived;
                stamp = Stamp();
                if (d is not null && d.Stamp == stamp)
                {
                    return d;
                }

                int w = _width, ew = _embedWidth, attrs = _tables.Length;
                ReadOnlySpan<float> mixW = _mix.Weight.Data<float>();
                var tables = new float[attrs][];
                using (var scope = new ActivationScope())
                {
                    float[] slice = new float[ew * w];
                    for (int a = 0; a < attrs; a++)
                    {
                        for (int i = 0; i < ew; i++)
                        {
                            for (int j = 0; j < w; j++)
                            {
                                slice[(i * w) + j] = mixW[(j * attrs * ew) + (a * ew) + i];
                            }
                        }

                        Tensor sliceT = Tensor.FromSpan<float>(slice, Shape.Create(ew, w));
                        Tensor product = Ops.MatMul(_tables[a].Weight, sliceT);
                        tables[a] = product.Data<float>().ToArray();
                        sliceT.Dispose();
                    }
                }

                var windows = new Tensor[_windows.Length];
                for (int l = 0; l < windows.Length; l++)
                {
                    ReadOnlySpan<float> ww = _windows[l].Weight.Data<float>();
                    float[] cat = new float[3 * w * w];
                    for (int k = 0; k < 3; k++)
                    {
                        for (int j = 0; j < w; j++)
                        {
                            ww.Slice((j * 3 * w) + (k * w), w).CopyTo(cat.AsSpan(((k * w) + j) * w, w));
                        }
                    }

                    windows[l] = Tensor.FromSpan<float>(cat, Shape.Create(3 * w, w));
                }

                d = new Derived(stamp, tables, windows);
                _derived = d;
                return d;
            }
        }

        // ids: Int32 [attribute * hashes + hash][n] flattened; prev/next as in Run. Returns [n, width] owned by the caller.
        public Tensor RunInference(int[] ids, int n, int[] prev, int[] next, int[] starts)
        {
            Derived d = GetDerived();
            int w = _width;
            float[] x = ArrayPool<float>.Shared.Rent(n * w);
            float[] z = ArrayPool<float>.Shared.Rent(w);
            try
            {
                ReadOnlySpan<float> mixBias = _mix.Bias.Data<float>();
                ReadOnlySpan<float> mixGamma = _plain ? default : _mixNorm!.Weight.Data<float>();
                ReadOnlySpan<float> mixBeta = _plain ? default : _mixNorm!.Bias.Data<float>();
                for (int r = 0; r < n; r++)
                {
                    Span<float> row = x.AsSpan(r * w, w);
                    mixBias.CopyTo(row);
                    for (int a = 0; a < _tables.Length; a++)
                    {
                        float[] table = d.Tables[a];
                        for (int h = 0; h < _hashes; h++)
                        {
                            int id = ids[(((a * _hashes) + h) * n) + r];
                            RowKernels.Add(row, table.AsSpan(id * w, w));
                        }
                    }

                    RowKernels.Relu(row);
                    if (!_plain)
                    {
                        RowKernels.LayerNorm(row, mixGamma, mixBeta, row);
                    }
                }

                Span<float> zs = z.AsSpan(0, w);
                for (int l = 0; l < _windows.Length; l++)
                {
                    using var scope = new ActivationScope();
                    Tensor xt = Tensor.FromSpan<float>(x.AsSpan(0, n * w), Shape.Create(n, w));
                    Tensor yt = Ops.Linear(xt, d.Windows[l]);
                    xt.Dispose();
                    ReadOnlySpan<float> y = yt.Data<float>();
                    ReadOnlySpan<float> bias = _windows[l].Bias.Data<float>();
                    ReadOnlySpan<float> gamma = _norms[l].Weight.Data<float>();
                    ReadOnlySpan<float> beta = _norms[l].Bias.Data<float>();
                    int stride = 3 * w;
                    for (int r = 0; r < n; r++)
                    {
                        bias.CopyTo(zs);
                        if (prev[r] != n)
                        {
                            RowKernels.Add(zs, y.Slice((r - 1) * stride, w));
                        }

                        RowKernels.Add(zs, y.Slice((r * stride) + w, w));
                        if (next[r] != n)
                        {
                            RowKernels.Add(zs, y.Slice(((r + 1) * stride) + (2 * w), w));
                        }

                        RowKernels.Relu(zs);
                        RowKernels.LayerNormAdd(zs, gamma, beta, x.AsSpan(r * w, w));
                    }
                }

                if (_lstm is not null)
                {
                    // The recurrent layers run on OneModel's graph ops; their output is added to the window output.
                    using var scope = new ActivationScope();
                    Tensor xt = Tensor.FromSpan<float>(x.AsSpan(0, n * w), Shape.Create(n, w));
                    Tensor zero = Tensor.Zeros(DType.F32, Shape.Create(1, w));
                    ReadOnlySpan<float> extra = Recurrence(xt, zero, starts).Data<float>();
                    RowKernels.Add(x.AsSpan(0, n * w), extra);
                    xt.Dispose();
                    zero.Dispose();
                }

                return Tensor.FromSpan<float>(x.AsSpan(0, n * w), Shape.Create(n, w));
            }
            finally
            {
                ArrayPool<float>.Shared.Return(x);
                ArrayPool<float>.Shared.Return(z);
            }
        }
    }
}
