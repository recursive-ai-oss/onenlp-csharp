using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>How the vectors of the subword pieces inside one token become that token's vector.</summary>
public enum SubwordPooling
{
    /// <summary>The vector of the first piece that overlaps the token (the usual choice for tagging; Devlin et al. 2019 §5.3).</summary>
    FirstPiece,

    /// <summary>The mean of the vectors of every piece that overlaps the token.</summary>
    Mean,
}

/// <summary>
/// The accurate tier's encoder: a transformer exported to ONNX (input ids and attention mask in, last hidden states out), run on
/// OneModel, with its own <c>tokenizer.json</c>. A document's tokens are mapped to subword pieces by character offsets and pooled
/// back to one vector per token, so heads see the same <see cref="IEncoder"/> as with the fast tier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Frozen.</b> The imported graph is evaluated by OneModel's inference runtime and has no autodiff graph, so
/// <see cref="Weights"/> is <see langword="null"/> and the encoder is never trained; heads train on its (constant) output. Outputs are
/// cached per <see cref="Doc"/> instance, so repeated epochs over the same documents run the transformer once.
/// </para>
/// <para>
/// <b>Pieces and tokens.</b> The text of the whole document is tokenized once, without special tokens, with offsets in UTF-16 units
/// of <see cref="Doc.Text"/>. A piece belongs to every token whose character range it overlaps; a token no piece overlaps gets a
/// zero vector.
/// </para>
/// <para>
/// <b>Long documents.</b> A document with more pieces than fit in <see cref="MaxLength"/> (less the special tokens) is cut into
/// windows that overlap by half. Each piece takes its vector from the window in which it lies nearest the centre, so no piece is read
/// near a window edge when another window sees it better.
/// </para>
/// <para>
/// The model and tokenizer files are referenced, not embedded: a saved pipeline stores their file names (see
/// <see cref="ModelDirectory"/>) and the model's SHA-256. This class is not safe for concurrent <see cref="Encode"/> calls.
/// </para>
/// </remarks>
public sealed class TransformerEncoder : IEncoder
{
    /// <summary>The kind name a saved pipeline uses for this encoder.</summary>
    public const string EncoderKind = "transformer";

    private const int MaxWidth = 8192;
    private const int MaxLengthLimit = 1 << 16;

    private readonly Session _session;
    private readonly Tokenizer _tokenizer;
    private readonly string _modelFile;
    private readonly string _tokenizerFile;
    private readonly string? _sha256;
    private readonly int[] _prefix;
    private readonly int[] _suffix;
    private readonly string _outputName;
    private readonly DType _idType;
    private readonly bool _hasMask;
    private readonly ConditionalWeakTable<Doc, CachedRows> _cache = new();

    /// <summary>
    /// The directory a saved pipeline's relative model and tokenizer file names are looked up in. <see langword="null"/> (the default)
    /// means the current directory. Absolute names ignore it.
    /// </summary>
    public static string? ModelDirectory { get; set; }

    /// <summary>Loads an exported encoder and its tokenizer.</summary>
    /// <param name="modelPath">The ONNX file, with inputs <c>input_ids</c> and <c>attention_mask</c> ([batch, sequence], integers) and a first output of shape [batch, sequence, width].</param>
    /// <param name="tokenizerPath">The model's <c>tokenizer.json</c>.</param>
    /// <param name="pooling">How pieces become tokens. Default <see cref="SubwordPooling.Mean"/>, which scored higher than the first piece on the English development set (docs/accurate-encoder.md).</param>
    /// <param name="maxLength">The longest sequence given to the model, special tokens included (2 to 65,536). Default 512.</param>
    /// <param name="modelSha256">The expected SHA-256 of the model file as lower-case hex, kept for <see cref="VerifyModelFile"/> and saved with the pipeline; may be <see langword="null"/>.</param>
    /// <param name="options">Session options; the default runs in OneModel's fast mode on every core.</param>
    /// <exception cref="InvalidDataException">The model's inputs or output are not as described, or the tokenizer adds a variable number of special tokens.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is out of range.</exception>
    public TransformerEncoder(string modelPath, string tokenizerPath, SubwordPooling pooling = SubwordPooling.Mean, int maxLength = 512, string? modelSha256 = null, ModelOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);
        ArgumentException.ThrowIfNullOrEmpty(tokenizerPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxLength, MaxLengthLimit);
        Pooling = pooling;
        MaxLength = maxLength;
        _sha256 = modelSha256;
        _modelFile = modelPath;
        _tokenizerFile = tokenizerPath;
        _tokenizer = Tokenizer.FromFile(tokenizerPath);
        _tokenizer.Padding = null;
        _tokenizer.Truncation = null;
        _session = Session.Load(modelPath, options);

        TensorInfo? ids = null;
        _hasMask = false;
        foreach (TensorInfo input in _session.Inputs)
        {
            if (input.Name == "input_ids")
            {
                ids = input;
            }
            else if (input.Name == "attention_mask")
            {
                _hasMask = true;
            }
            else
            {
                throw new InvalidDataException($"The encoder has an input '{input.Name}'; only 'input_ids' and 'attention_mask' are supported.");
            }
        }

        if (ids is null || !_hasMask || (ids.DType != DType.I64 && ids.DType != DType.I32))
        {
            throw new InvalidDataException("The encoder needs integer inputs 'input_ids' and 'attention_mask'.");
        }

        _idType = ids.DType;
        if (_session.Outputs.Length < 1 || _session.Outputs[0].Rank != 3 || !_session.Outputs[0].Dims[2].IsFixed)
        {
            throw new InvalidDataException("The encoder's first output must have the shape [batch, sequence, width] with a fixed width.");
        }

        _outputName = _session.Outputs[0].Name;
        long width = _session.Outputs[0].Dims[2].Value;
        if (width < 1 || width > MaxWidth)
        {
            throw new InvalidDataException($"The encoder width {width} is outside 1 to {MaxWidth}.");
        }

        Width = (int)width;
        (_prefix, _suffix) = SpecialTokenFrame(_tokenizer);
        if (_prefix.Length + _suffix.Length >= maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "The special tokens alone fill the sequence.");
        }
    }

    /// <inheritdoc/>
    public string Kind => EncoderKind;

    /// <inheritdoc/>
    public int Width { get; }

    /// <summary>How pieces are pooled into tokens.</summary>
    public SubwordPooling Pooling { get; }

    /// <summary>The longest sequence given to the model, special tokens included.</summary>
    public int MaxLength { get; }

    /// <summary>The most windows run in one model call. Default 8; larger batches use more memory.</summary>
    public int WindowBatch { get; set; } = 8;

    /// <inheritdoc/>
    /// <remarks>Always <see langword="null"/>: the imported graph is not trainable.</remarks>
    public NnModule? Weights => null;

    /// <inheritdoc/>
    public void Dispose() => _session.Dispose();

    /// <summary>Checks the model file against the SHA-256 given at construction.</summary>
    /// <exception cref="InvalidDataException">No hash was given, or the file's hash differs.</exception>
    public void VerifyModelFile()
    {
        if (_sha256 is null)
        {
            throw new InvalidDataException("No model hash is recorded.");
        }

        using FileStream f = File.OpenRead(_modelFile);
        string actual = Convert.ToHexStringLower(SHA256.HashData(f));
        if (!string.Equals(actual, _sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The model file's SHA-256 is {actual}, not {_sha256}.");
        }
    }

    /// <inheritdoc/>
    public EncodedBatch Encode(ReadOnlySpan<Doc> docs)
    {
        var starts = new int[docs.Length + 1];
        for (int d = 0; d < docs.Length; d++)
        {
            starts[d + 1] = starts[d] + docs[d].Length;
        }

        int total = starts[^1];
        var data = new float[checked(total * Width)];
        var missing = new List<int>();
        for (int d = 0; d < docs.Length; d++)
        {
            if (docs[d].Length == 0)
            {
                continue;
            }

            if (_cache.TryGetValue(docs[d], out CachedRows? hit) && hit.Length == docs[d].Length)
            {
                hit.Values.CopyTo(data, starts[d] * Width);
            }
            else
            {
                missing.Add(d);
            }
        }

        if (missing.Count > 0)
        {
            Compute(docs, missing, starts, data);
            foreach (int d in missing)
            {
                _cache.AddOrUpdate(docs[d], new CachedRows(docs[d].Length, data.AsSpan(starts[d] * Width, docs[d].Length * Width).ToArray()));
            }
        }

        Tensor vectors = total == 0 ? Tensor.Zeros(DType.F32, Shape.Create(0, Width)) : Tensor.FromSpan<float>(data, Shape.Create(total, Width));
        ActivationScope.Track(vectors);
        return new EncodedBatch(vectors, starts);
    }

    /// <inheritdoc/>
    public void WriteSettings(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString("model", _modelFile);
        writer.WriteString("tokenizer", _tokenizerFile);
        if (_sha256 is not null)
        {
            writer.WriteString("modelSha256", _sha256);
        }

        writer.WriteString("pooling", Pooling == SubwordPooling.Mean ? "mean" : "first");
        writer.WriteNumber("maxLength", MaxLength);
    }

    internal static IEncoder FromSettings(JsonElement s)
    {
        string pooling = JsonSettings.String(s, "pooling", 16);
        if (pooling is not ("first" or "mean"))
        {
            throw new InvalidDataException("'pooling' must be 'first' or 'mean'.");
        }

        string? sha = s.TryGetProperty("modelSha256", out _) ? JsonSettings.String(s, "modelSha256", 64) : null;
        string model = Resolve(JsonSettings.String(s, "model", 1024));
        string tokenizer = Resolve(JsonSettings.String(s, "tokenizer", 1024));
        return new TransformerEncoder(
            model,
            tokenizer,
            pooling == "mean" ? SubwordPooling.Mean : SubwordPooling.FirstPiece,
            JsonSettings.Int(s, "maxLength", 2, MaxLengthLimit),
            sha);
    }

    internal static string Resolve(string name) =>
        Path.IsPathRooted(name) || ModelDirectory is null ? name : Path.Combine(ModelDirectory, name);

    /// <summary>The special tokens the tokenizer wraps one non-empty text in, found by encoding a probe text with and without them.</summary>
    internal static (int[] Prefix, int[] Suffix) SpecialTokenFrame(Tokenizer tokenizer)
    {
        TokenEncoding with = tokenizer.Encode("a", addSpecialTokens: true);
        int first = 0;
        while (first < with.Length && with.SpecialTokensMask[first])
        {
            first++;
        }

        int last = with.Length;
        while (last > first && with.SpecialTokensMask[last - 1])
        {
            last--;
        }

        if (first == with.Length)
        {
            throw new InvalidDataException("The tokenizer produced only special tokens for a one-letter text.");
        }

        return (with.Ids[..first], with.Ids[last..]);
    }

    private void Compute(ReadOnlySpan<Doc> docs, List<int> docIndexes, int[] starts, float[] data)
    {
        int capacity = MaxLength - _prefix.Length - _suffix.Length;
        int step = Math.Max(1, capacity / 2);

        // Every window of every missing document, as (document, first piece, piece count).
        var windows = new List<Window>();
        var plans = new Dictionary<int, DocPlan>();
        foreach (int d in docIndexes)
        {
            Doc doc = docs[d];
            TokenEncoding enc = _tokenizer.Encode(doc.Text, addSpecialTokens: false);
            int n = enc.Length;
            var plan = new DocPlan(enc, MapPiecesToTokens(doc, enc), doc.Length, Pooling == SubwordPooling.Mean);
            plans[d] = plan;
            if (n == 0)
            {
                continue;
            }

            var firsts = new List<int>();
            if (n <= capacity)
            {
                firsts.Add(0);
            }
            else
            {
                for (int s = 0; ; s += step)
                {
                    if (s + capacity >= n)
                    {
                        firsts.Add(n - capacity);
                        break;
                    }

                    firsts.Add(s);
                }
            }

            // Window w owns the pieces from the midpoint between the centres of w-1 and w up to the midpoint with w+1,
            // so every piece is read where it lies nearest a window centre.
            for (int w = 0; w < firsts.Count; w++)
            {
                int lo = firsts[w];
                int hi = Math.Min(n, lo + capacity);
                int ownLo = w == 0 ? 0 : Midpoint(firsts[w - 1], firsts[w], capacity);
                int ownHi = w == firsts.Count - 1 ? n : Midpoint(firsts[w], firsts[w + 1], capacity);
                windows.Add(new Window(d, lo, hi - lo, Math.Max(ownLo, lo), Math.Min(ownHi, hi)));
            }
        }

        // Longest windows first so a batch pads little.
        int[] order = [.. Enumerable.Range(0, windows.Count).OrderByDescending(i => windows[i].Count).ThenBy(i => i)];
        int batchSize = Math.Max(1, WindowBatch);
        for (int b = 0; b < order.Length; b += batchSize)
        {
            int count = Math.Min(batchSize, order.Length - b);
            RunBatch(docs, order.AsSpan(b, count), windows, plans, starts, data);
        }

        if (Pooling == SubwordPooling.Mean)
        {
            foreach (int d in docIndexes)
            {
                int[] counts = plans[d].Counts!;
                for (int t = 0; t < counts.Length; t++)
                {
                    if (counts[t] > 1)
                    {
                        float inv = 1f / counts[t];
                        Span<float> row = data.AsSpan((starts[d] + t) * Width, Width);
                        for (int j = 0; j < row.Length; j++)
                        {
                            row[j] *= inv;
                        }
                    }
                }
            }
        }
    }

    // The window whose centre is nearer to a piece takes it: pieces from the boundary between two windows' centres on.
    private static int Midpoint(int firstA, int firstB, int capacity) => (firstA + firstB + capacity) / 2;

    private void RunBatch(ReadOnlySpan<Doc> docs, ReadOnlySpan<int> batch, List<Window> windows, Dictionary<int, DocPlan> plans, int[] starts, float[] data)
    {
        int longest = 0;
        foreach (int w in batch)
        {
            longest = Math.Max(longest, windows[w].Count);
        }

        int length = longest + _prefix.Length + _suffix.Length;
        int rows = batch.Length;
        var ids = new long[rows * length];
        var mask = new long[rows * length];
        for (int r = 0; r < rows; r++)
        {
            Window win = windows[batch[r]];
            int[] pieceIds = plans[win.Doc].Encoding.Ids;
            int p = r * length;
            foreach (int id in _prefix)
            {
                ids[p] = id;
                mask[p++] = 1;
            }

            for (int i = 0; i < win.Count; i++)
            {
                ids[p] = pieceIds[win.First + i];
                mask[p++] = 1;
            }

            foreach (int id in _suffix)
            {
                ids[p] = id;
                mask[p++] = 1;
            }
        }

        using Tensor idTensor = MakeIds(ids, rows, length);
        using Tensor maskTensor = MakeIds(mask, rows, length);
        using SessionOutputs outputs = _session.Run(
            new Dictionary<string, Tensor> { ["input_ids"] = idTensor, ["attention_mask"] = maskTensor },
            [_outputName]);
        ReadOnlySpan<float> hidden = outputs[_outputName].Data<float>();
        if (hidden.Length != rows * length * Width)
        {
            throw new InvalidDataException("The encoder's output does not have the expected size.");
        }

        for (int r = 0; r < rows; r++)
        {
            Window win = windows[batch[r]];
            DocPlan plan = plans[win.Doc];
            for (int i = win.OwnFrom; i < win.OwnTo; i++)
            {
                ReadOnlySpan<float> vec = hidden.Slice(((r * length) + _prefix.Length + (i - win.First)) * Width, Width);
                (int firstToken, int lastToken) = plan.PieceTokens[i];
                for (int t = firstToken; t <= lastToken && firstToken >= 0; t++)
                {
                    Span<float> dest = data.AsSpan((starts[win.Doc] + t) * Width, Width);
                    if (Pooling == SubwordPooling.Mean)
                    {
                        for (int j = 0; j < dest.Length; j++)
                        {
                            dest[j] += vec[j];
                        }

                        plan.Counts![t]++;
                    }
                    else if (plan.FirstPiece(t, i))
                    {
                        vec.CopyTo(dest);
                    }
                }
            }
        }
    }

    private Tensor MakeIds(long[] values, int rows, int length)
    {
        if (_idType == DType.I64)
        {
            return Tensor.FromSpan<long>(values, Shape.Create(rows, length));
        }

        var narrow = new int[values.Length];
        for (int i = 0; i < narrow.Length; i++)
        {
            narrow[i] = (int)values[i];
        }

        return Tensor.FromSpan<int>(narrow, Shape.Create(rows, length));
    }

    /// <summary>For every piece, the first and last token its character range overlaps, or (-1, -1) for a piece that covers no token.</summary>
    internal static (int First, int Last)[] MapPiecesToTokens(Doc doc, TokenEncoding enc)
    {
        ReadOnlySpan<int> tokenStart = doc.GetStarts();
        ReadOnlySpan<int> tokenLength = doc.GetLengths();
        var result = new (int, int)[enc.Length];
        int t = 0;
        for (int i = 0; i < enc.Length; i++)
        {
            TokenOffset o = enc.Offsets[i];
            result[i] = (-1, -1);
            if (o.End <= o.Start)
            {
                continue;
            }

            // Tokens are in text order and do not overlap, and pieces start in text order, so the scan only moves forward.
            while (t < tokenStart.Length && tokenStart[t] + tokenLength[t] <= o.Start)
            {
                t++;
            }

            int j = t;
            while (j < tokenStart.Length && tokenStart[j] < o.End)
            {
                j++;
            }

            if (j > t)
            {
                result[i] = (t, j - 1);
            }
        }

        return result;
    }

    private sealed class DocPlan(TokenEncoding encoding, (int First, int Last)[] pieceTokens, int tokenCount, bool mean)
    {
        private readonly int[] _firstPiece = BuildFirstPieces(pieceTokens, tokenCount);

        public TokenEncoding Encoding { get; } = encoding;

        public (int First, int Last)[] PieceTokens { get; } = pieceTokens;

        public int[]? Counts { get; } = mean ? new int[tokenCount] : null;

        /// <summary>True when <paramref name="piece"/> is the first piece (lowest index) that overlaps the token.</summary>
        public bool FirstPiece(int token, int piece) => _firstPiece[token] == piece;

        private static int[] BuildFirstPieces((int First, int Last)[] pieceTokens, int tokenCount)
        {
            var first = new int[tokenCount];
            Array.Fill(first, -1);
            for (int i = 0; i < pieceTokens.Length; i++)
            {
                (int a, int b) = pieceTokens[i];
                for (int t = a; a >= 0 && t <= b; t++)
                {
                    if (first[t] < 0)
                    {
                        first[t] = i;
                    }
                }
            }

            return first;
        }
    }

    private readonly record struct Window(int Doc, int First, int Count, int OwnFrom, int OwnTo);

    private sealed class CachedRows(int length, float[] values)
    {
        public int Length { get; } = length;

        public float[] Values { get; } = values;
    }
}
