using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// Lets one head learn from only some of the documents of a mixed batch. Training one shared encoder on several languages at once
/// needs a head per language, each seeing its own language's examples; the encoder sees them all. The wrapper selects the
/// in-scope rows of the encoded batch (the gradient still flows back to the encoder through them), passes them to the inner head, and
/// contributes a zero loss when a batch holds none.
/// </summary>
/// <remarks>
/// A scoped head is a training and scoring device: it has no saved form of its own. Save the inner head (a pipeline built from
/// the inner heads alone) after training.
/// </remarks>
public sealed class ScopedHead : ITrainableComponent
{
    private readonly ITrainableComponent _inner;
    private readonly Func<Doc, bool> _inScope;
    private float _lossWeight = 1f;

    /// <summary>Wraps a head.</summary>
    /// <param name="inner">The head that does the work.</param>
    /// <param name="inScope">Whether a document (an example's predicted document, or one to annotate) belongs to this head.</param>
    public ScopedHead(ITrainableComponent inner, Func<Doc, bool> inScope)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(inScope);
        _inner = inner;
        _inScope = inScope;
    }

    /// <summary>
    /// How much this head's loss counts against the other heads' in a mixed batch (default 1). A weight above 1 asks the shared encoder to serve
    /// this head more; the head's own gradient is scaled by the same factor. Not saved. Must be finite and not negative.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative, infinite or not a number.</exception>
    public float LossWeight
    {
        get => _lossWeight;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The loss weight must be finite and not negative.");
            }

            _lossWeight = value;
        }
    }

    /// <summary>The wrapped head.</summary>
    public ITrainableComponent Inner => _inner;

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <inheritdoc/>
    public string Kind => _inner.Kind;

    /// <inheritdoc/>
    public IEncoder Encoder => _inner.Encoder;

    /// <inheritdoc/>
    public bool IsInitialized => _inner.IsInitialized;

    /// <inheritdoc/>
    public NnModule Weights => _inner.Weights;

    /// <inheritdoc/>
    public void Initialize(IReadOnlyList<Example> examples, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(examples);
        _inner.Initialize([.. examples.Where(e => _inScope(e.Predicted))], seed);
    }

    /// <inheritdoc/>
    public Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        var picked = new List<int>();
        for (int i = 0; i < examples.Count; i++)
        {
            if (_inScope(examples[i].Predicted))
            {
                picked.Add(i);
            }
        }

        if (picked.Count == 0)
        {
            return Tensor.Scalar(0f);
        }

        Tensor loss;
        if (picked.Count == examples.Count)
        {
            loss = _inner.Loss(encoded, examples);
        }
        else
        {
            (EncodedBatch sub, List<Example> chosen) = Select(encoded, examples, picked);
            loss = _inner.Loss(sub, chosen);
        }

        return _lossWeight == 1f ? loss : Ops.Mul(loss, Tensor.Scalar(_lossWeight));
    }

    /// <inheritdoc/>
    public void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        var picked = new List<int>();
        for (int i = 0; i < docs.Length; i++)
        {
            if (_inScope(docs[i]))
            {
                picked.Add(i);
            }
        }

        if (picked.Count == 0)
        {
            return;
        }

        if (picked.Count == docs.Length)
        {
            _inner.Predict(encoded, docs);
            return;
        }

        var rows = new List<Tensor>(picked.Count);
        var starts = new int[picked.Count + 1];
        var chosen = new Doc[picked.Count];
        for (int k = 0; k < picked.Count; k++)
        {
            starts[k + 1] = starts[k] + encoded.Length(picked[k]);
            rows.Add(encoded.Rows(picked[k]));
            chosen[k] = docs[picked[k]];
        }

        _inner.Predict(new EncodedBatch(Ops.Concat(rows.ToArray(), 0), starts), chosen);
    }

    /// <inheritdoc/>
    public void WriteConfig(Utf8JsonWriter writer) => _inner.WriteConfig(writer);

    /// <inheritdoc/>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (_inScope(doc))
        {
            _inner.Process(doc);
        }
    }

    /// <inheritdoc/>
    public void ProcessBatch(ReadOnlySpan<Doc> docs)
    {
        var picked = new List<Doc>();
        foreach (Doc doc in docs)
        {
            if (_inScope(doc))
            {
                picked.Add(doc);
            }
        }

        if (picked.Count > 0)
        {
            _inner.ProcessBatch(picked.ToArray());
        }
    }

    private static (EncodedBatch Batch, List<Example> Chosen) Select(EncodedBatch encoded, IReadOnlyList<Example> examples, List<int> picked)
    {
        var rows = new List<Tensor>(picked.Count);
        var starts = new int[picked.Count + 1];
        var chosen = new List<Example>(picked.Count);
        for (int k = 0; k < picked.Count; k++)
        {
            starts[k + 1] = starts[k] + encoded.Length(picked[k]);
            rows.Add(encoded.Rows(picked[k]));
            chosen.Add(examples[picked[k]]);
        }

        return (new EncodedBatch(Ops.Concat(rows.ToArray(), 0), starts), chosen);
    }
}
