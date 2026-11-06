using System.Collections.Concurrent;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>Builds an encoder from the settings object written by <see cref="IEncoder.WriteSettings"/>. Weights are loaded afterwards.</summary>
/// <param name="settings">The settings object.</param>
/// <returns>An encoder with the shape the settings describe.</returns>
public delegate IEncoder EncoderFactory(JsonElement settings);

/// <summary>
/// The table of encoder kinds, so a saved pipeline can name its encoder. Filled by explicit calls, never by scanning assemblies,
/// so it is NativeAOT and trimming safe. The built-in kinds are registered the first time the registry is used.
/// </summary>
public static class EncoderRegistry
{
    private static readonly ConcurrentDictionary<string, EncoderFactory> Factories = new(StringComparer.Ordinal)
    {
        [HashEmbeddingEncoder.EncoderKind] = HashEmbeddingEncoder.FromSettings,
        [FastEncoder.EncoderKind] = FastEncoder.FromSettings,
        [TransformerEncoder.EncoderKind] = TransformerEncoder.FromSettings,
        [TrainableTransformerEncoder.EncoderKind] = TrainableTransformerEncoder.FromSettings,
        [SharedEncoder.EncoderKind] = SharedEncoder.FromSettings,
    };

    /// <summary>Registers an encoder kind.</summary>
    /// <param name="kind">The kind name, unique across the application; case-sensitive.</param>
    /// <param name="factory">The factory.</param>
    /// <exception cref="InvalidOperationException">The kind is already registered.</exception>
    public static void Register(string kind, EncoderFactory factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentNullException.ThrowIfNull(factory);
        if (!Factories.TryAdd(kind, factory))
        {
            throw new InvalidOperationException($"An encoder kind '{kind}' is already registered.");
        }
    }

    /// <summary>Creates an encoder of a registered kind.</summary>
    /// <param name="kind">The kind name.</param>
    /// <param name="settings">The settings object.</param>
    /// <exception cref="InvalidDataException">The kind is not registered; the message lists the registered kinds.</exception>
    public static IEncoder Create(string kind, JsonElement settings)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (!Factories.TryGetValue(kind, out EncoderFactory? factory))
        {
            throw new InvalidDataException($"No encoder kind '{kind}' is registered (registered: {string.Join(", ", Factories.Keys.Order(StringComparer.Ordinal))}).");
        }

        return factory(settings);
    }
}
