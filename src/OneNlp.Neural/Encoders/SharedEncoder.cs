using System.Collections.Concurrent;
using System.Text.Json;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// An encoder that several saved pipelines share without each carrying its weights: the pipelines name it, and the application (or a model
/// package) registers the one real encoder under that name before loading them. This is how the accurate tier ships one fine-tuned
/// transformer for many languages, with a small file of heads for each.
/// </summary>
/// <remarks>
/// <para>
/// A pipeline saved with a <see cref="SharedEncoder"/> holds the heads only: <see cref="Weights"/> is <see langword="null"/>, so nothing of
/// the encoder is written to the pipeline file, and the file records the kind <c>shared</c> with the name. Loading looks the name up in
/// the table filled by <see cref="Register"/>. The shared encoder is not disposed with the pipeline; whoever registered it owns it.
/// </para>
/// <para>Calls to the shared encoder take turns when several pipelines use it from different threads, because the real encoder is not thread-safe.</para>
/// </remarks>
public sealed class SharedEncoder : IEncoder
{
    /// <summary>The kind name a saved pipeline uses for this encoder: <c>shared</c>.</summary>
    public const string EncoderKind = "shared";

    private static readonly ConcurrentDictionary<string, IEncoder> Registered = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    private readonly IEncoder _inner;

    /// <summary>Wraps an encoder so that a pipeline can be saved without its weights.</summary>
    /// <param name="inner">The real encoder; stays owned by the caller.</param>
    /// <param name="name">The name pipelines are saved under, 1 to 64 characters.</param>
    /// <exception cref="ArgumentException">The name is empty or longer than 64 characters.</exception>
    public SharedEncoder(IEncoder inner, string name)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name.Length > 64)
        {
            throw new ArgumentException("The name is at most 64 characters.", nameof(name));
        }

        _inner = inner;
        Name = name;
    }

    /// <summary>The name pipelines record.</summary>
    public string Name { get; }

    /// <inheritdoc/>
    public string Kind => EncoderKind;

    /// <inheritdoc/>
    public int Width => _inner.Width;

    /// <summary>Always <see langword="null"/>: the weights belong to the real encoder and are not part of a pipeline that uses this one.</summary>
    public NnModule? Weights => null;

    /// <summary>Makes an encoder available to pipelines that were saved with a <see cref="SharedEncoder"/> of this name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="encoder">The real encoder. The caller keeps ownership and disposes it after every pipeline that uses it.</param>
    /// <exception cref="InvalidOperationException">The name is already registered.</exception>
    public static void Register(string name, IEncoder encoder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(encoder);
        if (!Registered.TryAdd(name, encoder))
        {
            throw new InvalidOperationException($"A shared encoder named '{name}' is already registered.");
        }
    }

    /// <summary>Removes a registration (for tests and shutdown). Pipelines already loaded keep working.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Whether it was registered.</returns>
    public static bool Unregister(string name) => Registered.TryRemove(name, out _);

    /// <inheritdoc/>
    public EncodedBatch Encode(ReadOnlySpan<Doc> docs)
    {
        lock (Gate)
        {
            return _inner.Encode(docs);
        }
    }

    /// <inheritdoc/>
    public void WriteSettings(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString("name", Name);
    }

    /// <summary>Does nothing: the real encoder belongs to whoever registered it.</summary>
    public void Dispose()
    {
    }

    internal static IEncoder FromSettings(JsonElement s)
    {
        string name = JsonSettings.String(s, "name", 64);
        if (!Registered.TryGetValue(name, out IEncoder? inner))
        {
            throw new InvalidDataException($"The pipeline needs the shared encoder '{name}', which has not been registered. Load the package that carries it first.");
        }

        return new SharedEncoder(inner, name);
    }
}
