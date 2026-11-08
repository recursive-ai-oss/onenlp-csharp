using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Neural.Embeddings;

/// <summary>
/// A sentence embedder over a transformer exported to ONNX: the text is tokenized (with the model's own <c>tokenizer.json</c>, special
/// tokens added, truncated to <see cref="MaxSequenceLength"/>), run through the encoder on OneModel, pooled over its tokens and
/// optionally scaled to unit length (Reimers and Gurevych 2019, Sentence-BERT §3). The model directory has the layout sentence-transformers
/// publishes: the ONNX file, <c>tokenizer.json</c>, <c>modules.json</c>, <c>1_Pooling/config.json</c> and <c>sentence_bert_config.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>int8.</b> Pass the name of an int8-quantized ONNX file of the same model as <c>modelFile</c> to <see cref="Load(string, string, string, ModelOptions?)"/>; OneModel
/// runs its integer operators exactly. This is opt-in and never the default: it is smaller and faster, and its vectors differ slightly from
/// the float model's (docs/embeddings.md reports how much).
/// </para>
/// <para>
/// <b>Batches.</b> Texts of a batch are sorted by length and run in groups of <see cref="BatchSize"/> so little padding is computed;
/// results come back in input order. Padding never changes a text's vector beyond float rounding because pooling reads only the tokens
/// the mask keeps. Calls are serialized with a lock.
/// </para>
/// </remarks>
public sealed class SentenceEmbedder : IEmbedder
{
    // The SentencePiece word-start marker U+2581, as a piece of its own.
    private const string SpaceMarker = "\u2581";
    private const int MaxWidth = 8192;
    private const int MaxBatchSize = 4096;

    private readonly Session _session;
    private Stream? _ownedModelStream;
    private readonly Tokenizer _tokenizer;
    private readonly string _outputName;
    private readonly DType _idType;
    private readonly bool _hasTypeIds;
    private readonly int _padId;
    private readonly bool _spaceMarker;
    private readonly int _frameLead;
    private readonly int _frameTrail;
    private readonly object _gate = new();
    private int _batchSize = 32;

    /// <summary>Creates an embedder from an opened session and tokenizer.</summary>
    /// <param name="session">A session over the encoder graph: inputs <c>input_ids</c> and <c>attention_mask</c> (and optionally <c>token_type_ids</c>, fed zeros), a first output [batch, sequence, width]. The embedder owns and disposes it.</param>
    /// <param name="tokenizer">The model's tokenizer, with special tokens added by its post-processor. Its <see cref="Tokenizer.Truncation"/> and <see cref="Tokenizer.Padding"/> are replaced.</param>
    /// <param name="config">Pooling, normalization and maximum length.</param>
    /// <param name="padId">The ID of the padding token.</param>
    /// <param name="prefix">Text put in front of every input (for example <c>query: </c> for models trained that way). Default none. Counts toward the maximum length.</param>
    /// <exception cref="InvalidDataException">The graph's inputs or output are not as described.</exception>
    public SentenceEmbedder(Session session, Tokenizer tokenizer, EmbeddingConfig config, int padId, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(prefix);
        _session = session;
        _tokenizer = tokenizer;
        Config = config;
        Prefix = prefix;
        _padId = padId;
        _tokenizer.Padding = null;
        _tokenizer.Truncation = null;
        _spaceMarker = tokenizer.TokenToId(SpaceMarker) is not null;
        (_frameLead, _frameTrail) = SpecialTokenFrame(tokenizer);
        if (_frameLead + _frameTrail >= config.MaxSequenceLength)
        {
            throw new ArgumentException("The special tokens alone fill the maximum sequence length.", nameof(config));
        }

        TensorInfo? ids = null;
        bool mask = false;
        foreach (TensorInfo input in session.Inputs)
        {
            switch (input.Name)
            {
                case "input_ids":
                    ids = input;
                    break;
                case "attention_mask":
                    mask = true;
                    break;
                case "token_type_ids":
                    _hasTypeIds = true;
                    break;
                default:
                    throw new InvalidDataException($"The encoder has an input '{input.Name}'; only 'input_ids', 'attention_mask' and 'token_type_ids' are supported.");
            }
        }

        if (ids is null || !mask || (ids.DType != DType.I64 && ids.DType != DType.I32))
        {
            throw new InvalidDataException("The encoder needs integer inputs 'input_ids' and 'attention_mask'.");
        }

        _idType = ids.DType;
        if (session.Outputs.Length < 1 || session.Outputs[0].Rank != 3 || !session.Outputs[0].Dims[2].IsFixed)
        {
            throw new InvalidDataException("The encoder's first output must have the shape [batch, sequence, width] with a fixed width.");
        }

        _outputName = session.Outputs[0].Name;
        long width = session.Outputs[0].Dims[2].Value;
        if (width < 1 || width > MaxWidth)
        {
            throw new InvalidDataException($"The encoder width {width} is outside 1 to {MaxWidth}.");
        }

        Dimensions = (int)width;
    }

    /// <summary>Loads a model directory in sentence-transformers layout.</summary>
    /// <param name="directory">The directory with <c>tokenizer.json</c> and the configuration files (see <see cref="EmbeddingConfig.Load"/>).</param>
    /// <param name="modelFile">The ONNX file name inside <paramref name="directory"/>. Default <c>model.onnx</c>; give an int8 file for the int8 mode.</param>
    /// <param name="prefix">Text put in front of every input. Default none (what sentence-transformers does).</param>
    /// <param name="options">Session options; the default runs in OneModel's fast mode on every core.</param>
    /// <returns>An embedder the caller disposes.</returns>
    /// <exception cref="FileNotFoundException">The model or tokenizer file is missing.</exception>
    /// <exception cref="InvalidDataException">A file is damaged or the graph is not an encoder as described for the constructor.</exception>
    /// <exception cref="NotSupportedException">The model uses modules this library does not run.</exception>
    public static SentenceEmbedder Load(string directory, string modelFile = "model.onnx", string prefix = "", ModelOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentException.ThrowIfNullOrEmpty(modelFile);
        EmbeddingConfig config = EmbeddingConfig.Load(directory);
        Tokenizer tokenizer = Tokenizer.FromFile(Path.Combine(directory, "tokenizer.json"));
        int pad = PadIdOf(tokenizer);
        Session session = Session.Load(Path.Combine(directory, modelFile), options);
        try
        {
            return new SentenceEmbedder(session, tokenizer, config, pad, prefix);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>Loads a model from streams, for weights held in memory or embedded in an assembly.</summary>
    /// <param name="onnx">The ONNX model; read to the end, not closed. This overload buffers the bytes, so the caller may close the stream after loading.</param>
    /// <param name="tokenizerJson">The model's <c>tokenizer.json</c>; read to the end, not closed.</param>
    /// <param name="config">Pooling, normalization and length.</param>
    /// <param name="prefix">Text put in front of every input.</param>
    /// <param name="options">Session options.</param>
    /// <returns>An embedder the caller disposes.</returns>
    /// <exception cref="InvalidDataException">A stream is damaged or the graph is not an encoder as described for the constructor.</exception>
    public static SentenceEmbedder Load(Stream onnx, Stream tokenizerJson, EmbeddingConfig config, string prefix = "", ModelOptions? options = null)
    {
        // Preserve the buffered overload's lifetime contract; only the ownership overload opts into in-place resources.
        Stream buffered = onnx is UnmanagedMemoryStream ? Stream.Synchronized(onnx) : onnx;
        return LoadCore(buffered, tokenizerJson, config, prefix, options);
    }

    private static SentenceEmbedder LoadCore(Stream onnx, Stream tokenizerJson, EmbeddingConfig config, string prefix, ModelOptions? options)
    {
        ArgumentNullException.ThrowIfNull(onnx);
        ArgumentNullException.ThrowIfNull(tokenizerJson);
        Tokenizer tokenizer = Tokenizer.FromStream(tokenizerJson, TokenizerFormat.HuggingFaceJson);
        int pad = PadIdOf(tokenizer);
        Session session = Session.Load(onnx, options);
        try
        {
            return new SentenceEmbedder(session, tokenizer, config, pad, prefix);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>Loads stream-backed weights, optionally transferring ownership of the model stream to the embedder.</summary>
    /// <param name="onnx">The ONNX model. Embedded resources load in place; their storage must remain unchanged while the embedder lives.</param>
    /// <param name="tokenizerJson">The tokenizer JSON; read to the end and left open.</param>
    /// <param name="config">Pooling, normalization and maximum sequence length.</param>
    /// <param name="leaveOpen">If false, the embedder closes the model stream on disposal, or this call closes it on failure. If true, the caller keeps it open until the embedder is disposed.</param>
    /// <param name="prefix">Text prepended to every input; default empty.</param>
    /// <param name="options">Session options; null uses OneModel defaults.</param>
    /// <returns>An embedder the caller disposes.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidDataException">A stream is damaged or the graph is not an encoder.</exception>
    public static SentenceEmbedder Load(Stream onnx, Stream tokenizerJson, EmbeddingConfig config, bool leaveOpen, string prefix = "", ModelOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(onnx);
        try
        {
            SentenceEmbedder embedder = LoadCore(onnx, tokenizerJson, config, prefix, options);
            if (!leaveOpen)
            {
                embedder._ownedModelStream = onnx;
            }

            return embedder;
        }
        catch
        {
            if (!leaveOpen)
            {
                onnx.Dispose();
            }

            throw;
        }
    }

    /// <summary>The pooling, normalization and length settings in use.</summary>
    public EmbeddingConfig Config { get; }

    /// <summary>The text put in front of every input.</summary>
    public string Prefix { get; }

    /// <summary>The longest input in tokens, special tokens included. Longer inputs are cut off at the end.</summary>
    public int MaxSequenceLength => Config.MaxSequenceLength;

    /// <inheritdoc/>
    public int Dimensions { get; }

    /// <summary>The most texts run through the model in one call. Default 32; 1 to 4096. Larger batches use more memory.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 1 to 4096.</exception>
    public int BatchSize
    {
        get => _batchSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxBatchSize);
            _batchSize = value;
        }
    }

    /// <summary>The token IDs the model sees for a text: prefix, special tokens and truncation included.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The IDs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public int[] TokenIds(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            return Tokenize(text);
        }
    }

    /// <inheritdoc/>
    public float[] Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            return RunBatch([Tokenize(text)])[0];
        }
    }

    /// <inheritdoc/>
    public float[][] EmbedBatch(IReadOnlyList<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        for (int i = 0; i < texts.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(texts[i]);
        }

        lock (_gate)
        {
            var tokens = new int[texts.Count][];
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = Tokenize(texts[i]);
            }

            // Longest first, so a group pads little (Reimers and Gurevych 2019 do the same); the order of results is restored below.
            int[] order = [.. Enumerable.Range(0, tokens.Length).OrderByDescending(i => tokens[i].Length).ThenBy(i => i)];
            var result = new float[tokens.Length][];
            for (int start = 0; start < order.Length; start += _batchSize)
            {
                int count = Math.Min(_batchSize, order.Length - start);
                var group = new int[count][];
                for (int k = 0; k < count; k++)
                {
                    group[k] = tokens[order[start + k]];
                }

                float[][] vectors = RunBatch(group);
                for (int k = 0; k < count; k++)
                {
                    result[order[start + k]] = vectors[k];
                }
            }

            return result;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<float[]> EmbedMany(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return EmbedManyCore(texts);
    }

    private IEnumerable<float[]> EmbedManyCore(IEnumerable<string> texts)
    {
        var buffer = new List<string>(_batchSize);
        foreach (string text in texts)
        {
            buffer.Add(text);
            if (buffer.Count == _batchSize)
            {
                foreach (float[] v in EmbedBatch(buffer))
                {
                    yield return v;
                }

                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            foreach (float[] v in EmbedBatch(buffer))
            {
                yield return v;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _session.Dispose();
        _ownedModelStream?.Dispose();
        _ownedModelStream = null;
    }

    private int[] Tokenize(string text)
    {
        string input = Prefix.Length == 0 ? text : Prefix + text;
        if (_spaceMarker)
        {
            // Observed in output of sentence-transformers 6.2.0 with multilingual-e5-small: U+0085 (next line) acts as a space, as
            // SentencePiece's normalizer makes it; the bare tokenizer.json pipeline keeps it as a piece of its own.
            input = input.Replace('\u0085', ' ');
        }

        TokenEncoding enc = _tokenizer.Encode(input.AsSpan());
        if (_spaceMarker)
        {
            // Passes are bounded: each one removes at least one whitespace piece or stops.
            for (int pass = 0; pass < 4; pass++)
            {
                string? rewritten = TrimWhitespacePieces(input, enc);
                if (rewritten is null)
                {
                    break;
                }

                input = rewritten;
                enc = _tokenizer.Encode(input.AsSpan());
            }
        }

        int[] ids = enc.Ids;
        int room = Config.MaxSequenceLength - _frameLead - _frameTrail;
        int content = ids.Length - _frameLead - _frameTrail;
        if (content > room)
        {
            // Longest-first truncation of a single sequence keeps the first pieces and the closing special tokens.
            ids = [.. ids[..(_frameLead + room)], .. ids[(ids.Length - _frameTrail)..]];
        }

        return ids;
    }

    // Observed in output of sentence-transformers 6.2.0 (transformers 5.22.1) with multilingual-e5-small: its SentencePiece
    // tokenizer treats white space as SentencePiece does (Kudo and Richardson 2018 §3.2, remove-extra-whitespaces): characters the
    // normalizer maps to a space are collapsed to one space, and white space before a special token or at the end of the text
    // gives no piece, whereas the bare tokenizer.json pipeline emits "▁" pieces for them. White space after a special token is kept.
    // Returns the text with those runs rewritten, or null when nothing needs to change.
    private string? TrimWhitespacePieces(string input, TokenEncoding enc)
    {
        List<(int Start, int End, string Replacement)>? edits = null;
        int n = enc.Length;
        int i = 0;
        while (i < n)
        {
            if (IsSpecial(input, enc, i) || !IsSpaceOnly(enc.Tokens[i]))
            {
                i++;
                continue;
            }

            int j = i;
            while (j + 1 < n && !IsSpecial(input, enc, j + 1) && IsSpaceOnly(enc.Tokens[j + 1]))
            {
                j++;
            }

            bool beforeSpecial = j + 1 >= n || IsSpecial(input, enc, j + 1);

            // The pieces of a run cover only some of the characters that became spaces, so the edit takes the whole gap between the
            // neighbouring pieces. A neighbour the post-processor added has no position: the gap then runs to the end of the text.
            int start = i == 0 || enc.SpecialTokensMask[i - 1] ? 0 : enc.Offsets[i - 1].End;
            int end = j + 1 >= n || enc.SpecialTokensMask[j + 1] ? input.Length : enc.Offsets[j + 1].Start;
            if (end > start && start >= 0 && end <= input.Length)
            {
                if (beforeSpecial)
                {
                    (edits ??= []).Add((start, end, ""));
                }
                else if (j > i || enc.Tokens[i].Length > 1)
                {
                    (edits ??= []).Add((start, end, " "));
                }
            }

            i = j + 1;
        }

        if (edits is null)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder(input.Length);
        int at = 0;
        foreach ((int start, int end, string replacement) in edits)
        {
            sb.Append(input, at, start - at).Append(replacement);
            at = end;
        }

        sb.Append(input, at, input.Length - at);
        return sb.ToString();
    }

    // A piece is special when the post-processor added it, or when it is an added special token the text itself spells out (an unknown
    // character that maps to the unknown token is not: its offsets cover the character, not the token's spelling).
    private bool IsSpecial(string input, TokenEncoding enc, int i)
    {
        if (enc.SpecialTokensMask[i])
        {
            return true;
        }

        if (!_tokenizer.IsSpecialToken(enc.Ids[i]))
        {
            return false;
        }

        TokenOffset o = enc.Offsets[i];
        return o.End <= input.Length && o.Start >= 0 && o.End >= o.Start
            && input.AsSpan(o.Start, o.End - o.Start).SequenceEqual(_tokenizer.IdToToken(enc.Ids[i]));
    }

    private static bool IsSpaceOnly(string piece)
    {
        foreach (char c in piece)
        {
            if (c != '\u2581')
            {
                return false;
            }
        }

        return piece.Length > 0;
    }

    private float[][] RunBatch(int[][] rows)
    {
        int n = rows.Length;
        int length = 1;
        foreach (int[] r in rows)
        {
            length = Math.Max(length, r.Length);
        }

        var ids = new long[n * length];
        var mask = new long[n * length];
        Array.Fill(ids, _padId);
        for (int r = 0; r < n; r++)
        {
            for (int i = 0; i < rows[r].Length; i++)
            {
                ids[(r * length) + i] = rows[r][i];
                mask[(r * length) + i] = 1;
            }
        }

        using Tensor idTensor = MakeIds(ids, n, length);
        using Tensor maskTensor = MakeIds(mask, n, length);
        using Tensor? typeTensor = _hasTypeIds ? MakeIds(new long[n * length], n, length) : null;
        var inputs = new Dictionary<string, Tensor> { ["input_ids"] = idTensor, ["attention_mask"] = maskTensor };
        if (typeTensor is { } types)
        {
            inputs["token_type_ids"] = types;
        }

        using SessionOutputs outputs = _session.Run(inputs, [_outputName]);
        ReadOnlySpan<float> hidden = outputs[_outputName].Data<float>();
        if (hidden.Length != n * length * Dimensions)
        {
            throw new InvalidDataException("The encoder's output does not have the expected size.");
        }

        var result = new float[n][];
        for (int r = 0; r < n; r++)
        {
            result[r] = Pool(hidden.Slice(r * length * Dimensions, rows[r].Length * Dimensions), rows[r].Length);
        }

        return result;
    }

    // Pools the rows of one text; only the rows the attention mask keeps are passed in, so padding never takes part.
    private float[] Pool(ReadOnlySpan<float> tokens, int count)
    {
        int w = Dimensions;
        var v = new float[w];
        if (count == 0)
        {
            return v;
        }

        switch (Config.Pooling)
        {
            case PoolingMode.Cls:
                tokens[..w].CopyTo(v);
                break;
            case PoolingMode.Max:
                tokens[..w].CopyTo(v);
                for (int t = 1; t < count; t++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        v[j] = Math.Max(v[j], tokens[(t * w) + j]);
                    }
                }

                break;
            default:
                for (int t = 0; t < count; t++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        v[j] += tokens[(t * w) + j];
                    }
                }

                float scale = Config.Pooling == PoolingMode.Mean ? 1f / count : 1f / MathF.Sqrt(count);
                for (int j = 0; j < w; j++)
                {
                    v[j] *= scale;
                }

                break;
        }

        if (Config.Normalize)
        {
            // L2 normalization with a floor of 1e-12 on the norm, so an all-zero vector stays zero instead of becoming NaN.
            double sum = 0;
            for (int j = 0; j < w; j++)
            {
                sum += (double)v[j] * v[j];
            }

            float inv = (float)(1.0 / Math.Max(Math.Sqrt(sum), 1e-12));
            for (int j = 0; j < w; j++)
            {
                v[j] *= inv;
            }
        }

        return v;
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

    // How many special tokens the post-processor puts before and after a single sequence.
    private static (int Lead, int Trail) SpecialTokenFrame(Tokenizer tokenizer)
    {
        (int[] prefix, int[] suffix) = Encoders.TransformerEncoder.SpecialTokenFrame(tokenizer);
        return (prefix.Length, suffix.Length);
    }

    // The padding token is the tokenizer file's own: its declared padding, else the first of the usual pad tokens.
    private static int PadIdOf(Tokenizer tokenizer)
    {
        if (tokenizer.Padding is { } p)
        {
            return p.PadId;
        }

        foreach (string name in new[] { "[PAD]", "<pad>" })
        {
            if (tokenizer.TokenToId(name) is int id)
            {
                return id;
            }
        }

        return 0;
    }
}
