using System.Collections.Concurrent;
using System.Text.Json;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>Rebuilds a head from the config written by <see cref="ITrainableComponent.WriteConfig"/>. Weights are loaded afterwards.</summary>
/// <param name="name">The component name in the pipeline.</param>
/// <param name="encoder">The encoder the head reads.</param>
/// <param name="config">The head's config object.</param>
/// <returns>An initialized head whose weights have the saved shapes. It should own the encoder (<c>OwnsEncoder</c> on <see cref="TrainableHead"/>), since a loaded pipeline has no other owner.</returns>
public delegate ITrainableComponent HeadFactory(string name, IEncoder encoder, JsonElement config);

/// <summary>
/// The table of trainable head kinds, so a saved pipeline can name its components. Filled by explicit calls, never by scanning
/// assemblies, so it is NativeAOT and trimming safe. The built-in kinds are present from the start.
/// </summary>
public static class HeadRegistry
{
    private static readonly ConcurrentDictionary<string, HeadFactory> Factories = new(StringComparer.Ordinal)
    {
        [Tagger.ComponentKind] = Tagger.FromConfig,
        [Parser.ComponentKind] = Parser.FromConfig,
        [Morphologizer.ComponentKind] = Morphologizer.FromConfig,
        [EditTreeLemmatizer.ComponentKind] = EditTreeLemmatizer.FromConfig,
        [EntityRecognizer.ComponentKind] = EntityRecognizer.FromConfig,
        [OpenEntityRecognizer.ComponentKind] = OpenEntityRecognizer.FromConfig,
    };

    /// <summary>Registers a head kind.</summary>
    /// <param name="kind">The kind name, unique across the application; case-sensitive.</param>
    /// <param name="factory">The factory.</param>
    /// <exception cref="InvalidOperationException">The kind is already registered.</exception>
    public static void Register(string kind, HeadFactory factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentNullException.ThrowIfNull(factory);
        if (!Factories.TryAdd(kind, factory))
        {
            throw new InvalidOperationException($"A head kind '{kind}' is already registered.");
        }
    }

    /// <summary>Whether a head kind is registered.</summary>
    /// <param name="kind">The kind name.</param>
    public static bool IsRegistered(string kind) => Factories.ContainsKey(kind);

    /// <summary>Creates a head of a registered kind.</summary>
    /// <param name="kind">The kind name.</param>
    /// <param name="name">The component name in the pipeline.</param>
    /// <param name="encoder">The encoder the head reads.</param>
    /// <param name="config">The head's config object.</param>
    /// <returns>The head.</returns>
    /// <exception cref="InvalidDataException">The kind is not registered; the message lists the registered kinds.</exception>
    public static ITrainableComponent Create(string kind, string name, IEncoder encoder, JsonElement config)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (!Factories.TryGetValue(kind, out HeadFactory? factory))
        {
            throw new InvalidDataException($"No head kind '{kind}' is registered (registered: {string.Join(", ", Factories.Keys.Order(StringComparer.Ordinal))}).");
        }

        return factory(name, encoder, config);
    }
}
