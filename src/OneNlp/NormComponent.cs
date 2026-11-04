namespace RecursiveAI.OneNlp;

/// <summary>
/// Writes each token's <see cref="Token.Norm"/> as its text lowercased with the invariant culture. It is the one component the core
/// package ships; registered as <c>norm</c>.
/// </summary>
public sealed class NormComponent : IPipelineComponent
{
    /// <summary>The registry name, <c>norm</c>.</summary>
    public const string ComponentName = "norm";

    /// <inheritdoc/>
    public string Name => ComponentName;

    /// <inheritdoc/>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        for (int i = 0; i < doc.Length; i++)
        {
            doc.SetAnnotation(TokenColumn.Norm, i, doc[i].ToString().ToLowerInvariant());
        }
    }
}
