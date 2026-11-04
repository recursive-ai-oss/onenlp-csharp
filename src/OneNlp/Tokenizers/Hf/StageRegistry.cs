namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// The tables that turn the <c>type</c> string of a <c>tokenizer.json</c> object into a stage: one table per kind (normalizer,
/// pre-tokenizer, model, post-processor, decoder). Registration is by explicit calls, so it works under NativeAOT and trimming, and
/// every unit of work registers the types it owns from its own file, without editing a shared list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where to register.</b> <see cref="Default"/> is built once, on first use: first the stubs for every documented type, then the
/// hooks below. A file that owns a type declares the matching partial method of this class and calls
/// <see cref="AddModel"/> (or the other <c>Add</c> methods) in it. Hooks that no file implements are removed by the compiler.
/// Registering a type that is already there replaces it, which is how a stub is replaced by the real stage.
/// </para>
/// <para>
/// The model hooks are <c>RegisterWordPiece</c>, <c>RegisterUnigram</c>, <c>RegisterBpe</c> and <c>RegisterTiktoken</c>. Stage hooks are
/// <c>RegisterNormalizers</c>, <c>RegisterPreTokenizers</c>, <c>RegisterPostProcessors</c> and <c>RegisterDecoders</c>.
/// </para>
/// <para>All members are thread-safe once construction is done; <c>Add</c> methods are meant for startup and tests.</para>
/// </remarks>
internal sealed partial class StageRegistry
{
    private static readonly Lazy<StageRegistry> DefaultInstance = new(CreateDefault);

    private readonly Dictionary<string, Func<StageConfig, INormalizer>> _normalizers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<StageConfig, IPreTokenizer>> _preTokenizers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<StageConfig, ITokenizerModel>> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<StageConfig, IPostProcessor>> _postProcessors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<StageConfig, ITokenDecoder>> _decoders = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Creates an empty registry (no stubs, no hooks): for tests that register toy stages.</summary>
    internal StageRegistry()
    {
    }

    /// <summary>The registry the loader uses: every documented type, as a stub or as the real stage.</summary>
    internal static StageRegistry Default => DefaultInstance.Value;

    /// <summary>Makes an independent copy, so that a test can replace a type without changing <see cref="Default"/>.</summary>
    internal StageRegistry Clone()
    {
        var copy = new StageRegistry();
        lock (_gate)
        {
            foreach (KeyValuePair<string, Func<StageConfig, INormalizer>> e in _normalizers) { copy._normalizers[e.Key] = e.Value; }
            foreach (KeyValuePair<string, Func<StageConfig, IPreTokenizer>> e in _preTokenizers) { copy._preTokenizers[e.Key] = e.Value; }
            foreach (KeyValuePair<string, Func<StageConfig, ITokenizerModel>> e in _models) { copy._models[e.Key] = e.Value; }
            foreach (KeyValuePair<string, Func<StageConfig, IPostProcessor>> e in _postProcessors) { copy._postProcessors[e.Key] = e.Value; }
            foreach (KeyValuePair<string, Func<StageConfig, ITokenDecoder>> e in _decoders) { copy._decoders[e.Key] = e.Value; }
        }

        return copy;
    }

    /// <summary>Registers (or replaces) a normalizer type.</summary>
    /// <param name="type">The <c>type</c> string.</param>
    /// <param name="factory">Builds the stage from its JSON object.</param>
    internal void AddNormalizer(string type, Func<StageConfig, INormalizer> factory) => Add(_normalizers, type, factory);

    /// <summary>Registers (or replaces) a pre-tokenizer type.</summary>
    /// <param name="type">The <c>type</c> string.</param>
    /// <param name="factory">Builds the stage from its JSON object.</param>
    internal void AddPreTokenizer(string type, Func<StageConfig, IPreTokenizer> factory) => Add(_preTokenizers, type, factory);

    /// <summary>Registers (or replaces) a model type.</summary>
    /// <param name="type">The <c>type</c> string, for example <c>WordPiece</c>.</param>
    /// <param name="factory">Builds the model from the <c>model</c> object, which carries the vocabulary.</param>
    internal void AddModel(string type, Func<StageConfig, ITokenizerModel> factory) => Add(_models, type, factory);

    /// <summary>Registers (or replaces) a post-processor type.</summary>
    /// <param name="type">The <c>type</c> string.</param>
    /// <param name="factory">Builds the stage from its JSON object.</param>
    internal void AddPostProcessor(string type, Func<StageConfig, IPostProcessor> factory) => Add(_postProcessors, type, factory);

    /// <summary>Registers (or replaces) a decoder type.</summary>
    /// <param name="type">The <c>type</c> string.</param>
    /// <param name="factory">Builds the stage from its JSON object.</param>
    internal void AddDecoder(string type, Func<StageConfig, ITokenDecoder> factory) => Add(_decoders, type, factory);

    /// <summary>Builds a normalizer from its JSON object.</summary>
    /// <param name="config">The object, which names its type.</param>
    /// <exception cref="NotSupportedException">The type is documented but not implemented yet, or unknown.</exception>
    /// <exception cref="InvalidDataException">The object is malformed.</exception>
    internal INormalizer CreateNormalizer(StageConfig config) => Create(_normalizers, "normalizer", config);

    /// <summary>Builds a pre-tokenizer from its JSON object.</summary>
    /// <param name="config">The object, which names its type.</param>
    /// <exception cref="NotSupportedException">The type is documented but not implemented yet, or unknown.</exception>
    /// <exception cref="InvalidDataException">The object is malformed.</exception>
    internal IPreTokenizer CreatePreTokenizer(StageConfig config) => Create(_preTokenizers, "pre-tokenizer", config);

    /// <summary>Builds a model from its JSON object.</summary>
    /// <param name="config">The object. Hugging Face files omit <c>type</c> for BPE-like models in old versions; a missing type is read as <c>BPE</c>.</param>
    /// <exception cref="NotSupportedException">The type is documented but not implemented yet, or unknown.</exception>
    /// <exception cref="InvalidDataException">The object is malformed.</exception>
    internal ITokenizerModel CreateModel(StageConfig config) => Create(_models, "model", config, defaultType: "BPE");

    /// <summary>Builds a post-processor from its JSON object.</summary>
    /// <param name="config">The object, which names its type.</param>
    /// <exception cref="NotSupportedException">The type is documented but not implemented yet, or unknown.</exception>
    /// <exception cref="InvalidDataException">The object is malformed.</exception>
    internal IPostProcessor CreatePostProcessor(StageConfig config) => Create(_postProcessors, "post-processor", config);

    /// <summary>Builds a decoder from its JSON object.</summary>
    /// <param name="config">The object, which names its type.</param>
    /// <exception cref="NotSupportedException">The type is documented but not implemented yet, or unknown.</exception>
    /// <exception cref="InvalidDataException">The object is malformed.</exception>
    internal ITokenDecoder CreateDecoder(StageConfig config) => Create(_decoders, "decoder", config);

    // Optional hooks: a partial method with no body is removed by the compiler, so a file adds its types by implementing the one it owns.
    static partial void RegisterNormalizers(StageRegistry registry);

    static partial void RegisterPreTokenizers(StageRegistry registry);

    static partial void RegisterPostProcessors(StageRegistry registry);

    static partial void RegisterDecoders(StageRegistry registry);

    static partial void RegisterWordPiece(StageRegistry registry);

    static partial void RegisterUnigram(StageRegistry registry);

    static partial void RegisterBpe(StageRegistry registry);

    static partial void RegisterTiktoken(StageRegistry registry);

    private static StageRegistry CreateDefault()
    {
        var registry = new StageRegistry();
        registry.AddStubs();
        RegisterNormalizers(registry);
        RegisterPreTokenizers(registry);
        RegisterPostProcessors(registry);
        RegisterDecoders(registry);
        RegisterWordPiece(registry);
        RegisterUnigram(registry);
        RegisterBpe(registry);
        RegisterTiktoken(registry);
        return registry;
    }

    private void Add<T>(Dictionary<string, Func<StageConfig, T>> table, string type, Func<StageConfig, T> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            table[type] = factory;
        }
    }

    private T Create<T>(Dictionary<string, Func<StageConfig, T>> table, string kind, StageConfig config, string? defaultType = null)
    {
        string type = config.Type is { Length: > 0 } t ? t : defaultType ?? throw config.Missing("type");
        Func<StageConfig, T>? factory;
        lock (_gate)
        {
            table.TryGetValue(type, out factory);
        }

        if (factory is null)
        {
            throw new NotSupportedException($"The {kind} type '{type}' is not supported.");
        }

        try
        {
            return factory(config);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException or IndexOutOfRangeException)
        {
            // A value of the wrong kind or size read straight from the JSON (a missing member, a string where a number belongs).
            throw new InvalidDataException($"The {kind} '{type}' is malformed: {ex.Message}", ex);
        }
    }

    // Every type the published tokenizer.json documentation lists, so that a file using one fails with a clear message until the
    // stage is written, and "not supported" always means "not a documented type".
    private void AddStubs()
    {
        foreach (string type in (string[])["BertNormalizer", "NFC", "NFD", "NFKC", "NFKD", "Lowercase", "Strip", "StripAccents", "Replace", "Prepend", "Precompiled", "ByteLevel", "Sequence"])
        {
            AddNormalizer(type, _ => throw new NotSupportedException($"The normalizer type '{type}' is not implemented yet."));
        }

        foreach (string type in (string[])["BertPreTokenizer", "Whitespace", "WhitespaceSplit", "Punctuation", "Metaspace", "ByteLevel", "Split", "Digits", "UnicodeScripts", "CharDelimiterSplit", "Sequence"])
        {
            AddPreTokenizer(type, _ => throw new NotSupportedException($"The pre-tokenizer type '{type}' is not implemented yet."));
        }

        foreach (string type in (string[])["WordPiece", "BPE", "Unigram", "WordLevel"])
        {
            AddModel(type, _ => throw new NotSupportedException($"The model type '{type}' is not implemented yet."));
        }

        foreach (string type in (string[])["TemplateProcessing", "BertProcessing", "RobertaProcessing", "ByteLevel", "Sequence"])
        {
            AddPostProcessor(type, _ => throw new NotSupportedException($"The post-processor type '{type}' is not implemented yet."));
        }

        foreach (string type in (string[])["WordPiece", "Metaspace", "ByteLevel", "ByteFallback", "Fuse", "Strip", "Replace", "BPEDecoder", "CTC", "Sequence"])
        {
            AddDecoder(type, _ => throw new NotSupportedException($"The decoder type '{type}' is not implemented yet."));
        }
    }
}
