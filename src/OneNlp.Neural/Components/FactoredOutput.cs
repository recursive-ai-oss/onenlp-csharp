using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// The output layer of a head with many classes, most of them rare: either one softmax over every class, or a two-level softmax
/// (a "class-based" factorization of the softmax, Goodman 2001, "Classes for fast maximum entropy training"): the first level chooses
/// among the <c>headClasses</c> most frequent classes or "other", and only the tokens for which "other" wins pay for the second level
/// over the remaining classes. The probability of a rare class is the product of the two levels.
/// </summary>
/// <remarks>
/// The classes must be numbered by falling frequency for the factorization to save anything: the head classes are the first
/// <c>headClasses</c> numbers. With <c>headClasses</c> 0 the layer is the plain softmax (and its weights keep the name <c>output</c>, as
/// the heads saved before this layer existed have them).
/// </remarks>
internal sealed class FactoredOutput : NnModule
{
    private readonly Linear? _flat;
    private readonly Linear? _head;
    private readonly Linear? _tail;

    public FactoredOutput(int width, int classes, int headClasses, Generator generator)
    {
        Classes = classes;
        HeadClasses = headClasses > 0 && headClasses < classes ? headClasses : 0;
        if (HeadClasses == 0)
        {
            _flat = RegisterModule("output", new Linear(width, classes, generator));
        }
        else
        {
            _head = RegisterModule("head", new Linear(width, HeadClasses + 1, generator));
            _tail = RegisterModule("tail", new Linear(width, classes - HeadClasses, generator));
        }
    }

    public int Classes { get; }

    /// <summary>The number of first-level classes before "other"; 0 for a plain softmax.</summary>
    public int HeadClasses { get; }

    public bool IsFactored => HeadClasses > 0;

    public override Tensor Forward(Tensor input) => throw new NotSupportedException("Use Loss, Decode or the level methods.");

    /// <summary>Plain softmax logits [rows, classes].</summary>
    public Tensor Logits(Tensor vectors) => _flat!.Forward(vectors);

    /// <summary>First-level logits [rows, headClasses + 1]; the last column is "other".</summary>
    public Tensor HeadLogits(Tensor vectors) => _head!.Forward(vectors);

    /// <summary>Second-level logits [rows.Length, classes - headClasses] of the given rows of <paramref name="vectors"/>.</summary>
    public Tensor TailLogits(Tensor vectors, int[] rows) => _tail!.Forward(Ops.Embedding(vectors, IntTensor(rows)));

    /// <summary>
    /// The mean negative log-likelihood of the targets over the <paramref name="counted"/> rows that have one; a target equal to
    /// <paramref name="ignoreIndex"/> leaves a row out. With nothing counted the result is a zero that still depends on the weights.
    /// </summary>
    public Tensor Loss(Tensor vectors, int[] targets, int ignoreIndex, int counted)
    {
        if (!IsFactored)
        {
            return Losses.CrossEntropy(Logits(vectors), IntTensor(targets), 0f, ignoreIndex, counted > 0);
        }

        int k = HeadClasses;
        var firstLevel = new int[targets.Length];
        var tailRows = new List<int>();
        var tailTargets = new List<int>();
        for (int i = 0; i < targets.Length; i++)
        {
            int t = targets[i];
            if (t == ignoreIndex || t < k)
            {
                firstLevel[i] = t;
            }
            else
            {
                firstLevel[i] = k;
                tailRows.Add(i);
                tailTargets.Add(t - k);
            }
        }

        Tensor total = Losses.CrossEntropy(HeadLogits(vectors), IntTensor(firstLevel), 0f, ignoreIndex, mean: false);
        if (tailRows.Count > 0)
        {
            Tensor second = Losses.CrossEntropy(TailLogits(vectors, [.. tailRows]), IntTensor([.. tailTargets]), 0f, ignoreIndex, mean: false);
            total = total + second;
        }

        return total * (counted > 0 ? 1f / counted : 0f);
    }

    /// <summary>The most probable class of every row and its probability.</summary>
    public void Decode(Tensor vectors, Span<int> best, Span<float> confidence)
    {
        if (!IsFactored)
        {
            ReadOnlySpan<float> p = Ops.Softmax(Logits(vectors)).Data<float>();
            for (int r = 0; r < best.Length; r++)
            {
                (best[r], confidence[r]) = Top(p.Slice(r * Classes, Classes));
            }

            return;
        }

        int k = HeadClasses;
        int width = k + 1;
        ReadOnlySpan<float> first = Ops.Softmax(HeadLogits(vectors)).Data<float>();
        var tailRows = new List<int>();
        for (int r = 0; r < best.Length; r++)
        {
            (int c, float p) = Top(first.Slice(r * width, width));
            if (c < k)
            {
                best[r] = c;
                confidence[r] = p;
            }
            else
            {
                tailRows.Add(r);
            }
        }

        if (tailRows.Count == 0)
        {
            return;
        }

        int tailWidth = Classes - k;
        ReadOnlySpan<float> second = Ops.Softmax(TailLogits(vectors, [.. tailRows])).Data<float>();
        for (int i = 0; i < tailRows.Count; i++)
        {
            int r = tailRows[i];
            (int c, float p) = Top(second.Slice(i * tailWidth, tailWidth));
            best[r] = k + c;
            confidence[r] = Math.Clamp(first[(r * width) + k] * p, 0f, 1f);
        }
    }

    // The arg-max of a row and its value; NaN becomes confidence 0.
    private static (int Index, float Value) Top(ReadOnlySpan<float> row)
    {
        int best = 0;
        float top = row[0];
        for (int c = 1; c < row.Length; c++)
        {
            float v = row[c];
            if (v > top)
            {
                top = v;
                best = c;
            }
        }

        return (best, float.IsNaN(top) ? 0f : Math.Clamp(top, 0f, 1f));
    }

    private static Tensor IntTensor(int[] values)
    {
        Tensor t = Tensor.FromSpan<int>(values, Shape.Create(values.Length));
        ActivationScope.Track(t);
        return t;
    }
}
