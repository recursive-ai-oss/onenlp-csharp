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

    /// <summary>The base-size accurate-tier package.</summary>
    internal const string LargePackageId = "RecursiveAI.OneNlp.Models.Multilingual.Large";

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
    public static void Register(Language language, Tier tier, string packageId, Func<Pipeline> create, int priority = 0) =>
        RegisterCore(language, tier, packageId, create, priority, detectable: true);

    /// <summary>Registers like <see cref="Register(Language, Tier, string, Func{Pipeline}, int)"/> for a model that language detection does not take into account (test doubles, which must not change how other texts are read).</summary>
    internal static void RegisterQuietly(Language language, string packageId, Func<Pipeline> create) =>
        RegisterCore(language, Tier.Fast, packageId, create, 0, detectable: false);

    internal static void RegisterQuietly(Language language, Tier tier, string packageId, Func<Pipeline> create, int priority = 0) =>
        RegisterCore(language, tier, packageId, create, priority, detectable: false);

    private static void RegisterCore(Language language, Tier tier, string packageId, Func<Pipeline> create, int priority, bool detectable)
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
                list.Add(new Entry(tier, packageId, create, priority) { Detectable = detectable });
                Interlocked.Increment(ref _version);
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
    /// <exception cref="ModelNotAvailableException">No package is registered for the language. The message names the package to add.</exception>
    public static Pipeline Get(Language language) =>
        (Resolve(language, Tier.Fast) ?? throw Missing(language, Tier.Fast)).Pipeline.Value;

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
    /// <exception cref="ModelNotAvailableException">No package is registered; the message names the package to add.</exception>
    internal static IReadOnlyList<Span> RecognizeOpen(string text, IReadOnlyList<string> types, float? threshold = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(types);
        Entry entry = _openEntities ?? throw new ModelNotAvailableException(
            $"No open-type entity model is available. Add the NuGet package {MultilingualPackageId}; models are never downloaded.",
            Language.Unknown, Tier.Accurate, [MultilingualPackageId]);
        Pipeline pipeline = entry.Pipeline.Value;
        IOpenEntityRecognizer recognizer = pipeline.Components.OfType<IOpenEntityRecognizer>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The pipeline of '{entry.PackageId}' has no open-type entity component.");
        Doc doc = pipeline.Tokenizer.Tokenize(text, pipeline.Strings);
        recognizer.Recognize(doc, types, threshold);
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
        Entry entry = Resolve(language, tier) ?? throw Missing(language, tier);
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

    // Fast-tier packages that exist, by language code. Packages that are not built yet are never named in an error.
    private static readonly Dictionary<string, string> FastPackages = new(StringComparer.Ordinal)
    {
        ["en"] = "RecursiveAI.OneNlp.Models.English",
        ["de"] = "RecursiveAI.OneNlp.Models.German",
        ["fr"] = "RecursiveAI.OneNlp.Models.French",
        ["pt"] = "RecursiveAI.OneNlp.Models.Portuguese",
        ["nl"] = "RecursiveAI.OneNlp.Models.Dutch",
        ["tr"] = "RecursiveAI.OneNlp.Models.Turkish",
        ["es"] = "RecursiveAI.OneNlp.Models.Spanish",
        ["it"] = "RecursiveAI.OneNlp.Models.Italian",
        ["zh"] = "RecursiveAI.OneNlp.Models.Chinese",
    };

    // The languages the two accurate-tier packages have heads for (their `Languages` lists). Core cannot ask a package that is not
    // referenced, so the table is here; a package that gains languages must be added to it.
    private static readonly HashSet<string> AccurateLanguages = new(StringComparer.Ordinal) { "en", "de", "fr", "es", "pt", "it", "nl", "tr", "zh" };

    /// <summary>
    /// The error for a language and tier without a model. The message names only packages that would change the outcome: ones that
    /// cover the language and are not already referenced. When there are none it says that no package covers the language, and
    /// <see cref="ModelNotAvailableException.PackageIds"/> is empty.
    /// </summary>
    internal static ModelNotAvailableException Missing(Language language, Tier tier)
    {
        string name = language == Language.Unknown ? "unknown" : language.Code;
        bool accurateCovers = language == Language.Unknown || AccurateLanguages.Contains(language.Code);
        var candidates = new List<string>();
        if (tier == Tier.Accurate)
        {
            if (accurateCovers)
            {
                candidates.Add(LargePackageId);
                candidates.Add(MultilingualPackageId);
            }
        }
        else
        {
            if (FastPackages.TryGetValue(language.Code, out string? fast))
            {
                candidates.Add(fast);
            }

            if (accurateCovers)
            {
                candidates.Add(MultilingualPackageId);
            }
        }

        lock (Gate)
        {
            candidates.RemoveAll(id => Entries.Values.Any(list => list.Exists(e => string.Equals(e.PackageId, id, StringComparison.Ordinal))));
        }

        string kind = tier == Tier.Accurate ? "accurate-tier model" : "model";
        if (candidates.Count == 0)
        {
            return new ModelNotAvailableException(
                $"No {kind} covers language '{name}': the referenced packages do not include it, and no published OneNLP model package does. Models are never downloaded.",
                language, tier, []);
        }

        string options = candidates.Count == 1 ? candidates[0] : $"{candidates[0]} (or {string.Join(", ", candidates.Skip(1))})";
        return new ModelNotAvailableException(
            $"No {kind} is available for language '{name}'. Add the NuGet package {options}; models are never downloaded.",
            language, tier, candidates);
    }

    /// <summary>Bumped on each registration, so a cached view of the registered languages can tell when it is stale.</summary>
    internal static int Version => Volatile.Read(ref _version);

    private static int _version;

    /// <summary>The languages for which the <see cref="Nlp"/> verbs have a model (see <see cref="IsRegistered"/>).</summary>
    internal static Language[] RegisteredLanguages()
    {
        lock (Gate)
        {
            return [.. Entries.Where(kv => kv.Value.Exists(e => e.Detectable)).Select(kv => Language.FromCode(kv.Key))];
        }
    }

    private sealed class Entry(Tier tier, string packageId, Func<Pipeline> create, int priority)
    {
        public Tier Tier { get; } = tier;

        public string PackageId { get; } = packageId;

        public int Priority { get; } = priority;

        public Func<Pipeline> Create { get; } = create;

        /// <summary>Whether language detection may choose this language for a text it is unsure about.</summary>
        public bool Detectable { get; init; } = true;

        // ExecutionAndPublication: the weights are read once even when several threads ask at the same moment.
        public Lazy<Pipeline> Pipeline { get; } = new(create, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
