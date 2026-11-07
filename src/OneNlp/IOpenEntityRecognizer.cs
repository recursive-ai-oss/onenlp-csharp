namespace RecursiveAI.OneNlp;

/// <summary>A pipeline component that finds entities of types given at call time rather than fixed by training.</summary>
/// <remarks>Implementations must be safe to call from several threads at once.</remarks>
public interface IOpenEntityRecognizer : IPipelineComponent
{
    /// <summary>Finds entities of the given types in a document.</summary>
    /// <param name="doc">The document; entities already set on it stay and are not overlapped.</param>
    /// <param name="types">The type names, such as <c>"contract party"</c>.</param>
    /// <param name="threshold">The probability at or above which a span is reported; <see langword="null"/> uses the component's default.</param>
    /// <exception cref="ArgumentException">The types are invalid or empty.</exception>
    void Recognize(Doc doc, IReadOnlyList<string> types, float? threshold = null);
}
