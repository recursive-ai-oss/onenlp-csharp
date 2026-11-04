namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>A half-open range of UTF-16 offsets into the caller's original input.</summary>
/// <param name="Start">The first offset.</param>
/// <param name="End">The offset just past the last unit.</param>
internal readonly record struct OffsetPair(int Start, int End);

/// <summary>
/// A string being normalized, with for every UTF-16 unit of the current text the range of the original input it came from.
/// Normalizers rewrite the text and keep that alignment up to date, so that however the text is changed, offsets reported later
/// still point into the original input. This is the "alignment" the Hugging Face <c>tokenizers</c> documentation describes for its
/// normalized strings: a unit inserted by a normalizer shares the origin of a neighbour, and a unit that merges several originals
/// covers their whole range.
/// </summary>
internal sealed class NormalizedString
{
    private string _text;
    private OffsetPair[] _alignment;

    /// <summary>Starts from the original input, every unit aligned with itself.</summary>
    /// <param name="original">The caller's input.</param>
    /// <param name="originOffset">The offset of <paramref name="original"/> inside the larger input, 0 for the whole input.</param>
    internal NormalizedString(string original, int originOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(original);
        _text = original;
        _alignment = new OffsetPair[original.Length];
        for (int i = 0; i < _alignment.Length; i++)
        {
            // Both halves of a surrogate pair stand for the same character, so both cover its two units.
            if (char.IsHighSurrogate(original[i]) && i + 1 < original.Length && char.IsLowSurrogate(original[i + 1]))
            {
                _alignment[i] = _alignment[i + 1] = new OffsetPair(originOffset + i, originOffset + i + 2);
                i++;
            }
            else
            {
                _alignment[i] = new OffsetPair(originOffset + i, originOffset + i + 1);
            }
        }
    }

    private NormalizedString(string text, OffsetPair[] alignment)
    {
        _text = text;
        _alignment = alignment;
    }

    /// <summary>The current, normalized text.</summary>
    internal string Text => _text;

    /// <summary>The number of UTF-16 units of the current text.</summary>
    internal int Length => _text.Length;

    /// <summary>The origin of one unit of the current text.</summary>
    /// <param name="index">The unit index.</param>
    internal OffsetPair AlignmentAt(int index) => _alignment[index];

    /// <summary>Replaces the text and its alignment. Both must have the same length.</summary>
    /// <param name="text">The new text.</param>
    /// <param name="alignment">The origin of each unit of <paramref name="text"/>.</param>
    /// <exception cref="ArgumentException">The lengths differ.</exception>
    internal void Replace(string text, OffsetPair[] alignment)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(alignment);
        if (text.Length != alignment.Length)
        {
            throw new ArgumentException("A normalized string needs one alignment entry per UTF-16 unit.", nameof(alignment));
        }

        _text = text;
        _alignment = alignment;
    }

    /// <summary>Takes a part of the current text, keeping its alignment, as a new string that reports offsets into the same original input.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="end">Just past the last unit.</param>
    internal NormalizedString Slice(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, _text.Length);
        return new NormalizedString(_text.Substring(start, end - start), _alignment[start..end]);
    }

    /// <summary>
    /// Maps a range of the current text to the range of the original input it came from: from the origin of the first unit to the
    /// end of the origin of the last. An empty range maps to an empty range at the origin of the unit it precedes.
    /// </summary>
    /// <param name="start">The first unit.</param>
    /// <param name="end">Just past the last unit.</param>
    internal OffsetPair ToOriginal(int start, int end)
    {
        if (_alignment.Length == 0)
        {
            return default;
        }

        if (end <= start)
        {
            int at = _alignment[Math.Min(start, _alignment.Length - 1)].Start;
            return new OffsetPair(at, at);
        }

        return new OffsetPair(_alignment[start].Start, _alignment[end - 1].End);
    }
}

/// <summary>Builds the replacement text of a <see cref="NormalizedString"/>, one unit at a time, with the origin of each unit.</summary>
internal sealed class NormalizedStringBuilder
{
    private readonly System.Text.StringBuilder _text = new();
    private readonly List<OffsetPair> _alignment = [];

    /// <summary>Appends one unit that came from the given origin.</summary>
    /// <param name="unit">The UTF-16 unit.</param>
    /// <param name="origin">Where it came from in the original input.</param>
    internal void Append(char unit, OffsetPair origin)
    {
        _text.Append(unit);
        _alignment.Add(origin);
    }

    /// <summary>Appends text whose every unit shares one origin (inserted or replaced text).</summary>
    /// <param name="text">The text.</param>
    /// <param name="origin">Where it came from in the original input.</param>
    internal void Append(ReadOnlySpan<char> text, OffsetPair origin)
    {
        foreach (char c in text)
        {
            Append(c, origin);
        }
    }

    /// <summary>Appends a unit of an existing normalized string with its own origin.</summary>
    /// <param name="source">The string.</param>
    /// <param name="index">The unit index.</param>
    internal void AppendFrom(NormalizedString source, int index) => Append(source.Text[index], source.AlignmentAt(index));

    /// <summary>The number of units appended so far.</summary>
    internal int Length => _alignment.Count;

    /// <summary>Moves every origin by a number of units, not below 0.</summary>
    /// <param name="by">The amount to add to the start and the end of each origin.</param>
    internal void ShiftOrigins(int by)
    {
        if (by == 0)
        {
            return;
        }

        for (int i = 0; i < _alignment.Count; i++)
        {
            _alignment[i] = new OffsetPair(Math.Max(0, _alignment[i].Start + by), Math.Max(0, _alignment[i].End + by));
        }
    }

    /// <summary>Writes the built text into a normalized string.</summary>
    /// <param name="target">The string to replace the content of.</param>
    internal void CopyTo(NormalizedString target) => target.Replace(_text.ToString(), [.. _alignment]);
}

/// <summary>
/// The text on its way through pre-tokenization: a list of pieces (splits), each a <see cref="NormalizedString"/> that still knows its
/// origin. The model later tokenizes each piece on its own, and the piece's index is the word index of its tokens.
/// </summary>
internal sealed class PreTokenizedString
{
    private List<NormalizedString> _splits;

    /// <summary>Starts with the whole normalized text as one piece (no pieces when it is empty).</summary>
    /// <param name="normalized">The normalized text.</param>
    internal PreTokenizedString(NormalizedString normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        _splits = normalized.Length == 0 ? [] : [normalized];
    }

    /// <summary>The pieces in text order. Never contains an empty piece.</summary>
    internal IReadOnlyList<NormalizedString> Splits => _splits;

    /// <summary>Replaces every piece by the pieces a function makes of it; empty results are dropped.</summary>
    /// <param name="split">Called once per piece, in order. It may return the piece itself, parts of it, or rewritten pieces.</param>
    internal void SplitEach(Func<NormalizedString, IEnumerable<NormalizedString>> split)
    {
        ArgumentNullException.ThrowIfNull(split);
        SplitEach((_, piece) => split(piece));
    }

    /// <summary>Like <see cref="SplitEach(Func{NormalizedString, IEnumerable{NormalizedString}})"/>, with the index of the piece, for stages that treat the first piece differently.</summary>
    /// <param name="split">Called once per piece with its index, in order.</param>
    internal void SplitEach(Func<int, NormalizedString, IEnumerable<NormalizedString>> split)
    {
        ArgumentNullException.ThrowIfNull(split);
        var result = new List<NormalizedString>(_splits.Count);
        for (int index = 0; index < _splits.Count; index++)
        {
            foreach (NormalizedString part in split(index, _splits[index]))
            {
                if (part.Length > 0)
                {
                    result.Add(part);
                }
            }
        }

        _splits = result;
    }
}
