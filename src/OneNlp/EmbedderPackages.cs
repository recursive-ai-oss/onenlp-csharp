using System.Threading;

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
    private static string? _packageId;
    private static Lazy<IEmbedder>? _embedder;

    /// <summary>Registers the default embedder. A second call from the same package is ignored.</summary>
    /// <param name="packageId">The NuGet package id, named in messages.</param>
    /// <param name="create">Builds the embedder; called at most once, on first use. The embedder lives for the rest of the process and must be safe to call from any thread.</param>
    /// <exception cref="ArgumentException"><paramref name="packageId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">A different package is already registered.</exception>
    public static void Register(string packageId, Func<IEmbedder> create)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentNullException.ThrowIfNull(create);
        lock (Gate)
        {
            if (_packageId is null)
            {
                _packageId = packageId;
                _embedder = new Lazy<IEmbedder>(create, LazyThreadSafetyMode.ExecutionAndPublication);
            }
            else if (!string.Equals(_packageId, packageId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The package '{_packageId}' is already registered as the default embedder; '{packageId}' cannot be registered as well.");
            }
        }
    }

    /// <summary>Whether a default embedder is registered.</summary>
    public static bool IsRegistered
    {
        get
        {
            lock (Gate)
            {
                return _embedder is not null;
            }
        }
    }

    /// <summary>Gets the shared default embedder, loading it on first use.</summary>
    /// <returns>The embedder; do not dispose it.</returns>
    /// <exception cref="InvalidOperationException">No package is registered. The message names the package to add; models are never downloaded.</exception>
    public static IEmbedder Get()
    {
        Lazy<IEmbedder>? embedder;
        lock (Gate)
        {
            embedder = _embedder;
        }

        return embedder?.Value
            ?? throw new InvalidOperationException($"No embedding model is available. Add the NuGet package {DefaultPackageId}; models are never downloaded.");
    }
}
