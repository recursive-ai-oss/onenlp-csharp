namespace RecursiveAI.OneNlp.Data;

/// <summary>
/// The token-to-token alignment between two tokenizations of the same text: for every token of <c>x</c> the tokens of <c>y</c> that
/// share characters with it, and the other way round.
/// </summary>
/// <remarks>
/// <para>
/// The two texts must be equal once whitespace is removed and case is ignored, so a gold tokenization that splits "don't" into two
/// tokens aligns with a prediction that keeps it whole, and different spacing between tokens does not matter. Each token covers a
/// range of the whitespace-free characters, and two tokens are aligned when their ranges overlap. Alignment is one-to-one,
/// one-to-many, many-to-one or many-to-many depending on how the boundaries fall.
/// </para>
/// <para>The same alignment concept is documented for spaCy's <c>Example</c>; this is an independent implementation of that description.</para>
/// </remarks>
public sealed class Alignment
{
    private readonly int[][] _xToY;
    private readonly int[][] _yToX;
    private readonly int[] _xStart;
    private readonly int[] _xEnd;
    private readonly int[] _yStart;
    private readonly int[] _yEnd;

    private Alignment(int[][] xToY, int[][] yToX, int[] xStart, int[] xEnd, int[] yStart, int[] yEnd)
    {
        _xToY = xToY;
        _yToX = yToX;
        _xStart = xStart;
        _xEnd = xEnd;
        _yStart = yStart;
        _yEnd = yEnd;
    }

    /// <summary>The number of tokens in the first document.</summary>
    public int XLength => _xToY.Length;

    /// <summary>The number of tokens in the second document.</summary>
    public int YLength => _yToX.Length;

    /// <summary>The tokens of the second document that overlap a token of the first, in order. Empty for a token with no characters.</summary>
    /// <param name="x">A token index of the first document.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is outside the document.</exception>
    public ReadOnlySpan<int> XToY(int x) => _xToY[x];

    /// <summary>The tokens of the first document that overlap a token of the second, in order.</summary>
    /// <param name="y">A token index of the second document.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="y"/> is outside the document.</exception>
    public ReadOnlySpan<int> YToX(int y) => _yToX[y];

    /// <summary>Aligns two tokenizations.</summary>
    /// <param name="x">The first document.</param>
    /// <param name="y">The second document.</param>
    /// <exception cref="ArgumentException">The texts differ after whitespace is removed and case is ignored.</exception>
    public static Alignment Create(Doc x, Doc y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        int[] xs = new int[x.Length], xe = new int[x.Length], ys = new int[y.Length], ye = new int[y.Length];
        int xCount = Ranges(x, xs, xe);
        int yCount = Ranges(y, ys, ye);
        if (xCount != yCount || !SameLetters(x.Text, y.Text))
        {
            throw new ArgumentException("The two documents cannot be aligned: their texts differ by more than whitespace and capitalization.");
        }

        List<int>[] xy = NewLists(x.Length), yx = NewLists(y.Length);
        int j = 0;
        for (int i = 0; i < x.Length; i++)
        {
            if (xe[i] <= xs[i])
            {
                continue;
            }

            while (j < y.Length && ye[j] <= xs[i])
            {
                j++;
            }

            for (int k = j; k < y.Length && ys[k] < xe[i]; k++)
            {
                if (ye[k] > ys[k])
                {
                    xy[i].Add(k);
                    yx[k].Add(i);
                }
            }
        }

        return new Alignment(ToArrays(xy), ToArrays(yx), xs, xe, ys, ye);
    }

    /// <summary>
    /// Maps token spans of the second document to the first, keeping a span only when its edges fall exactly on token boundaries of
    /// the first document. Used to carry gold entities onto a differently tokenized prediction.
    /// </summary>
    /// <param name="spans">Spans as token ranges (<see cref="SpanSpec.Start"/> inclusive, <see cref="SpanSpec.End"/> exclusive) of the second document.</param>
    /// <returns>One entry per input span: the span in the first document, or <see langword="null"/> when it does not align on whole tokens.</returns>
    public SpanSpec?[] MapSpansYToX(IReadOnlyList<SpanSpec> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        var result = new SpanSpec?[spans.Count];
        for (int i = 0; i < spans.Count; i++)
        {
            SpanSpec s = spans[i];
            if (s.Start < 0 || s.End > YLength || s.End <= s.Start)
            {
                continue;
            }

            int[] first = _yToX[s.Start];
            int[] last = _yToX[s.End - 1];
            if (first.Length == 0 || last.Length == 0)
            {
                continue;
            }

            int xFirst = first[0];
            int xLast = last[^1];
            if (_xStart[xFirst] == _yStart[s.Start] && _xEnd[xLast] == _yEnd[s.End - 1])
            {
                result[i] = s with { Start = xFirst, End = xLast + 1 };
            }
        }

        return result;
    }

    /// <summary>Whether token <paramref name="x"/> of the first document is aligned with exactly one token of the second, and that token with only it.</summary>
    /// <param name="x">A token index of the first document.</param>
    public bool IsOneToOne(int x) => _xToY[x].Length == 1 && _yToX[_xToY[x][0]].Length == 1;

    private static int Ranges(Doc doc, int[] starts, int[] ends)
    {
        string text = doc.Text;
        int count = 0;
        int pos = 0;
        ReadOnlySpan<int> idx = doc.GetStarts();
        ReadOnlySpan<int> len = doc.GetLengths();
        for (int t = 0; t < doc.Length; t++)
        {
            for (; pos < idx[t]; pos++)
            {
                if (!char.IsWhiteSpace(text[pos]))
                {
                    count++;
                }
            }

            starts[t] = count;
            int end = idx[t] + len[t];
            for (; pos < end; pos++)
            {
                if (!char.IsWhiteSpace(text[pos]))
                {
                    count++;
                }
            }

            ends[t] = count;
        }

        for (; pos < text.Length; pos++)
        {
            if (!char.IsWhiteSpace(text[pos]))
            {
                count++;
            }
        }

        return count;
    }

    private static bool SameLetters(string a, string b)
    {
        int i = 0, j = 0;
        while (true)
        {
            while (i < a.Length && char.IsWhiteSpace(a[i]))
            {
                i++;
            }

            while (j < b.Length && char.IsWhiteSpace(b[j]))
            {
                j++;
            }

            if (i == a.Length || j == b.Length)
            {
                return i == a.Length && j == b.Length;
            }

            if (a[i] != b[j] && char.ToLowerInvariant(a[i]) != char.ToLowerInvariant(b[j]))
            {
                return false;
            }

            i++;
            j++;
        }
    }

    private static List<int>[] NewLists(int n)
    {
        var lists = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            lists[i] = [];
        }

        return lists;
    }

    private static int[][] ToArrays(List<int>[] lists)
    {
        var arrays = new int[lists.Length][];
        for (int i = 0; i < lists.Length; i++)
        {
            arrays[i] = [.. lists[i]];
        }

        return arrays;
    }
}
