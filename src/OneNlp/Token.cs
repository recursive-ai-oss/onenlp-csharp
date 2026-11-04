namespace RecursiveAI.OneNlp;

/// <summary>
/// A view of one token of a <see cref="Doc"/>: a document reference and an index, copying nothing. Attribute names follow spaCy's
/// documented ones. The default value is not a token; using it throws <see cref="NullReferenceException"/>.
/// </summary>
public readonly struct Token : IEquatable<Token>
{
    private readonly Doc _doc;
    private readonly int _index;

    internal Token(Doc doc, int index)
    {
        _doc = doc;
        _index = index;
    }

    /// <summary>The document this token belongs to.</summary>
    public Doc Doc => _doc;

    /// <summary>The position of the token in the document, from 0 (spaCy's <c>i</c>).</summary>
    public int Index => _index;

    /// <summary>The offset of the first character of the token in the original text (spaCy's <c>idx</c>).</summary>
    public int Idx => _doc.StartOf(_index);

    /// <summary>The length of the token in UTF-16 code units.</summary>
    public int Length => _doc.LengthOf(_index);

    /// <summary>The offset just past the last character of the token in the original text.</summary>
    public int EndIdx => Idx + Length;

    /// <summary>The token's text: a slice of the original string, without copying.</summary>
    public ReadOnlySpan<char> Text => _doc.Text.AsSpan(Idx, Length);

    /// <summary>The token's text as memory over the original string, for use where a span cannot be held.</summary>
    public ReadOnlyMemory<char> TextMemory => _doc.Text.AsMemory(Idx, Length);

    /// <summary>Whether the character right after the token in the original text is whitespace (spaCy's <c>whitespace_</c> as a flag).</summary>
    public bool HasSpaceAfter => _doc.SpaceAfterOf(_index);

    /// <summary>The normalized form; the empty string until a component sets it.</summary>
    public string Norm => Get(TokenColumn.Norm);

    /// <summary>The lemma; the empty string until a component sets it.</summary>
    public string Lemma => Get(TokenColumn.Lemma);

    /// <summary>The coarse part of speech (UPOS); the empty string until a component sets it.</summary>
    public string Pos => Get(TokenColumn.Pos);

    /// <summary>The fine-grained tag; the empty string until a component sets it.</summary>
    public string Tag => Get(TokenColumn.Tag);

    /// <summary>The dependency relation to the head; the empty string until a component sets it.</summary>
    public string Dep => Get(TokenColumn.Dep);

    /// <summary>The morphological features, for example <c>Case=Nom|Number=Sing</c>; the empty string until a component sets it.</summary>
    public string Morph => Get(TokenColumn.Morph);

    /// <summary>The index of the head token. A token that is its own head is a root, the value before any parser has run.</summary>
    public int HeadIndex => _doc.HeadOf(_index);

    /// <summary>The head token.</summary>
    public Token Head => new(_doc, HeadIndex);

    /// <summary>
    /// Whether the token starts a sentence: <see langword="true"/> or <see langword="false"/> once a component decided, and
    /// <see langword="null"/> before that (spaCy's <c>is_sent_start</c>). Token 0 counts as a sentence start in <see cref="Doc.Sentences"/> whatever its value.
    /// </summary>
    public bool? IsSentStart => _doc.BoundaryOf(_index) switch
    {
        SentenceBoundary.Start => true,
        SentenceBoundary.Inside => false,
        _ => null,
    };

    /// <summary>The token's position in an entity (spaCy's <c>ent_iob</c>).</summary>
    public EntIob EntIob => _doc.EntIobOf(_index);

    /// <summary>The label of the entity the token is in; the empty string outside entities.</summary>
    public string EntType => _doc.Strings[_doc.EntTypeOf(_index)];

    /// <summary>
    /// The confidence of the token's tag annotation, from 0 to 1. It is 1 until a model component writes it, and for rule-based results.
    /// </summary>
    public float Confidence => _doc.ConfidenceOf(_index);

    /// <summary>Copies the token's text into a new string.</summary>
    public override string ToString() => _doc.Text.Substring(Idx, Length);

    /// <inheritdoc/>
    public bool Equals(Token other) => ReferenceEquals(_doc, other._doc) && _index == other._index;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Token other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_doc, _index);

    /// <summary>Whether two views are the same token of the same document.</summary>
    public static bool operator ==(Token left, Token right) => left.Equals(right);

    /// <summary>Whether two views are different tokens or from different documents.</summary>
    public static bool operator !=(Token left, Token right) => !left.Equals(right);

    private string Get(TokenColumn attribute) => _doc.Strings[_doc.AttributeId(attribute, _index)];
}
