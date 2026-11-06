using System.Collections.Concurrent;

namespace RecursiveAI.OneNlp;

/// <summary>
/// The table through which the <see cref="Nlp"/> verbs find a trained pipeline. A model package (for example
/// <c>RecursiveAI.OneNlp.Models.English</c>) adds an entry for its language with <see cref="Register"/>; nothing is discovered by
/// scanning assemblies, loading them by name or reflection, so this is NativeAOT and trimming safe.
/// </summary>
/// <remarks>
/// <para>
/// A model package calls <see cref="Register"/> from a module initializer that the package adds to the referencing project (see
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

    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    // Pipelines of the accurate-tier packages: used for a language only when no fast-tier package registered one.
    private static readonly ConcurrentDictionary<string, Entry> Fallbacks = new(StringComparer.Ordinal);

    /// <summary>Registers the pipeline of a model package for a language. A second call for the same language and package is ignored.</summary>
    /// <param name="language">The language the pipeline analyzes; must be known.</param>
    /// <param name="packageId">The NuGet package id, named in messages.</param>
    /// <param name="create">Builds the pipeline; called at most once, on first use, and the pipeline lives for the rest of the process. It must be safe to call from any thread.</param>
    /// <exception cref="ArgumentException"><paramref name="language"/> is <see cref="Language.Unknown"/>, or <paramref name="packageId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">A different package is already registered for the language.</exception>
    public static void Register(Language language, string packageId, Func<Pipeline> create)
    {
        if (language == Language.Unknown)
        {
            throw new ArgumentException("A model package is registered for a known language.", nameof(language));
        }

        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentNullException.ThrowIfNull(create);
        Entry entry = Entries.GetOrAdd(language.Code, _ => new Entry(packageId, create));
        if (!string.Equals(entry.PackageId, packageId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The package '{entry.PackageId}' is already registered for language '{language.Code}'; '{packageId}' cannot be registered as well.");
        }
    }

    /// <summary>
    /// Registers the pipeline of a package that serves a language only when no other package has registered one: the accurate tier, which
    /// the <see cref="Nlp"/> verbs use for a language whose fast-tier package is not referenced. A fast-tier package registered with
    /// <see cref="Register"/> always wins, in whichever order the two register.
    /// </summary>
    /// <param name="language">The language the pipeline analyzes; must be known.</param>
    /// <param name="packageId">The NuGet package id, named in messages.</param>
    /// <param name="create">Builds the pipeline; called at most once, on first use, only if the language has no regular registration. It must be safe to call from any thread.</param>
    /// <exception cref="ArgumentException"><paramref name="language"/> is <see cref="Language.Unknown"/>, or <paramref name="packageId"/> is empty.</exception>
    public static void RegisterFallback(Language language, string packageId, Func<Pipeline> create)
    {
        if (language == Language.Unknown)
        {
            throw new ArgumentException("A model package is registered for a known language.", nameof(language));
        }

        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentNullException.ThrowIfNull(create);
        Fallbacks.GetOrAdd(language.Code, _ => new Entry(packageId, create));
    }

    /// <summary>Whether a model package is registered for a language, as its model or as the fallback.</summary>
    /// <param name="language">The language.</param>
    public static bool IsRegistered(Language language) => Entries.ContainsKey(language.Code) || Fallbacks.ContainsKey(language.Code);

    /// <summary>Gets the shared pipeline of the package registered for a language, loading it on first use.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The pipeline; do not dispose it.</returns>
    /// <exception cref="InvalidOperationException">No package is registered for the language. The message names the package to add.</exception>
    public static Pipeline Get(Language language)
    {
        if (!Entries.TryGetValue(language.Code, out Entry? entry) && !Fallbacks.TryGetValue(language.Code, out entry))
        {
            throw new InvalidOperationException(MissingMessage(language));
        }

        return entry.Pipeline.Value;
    }

    internal static bool TryGet(Language language, out Pipeline pipeline)
    {
        if (Entries.TryGetValue(language.Code, out Entry? entry) || Fallbacks.TryGetValue(language.Code, out entry))
        {
            pipeline = entry.Pipeline.Value;
            return true;
        }

        pipeline = null!;
        return false;
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
                            : MultilingualPackageId;
        return $"No model is available for language '{(language == Language.Unknown ? "unknown" : language.Code)}'. Add the NuGet package {add}; "
            + "models are never downloaded.";
    }

    private sealed class Entry(string packageId, Func<Pipeline> create)
    {
        public string PackageId { get; } = packageId;

        // ExecutionAndPublication: the weights are read once even when several threads ask at the same moment.
        public Lazy<Pipeline> Pipeline { get; } = new(create, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
