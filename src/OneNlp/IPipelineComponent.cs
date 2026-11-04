namespace RecursiveAI.OneNlp;

/// <summary>One step of a <see cref="Pipeline"/>: reads a <see cref="Doc"/> and writes annotations into it.</summary>
/// <remarks>
/// <para>
/// A component never changes token boundaries or <see cref="Doc.Text"/>; it only fills attribute columns, entities and sentence
/// boundaries. It may overwrite what an earlier component wrote.
/// </para>
/// <para>
/// A pipeline may call one component from several threads, each with its own document, so <see cref="Process"/> must be safe
/// for that: keep per-call state in locals and share only read-only data. A component that holds resources implements
/// <see cref="IDisposable"/>; the pipeline disposes it.
/// </para>
/// </remarks>
public interface IPipelineComponent
{
    /// <summary>The name, unique within a pipeline (for example <c>tagger</c>). Used for lookup and in error messages.</summary>
    string Name { get; }

    /// <summary>Annotates one document.</summary>
    /// <param name="doc">The document to write into.</param>
    void Process(Doc doc);

    /// <summary>
    /// Annotates several documents. Components that gain from batching (a neural encoder) override this; the default calls
    /// <see cref="Process"/> on each document in order.
    /// </summary>
    /// <param name="docs">The documents, each used by this call alone.</param>
    void ProcessBatch(ReadOnlySpan<Doc> docs)
    {
        foreach (Doc doc in docs)
        {
            Process(doc);
        }
    }
}

/// <summary>Turns text into a <see cref="Doc"/> with token boundaries: the first step of every <see cref="Pipeline"/>.</summary>
/// <remarks>Implementations must be safe to call from several threads at once.</remarks>
public interface IWordTokenizer
{
    /// <summary>Splits a text into tokens.</summary>
    /// <param name="text">The text; the document keeps this exact string.</param>
    /// <param name="strings">The interning table for the new document.</param>
    /// <returns>A document whose token offsets are into <paramref name="text"/>.</returns>
    Doc Tokenize(string text, StringStore strings);
}
