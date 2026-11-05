using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

// An inference copy of a Linear output layer in the layout RowKernels.Dense wants: weights transposed to [inputs, stride], the classes
// padded to a multiple of 4 with zeros. Cheap to keep: the same number of values as the layer.
internal sealed class RowClassifier
{
    private readonly float[] _wT;
    private readonly float[] _bias;

    public RowClassifier(Linear layer, long stamp)
    {
        Stamp = stamp;
        Classes = layer.Weight.Shape[0];
        Inputs = layer.Weight.Shape[1];
        Stride = (Classes + 3) & ~3;
        _wT = new float[Inputs * Stride];
        _bias = new float[Stride];
        ReadOnlySpan<float> w = layer.Weight.Data<float>();
        for (int c = 0; c < Classes; c++)
        {
            for (int i = 0; i < Inputs; i++)
            {
                _wT[(i * Stride) + c] = w[(c * Inputs) + i];
            }
        }

        layer.Bias.Data<float>().CopyTo(_bias);
    }

    public long Stamp { get; }

    public int Classes { get; }

    public int Inputs { get; }

    /// <summary>Length of one row of scores (the classes rounded up to a multiple of 4).</summary>
    public int Stride { get; }

    /// <summary>Scores of one input row into <paramref name="logits"/> (at least <see cref="Stride"/> long).</summary>
    public void Logits(ReadOnlySpan<float> x, Span<float> logits) => RowKernels.Dense(x, _wT, Stride, _bias, logits);

    /// <summary>Scores of every row of <paramref name="vectors"/> ([rows, Inputs]) into <paramref name="logits"/> ([rows, Stride]).</summary>
    public void LogitsAll(ReadOnlySpan<float> vectors, int rows, Span<float> logits)
    {
        for (int r = 0; r < rows; r++)
        {
            Logits(vectors.Slice(r * Inputs, Inputs), logits.Slice(r * Stride, Stride));
        }
    }
}

// Keeps the RowClassifier of a Linear and rebuilds it when the layer's weights change (the storage version counters tell).
internal sealed class FrozenLinear(Linear layer)
{
    private readonly object _lock = new();
    private volatile RowClassifier? _current;

    public RowClassifier Get()
    {
        long stamp = layer.Weight.Version + layer.Bias.Version;
        RowClassifier? c = _current;
        if (c is not null && c.Stamp == stamp)
        {
            return c;
        }

        lock (_lock)
        {
            stamp = layer.Weight.Version + layer.Bias.Version;
            c = _current;
            if (c is null || c.Stamp != stamp)
            {
                c = new RowClassifier(layer, stamp);
                _current = c;
            }

            return c;
        }
    }
}
