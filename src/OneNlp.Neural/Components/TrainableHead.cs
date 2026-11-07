using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// The shared part of a trainable head: the pipeline-facing methods. A subclass supplies the model-specific parts of
/// <see cref="ITrainableComponent"/>; this class runs the encoder and the head without gradients, in evaluation mode, for
/// <see cref="Process"/> and <see cref="ProcessBatch"/>.
/// </summary>
/// <remarks>
/// A head's module is not thread-safe, so the prediction step of calls to <see cref="ProcessBatch"/> on one head takes turns (and with an encoder other
/// than <see cref="FastEncoder"/>, so does the encoding). Different heads, and different
/// pipelines, run in parallel.
/// </remarks>
public abstract class TrainableHead : ITrainableComponent, IDisposable
{
    private readonly object _gate = new();

    /// <summary>Creates the base.</summary>
    /// <param name="name">The component name, unique in its pipeline.</param>
    /// <param name="encoder">The encoder the head reads.</param>
    protected TrainableHead(string name, IEncoder encoder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(encoder);
        Name = name;
        Encoder = encoder;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>
    /// Whether disposing this head also disposes its encoder. Heads rebuilt from a saved pipeline own their encoder (a loaded pipeline has
    /// no other owner); heads a caller builds with an encoder it created do not. Default false.
    /// </summary>
    public bool OwnsEncoder { get; init; }

    /// <inheritdoc/>
    public abstract string Kind { get; }

    /// <inheritdoc/>
    public IEncoder Encoder { get; }

    /// <inheritdoc/>
    public abstract bool IsInitialized { get; }

    /// <inheritdoc/>
    public abstract NnModule Weights { get; }

    /// <inheritdoc/>
    public abstract void Initialize(IReadOnlyList<Example> examples, ulong seed);

    /// <inheritdoc/>
    /// <inheritdoc/>
    public virtual IReadOnlyList<string> ExtendLabels(IReadOnlyList<Example> examples, ulong seed) => [];

    /// <inheritdoc/>
    public abstract Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples);

    /// <inheritdoc/>
    public abstract void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs);

    /// <inheritdoc/>
    public abstract void WriteConfig(Utf8JsonWriter writer);

    /// <summary>Releases the head's weights, and the encoder when <see cref="OwnsEncoder"/> is set. Safe to call more than once.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ProcessBatch([doc]);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The head is not initialized (not trained and not loaded).</exception>
    public void ProcessBatch(ReadOnlySpan<Doc> docs)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException($"The component '{Name}' has no weights yet: train it or load a saved pipeline.");
        }

        var nonEmpty = new List<Doc>(docs.Length);
        foreach (Doc doc in docs)
        {
            if (doc.Length > 0)
            {
                nonEmpty.Add(doc);
            }
        }

        if (nonEmpty.Count == 0)
        {
            return;
        }

        Doc[] batch = [.. nonEmpty];
        if (Encoder is FastEncoder)
        {
            // The fast encoder is safe to call from several threads (its per-thread cache and rebuilt tables take care of themselves), and
            // encoding is most of the work, so only the head's own weights and caches are taken in turns.
            Encoder.Weights?.Eval();
            using NoGradScope fastNoGrad = GradMode.NoGrad();
            using var fastScope = new ActivationScope();
            EncodedBatch fastEncoded = Encoder.Encode(batch);
            lock (_gate)
            {
                Weights.Eval();
                Predict(fastEncoded, batch);
            }

            return;
        }

        lock (_gate)
        {
            Encoder.Weights?.Eval();
            Weights.Eval();
            using NoGradScope noGrad = GradMode.NoGrad();
            using var scope = new ActivationScope();
            EncodedBatch encoded = Encoder.Encode(batch);
            Predict(encoded, batch);
        }
    }

    /// <summary>Releases what the head owns.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && OwnsEncoder)
        {
            Encoder.Dispose();
        }
    }
}
