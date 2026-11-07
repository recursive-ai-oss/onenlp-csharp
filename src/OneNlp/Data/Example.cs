using RecursiveAI.OneNlp.Data;

namespace RecursiveAI.OneNlp;

/// <summary>
/// A training or evaluation example: a <see cref="Predicted"/> document, which components annotate, paired with a
/// <see cref="Reference"/> document that holds the gold annotations. The two may be tokenized differently; <see cref="Alignment"/>
/// maps between them.
/// </summary>
/// <remarks>
/// Besides what a <see cref="Doc"/> stores (tags, lemmas, morphology, heads, sentence starts, entities) an example carries the gold
/// text categories (<see cref="Cats"/>) and span groups (<see cref="Spans"/>), which have no column in a document.
/// </remarks>
public sealed class Example
{
    private Alignment? _alignment;

    /// <summary>Pairs a predicted and a reference document.</summary>
    /// <param name="predicted">The document that components annotate. It need not carry any annotation.</param>
    /// <param name="reference">The gold document.</param>
    /// <param name="cats">Gold category scores, or <see langword="null"/> when none are given.</param>
    /// <param name="spans">Gold span groups by name; spans are token ranges of <paramref name="reference"/>.</param>
    /// <exception cref="ArgumentNullException">A document is <see langword="null"/>.</exception>
    public Example(Doc predicted, Doc reference, IReadOnlyDictionary<string, float>? cats = null, IReadOnlyDictionary<string, IReadOnlyList<SpanSpec>>? spans = null)
    {
        ArgumentNullException.ThrowIfNull(predicted);
        ArgumentNullException.ThrowIfNull(reference);
        Predicted = predicted;
        Reference = reference;
        Cats = cats;
        Spans = spans;
    }

    /// <summary>The document that components annotate.</summary>
    public Doc Predicted { get; }

    /// <summary>The gold document.</summary>
    public Doc Reference { get; }

    /// <summary>Gold category scores (label to a value from 0 to 1), or <see langword="null"/> when the example has none.</summary>
    public IReadOnlyDictionary<string, float>? Cats { get; }

    /// <summary>Gold span groups by name, as token ranges of <see cref="Reference"/>, or <see langword="null"/> when there are none.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SpanSpec>>? Spans { get; }

    /// <summary>
    /// The alignment from <see cref="Predicted"/> (<c>x</c>) to <see cref="Reference"/> (<c>y</c>), computed on first use.
    /// </summary>
    /// <exception cref="ArgumentException">The two texts differ by more than whitespace and capitalization.</exception>
    public Alignment Alignment => _alignment ??= Alignment.Create(Predicted, Reference);

    /// <summary>
    /// Makes an example whose predicted document is an unannotated copy of the reference: same text, same token boundaries, no tags.
    /// This is how gold data read from a file becomes something to train on and to predict into.
    /// </summary>
    /// <param name="reference">The gold document.</param>
    /// <param name="cats">Gold category scores, or <see langword="null"/>.</param>
    /// <param name="spans">Gold span groups, or <see langword="null"/>.</param>
    public static Example FromReference(Doc reference, IReadOnlyDictionary<string, float>? cats = null, IReadOnlyDictionary<string, IReadOnlyList<SpanSpec>>? spans = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new Example(Blank(reference), reference, cats, spans);
    }

    /// <summary>
    /// Makes an example from a text and gold annotations given as character offsets (spaCy's <c>Example.from_dict</c>): the text is split
    /// with the pipeline's tokenizer, so training sees the token boundaries the pipeline produces at prediction time.
    /// </summary>
    /// <param name="nlp">The pipeline whose tokenizer and string table are used.</param>
    /// <param name="text">The text.</param>
    /// <param name="entities">Gold entities as character offsets into <paramref name="text"/> (<c>End</c> exclusive, UTF-16 units) and a label, in any order; they must not overlap. <see langword="null"/> for none.</param>
    /// <param name="cats">Gold category scores from 0 to 1, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="nlp"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An entity starts before 0 or ends after the end of the text; the message gives the text's length.</exception>
    /// <exception cref="ArgumentException">An entity does not start and end on token boundaries of the pipeline's tokenization, is empty, has no label, or overlaps another; the message names it.</exception>
    public static Example Create(Pipeline nlp, string text, IEnumerable<(int Start, int End, string Label)>? entities = null, IReadOnlyDictionary<string, float>? cats = null)
    {
        ArgumentNullException.ThrowIfNull(nlp);
        ArgumentNullException.ThrowIfNull(text);
        Doc reference = nlp.Tokenizer.Tokenize(text, nlp.Strings);
        if (entities is not null)
        {
            var specs = new List<SpanSpec>();
            foreach ((int start, int end, string label) in entities)
            {
                if (start < 0 || end > text.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(entities), $"The entity {label} at characters {start}..{end} is outside the text, which has {text.Length} characters.");
                }

                if (!reference.TryGetCharSpan(start, end, out Span span, label))
                {
                    throw new ArgumentException($"The entity {label} at characters {start}..{end} does not start and end on token boundaries of the pipeline's tokenization.", nameof(entities));
                }

                specs.Add(new SpanSpec(span.Start, span.End, label));
            }

            specs.Sort(static (x, y) => x.Start.CompareTo(y.Start));
            reference.SetEntities([.. specs]);
        }

        return FromReference(reference, cats);
    }

    /// <summary>Makes an unannotated document with the text and token boundaries of another, sharing its string table.</summary>
    /// <param name="doc">The document to copy the tokenization of.</param>
    public static Doc Blank(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var boundaries = new TokenBoundary[doc.Length];
        ReadOnlySpan<int> starts = doc.GetStarts();
        ReadOnlySpan<int> lengths = doc.GetLengths();
        for (int i = 0; i < boundaries.Length; i++)
        {
            boundaries[i] = new TokenBoundary(starts[i], lengths[i]);
        }

        return new Doc(doc.Strings, doc.Text, boundaries);
    }

    /// <summary>
    /// The gold value of a string attribute for each predicted token, carried over the alignment. A predicted token takes the value of
    /// the reference token it lies in, when it overlaps exactly one reference token; a predicted token that spans several reference
    /// tokens, or whose reference token has no value, gets <see langword="null"/>, which a loss treats as "no gold label".
    /// </summary>
    /// <param name="column">The attribute.</param>
    /// <returns>One entry per predicted token.</returns>
    /// <exception cref="ArgumentException">The texts cannot be aligned.</exception>
    public string?[] GetAligned(TokenColumn column)
    {
        Alignment a = Alignment;
        ReadOnlySpan<int> ids = Reference.GetColumn(column);
        var result = new string?[Predicted.Length];
        for (int x = 0; x < result.Length; x++)
        {
            ReadOnlySpan<int> ys = a.XToY(x);
            if (ys.Length == 1 && ids[ys[0]] != 0)
            {
                result[x] = Reference.Strings[ids[ys[0]]];
            }
        }

        return result;
    }

    /// <summary>The reference entities, carried onto the predicted tokens. An entity whose edges are not predicted token boundaries is <see langword="null"/>.</summary>
    /// <returns>One entry per reference entity, in order.</returns>
    /// <exception cref="ArgumentException">The texts cannot be aligned.</exception>
    public SpanSpec?[] GetAlignedEntities()
    {
        IReadOnlyList<Span> ents = Reference.Ents;
        var specs = new SpanSpec[ents.Count];
        for (int i = 0; i < specs.Length; i++)
        {
            specs[i] = new SpanSpec(ents[i].Start, ents[i].End, ents[i].Label, ents[i].Confidence);
        }

        return Alignment.MapSpansYToX(specs);
    }
}
