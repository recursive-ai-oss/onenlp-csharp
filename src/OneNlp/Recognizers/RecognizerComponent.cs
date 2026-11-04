namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>
/// A pipeline component that writes the results of a <see cref="Recognizer"/> into a document as entities, labelled
/// <c>CARDINAL</c>, <c>ORDINAL</c>, <c>PERCENT</c>, <c>MONEY</c>, <c>QUANTITY</c>, <c>EMAIL</c>, <c>URL</c>, <c>IP_ADDRESS</c>,
/// <c>PHONE_LIKE</c>, <c>HASHTAG</c>, <c>MENTION</c> or <c>GUID</c>. Registered as <c>recognizers</c>.
/// </summary>
/// <remarks>
/// <para>
/// Entities cover whole tokens. A result that starts or ends inside a token (a unit touching its number, a URL cut by the tokenizer)
/// is widened to the tokens it touches. The component does not read the other annotations. Settings of the registry factory:
/// <c>language</c> (default <c>en</c>) and <c>overwrite_ents</c> (<c>true</c> or <c>false</c>, default <c>false</c>).
/// </para>
/// <para>
/// <b>Existing entities</b> (from a model or a ruler): with <see cref="OverwriteEnts"/> false, a recognizer result that overlaps one is
/// dropped and the existing entity stays. With it true, results win and each existing entity they overlap is removed. The entities
/// stay in text order without overlap. The labels are the OntoNotes names for numbers, which a NER model that predicts PER, ORG and
/// LOC does not use, so the two do not clash.
/// </para>
/// </remarks>
public sealed class RecognizerComponent : IPipelineComponent
{
    /// <summary>The registry name, <c>recognizers</c>.</summary>
    public const string ComponentName = "recognizers";

    private readonly Recognizer _recognizer;

    /// <summary>Creates the component.</summary>
    /// <param name="recognizer">The rules to apply.</param>
    /// <param name="overwriteEnts">Whether results replace overlapping entities already in the document.</param>
    public RecognizerComponent(Recognizer recognizer, bool overwriteEnts = false)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        _recognizer = recognizer;
        OverwriteEnts = overwriteEnts;
    }

    /// <summary>Whether results replace overlapping entities already in the document.</summary>
    public bool OverwriteEnts { get; }

    /// <inheritdoc />
    public string Name => ComponentName;

    internal static IPipelineComponent FromSettings(IReadOnlyDictionary<string, string> settings)
    {
        Language language = settings.TryGetValue("language", out string? code) ? Language.FromCode(code) : Language.English;
        bool overwrite = settings.TryGetValue("overwrite_ents", out string? o) && bool.TryParse(o, out bool b) && b;
        return new RecognizerComponent(Recognizer.For(language), overwrite);
    }

    /// <inheritdoc />
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        IReadOnlyList<Recognition> results = _recognizer.Recognize(doc.Text);
        if (doc.Length == 0)
        {
            return;
        }

        ReadOnlySpan<int> starts = doc.GetStarts();
        ReadOnlySpan<int> lengths = doc.GetLengths();
        var accepted = new List<SpanSpec>(results.Count);
        int previousEnd = 0;
        foreach (Recognition r in results)
        {
            // The first token that ends after the result starts, and the last that starts before it ends.
            int first = FirstEndingAfter(starts, lengths, r.Start);
            int last = FirstStartingAtOrAfter(starts, r.End) - 1;
            if (first > last || first >= doc.Length || first < previousEnd)
            {
                continue;
            }

            accepted.Add(new SpanSpec(first, last + 1, r.Label, r.Confidence));
            previousEnd = last + 1;
        }

        var merged = new List<SpanSpec>();
        foreach (Span existing in doc.Ents)
        {
            merged.Add(new SpanSpec(existing.Start, existing.End, existing.Label, existing.Confidence));
        }

        foreach (SpanSpec s in accepted)
        {
            if (OverwriteEnts)
            {
                merged.RemoveAll(e => e.Start < s.End && s.Start < e.End);
                merged.Add(s);
            }
            else if (!merged.Exists(e => e.Start < s.End && s.Start < e.End))
            {
                merged.Add(s);
            }
        }

        merged.Sort((a, b) => a.Start.CompareTo(b.Start));
        doc.SetEntities([.. merged]);
    }

    /// <summary>The index of the first token whose end is after <paramref name="offset"/> (the token containing it, or the next one).</summary>
    private static int FirstEndingAfter(ReadOnlySpan<int> starts, ReadOnlySpan<int> lengths, int offset)
    {
        int lo = 0;
        int hi = starts.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (starts[mid] + lengths[mid] <= offset)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>The index of the first token that starts at or after <paramref name="offset"/>.</summary>
    private static int FirstStartingAtOrAfter(ReadOnlySpan<int> starts, int offset)
    {
        int lo = 0;
        int hi = starts.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (starts[mid] < offset)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
