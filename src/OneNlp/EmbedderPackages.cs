namespace RecursiveAI.OneNlp;

/// <summary>
/// The table through which <see cref="Nlp.Embed(string)"/> finds the default embedder. A model package (for example
/// <c>RecursiveAI.OneNlp.Models.Embeddings</c>) registers a factory with <see cref="Register"/>; nothing is discovered by scanning
/// assemblies or reflection, so this is NativeAOT and trimming safe. All members are thread-safe.
/// </summary>
public static class EmbedderPackages
{
    /// <summary>The package that carries the default embedder. Named in the error when none is registered.</summary>
    public const string DefaultPackageId = "RecursiveAI.OneNlp.Models.Embeddings";

    private static readonly Lock Gate = new();
    private static readonly List<Entry> Entries = [];

    /// <summary>Registers an embedding model package. A second call from the same package is ignored.</summary>
    /// <param name="packageId">The NuGet package id, named in messages.</param>
    /// <param name="create">Builds the embedder; called at most once, on first use. The embedder lives for the rest of the process and must be safe to call from any thread.</param>
    /// <param name="priority">Which registered package <see cref="Get"/> returns when several are referenced: the highest priority wins, the first registered among equals. The English package uses 0 and the multilingual package 1.</param>
    /// <exception cref="ArgumentException"><paramref name="packageId"/> is empty.</exception>
    public static void Register(string packageId, Func<IEmbedder> create, int priority = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentNullException.ThrowIfNull(create);
        lock (Gate)
        {
            if (!Entries.Exists(e => string.Equals(e.PackageId, packageId, StringComparison.Ordinal)))
            {
                Entries.Add(new Entry(packageId, priority, new Lazy<IEmbedder>(create, LazyThreadSafetyMode.ExecutionAndPublication)));
            }
        }
    }

    /// <summary>Whether an embedding package is registered.</summary>
    public static bool IsRegistered
    {
        get
        {
            lock (Gate)
            {
                return Entries.Count > 0;
            }
        }
    }

    /// <summary>Gets the shared default embedder, loading it on first use.</summary>
    /// <returns>The embedder; do not dispose it.</returns>
    /// <exception cref="ModelNotAvailableException">No package is registered. The message names the package to add; models are never downloaded.</exception>
    public static IEmbedder Get()
    {
        Lazy<IEmbedder>? best = null;
        int bestPriority = int.MinValue;
        lock (Gate)
        {
            foreach (Entry e in Entries)
            {
                if (e.Priority > bestPriority)
                {
                    best = e.Embedder;
                    bestPriority = e.Priority;
                }
            }
        }

        return best?.Value
            ?? throw new ModelNotAvailableException(
                $"No embedding model is available. Add the NuGet package {DefaultPackageId}; models are never downloaded.",
                Language.Unknown, Tier.Fast, [DefaultPackageId]);
    }

    private sealed record Entry(string PackageId, int Priority, Lazy<IEmbedder> Embedder);
}
