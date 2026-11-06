namespace RecursiveAI.OneNlp.Segmentation;

/// <summary>
/// Writes the word of each piece of a multiword token into <see cref="Token.Norm"/>. Treebanks split some written words into syntactic words
/// (French <c>du</c> is <c>de</c> + <c>le</c>, German <c>zum</c> is <c>zu</c> + <c>dem</c>); the tokenizer cuts such a word into one piece for each, and
/// this component says which words the pieces stand for, using the same data as <see cref="SegmentationData.TryMatchMultiword"/>. A transformer
/// encoder with <c>ExpandWordForms</c> then reads <c>de le</c> instead of <c>du</c>, and a caller can show the words next to the surface pieces.
/// Pieces outside a multiword token keep no norm. Registered as <c>mwt-forms</c>; the setting <c>language</c> is a language code.
/// </summary>
public sealed class MultiwordFormsComponent : IPipelineComponent, IConfigurableComponent
{
    /// <summary>The registry name, <c>mwt-forms</c>.</summary>
    public const string ComponentName = "mwt-forms";

    private readonly Language _language;
    private readonly SegmentationData _data;

    /// <summary>Creates the component for a language.</summary>
    /// <param name="language">The language whose multiword tokens are recognized. Languages without any do nothing.</param>
    public MultiwordFormsComponent(Language language)
    {
        _language = language;
        _data = SegmentationData.For(language);
    }

    /// <inheritdoc/>
    public string Name => ComponentName;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string> { ["language"] = _language.Code };

    internal static IPipelineComponent FromSettings(IReadOnlyDictionary<string, string> settings) =>
        new MultiwordFormsComponent(settings.TryGetValue("language", out string? code) ? Language.FromCode(code) : Language.Unknown);

    /// <inheritdoc/>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        for (int i = 0; i < doc.Length; i++)
        {
            if (_data.TryMatchMultiword(doc, i, out int count, out string[]? words) && count > 1 && i + count <= doc.Length)
            {
                for (int k = 0; k < count; k++)
                {
                    doc.SetAnnotation(TokenColumn.Norm, i + k, words[k]);
                }

                i += count - 1;
            }
        }
    }
}
