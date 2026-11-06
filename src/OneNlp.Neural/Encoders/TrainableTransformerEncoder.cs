using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Onnx;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>The architecture family of a trainable transformer encoder.</summary>
public enum TransformerFamily
{
    /// <summary>Decide from the names of the weights in the exported graph.</summary>
    Auto,

    /// <summary>ModernBERT and its relatives, such as mmBERT: pre-norm, rotary positions, gated feed-forward, alternating global and local attention (<see cref="ModernBertModule"/>).</summary>
    ModernBert,

    /// <summary>BERT, RoBERTa and XLM-R: post-norm, learned absolute positions (<see cref="PostNormModule"/>).</summary>
    PostNorm,
}

/// <summary>Options for loading a <see cref="TrainableTransformerEncoder"/>. Every number left at 0 is read from the exported graph, falling back to the model family's usual value.</summary>
public sealed class TrainableTransformerOptions
{
    /// <summary>The architecture family. Default <see cref="TransformerFamily.Auto"/>.</summary>
    public TransformerFamily Family { get; init; } = TransformerFamily.Auto;

    /// <summary>How the pieces of one token become its vector. Default <see cref="SubwordPooling.Mean"/>.</summary>
    public SubwordPooling Pooling { get; init; } = SubwordPooling.Mean;

    /// <summary>The longest sequence given to the model, special tokens included (2 to 65,536). Default 512.</summary>
    public int MaxLength { get; init; } = 512;

    /// <summary>
    /// Keep the token embedding table out of training. Default <see langword="true"/>: the table (192 to 197 million values in the multilingual
    /// models) is a large share of the optimizer's work and memory, fine-tuning for a task barely moves it, and it is not saved with a pipeline
    /// (it is read again from the exported model when the pipeline loads). With <see langword="false"/> the table trains, and a pipeline holding the encoder
    /// cannot be saved.
    /// </summary>
    public bool FreezeEmbeddings { get; init; } = true;

    /// <summary>Attention heads; 0 means width / 64.</summary>
    public int Heads { get; init; }

    /// <summary>ModernBERT family: every N-th layer, from the first, attends globally. Default 3.</summary>
    public int GlobalEvery { get; init; } = 3;

    /// <summary>ModernBERT family: the total width of a local layer's window. Default 128.</summary>
    public int LocalWindow { get; init; } = 128;

    /// <summary>ModernBERT family: the rotary base of global layers; 0 reads it from the graph, falling back to 160,000.</summary>
    public float GlobalRopeTheta { get; init; }

    /// <summary>ModernBERT family: the rotary base of local layers; 0 reads it from the graph, falling back to the global base.</summary>
    public float LocalRopeTheta { get; init; }

    /// <summary>The ε of the layer norms; 0 reads it from the graph.</summary>
    public float NormEpsilon { get; init; }

    /// <summary>Post-norm family: the position of the first token. Default 2 (XLM-R, RoBERTa); use 0 for BERT.</summary>
    public int PositionOffset { get; init; } = 2;

    /// <summary>Most padded pieces (rows × length) run through the model at once. Default 8192.</summary>
    public int MaxBatchPieces { get; init; } = 8192;

    /// <summary>The seed of the (immediately overwritten) initial weights; kept so every load is deterministic. Default 1.</summary>
    public ulong Seed { get; init; } = 1;

    /// <summary>
    /// Whether the encoder reads the words of a multiword token instead of its surface. In many languages the treebanks split a written word into
    /// several syntactic words (French <c>du</c> = <c>de</c> + <c>le</c>, German <c>zum</c> = <c>zu</c> + <c>dem</c>, Spanish <c>dárselo</c> = <c>dar</c> + <c>se</c> +
    /// <c>lo</c>); a transformer that reads the surface gives all of them the same vector. With this option, a token whose <see cref="TokenColumn.Norm"/> is set
    /// is read as that word, and neighbouring tokens that both have a norm and touch each other are separated by a space, so the encoder sees
    /// <c>de le</c>. Tokens without a norm are read as they are written. Default <see langword="false"/>.
    /// </summary>
    public bool ExpandWordForms { get; init; }
}

/// <summary>
/// A transformer encoder whose weights train: the ModernBERT family (mmBERT) or the BERT/XLM-R family, rebuilt from an exported ONNX model's weights
/// in modules written on OneModel's tensor ops. It is the fine-tunable twin of <see cref="TransformerEncoder"/>, with the same tokenizer, windows,
/// and subword pooling, and it plugs into the same heads and into <c>Train</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Training.</b> <see cref="Weights"/> holds the layers' weights; inside a recording scope <see cref="Encode"/> returns a tensor that carries the autodiff graph back to
/// them, and <c>Train</c> updates them with the heads (with one learning rate for both). Inside <c>GradMode.NoGrad()</c>, or in evaluation mode, it computes plain values.
/// </para>
/// <para>
/// <b>Pooling</b> is a differentiable gather: each token's vector is the mean (or the first) of the rows of its pieces.
/// Documents longer than <see cref="MaxLength"/> are cut into windows that overlap by half, as in <see cref="TransformerEncoder"/>.
/// </para>
/// <para>
/// <b>Saving.</b> A saved pipeline names this encoder by kind <c>transformer-trainable</c> and stores its settings and the weights that train. The exported
/// model and the tokenizer are referenced by file name, as for <see cref="TransformerEncoder"/> (see <see cref="TransformerEncoder.ModelDirectory"/>), because the
/// frozen embedding table is read from the model again on load. This class is not safe for concurrent calls.
/// </para>
/// </remarks>
public sealed class TrainableTransformerEncoder : IEncoder
{
    /// <summary>The kind name a saved pipeline uses for this encoder.</summary>
    public const string EncoderKind = "transformer-trainable";

    private const int MaxLengthLimit = 1 << 16;

    private readonly NnModule _module;
    private readonly Func<Tensor, int[], Tensor> _forward;
    private readonly Tokenizer _tokenizer;
    private readonly int[] _prefix;
    private readonly int[] _suffix;
    private readonly string _modelFile;
    private readonly string _tokenizerFile;
    private readonly string? _sha256;
    private readonly TransformerFamily _family;
    private readonly TrainableTransformerOptions _options;
    private readonly string _settingsJson;

    private TrainableTransformerEncoder(
        NnModule module,
        Func<Tensor, int[], Tensor> forward,
        int width,
        TransformerFamily family,
        Tokenizer tokenizer,
        string modelFile,
        string tokenizerFile,
        string? sha256,
        TrainableTransformerOptions options,
        string resolvedSettings)
    {
        _module = module;
        _forward = forward;
        Width = width;
        _family = family;
        _tokenizer = tokenizer;
        _modelFile = modelFile;
        _tokenizerFile = tokenizerFile;
        _sha256 = sha256;
        _options = options;
        _settingsJson = resolvedSettings;
        (_prefix, _suffix) = TransformerEncoder.SpecialTokenFrame(tokenizer);
        if (_prefix.Length + _suffix.Length >= options.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The special tokens alone fill the sequence.");
        }
    }

    /// <summary>Loads an exported transformer and its tokenizer as a trainable encoder.</summary>
    /// <param name="modelPath">The ONNX file, written by the PyTorch exporter (the non-dynamo one) from a ModernBERT-family or BERT/XLM-R-family model.</param>
    /// <param name="tokenizerPath">The model's <c>tokenizer.json</c>.</param>
    /// <param name="options">The options; <see langword="null"/> for the defaults.</param>
    /// <param name="modelSha256">The expected SHA-256 of the model file as lower-case hex, saved with the pipeline; may be <see langword="null"/>.</param>
    /// <returns>The encoder, in training mode. The caller disposes it.</returns>
    /// <exception cref="InvalidDataException">The graph is not one of the supported families or lacks the expected weights.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="TrainableTransformerOptions.MaxLength"/> is out of range.</exception>
    public static TrainableTransformerEncoder LoadOnnx(string modelPath, string tokenizerPath, TrainableTransformerOptions? options = null, string? modelSha256 = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);
        ArgumentException.ThrowIfNullOrEmpty(tokenizerPath);
        options ??= new TrainableTransformerOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxLength, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaxLength, MaxLengthLimit);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxBatchPieces, 2);
        Tokenizer tokenizer = Tokenizer.FromFile(tokenizerPath);
        tokenizer.Padding = null;
        tokenizer.Truncation = null;

        using IrModel model = OnnxImporter.Load(modelPath);
        var reader = new OnnxEncoderWeights(model.Graph);
        TransformerFamily family = options.Family == TransformerFamily.Auto
            ? reader.DetectFamily() ?? throw new InvalidDataException("The graph is neither a ModernBERT-family nor a BERT/XLM-R-family encoder (no 'tok_embeddings' or 'word_embeddings' weight).")
            : options.Family;

        if (family == TransformerFamily.ModernBert)
        {
            (ModernBertModule module, ModernBertConfig config) = reader.LoadModernBert(new ModernBertConfig(), options, options.FreezeEmbeddings, options.Seed);
            string settings = Resolved(family, options, config.Heads, config.GlobalEvery, config.LocalWindow, config.GlobalRopeTheta, config.LocalRopeTheta, config.NormEpsilon, options.PositionOffset);
            return new TrainableTransformerEncoder(module, (ids, lengths) => module.Forward(ids, lengths), config.Width, family, tokenizer, modelPath, tokenizerPath, modelSha256, options, settings);
        }
        else
        {
            (PostNormModule module, PostNormConfig config) = reader.LoadPostNorm(new PostNormConfig(), options, options.FreezeEmbeddings, options.Seed, ConfigHeads(modelPath));
            string settings = Resolved(family, options, config.Heads, options.GlobalEvery, options.LocalWindow, options.GlobalRopeTheta, options.LocalRopeTheta, config.NormEpsilon, config.PositionOffset);
            return new TrainableTransformerEncoder(module, (ids, lengths) => module.Forward(ids, lengths), config.Width, family, tokenizer, modelPath, tokenizerPath, modelSha256, options, settings);
        }
    }

    /// <inheritdoc/>
    public string Kind => EncoderKind;

    /// <inheritdoc/>
    public int Width { get; }

    /// <summary>The architecture family in use.</summary>
    public TransformerFamily Family => _family;

    /// <summary>How pieces are pooled into tokens.</summary>
    public SubwordPooling Pooling => _options.Pooling;

    /// <summary>The longest sequence given to the model, special tokens included.</summary>
    public int MaxLength => _options.MaxLength;

    /// <inheritdoc/>
    /// <remarks>The layers' weights, in a fixed state order. A frozen embedding table is not part of it.</remarks>
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

        int capacity = MaxLength - _prefix.Length - _suffix.Length;
        var plans = new DocPlan?[docs.Length];
        var windows = new List<Window>();
        for (int d = 0; d < docs.Length; d++)
        {
            if (docs[d].Length == 0)
            {
                continue;
            }

            Doc source = _options.ExpandWordForms ? WordFormView(docs[d]) : docs[d];
            TokenEncoding enc = _tokenizer.Encode(source.Text, addSpecialTokens: false);
            plans[d] = new DocPlan(enc, TransformerEncoder.MapPiecesToTokens(source, enc));
            AddWindows(windows, d, enc.Length, capacity);
        }

        // Longest windows first, so each batch pads little; ties keep document order so the result does not depend on the sort.
        int[] order = [.. Enumerable.Range(0, windows.Count).OrderByDescending(i => windows[i].Count).ThenBy(i => i)];
        var hidden = new List<Tensor>();
        var pieceRows = new int[windows.Count]; // row in the concatenated hidden states of each window's first piece
        int rowBase = 0;
        for (int at = 0; at < order.Length;)
        {
            int length = windows[order[at]].Count + _prefix.Length + _suffix.Length;
            int rows = Math.Max(1, Math.Min(order.Length - at, _options.MaxBatchPieces / length));
            var ids = new long[rows * length];
            var lengths = new int[rows];
            for (int r = 0; r < rows; r++)
            {
                Window w = windows[order[at + r]];
                int[] pieceIds = plans[w.Doc]!.Encoding.Ids;
                int p = r * length;
                foreach (int id in _prefix)
                {
                    ids[p++] = id;
                }

                for (int i = 0; i < w.Count; i++)
                {
                    ids[p++] = pieceIds[w.First + i];
                }

                foreach (int id in _suffix)
                {
                    ids[p++] = id;
                }

                lengths[r] = w.Count + _prefix.Length + _suffix.Length;
                pieceRows[order[at + r]] = rowBase + (r * length) + _prefix.Length;
            }

            Tensor idTensor = Tensor.FromSpan<long>(ids, Shape.Create(rows, length));
            ActivationScope.Track(idTensor);
            Tensor h = _forward(idTensor, lengths);
            hidden.Add(h.Reshape(rows * length, Width));
            rowBase += rows * length;
            at += rows;
        }

        // The token rows are gathered from the pieces' rows; a token no piece reaches reads the extra zero row at the end.
        Tensor zeroRow = Tensor.Zeros(DType.F32, Shape.Create(1, Width));
        ActivationScope.Track(zeroRow);
        hidden.Add(zeroRow);
        Tensor all = Ops.Concat(hidden.ToArray(), 0);
        int zero = rowBase;
        bool mean = Pooling == SubwordPooling.Mean;
        var rowsOf = new List<int>?[total];
        var firstPiece = new int[total];
        Array.Fill(firstPiece, int.MaxValue);
        var firstRow = new int[total];
        for (int wi = 0; wi < windows.Count; wi++)
        {
            Window w = windows[wi];
            DocPlan plan = plans[w.Doc]!;
            for (int i = w.OwnFrom; i < w.OwnTo; i++)
            {
                (int a, int b) = plan.PieceTokens[i];
                int row = pieceRows[wi] + (i - w.First);
                for (int t = a; a >= 0 && t <= b; t++)
                {
                    int token = starts[w.Doc] + t;
                    if (mean)
                    {
                        (rowsOf[token] ??= []).Add(row);
                    }
                    else if (i < firstPiece[token])
                    {
                        firstPiece[token] = i;
                        firstRow[token] = row;
                    }
                }
            }
        }

        int maxPieces = 1;
        if (mean)
        {
            foreach (List<int>? l in rowsOf)
            {
                maxPieces = Math.Max(maxPieces, l?.Count ?? 0);
            }
        }

        Tensor pooled = default;
        bool scaled = false;
        var scale = new float[total];
        for (int j = 0; j < maxPieces; j++)
        {
            var index = new int[total];
            for (int token = 0; token < total; token++)
            {
                if (mean)
                {
                    List<int>? l = rowsOf[token];
                    index[token] = l is not null && j < l.Count ? l[j] : zero;
                    if (j == 0)
                    {
                        scale[token] = l is null ? 0f : 1f / l.Count;
                        scaled |= l is not null && l.Count > 1;
                    }
                }
                else
                {
                    index[token] = firstPiece[token] == int.MaxValue ? zero : firstRow[token];
                }
            }

            Tensor indexTensor = Tensor.FromSpan<int>(index, Shape.Create(total));
            ActivationScope.Track(indexTensor);
            Tensor part = Ops.Embedding(all, indexTensor);
            pooled = j == 0 ? part : Ops.Add(pooled, part);
        }

        if (scaled)
        {
            Tensor scaleTensor = Tensor.FromSpan<float>(scale, Shape.Create(total, 1));
            ActivationScope.Track(scaleTensor);
            pooled = Ops.Mul(pooled, scaleTensor);
        }

        return new EncodedBatch(pooled, starts);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">The embedding table is trainable: the pipeline file cannot hold it.</exception>
    public void WriteSettings(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (!_options.FreezeEmbeddings)
        {
            throw new NotSupportedException("A pipeline whose encoder trains the token embedding table is too large for the pipeline file; fine-tune with FreezeEmbeddings = true to save it.");
        }

        writer.WriteString("model", _modelFile);
        writer.WriteString("tokenizer", _tokenizerFile);
        if (_sha256 is not null)
        {
            writer.WriteString("modelSha256", _sha256);
        }

        using JsonDocument resolved = JsonDocument.Parse(_settingsJson);
        foreach (JsonProperty p in resolved.RootElement.EnumerateObject())
        {
            p.WriteTo(writer);
        }
    }

    internal static IEncoder FromSettings(JsonElement s)
    {
        string family = JsonSettings.String(s, "family", 16);
        string pooling = JsonSettings.String(s, "pooling", 16);
        if (pooling is not ("first" or "mean") || family is not ("modernbert" or "postnorm"))
        {
            throw new InvalidDataException("'family' must be 'modernbert' or 'postnorm' and 'pooling' must be 'first' or 'mean'.");
        }

        string? sha = s.TryGetProperty("modelSha256", out _) ? JsonSettings.String(s, "modelSha256", 64) : null;
        var options = new TrainableTransformerOptions
        {
            Family = family == "modernbert" ? TransformerFamily.ModernBert : TransformerFamily.PostNorm,
            Pooling = pooling == "mean" ? SubwordPooling.Mean : SubwordPooling.FirstPiece,
            MaxLength = JsonSettings.Int(s, "maxLength", 2, MaxLengthLimit),
            Heads = JsonSettings.Int(s, "heads", 1, 1024),
            GlobalEvery = JsonSettings.Int(s, "globalEvery", 1, 1024),
            LocalWindow = JsonSettings.Int(s, "localWindow", 2, MaxLengthLimit),
            GlobalRopeTheta = Real(s, "ropeThetaGlobal"),
            LocalRopeTheta = Real(s, "ropeThetaLocal"),
            NormEpsilon = Real(s, "normEpsilon"),
            PositionOffset = JsonSettings.Int(s, "positionOffset", 0, MaxLengthLimit),
            MaxBatchPieces = JsonSettings.Int(s, "maxBatchPieces", 2, 1 << 24),
            ExpandWordForms = s.TryGetProperty("expandForms", out JsonElement expand) && expand.ValueKind == JsonValueKind.True,
        };

        return LoadOnnx(TransformerEncoder.Resolve(JsonSettings.String(s, "model", 1024)), TransformerEncoder.Resolve(JsonSettings.String(s, "tokenizer", 1024)), options, sha);
    }

    /// <summary>
    /// Rebuilds a ModernBERT-family encoder from its parts, without the exported ONNX file: the settings a saved pipeline records, the shape,
    /// the tokenizer, the frozen token table (in row-range pieces) and the weights of the layers (a OneModel checkpoint of <see cref="Weights"/>).
    /// A model package that ships a fine-tuned encoder in several files loads it this way.
    /// </summary>
    /// <param name="settings">The settings object <see cref="WriteSettings"/> wrote (family <c>modernbert</c>, pooling, window sizes, rotary bases, epsilon, head count).</param>
    /// <param name="vocabularySize">Rows of the token table.</param>
    /// <param name="width">Hidden width.</param>
    /// <param name="layers">Number of transformer layers.</param>
    /// <param name="feedForwardWidth">Inner width of each half of the gated feed-forward.</param>
    /// <param name="tokenizerJson">The model's <c>tokenizer.json</c>; read to the end, not closed.</param>
    /// <param name="tableParts">The token table as little-endian float32 rows, split into consecutive pieces in row order; each piece is read to the end, not closed. Together exactly <c>vocabularySize * width * 4</c> bytes.</param>
    /// <param name="layerWeights">The checkpoint of the layers; read to the end, not closed.</param>
    /// <returns>The encoder in evaluation mode. The caller disposes it. It has no model file to refer to, so a pipeline that uses it is saved through <see cref="SharedEncoder"/>.</returns>
    /// <exception cref="InvalidDataException">The settings, the table's length or the checkpoint do not fit.</exception>
    public static TrainableTransformerEncoder FromParts(JsonElement settings, int vocabularySize, int width, int layers, int feedForwardWidth, Stream tokenizerJson, IReadOnlyList<Stream> tableParts, Stream layerWeights)
    {
        ArgumentNullException.ThrowIfNull(tokenizerJson);
        ArgumentNullException.ThrowIfNull(tableParts);
        ArgumentNullException.ThrowIfNull(layerWeights);
        if (JsonSettings.String(settings, "family", 16) != "modernbert")
        {
            throw new InvalidDataException("Only the ModernBERT family can be rebuilt from parts.");
        }

        var o = new TrainableTransformerOptions
        {
            Family = TransformerFamily.ModernBert,
            Pooling = JsonSettings.String(settings, "pooling", 16) == "first" ? SubwordPooling.FirstPiece : SubwordPooling.Mean,
            MaxLength = JsonSettings.Int(settings, "maxLength", 2, MaxLengthLimit),
            Heads = JsonSettings.Int(settings, "heads", 1, 1024),
            GlobalEvery = JsonSettings.Int(settings, "globalEvery", 1, 1024),
            LocalWindow = JsonSettings.Int(settings, "localWindow", 2, MaxLengthLimit),
            GlobalRopeTheta = Real(settings, "ropeThetaGlobal"),
            LocalRopeTheta = Real(settings, "ropeThetaLocal"),
            NormEpsilon = Real(settings, "normEpsilon"),
            PositionOffset = JsonSettings.Int(settings, "positionOffset", 0, MaxLengthLimit),
            MaxBatchPieces = JsonSettings.Int(settings, "maxBatchPieces", 2, 1 << 24),
            ExpandWordForms = settings.TryGetProperty("expandForms", out JsonElement expand) && expand.ValueKind == JsonValueKind.True,
        };

        var config = new ModernBertConfig
        {
            VocabularySize = vocabularySize,
            Width = width,
            Layers = layers,
            Heads = o.Heads,
            FeedForwardWidth = feedForwardWidth,
            GlobalEvery = o.GlobalEvery,
            LocalWindow = o.LocalWindow,
            GlobalRopeTheta = o.GlobalRopeTheta,
            LocalRopeTheta = o.LocalRopeTheta,
            NormEpsilon = o.NormEpsilon,
        };

        Tokenizer tokenizer = Tokenizer.FromStream(tokenizerJson, TokenizerFormat.HuggingFaceJson);
        tokenizer.Padding = null;
        tokenizer.Truncation = null;

        Tensor table = Tensor.Zeros(DType.F32, Shape.Create(vocabularySize, width));
        ModernBertModule? module = null;
        try
        {
            Span<byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(table.MutableData<float>());
            int filled = 0;
            foreach (Stream part in tableParts)
            {
                // Bounds: a read never runs past the end of the table; a piece that has more left over is rejected below.
                int n;
                while (filled < bytes.Length && (n = part.Read(bytes[filled..])) > 0)
                {
                    filled += n;
                }

                if (part.ReadByte() >= 0)
                {
                    throw new InvalidDataException("The token table pieces hold more than vocabularySize * width * 4 bytes.");
                }
            }

            if (filled != bytes.Length)
            {
                throw new InvalidDataException($"The token table pieces hold {filled} bytes where {bytes.Length} are needed.");
            }

            module = new ModernBertModule(config, o.Seed, table);
            table = default;
            Checkpoint.Load(layerWeights, module);
            module.Eval();
            string resolved = Resolved(TransformerFamily.ModernBert, o, config.Heads, config.GlobalEvery, config.LocalWindow, config.GlobalRopeTheta, config.LocalRopeTheta, config.NormEpsilon, o.PositionOffset);
            ModernBertModule m = module;
            module = null;
            return new TrainableTransformerEncoder(m, (ids, lengths) => m.Forward(ids, lengths), width, TransformerFamily.ModernBert, tokenizer, "parts", "parts", null, o, resolved);
        }
        catch (Exception e) when (e is CheckpointException or ArgumentException)
        {
            throw new InvalidDataException("The encoder parts do not fit together: " + e.Message, e);
        }
        finally
        {
            module?.Dispose();
            if (!table.IsEmpty)
            {
                table.Dispose();
            }
        }
    }

    // The head count of a distilled student is not in its weights; its export writes <model>.config.json beside the model. 0 when absent.
    private static int ConfigHeads(string modelPath)
    {
        string config = Path.ChangeExtension(modelPath, ".config.json");
        if (!File.Exists(config) || new FileInfo(config).Length > 1 << 20)
        {
            return 0;
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(config));
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("heads", out JsonElement h) && h.TryGetInt32(out int n) && n is >= 1 and <= 1024 ? n : 0;
    }

    private static float Real(JsonElement s, string name)
    {
        JsonElement p = JsonSettings.Property(s, name);
        if (p.ValueKind != JsonValueKind.Number || !p.TryGetSingle(out float v) || !float.IsFinite(v) || v < 0)
        {
            throw new InvalidDataException($"'{name}' must be a non-negative number.");
        }

        return v;
    }

    private static string Resolved(TransformerFamily family, TrainableTransformerOptions o, int heads, int globalEvery, int localWindow, float ropeGlobal, float ropeLocal, float epsilon, int positionOffset)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("family", family == TransformerFamily.ModernBert ? "modernbert" : "postnorm");
            w.WriteString("pooling", o.Pooling == SubwordPooling.Mean ? "mean" : "first");
            w.WriteNumber("maxLength", o.MaxLength);
            w.WriteNumber("heads", heads);
            w.WriteNumber("globalEvery", globalEvery);
            w.WriteNumber("localWindow", localWindow);
            w.WriteNumber("ropeThetaGlobal", ropeGlobal);
            w.WriteNumber("ropeThetaLocal", ropeLocal);
            w.WriteNumber("normEpsilon", epsilon);
            w.WriteNumber("positionOffset", positionOffset);
            w.WriteNumber("maxBatchPieces", o.MaxBatchPieces);
            if (o.ExpandWordForms)
            {
                w.WriteBoolean("expandForms", true);
            }

            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // The document as the encoder reads it with ExpandWordForms: tokens that carry a norm are read as that word, and two touching tokens that both
    // carry one (the pieces of one multiword token) are separated by a space. The view has the same tokens in the same order, so rows map back one to one.
    // Returns the document itself when no token has a norm.
    internal static Doc WordFormView(Doc doc)
    {
        ReadOnlySpan<int> norms = doc.GetColumn(TokenColumn.Norm);
        bool any = false;
        for (int i = 0; i < norms.Length && !any; i++)
        {
            any = norms[i] != 0;
        }

        if (!any)
        {
            return doc;
        }

        var text = new System.Text.StringBuilder(doc.Text.Length + 16);
        var tokens = new TokenBoundary[doc.Length];
        int copied = 0; // characters of the original text already copied
        for (int i = 0; i < doc.Length; i++)
        {
            Token t = doc[i];
            bool expanded = norms[i] != 0;
            if (t.Idx > copied)
            {
                text.Append(doc.Text, copied, t.Idx - copied);
            }
            else if (i > 0 && t.Idx == copied && expanded && norms[i - 1] != 0)
            {
                text.Append(' ');
            }

            int start = text.Length;
            if (expanded)
            {
                text.Append(doc.Strings[norms[i]]);
            }
            else
            {
                text.Append(doc.Text, t.Idx, t.Length);
            }

            tokens[i] = new TokenBoundary(start, text.Length - start);
            copied = t.Idx + t.Length;
        }

        if (copied < doc.Text.Length)
        {
            text.Append(doc.Text, copied, doc.Text.Length - copied);
        }

        return new Doc(doc.Strings, text.ToString(), tokens);
    }

    // The same windows as TransformerEncoder: overlap by half, each piece owned by the window whose centre is nearest.
    private static void AddWindows(List<Window> windows, int doc, int n, int capacity)
    {
        if (n == 0)
        {
            return;
        }

        int step = Math.Max(1, capacity / 2);
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

        for (int w = 0; w < firsts.Count; w++)
        {
            int lo = firsts[w];
            int hi = Math.Min(n, lo + capacity);
            int ownLo = w == 0 ? 0 : (firsts[w - 1] + firsts[w] + capacity) / 2;
            int ownHi = w == firsts.Count - 1 ? n : (firsts[w] + firsts[w + 1] + capacity) / 2;
            windows.Add(new Window(doc, lo, hi - lo, Math.Max(ownLo, lo), Math.Min(ownHi, hi)));
        }
    }

    private sealed class DocPlan(TokenEncoding encoding, (int First, int Last)[] pieceTokens)
    {
        public TokenEncoding Encoding { get; } = encoding;

        public (int First, int Last)[] PieceTokens { get; } = pieceTokens;
    }

    private readonly record struct Window(int Doc, int First, int Count, int OwnFrom, int OwnTo);
}
