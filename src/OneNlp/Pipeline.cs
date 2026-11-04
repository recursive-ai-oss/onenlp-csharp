using System.Runtime.CompilerServices;

namespace RecursiveAI.OneNlp;

/// <summary>
/// Runs a word tokenizer, then components in the order they were added, on texts (spaCy's <c>Language</c> object). CPU-bound work
/// is synchronous; <see cref="ProcessMany"/> batches, and <see cref="ProcessManyAsync"/> is the streaming twin over an async source.
/// </summary>
/// <remarks>
/// Build a pipeline on one thread (<see cref="Add(IPipelineComponent)"/> is not thread-safe), then <see cref="Process(string)"/> may be
/// called from many threads at once as long as the components allow it (see <see cref="IPipelineComponent"/>). All documents of a
/// pipeline share its <see cref="Strings"/> table, which only grows.
/// </remarks>
public sealed class Pipeline : IDisposable
{
    private readonly List<IPipelineComponent> _components = [];

    /// <summary>Creates a pipeline with no components.</summary>
    /// <param name="tokenizer">The first step, which creates documents.</param>
    /// <param name="language">The language the pipeline is for.</param>
    /// <param name="strings">The interning table to share, or <see langword="null"/> for a new one.</param>
    public Pipeline(IWordTokenizer tokenizer, Language language = default, StringStore? strings = null)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);
        Tokenizer = tokenizer;
        Language = language;
        Strings = strings ?? new StringStore();
    }

    /// <summary>The first step, which creates documents.</summary>
    public IWordTokenizer Tokenizer { get; }

    /// <summary>The language the pipeline is for; <see cref="Language.Unknown"/> when not specified.</summary>
    public Language Language { get; }

    /// <summary>The interning table shared by every document this pipeline creates.</summary>
    public StringStore Strings { get; }

    /// <summary>The components in the order they run.</summary>
    public IReadOnlyList<IPipelineComponent> Components => _components;

    /// <summary>
    /// Makes a pipeline with the <see cref="Segmentation.UaxWordTokenizer"/> for the language and no components (spaCy's <c>spacy.blank</c>).
    /// Add <c>sentencizer</c> for rule-based sentence boundaries.
    /// </summary>
    /// <param name="language">The language of the pipeline; English gets English tokenization data, any other language (or none) the neutral defaults.</param>
    public static Pipeline Blank(Language language = default) => new(Segmentation.UaxWordTokenizer.For(language), language);

    /// <summary>Adds a component after the existing ones.</summary>
    /// <param name="component">The component; the pipeline takes ownership and disposes it with the pipeline.</param>
    /// <returns>This pipeline, so calls chain.</returns>
    /// <exception cref="ArgumentException">A component with the same <see cref="IPipelineComponent.Name"/> is already in the pipeline.</exception>
    public Pipeline Add(IPipelineComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_components.Exists(c => string.Equals(c.Name, component.Name, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"The pipeline already has a component named '{component.Name}'.", nameof(component));
        }

        _components.Add(component);
        return this;
    }

    /// <summary>Adds a component created by name from the <see cref="ComponentRegistry"/>.</summary>
    /// <param name="name">The registered name.</param>
    /// <param name="settings">Settings for the component's factory, or <see langword="null"/>.</param>
    /// <returns>This pipeline.</returns>
    /// <exception cref="InvalidOperationException">No component has this name.</exception>
    /// <exception cref="ArgumentException">A component with the same name is already in the pipeline.</exception>
    public Pipeline Add(string name, IReadOnlyDictionary<string, string>? settings = null) => Add(ComponentRegistry.Create(name, settings));

    /// <summary>Tokenizes a text and runs every component on it.</summary>
    /// <param name="text">The text; may be empty. The returned document keeps this string and its offsets are into it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public Doc Process(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Doc doc = Tokenizer.Tokenize(text, Strings);
        Process(doc);
        return doc;
    }

    /// <summary>Runs every component, in order, on a document that already has its tokens.</summary>
    /// <param name="doc">The document, which this call uses alone.</param>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        foreach (IPipelineComponent component in _components)
        {
            component.Process(doc);
        }
    }

    /// <summary>
    /// Processes many texts, a batch at a time: tokenizes <paramref name="batchSize"/> texts, then runs each component over the whole
    /// batch (<see cref="IPipelineComponent.ProcessBatch"/>) before the next batch is read. Documents come out in input order, and
    /// the source is read lazily, so a huge or endless sequence streams in constant memory.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <param name="batchSize">Texts per batch, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize"/> is less than 1.</exception>
    public IEnumerable<Doc> ProcessMany(IEnumerable<string> texts, int batchSize = 64)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        return ProcessManyCore(texts, batchSize);
    }

    /// <summary>
    /// The streaming twin of <see cref="ProcessMany"/> over an asynchronous source (a network or file reader). Only reading the
    /// source awaits; processing a batch is synchronous CPU work.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <param name="batchSize">Texts per batch, at least 1.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize"/> is less than 1.</exception>
    public IAsyncEnumerable<Doc> ProcessManyAsync(IAsyncEnumerable<string> texts, int batchSize = 64, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        return ProcessManyAsyncCore(texts, batchSize, cancellationToken);
    }

    /// <summary>Disposes the components that implement <see cref="IDisposable"/>.</summary>
    public void Dispose()
    {
        foreach (IPipelineComponent component in _components)
        {
            (component as IDisposable)?.Dispose();
        }
    }

    private IEnumerable<Doc> ProcessManyCore(IEnumerable<string> texts, int batchSize)
    {
        var batch = new List<Doc>(batchSize);
        foreach (string text in texts)
        {
            batch.Add(Tokenizer.Tokenize(text ?? throw new ArgumentException("A text is null.", nameof(texts)), Strings));
            if (batch.Count == batchSize)
            {
                RunBatch(batch);
                foreach (Doc doc in batch)
                {
                    yield return doc;
                }

                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            RunBatch(batch);
            foreach (Doc doc in batch)
            {
                yield return doc;
            }
        }
    }

    private async IAsyncEnumerable<Doc> ProcessManyAsyncCore(
        IAsyncEnumerable<string> texts, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<Doc>(batchSize);
        await foreach (string text in texts.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(Tokenizer.Tokenize(text ?? throw new ArgumentException("A text is null.", nameof(texts)), Strings));
            if (batch.Count == batchSize)
            {
                RunBatch(batch);
                foreach (Doc doc in batch)
                {
                    yield return doc;
                }

                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            RunBatch(batch);
            foreach (Doc doc in batch)
            {
                yield return doc;
            }
        }
    }

    private void RunBatch(List<Doc> batch)
    {
        Doc[] docs = [.. batch];
        foreach (IPipelineComponent component in _components)
        {
            component.ProcessBatch(docs);
        }
    }
}
