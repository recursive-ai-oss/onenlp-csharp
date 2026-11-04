using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural;

/// <summary>
/// Saves a trained pipeline to one file and loads it back: the pipeline's structure and label sets as JSON, and the weights of its
/// encoders and heads in OneModel's checkpoint encoding.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layout</b> (little endian): 8 bytes <c>ONENLPPL</c>; u32 version (1); u32 flags (0); u64 config length; u64 weights length;
/// the config (UTF-8 JSON); the weights (a OneModel checkpoint holding the state of every encoder and head, named
/// <c>encoder0</c>…, <c>head0</c>…). The file length must equal 32 plus the two lengths.
/// </para>
/// <para>
/// <b>Config</b> (<c>docs/model-files.md</c>): <c>format</c>, <c>version</c>, <c>language</c>, <c>tokenizer</c> (only <c>rule</c> so far),
/// <c>encoders</c> (kind and settings), <c>components</c> in pipeline order, each a trainable head (kind, encoder index, config) or the name
/// of a registered built-in component.
/// </para>
/// <para>
/// <b>Reading is bounded.</b> The config is at most 16 MiB with nesting at most 32; the weights at most 1 GiB; counts of encoders
/// and components, label sets and sizes are capped; the declared lengths are checked against the real file length before anything is
/// allocated; the checkpoint reader checks every tensor against the shapes the config implies. A bad file is an
/// <see cref="InvalidDataException"/>, never another exception type.
/// </para>
/// </remarks>
public static class PipelineFile
{
    private const string Magic = "ONENLPPL";
    private const int HeaderSize = 32;
    private const int FormatVersion = 1;
    private const long MaxConfigBytes = 16L << 20;
    private const long MaxWeightsBytes = 1L << 30;
    private const int MaxEncoders = 64;
    private const int MaxComponents = 1024;
    private const string FormatName = "onenlp-pipeline";

    /// <summary>Saves a pipeline to a file, replacing it (written to a temporary name first, then renamed).</summary>
    /// <param name="pipeline">The pipeline. Its trainable components must be initialized; its other components must be registered in <see cref="ComponentRegistry"/>; its tokenizer must be a <see cref="RuleWordTokenizer"/>.</param>
    /// <param name="path">The destination file.</param>
    /// <exception cref="NotSupportedException">The pipeline holds a tokenizer or component that cannot be saved.</exception>
    /// <exception cref="InvalidOperationException">A trainable component has no weights yet.</exception>
    public static void Save(Pipeline pipeline, string path)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentException.ThrowIfNullOrEmpty(path);
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            Save(pipeline, stream);
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Saves a pipeline to a stream.</summary>
    /// <param name="pipeline">The pipeline; see <see cref="Save(Pipeline, string)"/> for what it may hold.</param>
    /// <param name="stream">A writable stream; left open.</param>
    /// <exception cref="NotSupportedException">The pipeline holds a tokenizer or component that cannot be saved.</exception>
    /// <exception cref="InvalidOperationException">A trainable component has no weights yet.</exception>
    public static void Save(Pipeline pipeline, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(stream);
        if (pipeline.Tokenizer is not RuleWordTokenizer)
        {
            throw new NotSupportedException("Only a pipeline with the rule word tokenizer can be saved so far.");
        }

        (List<ITrainableComponent> heads, List<IEncoder> encoders) = StateModule.Collect(pipeline);
        foreach (ITrainableComponent head in heads)
        {
            if (!head.IsInitialized)
            {
                throw new InvalidOperationException($"The component '{head.Name}' has no weights yet: train it before saving.");
            }
        }

        byte[] config = WriteConfig(pipeline, encoders);
        using var weights = new MemoryStream();
        Checkpoint.Save(weights, StateModule.ForWeights(encoders, heads));

        Span<byte> header = stackalloc byte[HeaderSize];
        Encoding.ASCII.GetBytes(Magic, header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], FormatVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header[16..], (ulong)config.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(header[24..], (ulong)weights.Length);
        stream.Write(header);
        stream.Write(config);
        weights.Position = 0;
        weights.CopyTo(stream);
    }

    /// <summary>Loads a saved pipeline from a file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>A pipeline with its components in evaluation mode, ready to <see cref="Pipeline.Process(string)"/> text. The caller disposes it.</returns>
    /// <exception cref="InvalidDataException">The file is not a valid saved pipeline.</exception>
    public static Pipeline Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Load(stream);
    }

    /// <summary>Loads a saved pipeline from a stream.</summary>
    /// <param name="stream">A readable, seekable stream positioned at the start of the saved pipeline and holding nothing after it; left open.</param>
    /// <returns>A pipeline with its components in evaluation mode. The caller disposes it.</returns>
    /// <exception cref="ArgumentException">The stream cannot seek.</exception>
    /// <exception cref="InvalidDataException">The data is not a valid saved pipeline.</exception>
    public static Pipeline Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must support seeking so the lengths can be checked.", nameof(stream));
        }

        try
        {
            return LoadCore(stream);
        }
        catch (Exception e) when (e is JsonException or CheckpointException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new InvalidDataException("The saved pipeline is invalid: " + e.Message, e);
        }
    }

    private static Pipeline LoadCore(Stream stream)
    {
        long remaining = stream.Length - stream.Position;
        if (remaining < HeaderSize)
        {
            throw new InvalidDataException("The saved pipeline is shorter than its header.");
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        stream.ReadExactly(header);
        if (!header[..8].SequenceEqual("ONENLPPL"u8))
        {
            throw new InvalidDataException("This is not a saved pipeline (bad magic).");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        ulong configLength = BinaryPrimitives.ReadUInt64LittleEndian(header[16..]);
        ulong weightsLength = BinaryPrimitives.ReadUInt64LittleEndian(header[24..]);
        if (version != FormatVersion || flags != 0)
        {
            throw new InvalidDataException($"Unsupported saved-pipeline version {version} (flags {flags}); this reader reads version {FormatVersion}.");
        }

        if (configLength > MaxConfigBytes || weightsLength > MaxWeightsBytes || configLength + weightsLength != (ulong)(remaining - HeaderSize))
        {
            throw new InvalidDataException("The declared lengths are too large or do not match the length of the data.");
        }

        byte[] config = new byte[(int)configLength];
        stream.ReadExactly(config);
        byte[] weights = new byte[(int)weightsLength];
        stream.ReadExactly(weights);

        using JsonDocument document = JsonDocument.Parse(config, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = document.RootElement;
        if (JsonSettings.String(root, "format") != FormatName)
        {
            throw new InvalidDataException("The config is not a OneNLP pipeline.");
        }

        if (JsonSettings.Int(root, "version", 0, int.MaxValue) != FormatVersion)
        {
            throw new InvalidDataException("Unsupported config version.");
        }

        Language language = Language.FromCode(JsonSettings.String(root, "language", 32));
        if (JsonSettings.String(root, "tokenizer") != "rule")
        {
            throw new InvalidDataException("Unsupported tokenizer; only 'rule' is known.");
        }

        JsonElement encodersJson = JsonSettings.Property(root, "encoders");
        JsonElement componentsJson = JsonSettings.Property(root, "components");
        if (encodersJson.ValueKind != JsonValueKind.Array || encodersJson.GetArrayLength() > MaxEncoders
            || componentsJson.ValueKind != JsonValueKind.Array || componentsJson.GetArrayLength() > MaxComponents)
        {
            throw new InvalidDataException("'encoders' and 'components' must be arrays of limited length.");
        }

        var encoders = new List<IEncoder>();
        foreach (JsonElement e in encodersJson.EnumerateArray())
        {
            encoders.Add(EncoderRegistry.Create(JsonSettings.String(e, "kind"), JsonSettings.Property(e, "settings")));
        }

        var pipeline = new Pipeline(new RuleWordTokenizer(), language);
        try
        {
            foreach (JsonElement c in componentsJson.EnumerateArray())
            {
                string name = JsonSettings.String(c, "name");
                if (JsonSettings.String(c, "type") == "head")
                {
                    int encoderIndex = JsonSettings.Int(c, "encoder", 0, encoders.Count - 1);
                    pipeline.Add(HeadRegistry.Create(JsonSettings.String(c, "kind"), name, encoders[encoderIndex], JsonSettings.Property(c, "config")));
                }
                else
                {
                    pipeline.Add(name);
                }
            }

            (List<ITrainableComponent> heads, List<IEncoder> used) = StateModule.Collect(pipeline);
            StateModule module = StateModule.ForWeights(used, heads);
            using var weightStream = new MemoryStream(weights, writable: false);
            Checkpoint.Load(weightStream, module);
            module.Eval();
            return pipeline;
        }
        catch
        {
            pipeline.Dispose();
            throw;
        }
    }

    private static byte[] WriteConfig(Pipeline pipeline, List<IEncoder> encoders)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("format", FormatName);
            w.WriteNumber("version", FormatVersion);
            w.WriteString("language", pipeline.Language.Code);
            w.WriteString("tokenizer", "rule");
            w.WriteStartArray("encoders");
            foreach (IEncoder encoder in encoders)
            {
                w.WriteStartObject();
                w.WriteString("kind", encoder.Kind);
                w.WriteStartObject("settings");
                encoder.WriteSettings(w);
                w.WriteEndObject();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("components");
            foreach (IPipelineComponent component in pipeline.Components)
            {
                w.WriteStartObject();
                w.WriteString("name", component.Name);
                if (component is ITrainableComponent head)
                {
                    w.WriteString("type", "head");
                    w.WriteString("kind", head.Kind);
                    w.WriteNumber("encoder", encoders.FindIndex(e => ReferenceEquals(e, head.Encoder)));
                    w.WriteStartObject("config");
                    head.WriteConfig(w);
                    w.WriteEndObject();
                }
                else if (ComponentRegistry.Names.Contains(component.Name))
                {
                    w.WriteString("type", "builtin");
                }
                else
                {
                    throw new NotSupportedException($"The component '{component.Name}' is neither a trainable head nor registered in ComponentRegistry, so it cannot be saved.");
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return buffer.ToArray();
    }
}
