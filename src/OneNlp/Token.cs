namespace RecursiveAI.OneNlp;

/// <summary>
/// A view of one token of a <see cref="Doc"/>: a document reference and an index, copying nothing. Attribute names follow spaCy's
/// documented ones. The default value is not a token; using it throws <see cref="NullReferenceException"/>.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Serialization.TokenJsonConverter))]
public readonly struct Token : IEquatable<Token>
{
    private readonly Doc _doc;

    private Doc Owner => _doc ?? throw new InvalidOperationException("A default Token has no document; get tokens from a Doc.");
    private readonly int _index;

    internal Token(Doc doc, int index)
    {
        _doc = doc;
        _index = index;
    }

    /// <summary>The document this token belongs to.</summary>
    public Doc Doc => Owner;

    /// <summary>The position of the token in the document, from 0 (spaCy's <c>i</c>).</summary>
    public int Index => _index;

    /// <summary>The offset of the first character of the token in the original text (spaCy's <c>idx</c>).</summary>
    public int Idx => Owner.StartOf(_index);

    /// <summary>The length of the token in UTF-16 code units.</summary>
    public int Length => Owner.LengthOf(_index);

    /// <summary>The offset just past the last character of the token in the original text.</summary>
    public int EndIdx => Idx + Length;

    /// <summary>
    /// The token's text as a new string (spaCy's <c>text</c>), so <c>token.Text == "x"</c> and <c>doc.Select(t => t.Text)</c> work. Each call copies the
    /// characters; where that matters, read <see cref="TextSpan"/>.
    /// </summary>
    public string Text => Owner.Text.Substring(Idx, Length);

    /// <summary>The token's text as a slice of the original string, without copying. Compare its contents with <see cref="System.MemoryExtensions.SequenceEqual{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>; <c>==</c> on spans compares memory.</summary>
    public ReadOnlySpan<char> TextSpan => Owner.Text.AsSpan(Idx, Length);

    /// <summary>The token's text as memory over the original string, for use where a span cannot be held.</summary>
    public ReadOnlyMemory<char> TextMemory => Owner.Text.AsMemory(Idx, Length);

    /// <summary>Whether the character right after the token in the original text is whitespace (spaCy's <c>whitespace_</c> as a flag).</summary>
    public bool HasSpaceAfter => Owner.SpaceAfterOf(_index);

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
    public int HeadIndex => Owner.HeadOf(_index);

    /// <summary>The head token.</summary>
    public Token Head => new(Owner, HeadIndex);

    /// <summary>
    /// Whether the token starts a sentence: <see langword="true"/> or <see langword="false"/> once a component decided, and
    /// <see langword="null"/> before that (spaCy's <c>is_sent_start</c>). Token 0 counts as a sentence start in <see cref="Doc.Sentences"/> whatever its value.
    /// </summary>
    public bool? IsSentStart => Owner.BoundaryOf(_index) switch
    {
        SentenceBoundary.Start => true,
        SentenceBoundary.Inside => false,
        _ => null,
    };

    /// <summary>The token's position in an entity (spaCy's <c>ent_iob</c>).</summary>
    public EntIob EntIob => Owner.EntIobOf(_index);

    /// <summary>The label of the entity the token is in; the empty string outside entities.</summary>
    public string EntType => Owner.Strings[Owner.EntTypeOf(_index)];

    /// <summary>
    /// The confidence of the token's tag annotation, from 0 to 1. It is 1 until a model component writes it, and for rule-based results.
    /// </summary>
    public float Confidence => Owner.ConfidenceOf(_index);

    /// <summary>Returns <see cref="Text"/>.</summary>
    public override string ToString() => Text;

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

    private string Get(TokenColumn attribute) => Owner.Strings[Owner.AttributeId(attribute, _index)];
}
