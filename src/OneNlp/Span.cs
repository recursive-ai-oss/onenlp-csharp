namespace RecursiveAI.OneNlp;

/// <summary>
/// A view of a run of tokens of a <see cref="Doc"/> with an optional label and a confidence: an entity, a sentence, a rule match or
/// any slice. It copies nothing. Offsets are character offsets into the original text. The default value is not a span.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Serialization.SpanJsonConverter))]
public readonly struct Span : IEquatable<Span>
{
    private readonly Doc _doc;

    private Doc Owner => _doc ?? throw new InvalidOperationException("A default Span has no document; get spans from a Doc.");
    private readonly int _start;
    private readonly int _end;
    private readonly int _label;
    private readonly float _confidence;

    /// <summary>Creates a span over tokens <paramref name="start"/> up to but not including <paramref name="end"/>.</summary>
    /// <param name="doc">The document.</param>
    /// <param name="start">The index of the first token.</param>
    /// <param name="end">The index just past the last token; equal to <paramref name="start"/> for an empty span.</param>
    /// <param name="label">The label, interned in the document's string table; <see langword="null"/> or empty for none.</param>
    /// <param name="confidence">From 0 to 1; 1 for rule-based and unscored spans.</param>
    /// <exception cref="ArgumentNullException"><paramref name="doc"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range is not inside the document, or the confidence is not in [0, 1].</exception>
    public Span(Doc doc, int start, int end, string? label = null, float confidence = 1f)
        : this(Check(doc, start, end, confidence), start, end, label is null ? 0 : doc.Strings.Add(label), confidence)
    {
    }

    internal Span(Doc doc, int start, int end, int labelId, float confidence)
    {
        _doc = doc;
        _start = start;
        _end = end;
        _label = labelId;
        _confidence = confidence;
    }

    /// <summary>The document this span belongs to.</summary>
    public Doc Doc => Owner;

    /// <summary>The index of the first token.</summary>
    public int Start => _start;

    /// <summary>The index just past the last token.</summary>
    public int End => _end;

    /// <summary>The number of tokens.</summary>
    public int Length => _end - _start;

    /// <summary>
    /// The offset of the first character: the start of the first token. An empty span sits at the start of the token it precedes, or at
    /// the end of the text.
    /// </summary>
    public int StartChar => _start < Owner.Length ? Owner.StartOf(_start) : Owner.Text.Length;

    /// <summary>The offset just past the last character: the end of the last token (equal to <see cref="StartChar"/> when empty).</summary>
    public int EndChar => _end > _start ? Owner.StartOf(_end - 1) + Owner.LengthOf(_end - 1) : StartChar;

    /// <summary>The label; the empty string when there is none.</summary>
    public string Label => Owner.Strings[_label];

    /// <summary>The label's ID in the document's string table; 0 for none.</summary>
    public int LabelId => _label;

    /// <summary>The model's confidence from 0 to 1; 1 for rule-based and unscored spans.</summary>
    public float Confidence => _confidence;

    /// <summary>The text from <see cref="StartChar"/> to <see cref="EndChar"/>, whitespace between tokens included, as a new string (spaCy's <c>text</c>). Each call copies; see <see cref="TextSpan"/>.</summary>
    public string Text => Owner.Text.Substring(StartChar, EndChar - StartChar);

    /// <summary>The same text as a slice of the original string, without copying. Compare contents with <see cref="System.MemoryExtensions.SequenceEqual{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>; <c>==</c> on spans compares memory.</summary>
    public ReadOnlySpan<char> TextSpan => Owner.Text.AsSpan(StartChar, EndChar - StartChar);

    /// <summary>The text as memory over the original string.</summary>
    public ReadOnlyMemory<char> TextMemory => Owner.Text.AsMemory(StartChar, EndChar - StartChar);

    /// <summary>Gets a token of the span.</summary>
    /// <param name="index">The position within the span, from 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the span.</exception>
    public Token this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The index is outside the span.");
            }

            return new Token(Owner, _start + index);
        }
    }

    /// <summary>Returns the same tokens with another label and confidence.</summary>
    /// <param name="label">The new label.</param>
    /// <param name="confidence">The new confidence from 0 to 1.</param>
    public Span With(string? label, float confidence = 1f) => new(Owner, _start, _end, label, confidence);

    /// <summary>Returns <see cref="Text"/>.</summary>
    public override string ToString() => Text;

    /// <summary>Enumerates the span's tokens without allocating.</summary>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    public bool Equals(Span other) =>
        ReferenceEquals(_doc, other._doc) && _start == other._start && _end == other._end && _label == other._label
        && _confidence.Equals(other._confidence);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Span other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_doc, _start, _end, _label);

    /// <summary>Whether two spans cover the same tokens of the same document with the same label and confidence.</summary>
    public static bool operator ==(Span left, Span right) => left.Equals(right);

    /// <summary>Whether two spans differ.</summary>
    public static bool operator !=(Span left, Span right) => !left.Equals(right);

    private static Doc Check(Doc doc, int start, int end, float confidence)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (start < 0 || end < start || end > doc.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(end), $"The token range {start}..{end} is not inside a document of {doc.Length} tokens.");
        }

        if (!(confidence >= 0f && confidence <= 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "A confidence is from 0 to 1.");
        }

        return doc;
    }

    /// <summary>Walks the tokens of a <see cref="Span"/>.</summary>
    public struct Enumerator
    {
        private readonly Span _span;
        private int _next;

        internal Enumerator(Span span)
        {
            _span = span;
            _next = span._start;
            Current = default;
        }

        /// <summary>The current token.</summary>
        public Token Current { get; private set; }

        /// <summary>Moves to the next token.</summary>
        public bool MoveNext()
        {
            if (_next >= _span._end)
            {
                return false;
            }

            Current = new Token(_span.Owner, _next++);
            return true;
        }
    }
}
