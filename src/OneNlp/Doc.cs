using System.Collections;

namespace RecursiveAI.OneNlp;

/// <summary>
/// A processed text: the original string plus one array per token attribute (a columnar layout, which serializes and scans cheaply).
/// <see cref="Token"/> and <see cref="Span"/> are views over a document and copy nothing.
/// </summary>
/// <remarks>
/// <para>
/// Every offset a document hands out is a UTF-16 code unit offset into <see cref="Text"/>, the string the caller passed in, never
/// into a normalized copy.
/// </para>
/// <para>
/// A document is created with its token boundaries, which never change afterwards. Components then fill the attribute columns
/// (<see cref="SetAnnotation"/>, <see cref="SetColumn"/>, <see cref="SetHeads"/>, <see cref="SetSentenceBoundaries"/>,
/// <see cref="SetEntities"/>, <see cref="SetConfidences"/>). Strings are stored as IDs in the shared <see cref="Strings"/> table;
/// ID 0 (the empty string) means "not set". A new document has the empty string in every string column, every token as its
/// own head, every sentence boundary unset, no entity annotation and a confidence of 1.
/// </para>
/// <para>
/// Reads may run concurrently with each other. Writes need the caller's exclusive use of the document, which a pipeline gives
/// each component in turn.
/// </para>
/// </remarks>
public sealed class Doc : IEnumerable<Token>
{
    private const int AttributeCount = 6;

    private readonly int[] _start;
    private readonly int[] _length;
    private readonly bool[] _spaceAfter;
    private readonly int[][] _attributes;
    private readonly int[] _heads;
    private readonly SentenceBoundary[] _sentences;
    private readonly float[] _confidence;
    private readonly EntIob[] _entIob;
    private readonly int[] _entType;
    private EntityRecord[] _entities = [];
    private Span[]? _sentenceSpans;
    private Span[]? _entitySpans;

    private readonly record struct EntityRecord(int Start, int End, int Label, float Confidence);

    /// <summary>Creates a document from a text and the boundaries of its tokens.</summary>
    /// <param name="strings">The interning table shared by the documents of one pipeline.</param>
    /// <param name="text">The original text. The document keeps this string and returns slices of it.</param>
    /// <param name="tokens">
    /// The tokens in text order. Each has a length of at least 1, lies inside <paramref name="text"/> and starts at or after the end of
    /// the one before. Characters between tokens (whitespace, for the rule tokenizer) belong to no token.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary is empty, out of range, out of order or overlaps the one before it.</exception>
    public Doc(StringStore strings, string text, ReadOnlySpan<TokenBoundary> tokens)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(text);
        Strings = strings;
        Text = text;

        int n = tokens.Length;
        _start = new int[n];
        _length = new int[n];
        _spaceAfter = new bool[n];
        int previousEnd = 0;
        for (int i = 0; i < n; i++)
        {
            TokenBoundary t = tokens[i];
            if (t.Length < 1 || t.Start < previousEnd || t.Length > text.Length - t.Start)
            {
                throw new ArgumentException(
                    $"Token {i} ({t.Start}, length {t.Length}) is empty, out of range, or not after the token before it.", nameof(tokens));
            }

            _start[i] = t.Start;
            _length[i] = t.Length;
            previousEnd = t.End;
            _spaceAfter[i] = previousEnd < text.Length && char.IsWhiteSpace(text[previousEnd]);
        }

        _attributes = new int[AttributeCount][];
        for (int a = 0; a < AttributeCount; a++)
        {
            _attributes[a] = new int[n];
        }

        _heads = new int[n];
        for (int i = 0; i < n; i++)
        {
            _heads[i] = i;
        }

        _sentences = new SentenceBoundary[n];
        _confidence = new float[n];
        Array.Fill(_confidence, 1f);
        _entIob = new EntIob[n];
        _entType = new int[n];
    }

    /// <summary>The interning table holding the strings that the attribute columns refer to.</summary>
    public StringStore Strings { get; }

    /// <summary>The original text, exactly as given.</summary>
    public string Text { get; }

    /// <summary>The number of tokens.</summary>
    public int Length => _start.Length;

    /// <summary>Gets a token.</summary>
    /// <param name="index">The token index, from 0; <c>doc[^1]</c> is the last token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the document.</exception>
    public Token this[int index]
    {
        get
        {
            CheckToken(index);
            return new Token(this, index);
        }
    }

    /// <summary>Gets a view of a token range, as <c>doc[2..5]</c> does. The span has no label and a confidence of 1.</summary>
    /// <param name="start">The index of the first token.</param>
    /// <param name="length">The number of tokens; 0 gives an empty span.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not inside the document.</exception>
    public Span Slice(int start, int length) => new(this, start, start + length);

    /// <summary>The sentences in order, covering every token.</summary>
    /// <remarks>
    /// A sentence starts at token 0 and at each token whose boundary is <see cref="SentenceBoundary.Start"/>. When no component has
    /// set boundaries, the whole document is one sentence (and an empty document has none). Spans have no label and a confidence of 1.
    /// </remarks>
    public IReadOnlyList<Span> Sentences => _sentenceSpans ??= BuildSentences();

    /// <summary>The entities in text order. They never overlap. Empty until <see cref="SetEntities"/> runs.</summary>
    public IReadOnlyList<Span> Ents => _entitySpans ??= BuildEntities();

    /// <summary>The per-token sentence boundary column, for serializers and components that read it in bulk.</summary>
    public ReadOnlySpan<SentenceBoundary> GetSentenceBoundaries() => _sentences;

    /// <summary>The head of each token as a token index. A token that is its own head is a root (spaCy's convention).</summary>
    public ReadOnlySpan<int> GetHeads() => _heads;

    /// <summary>The confidence column; see <see cref="Token.Confidence"/>.</summary>
    public ReadOnlySpan<float> GetConfidences() => _confidence;

    /// <summary>The start offset of each token, for serializers and bulk readers.</summary>
    public ReadOnlySpan<int> GetStarts() => _start;

    /// <summary>The length of each token in UTF-16 code units, for serializers and bulk readers.</summary>
    public ReadOnlySpan<int> GetLengths() => _length;

    /// <summary>The interned IDs of one string-valued attribute, one per token; 0 means not set.</summary>
    /// <param name="attribute">The attribute.</param>
    public ReadOnlySpan<int> GetColumn(TokenColumn attribute) => Column(attribute);

    /// <summary>Sets one string-valued attribute of one token, interning the value.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="token">The token index.</param>
    /// <param name="value">The value; the empty string clears it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> or <paramref name="attribute"/> is out of range.</exception>
    public void SetAnnotation(TokenColumn attribute, int token, string value)
    {
        int[] column = Column(attribute);
        CheckToken(token);
        column[token] = Strings.Add(value);
    }

    /// <summary>Replaces one string-valued attribute column with IDs already interned in <see cref="Strings"/>.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="ids">One ID per token.</param>
    /// <exception cref="ArgumentException">The length is not <see cref="Length"/>, or an ID is not in <see cref="Strings"/>.</exception>
    public void SetColumn(TokenColumn attribute, ReadOnlySpan<int> ids)
    {
        int[] column = Column(attribute);
        if (ids.Length != Length)
        {
            throw new ArgumentException("The column needs one ID per token.", nameof(ids));
        }

        int count = Strings.Count;
        foreach (int id in ids)
        {
            if ((uint)id >= (uint)count)
            {
                throw new ArgumentException($"ID {id} is not in the string table.", nameof(ids));
            }
        }

        ids.CopyTo(column);
    }

    /// <summary>Sets the head of one token.</summary>
    /// <param name="token">The dependent token index.</param>
    /// <param name="head">The head token index; the token's own index makes it a root.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the document.</exception>
    public void SetHead(int token, int head)
    {
        CheckToken(token);
        CheckToken(head);
        _heads[token] = head;
    }

    /// <summary>Replaces the head column.</summary>
    /// <param name="heads">One token index per token.</param>
    /// <exception cref="ArgumentException">The length is not <see cref="Length"/>, or an index is outside the document.</exception>
    public void SetHeads(ReadOnlySpan<int> heads)
    {
        if (heads.Length != Length)
        {
            throw new ArgumentException("The column needs one head per token.", nameof(heads));
        }

        foreach (int h in heads)
        {
            if ((uint)h >= (uint)Length)
            {
                throw new ArgumentException($"Head {h} is outside the document.", nameof(heads));
            }
        }

        heads.CopyTo(_heads);
    }

    /// <summary>Marks one token as starting a sentence or continuing one.</summary>
    /// <param name="token">The token index.</param>
    /// <param name="isStart"><see langword="true"/> for <see cref="SentenceBoundary.Start"/>, <see langword="false"/> for <see cref="SentenceBoundary.Inside"/>.</param>
    public void SetSentenceStart(int token, bool isStart)
    {
        CheckToken(token);
        _sentences[token] = isStart ? SentenceBoundary.Start : SentenceBoundary.Inside;
        _sentenceSpans = null;
    }

    /// <summary>Replaces the sentence boundary column.</summary>
    /// <param name="boundaries">One value per token.</param>
    /// <exception cref="ArgumentException">The length is not <see cref="Length"/>, or a value is not a defined <see cref="SentenceBoundary"/>.</exception>
    public void SetSentenceBoundaries(ReadOnlySpan<SentenceBoundary> boundaries)
    {
        if (boundaries.Length != Length)
        {
            throw new ArgumentException("The column needs one value per token.", nameof(boundaries));
        }

        foreach (SentenceBoundary b in boundaries)
        {
            if (b > SentenceBoundary.Inside)
            {
                throw new ArgumentException("Unknown sentence boundary value.", nameof(boundaries));
            }
        }

        boundaries.CopyTo(_sentences);
        _sentenceSpans = null;
    }

    /// <summary>Sets the confidence of one token's annotations.</summary>
    /// <param name="token">The token index.</param>
    /// <param name="confidence">From 0 to 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">The token is outside the document, or the confidence is not in [0, 1].</exception>
    public void SetConfidence(int token, float confidence)
    {
        CheckToken(token);
        CheckConfidence(confidence);
        _confidence[token] = confidence;
    }

    /// <summary>Replaces the confidence column.</summary>
    /// <param name="confidences">One value from 0 to 1 per token.</param>
    /// <exception cref="ArgumentException">The length is not <see cref="Length"/>, or a value is not in [0, 1].</exception>
    public void SetConfidences(ReadOnlySpan<float> confidences)
    {
        if (confidences.Length != Length)
        {
            throw new ArgumentException("The column needs one value per token.", nameof(confidences));
        }

        foreach (float c in confidences)
        {
            if (!(c >= 0f && c <= 1f))
            {
                throw new ArgumentException("A confidence is from 0 to 1.", nameof(confidences));
            }
        }

        confidences.CopyTo(_confidence);
    }

    /// <summary>
    /// Replaces the document's entities. Afterwards every token has an <see cref="EntIob"/> (Outside unless inside an entity), even if
    /// the list is empty, which is how "no entities found" differs from "not run".
    /// </summary>
    /// <param name="entities">The entities in text order, with no overlap.</param>
    /// <exception cref="ArgumentException">An entity is empty, outside the document, has no label, has a confidence outside [0, 1], or overlaps or precedes the one before it.</exception>
    public void SetEntities(ReadOnlySpan<SpanSpec> entities)
    {
        var records = new EntityRecord[entities.Length];
        int previousEnd = 0;
        for (int i = 0; i < entities.Length; i++)
        {
            SpanSpec e = entities[i];
            if (e.Start < previousEnd || e.End <= e.Start || e.End > Length || string.IsNullOrEmpty(e.Label) || !(e.Confidence >= 0f && e.Confidence <= 1f))
            {
                throw new ArgumentException($"Entity {i} is empty, outside the document, unlabelled, unordered or overlapping.", nameof(entities));
            }

            records[i] = new EntityRecord(e.Start, e.End, Strings.Add(e.Label), e.Confidence);
            previousEnd = e.End;
        }

        Array.Fill(_entIob, EntIob.Outside);
        Array.Clear(_entType);
        foreach (EntityRecord r in records)
        {
            _entIob[r.Start] = EntIob.Begin;
            for (int t = r.Start + 1; t < r.End; t++)
            {
                _entIob[t] = EntIob.Inside;
            }

            Array.Fill(_entType, r.Label, r.Start, r.End - r.Start);
        }

        _entities = records;
        _entitySpans = null;
    }

    /// <summary>Finds the token that contains a character offset.</summary>
    /// <param name="charOffset">An offset into <see cref="Text"/>.</param>
    /// <returns>The token index, or -1 when the offset is in the gap between tokens or outside the text.</returns>
    public int FindToken(int charOffset)
    {
        int lo = 0;
        int hi = Length - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (charOffset < _start[mid])
            {
                hi = mid - 1;
            }
            else if (charOffset >= _start[mid] + _length[mid])
            {
                lo = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return -1;
    }

    /// <summary>
    /// Makes a span from character offsets, the way rule matches and regex hits are turned into spans. The offsets must fall on token
    /// boundaries; otherwise there is no span (nothing is rounded).
    /// </summary>
    /// <param name="startChar">The offset of the first character; the start of a token.</param>
    /// <param name="endChar">The offset just past the last character; the end of a token.</param>
    /// <param name="span">Receives the span, whose <see cref="Span.StartChar"/> and <see cref="Span.EndChar"/> equal the arguments.</param>
    /// <param name="label">The span's label, or <see langword="null"/> for none.</param>
    /// <param name="confidence">The span's confidence from 0 to 1.</param>
    /// <returns><see langword="true"/> when both offsets are token boundaries and <paramref name="endChar"/> is after <paramref name="startChar"/>.</returns>
    public bool TryGetCharSpan(int startChar, int endChar, out Span span, string? label = null, float confidence = 1f)
    {
        span = default;
        int first = Array.BinarySearch(_start, startChar);
        if (first < 0 || endChar <= startChar)
        {
            return false;
        }

        int lo = first;
        int hi = Length - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            int end = _start[mid] + _length[mid];
            if (end == endChar)
            {
                span = new Span(this, first, mid + 1, label, confidence);
                return true;
            }

            if (end < endChar)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return false;
    }

    /// <summary>Returns <see cref="Text"/>.</summary>
    public override string ToString() => Text;

    /// <summary>Enumerates the tokens in order without allocating.</summary>
    public TokenEnumerator GetEnumerator() => new(this);

    IEnumerator<Token> IEnumerable<Token>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal int StartOf(int token) => _start[token];

    internal int LengthOf(int token) => _length[token];

    internal bool SpaceAfterOf(int token) => _spaceAfter[token];

    internal int AttributeId(TokenColumn attribute, int token) => _attributes[(int)attribute][token];

    internal int HeadOf(int token) => _heads[token];

    internal SentenceBoundary BoundaryOf(int token) => _sentences[token];

    internal float ConfidenceOf(int token) => _confidence[token];

    internal EntIob EntIobOf(int token) => _entIob[token];

    internal int EntTypeOf(int token) => _entType[token];

    private int[] Column(TokenColumn attribute)
    {
        if ((uint)attribute >= AttributeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(attribute), attribute, "Not a token attribute.");
        }

        return _attributes[(int)attribute];
    }

    private void CheckToken(int index)
    {
        if ((uint)index >= (uint)Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The token index is outside the document.");
        }
    }

    private static void CheckConfidence(float confidence)
    {
        if (!(confidence >= 0f && confidence <= 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "A confidence is from 0 to 1.");
        }
    }

    private Span[] BuildSentences()
    {
        if (Length == 0)
        {
            return [];
        }

        var spans = new List<Span>();
        int start = 0;
        for (int i = 1; i < Length; i++)
        {
            if (_sentences[i] == SentenceBoundary.Start)
            {
                spans.Add(new Span(this, start, i));
                start = i;
            }
        }

        spans.Add(new Span(this, start, Length));
        return [.. spans];
    }

    private Span[] BuildEntities()
    {
        var spans = new Span[_entities.Length];
        for (int i = 0; i < spans.Length; i++)
        {
            EntityRecord r = _entities[i];
            spans[i] = new Span(this, r.Start, r.End, r.Label, r.Confidence);
        }

        return spans;
    }

    /// <summary>Walks the tokens of a <see cref="Doc"/>.</summary>
    public struct TokenEnumerator : IEnumerator<Token>
    {
        private readonly Doc _doc;
        private int _next;

        internal TokenEnumerator(Doc doc)
        {
            _doc = doc;
            _next = 0;
            Current = default;
        }

        /// <summary>The current token.</summary>
        public Token Current { get; private set; }

        object IEnumerator.Current => Current;

        /// <summary>Moves to the next token.</summary>
        public bool MoveNext()
        {
            if (_next >= _doc.Length)
            {
                return false;
            }

            Current = new Token(_doc, _next++);
            return true;
        }

        /// <summary>Not supported; create a new enumerator.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        public readonly void Reset() => throw new NotSupportedException();

        /// <summary>Does nothing.</summary>
        public readonly void Dispose()
        {
        }
    }
}
