using System.Text.Json;

namespace RecursiveAI.OneNlp.Neural.Embeddings;

/// <summary>How the per-token vectors of a sentence become one vector.</summary>
public enum PoolingMode
{
    /// <summary>The vector of the first token (<c>[CLS]</c> or <c>&lt;s&gt;</c>). Devlin et al. 2019 §3.</summary>
    Cls,

    /// <summary>The mean over the tokens the attention mask keeps (Reimers and Gurevych 2019 §3, "MEAN" strategy).</summary>
    Mean,

    /// <summary>The element-wise maximum over the kept tokens (Reimers and Gurevych 2019 §3, "MAX" strategy).</summary>
    Max,

    /// <summary>The sum over the kept tokens divided by the square root of their count.</summary>
    MeanSqrtLength,
}

/// <summary>
/// What a sentence-transformers-format model directory says about how to turn the transformer's output into an embedding: the
/// pooling mode, whether the result is normalized to unit length, and the longest sequence. It is read as data from the files
/// <c>modules.json</c>, <c>1_Pooling/config.json</c> and <c>sentence_bert_config.json</c>.
/// </summary>
/// <param name="Pooling">The pooling mode.</param>
/// <param name="Normalize">Whether the vector is scaled to unit L2 length.</param>
/// <param name="MaxSequenceLength">The longest input in tokens, special tokens included.</param>
public sealed record EmbeddingConfig(PoolingMode Pooling, bool Normalize, int MaxSequenceLength)
{
    /// <summary>The largest sequence length any file may declare.</summary>
    public const int MaxSequenceLengthLimit = 1 << 16;

    private const int MaxFileBytes = 1 << 20;

    /// <summary>The sequence length used when the model gives none.</summary>
    public const int DefaultMaxSequenceLength = 512;

    /// <summary>Reads the configuration of a model directory.</summary>
    /// <param name="directory">The directory with <c>modules.json</c>, the pooling folder it names and, optionally, <c>sentence_bert_config.json</c>.</param>
    /// <returns>The configuration. A missing <c>modules.json</c> means a bare transformer with mean pooling and no normalization.</returns>
    /// <exception cref="NotSupportedException">The model has a module this library does not run (for example a dense projection), combines several pooling modes, or uses a pooling mode other than cls, mean, max and mean_sqrt_len (an enabled flag this library does not know is reported as no pooling mode).</exception>
    /// <exception cref="InvalidDataException">A file is not valid JSON of the expected shape, or is larger than 1 MiB.</exception>
    public static EmbeddingConfig Load(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string modulesPath = Path.Combine(directory, "modules.json");
        string? modules = File.Exists(modulesPath) ? ReadBounded(modulesPath) : null;
        string? bert = Optional(Path.Combine(directory, "sentence_bert_config.json"));
        string? poolingPath = null;
        if (modules is not null)
        {
            poolingPath = Path.Combine(directory, PoolingFolder(modules), "config.json");
        }

        string? pooling = poolingPath is not null && File.Exists(poolingPath) ? ReadBounded(poolingPath) : null;
        return Parse(modules, pooling, bert);
    }

    /// <summary>Builds the configuration from the contents of the three files.</summary>
    /// <param name="modulesJson">The contents of <c>modules.json</c>, or <see langword="null"/>.</param>
    /// <param name="poolingJson">The contents of the pooling module's <c>config.json</c>, or <see langword="null"/>.</param>
    /// <param name="sentenceBertJson">The contents of <c>sentence_bert_config.json</c>, or <see langword="null"/>.</param>
    /// <exception cref="NotSupportedException">See <see cref="Load(string)"/>.</exception>
    /// <exception cref="InvalidDataException">A text is not valid JSON of the expected shape.</exception>
    public static EmbeddingConfig Parse(string? modulesJson, string? poolingJson, string? sentenceBertJson)
    {
        bool normalize = false;
        bool hasPooling = false;
        if (modulesJson is not null)
        {
            using JsonDocument doc = Open(modulesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("modules.json must be an array.");
            }

            foreach (JsonElement m in doc.RootElement.EnumerateArray())
            {
                string type = TypeOf(m);
                switch (type[(type.LastIndexOf('.') + 1)..])
                {
                    case "Transformer":
                        break;
                    case "Pooling":
                        hasPooling = true;
                        break;
                    case "Normalize":
                        normalize = true;
                        break;
                    default:
                        throw new NotSupportedException($"The model has a '{type}' module, which this library does not run. Supported modules: Transformer, Pooling (cls, mean, max or mean_sqrt_len), Normalize.");
                }
            }
        }

        PoolingMode mode = PoolingMode.Mean;
        if (hasPooling || poolingJson is not null)
        {
            if (poolingJson is null)
            {
                throw new InvalidDataException("modules.json names a pooling module but its config.json is missing.");
            }

            mode = ReadPooling(poolingJson);
        }

        int max = DefaultMaxSequenceLength;
        if (sentenceBertJson is not null)
        {
            using JsonDocument doc = Open(sentenceBertJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("max_seq_length", out JsonElement v) && v.ValueKind == JsonValueKind.Number)
            {
                max = v.TryGetInt32(out int n) ? n : -1;
            }
        }

        if (max < 2 || max > MaxSequenceLengthLimit)
        {
            throw new InvalidDataException($"max_seq_length must be 2 to {MaxSequenceLengthLimit}.");
        }

        return new EmbeddingConfig(mode, normalize, max);
    }

    private static PoolingMode ReadPooling(string json)
    {
        using JsonDocument doc = Open(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The pooling config must be an object.");
        }

        (string Key, PoolingMode Mode)[] known =
        [
            ("pooling_mode_cls_token", PoolingMode.Cls),
            ("pooling_mode_mean_tokens", PoolingMode.Mean),
            ("pooling_mode_max_tokens", PoolingMode.Max),
            ("pooling_mode_mean_sqrt_len_tokens", PoolingMode.MeanSqrtLength),
        ];
        PoolingMode? found = null;
        foreach ((string key, PoolingMode mode) in known)
        {
            if (doc.RootElement.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.True)
            {
                if (found is not null)
                {
                    throw new NotSupportedException("The model concatenates several pooling modes, which this library does not run.");
                }

                found = mode;
            }
        }

        return found ?? throw new InvalidDataException("The pooling config enables no pooling mode.");
    }

    private static string PoolingFolder(string modulesJson)
    {
        using JsonDocument doc = Open(modulesJson);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement m in doc.RootElement.EnumerateArray())
            {
                string type = TypeOf(m);
                if (type.EndsWith(".Pooling", StringComparison.Ordinal) && m.TryGetProperty("path", out JsonElement p) && p.ValueKind == JsonValueKind.String)
                {
                    string path = p.GetString() ?? "";
                    // The path names a folder inside the model directory; refuse anything that leaves it.
                    if (path.Length == 0 || Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("The pooling module's path must be a folder inside the model directory.");
                    }

                    return path;
                }
            }
        }

        return "1_Pooling";
    }

    private static string TypeOf(JsonElement module)
    {
        if (module.ValueKind != JsonValueKind.Object || !module.TryGetProperty("type", out JsonElement t) || t.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Every module in modules.json needs a string 'type'.");
        }

        return t.GetString() ?? "";
    }

    private static JsonDocument Open(string json)
    {
        try
        {
            return JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("A model configuration file is not valid JSON.", e);
        }
    }

    private static string? Optional(string path) => File.Exists(path) ? ReadBounded(path) : null;

    private static string ReadBounded(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxFileBytes)
        {
            throw new InvalidDataException($"'{info.Name}' is larger than 1 MiB.");
        }

        return File.ReadAllText(path);
    }
}
