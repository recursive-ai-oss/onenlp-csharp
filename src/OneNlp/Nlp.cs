namespace RecursiveAI.OneNlp;

/// <summary>
/// The happy path: static task verbs with no options objects, each one line over the low-level types (<see cref="Pipeline"/>,
/// <see cref="Doc"/>). Nothing here downloads anything.
/// </summary>
public static class Nlp
{
    // Built once. With no components it only tokenizes, and it never writes to the string table, so sharing it is safe.
    private static readonly Pipeline DefaultPipeline = Pipeline.Blank();

    /// <summary>Analyzes a text with the default pipeline. For now that is tokenization only.</summary>
    /// <param name="text">The text; may be empty. The document keeps this string and every offset is into it.</param>
    /// <returns>A document with its tokens and nothing else yet; the other annotations stay unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static Doc Analyze(string text) => DefaultPipeline.Process(text);
}
