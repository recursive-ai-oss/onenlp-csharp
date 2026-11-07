namespace RecursiveAI.OneNlp;

/// <summary>
/// The table through which the <see cref="Nlp"/> verbs find a trained pipeline. A model package (for example
/// <c>RecursiveAI.OneNlp.Models.English</c>) adds an entry for its language with <see cref="Register(Language, string, Func{Pipeline})"/>; nothing is discovered by
/// scanning assemblies, loading them by name or reflection, so this is NativeAOT and trimming safe.
/// </summary>
/// <remarks>
/// <para>
/// A model package calls <see cref="Register(Language, string, Func{Pipeline})"/> from a module initializer that the package adds to the referencing project (see
/// <c>docs/model-packages.md</c>); applications that do not use that mechanism call the package's own <c>Register</c> method once at
/// start-up. Registration stores a factory only: the weights are read the first time a verb needs the language, and the pipeline is then
/// shared by all callers.
/// </para>
/// <para>All members are thread-safe.</para>
/// </remarks>
public static class ModelPackages
{
    /// <summary>The package that carries every language through the accurate tier. Named in the error when no model is available.</summary>
    internal const string MultilingualPackageId = "RecursiveAI.OneNlp.Models.Multilingual";

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, List<Entry>> Entries = new(StringComparer.Ordinal);

    /// <summary>Registers the pipeline of a fast-tier model package for a language. A second call for the same language and package is ignored.</summary>
    /// <param name="language">The language the pipeline analyzes; must be known.</param>
    /// <param name="packageId">The NuGet package id, named in messages.</param>
    /// <param name="create">Builds the pipeline; called at most once for the shared pipeline of the <see cref="Nlp"/> verbs, and once more for each <see cref="Pipeline.Create"/>. It must return a new copy each time and be safe to call from any thread.</param>
    /// <exception cref="ArgumentException"><paramref name="language"/> is <see cref="Language.Unknown"/>, or <paramref name="packageId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">A different fast-tier package is already registered for the language.</exception>
    public static void Register(Language language, string packageId, Func<Pipeline> create) => Register(language, Tier.Fast, packageId, create);

    /// <summary>Registers the pipeline of a model package for a language and tier.</summary>
    /// <param name="language">The language the pipeline analyzes; must be known.</param>
    /// <param name="tier">The tier the package belongs to.</param>
    /// <param name="packageId">The NuGet package id, named in messages.</param>
    /// <param name="create">Builds a new copy of the pipeline on each call; see <see cref="Register(Language, string, Func{Pipeline})"/>.</param>
    /// <param name="priority">Among several <see cref="Tier.Accurate"/> packages for a language the highest priority is used (the large model over the base one); ignored for the fast tier.</param>
    /// <exception cref="ArgumentException"><paramref name="language"/> is <see cref="Language.Unknown"/>, or <paramref name="packageId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">A different fast-tier package is already registered for the language.</exception>
    public static void Register(Language language, Tier tier, string packageId, Func<Pipeline> create, int priority = 0)
    {
        if (language == Language.Unknown)
        {
            throw new ArgumentException("A model package is registered for a known language.", nameof(language));
        }

        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentNullException.ThrowIfNull(create);
        lock (Gate)
        {
            if (!Entries.TryGetValue(language.Code, out List<Entry>? list))
            {
                Entries[language.Code] = list = [];
            }

            Entry? same = list.Find(e => e.Tier == tier && (tier == Tier.Fast || string.Equals(e.PackageId, packageId, StringComparison.Ordinal)));
            if (same is null)
            {
                list.Add(new Entry(tier, packageId, create, priority));
            }
            else if (!string.Equals(same.PackageId, packageId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The package '{same.PackageId}' is already registered for language '{language.Code}'; '{packageId}' cannot be registered as well.");
            }
        }
    }

    /// <summary>Whether a model package is registered for a language (a fast-tier one, or an accurate-tier one the verbs fall back to).</summary>
    /// <param name="language">The language.</param>
    public static bool IsRegistered(Language language) => Resolve(language, Tier.Fast) is not null;

    /// <summary>Gets the shared pipeline of the package the <see cref="Nlp"/> verbs use for a language, loading it on first use: the fast-tier package if referenced, otherwise the best accurate-tier one.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The pipeline; do not dispose it.</returns>
    /// <exception cref="InvalidOperationException">No package is registered for the language. The message names the package to add.</exception>
    public static Pipeline Get(Language language) =>
        (Resolve(language, Tier.Fast) ?? throw new InvalidOperationException(MissingMessage(language))).Pipeline.Value;

    private static Entry? _openEntities;
    private static readonly Lock OpenGate = new();

    /// <summary>Registers the pipeline that finds entities of types named at run time (see <see cref="Nlp.Entities(string, string[])"/>).</summary>
    /// <param name="packageId">The NuGet package id, named in messages.</param>
    /// <param name="create">Builds a pipeline with an <see cref="IOpenEntityRecognizer"/> component; called at most once, on first use. It must be safe to call from any thread.</param>
    /// <exception cref="ArgumentException"><paramref name="packageId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">A different package is already registered.</exception>
    public static void RegisterOpenEntities(string packageId, Func<Pipeline> create)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentNullException.ThrowIfNull(create);
        lock (OpenGate)
        {
            _openEntities ??= new Entry(Tier.Accurate, packageId, create, 0);
            if (!string.Equals(_openEntities.PackageId, packageId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The package '{_openEntities.PackageId}' is already registered for open-type entities; '{packageId}' cannot be registered as well.");
            }
        }
    }

    /// <summary>Finds entities of the given types with the registered open-type recognizer.</summary>
    /// <exception cref="InvalidOperationException">No package is registered; the message names the package to add.</exception>
    internal static IReadOnlyList<Span> RecognizeOpen(string text, string[] types)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(types);
        Entry entry = _openEntities ?? throw new InvalidOperationException(
            $"No open-type entity model is available. Add the NuGet package {MultilingualPackageId}; models are never downloaded.");
        Pipeline pipeline = entry.Pipeline.Value;
        IOpenEntityRecognizer recognizer = pipeline.Components.OfType<IOpenEntityRecognizer>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The pipeline of '{entry.PackageId}' has no open-type entity component.");
        Doc doc = pipeline.Tokenizer.Tokenize(text, pipeline.Strings);
        recognizer.Recognize(doc, types);
        return doc.Ents;
    }

    internal static bool TryGet(Language language, out Pipeline pipeline)
    {
        Entry? entry = Resolve(language, Tier.Fast);
        pipeline = entry is null ? null! : entry.Pipeline.Value;
        return entry is not null;
    }

    /// <summary>Loads a new, unshared copy of the pipeline for a language and tier, for <see cref="Pipeline.Create"/>.</summary>
    internal static (Pipeline Pipeline, string PackageId) Load(Language language, Tier tier)
    {
        Entry entry = Resolve(language, tier) ?? throw new InvalidOperationException(MissingMessage(language, tier));
        return (entry.Create(), entry.PackageId);
    }

    // The fast tier asks for the fast package, then falls back to the accurate packages (DESIGN: otherwise the multilingual model);
    // the accurate tier never falls back to the fast one, because the caller asked for the other kind of model.
    private static Entry? Resolve(Language language, Tier tier)
    {
        lock (Gate)
        {
            if (!Entries.TryGetValue(language.Code, out List<Entry>? list))
            {
                return null;
            }

            Entry? best = null;
            foreach (Entry e in list)
            {
                if (e.Tier == Tier.Accurate && (best is null || best.Tier == Tier.Accurate && e.Priority > best.Priority))
                {
                    best = e;
                }
            }

            Entry? fast = list.Find(e => e.Tier == Tier.Fast);
            return tier == Tier.Fast ? fast ?? best : best;
        }
    }

    internal static string MissingMessage(Language language, Tier tier)
    {
        if (tier == Tier.Fast)
        {
            return MissingMessage(language);
        }

        return $"No accurate-tier model is available for language '{(language == Language.Unknown ? "unknown" : language.Code)}'. Add the NuGet package "
            + $"{MultilingualPackageId} (or {MultilingualPackageId}.Large); models are never downloaded.";
    }

    /// <summary>The error text for a language without a model: the fast-tier package for the language, or the multilingual package.</summary>
    internal static string MissingMessage(Language language)
    {
        string add = language == Language.English
            ? $"RecursiveAI.OneNlp.Models.English (fast tier), or {MultilingualPackageId}"
            : language == Language.German
                ? $"RecursiveAI.OneNlp.Models.German (fast tier), or {MultilingualPackageId}"
                : language == Language.French
                    ? $"RecursiveAI.OneNlp.Models.French (fast tier), or {MultilingualPackageId}"
                    : language == Language.Portuguese
                        ? $"RecursiveAI.OneNlp.Models.Portuguese (fast tier), or {MultilingualPackageId}"
                    : language == Language.Dutch
                        ? $"RecursiveAI.OneNlp.Models.Dutch (fast tier), or {MultilingualPackageId}"
                        : language == Language.Turkish
                            ? $"RecursiveAI.OneNlp.Models.Turkish (fast tier), or {MultilingualPackageId}"
                            : language == Language.Spanish
                                ? $"RecursiveAI.OneNlp.Models.Spanish (fast tier), or {MultilingualPackageId}"
                            : language == Language.Italian
                                ? $"RecursiveAI.OneNlp.Models.Italian (fast tier), or {MultilingualPackageId}"
                            : language == Language.Chinese
                                ? $"RecursiveAI.OneNlp.Models.Chinese (fast tier), or {MultilingualPackageId}"
                            : MultilingualPackageId;
        return $"No model is available for language '{(language == Language.Unknown ? "unknown" : language.Code)}'. Add the NuGet package {add}; "
            + "models are never downloaded.";
    }

    private sealed class Entry(Tier tier, string packageId, Func<Pipeline> create, int priority)
    {
        public Tier Tier { get; } = tier;

        public string PackageId { get; } = packageId;

        public int Priority { get; } = priority;

        public Func<Pipeline> Create { get; } = create;

        // ExecutionAndPublication: the weights are read once even when several threads ask at the same moment.
        public Lazy<Pipeline> Pipeline { get; } = new(create, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
