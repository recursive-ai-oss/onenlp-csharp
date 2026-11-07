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

    // Set by Create: the trained components of the model package that Add<T>() has not taken yet.
    private List<IPipelineComponent>? _available;
    private string _packageId = string.Empty;

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

    /// <summary>
    /// The longest text, in UTF-16 characters, that <see cref="Process(string)"/> and <see cref="ProcessMany"/> accept (spaCy's <c>nlp.max_length</c>).
    /// Unless set, it follows <see cref="Nlp.MaxLength"/> (1,000,000 by default), so one setting bounds the <see cref="Nlp"/> verbs and every pipeline.
    /// </summary>
    /// <value>At least 1. A text longer than this is refused with an <see cref="ArgumentOutOfRangeException"/> that names the limit; memory use grows with the text, so a service that takes untrusted text should keep the default or lower it.</value>
    /// <exception cref="ArgumentOutOfRangeException">Set to less than 1.</exception>
    public int MaxLength
    {
        get => _maxLength > 0 ? _maxLength : Nlp.MaxLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxLength = value;
        }
    }

    private int _maxLength;

    /// <summary>The components in the order they run.</summary>
    public IReadOnlyList<IPipelineComponent> Components => _components;

    /// <summary>
    /// Makes a pipeline with the <see cref="Segmentation.UaxWordTokenizer"/> for the language and no components (spaCy's <c>spacy.blank</c>).
    /// Add <c>sentencizer</c> for rule-based sentence boundaries.
    /// </summary>
    /// <param name="language">The language of the pipeline; English gets English tokenization data, any other language (or none) the neutral defaults.</param>
    public static Pipeline Blank(Language language = default) => new(Segmentation.UaxWordTokenizer.For(language), language);

    /// <summary>
    /// Makes a pipeline for a language from the model package that is referenced for it, with the package's tokenizer and no components
    /// yet: choose them with <see cref="Add{T}"/> (spaCy's <c>spacy.load</c> with everything excluded, then opted in).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Tier.Fast"/> uses the language's fast-tier package (for example <c>RecursiveAI.OneNlp.Models.German</c>), otherwise
    /// <c>RecursiveAI.OneNlp.Models.Multilingual</c>. <see cref="Tier.Accurate"/> uses <c>RecursiveAI.OneNlp.Models.Multilingual.Large</c>
    /// when referenced, otherwise <c>RecursiveAI.OneNlp.Models.Multilingual</c>. Packages register themselves when the application starts; nothing is
    /// discovered by reflection and nothing is downloaded.
    /// </para>
    /// <para>The weights are read on each call into a new copy that the pipeline owns, so a created pipeline shares nothing with the pipelines the <see cref="Nlp"/> verbs use. Dispose it.</para>
    /// </remarks>
    /// <param name="language">The language.</param>
    /// <param name="tier">The kind of model to load.</param>
    /// <exception cref="ModelNotAvailableException">No package is referenced for the language and tier. The message names the package to add.</exception>
    public static Pipeline Create(Language language, Tier tier = Tier.Fast)
    {
        (Pipeline source, string packageId) = ModelPackages.Load(language, tier);
        return new Pipeline(source.Tokenizer, language, source.Strings)
        {
            _available = [.. source.Components],
            _packageId = packageId,
        };
    }

    /// <summary>
    /// Adds the trained component of type <typeparamref name="T"/> from the model package this pipeline was created from (see
    /// <see cref="Create"/>), after the existing components. Components run in the order they are added, so add the tagger before
    /// components that read tags. Components of the package that are never added are released when the pipeline is disposed.
    /// </summary>
    /// <typeparam name="T">The component class, for example <c>Tagger</c>, <c>Parser</c> or <c>EntityRecognizer</c>.</typeparam>
    /// <returns>This pipeline.</returns>
    /// <exception cref="InvalidOperationException">The pipeline was not made by <see cref="Create"/>; or the package has no (further) component of this type (the message lists what it still has).</exception>
    public Pipeline Add<T>()
        where T : class, IPipelineComponent
    {
        if (_available is null)
        {
            throw new InvalidOperationException("Add<T>() takes a trained component from a model package: make the pipeline with Pipeline.Create(language, tier), or pass the component to Add(component).");
        }

        int at = _available.FindIndex(static c => c is T);
        if (at < 0)
        {
            string added = _components.Exists(static c => c is T) ? $"A {typeof(T).Name} was already added to this pipeline. " : string.Empty;
            string has = _available.Count == 0 ? "nothing more" : string.Join(", ", _available.Select(static c => c.GetType().Name));
            throw new InvalidOperationException($"{added}The model package {_packageId} has no {typeof(T).Name} to add; it still has: {has}.");
        }

        IPipelineComponent component = _available[at];
        Add(component);
        _available.RemoveAt(at);
        return this;
    }

    /// <summary>Adds a component after the existing ones.</summary>
    /// <param name="component">The component; the pipeline takes ownership and disposes it with the pipeline. An <see cref="Matching.EntityRuler"/> without a tokenizer of its own starts splitting its phrases with this pipeline's <see cref="Tokenizer"/>.</param>
    /// <returns>This pipeline, so calls chain.</returns>
    /// <exception cref="ArgumentException">A component with the same <see cref="IPipelineComponent.Name"/> is already in the pipeline.</exception>
    public Pipeline Add(IPipelineComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_components.Exists(c => string.Equals(c.Name, component.Name, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"The pipeline already has a component named '{component.Name}'.", nameof(component));
        }

        if (component is Matching.EntityRuler ruler)
        {
            ruler.BindTokenizer(Tokenizer);
        }

        _components.Add(component);
        return this;
    }

    /// <summary>Adds a component created by name from the <see cref="ComponentRegistry"/>.</summary>
    /// <param name="name">The registered name.</param>
    /// <param name="settings">Settings for the component's factory, or <see langword="null"/>. For <c>sentencizer</c>, without <c>data</c> or <c>language</c> the pipeline's own language picks the segmentation data.</param>
    /// <returns>This pipeline.</returns>
    /// <exception cref="InvalidOperationException">No component has this name.</exception>
    /// <exception cref="ArgumentException">A component with the same name is already in the pipeline.</exception>
    public Pipeline Add(string name, IReadOnlyDictionary<string, string>? settings = null)
    {
        // A sentencizer added by name to a pipeline for a language takes that language's abbreviations (English "Mr." does not end a
        // sentence) unless the settings name the data or the language; without this the pipeline's language is lost on the way to the factory.
        if (name == Segmentation.SentencizerComponent.ComponentName && Language != Language.Unknown
            && (settings is null || !(settings.ContainsKey("data") || settings.ContainsKey("language"))))
        {
            var merged = new Dictionary<string, string>(settings ?? new Dictionary<string, string>(), StringComparer.Ordinal) { ["language"] = Language.Code };
            settings = merged;
        }

        return Add(ComponentRegistry.Create(name, settings));
    }

    /// <summary>Tokenizes a text and runs every component on it.</summary>
    /// <param name="text">The text; may be empty. The returned document keeps this string and its offsets are into it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    public Doc Process(string text)
    {
        Nlp.CheckLength(text, MaxLength, nameof(text), "Pipeline.MaxLength");
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
    /// <param name="cancellationToken">Checked before each text is read and each batch is processed; enumeration then throws <see cref="OperationCanceledException"/>. Default: never cancelled.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A text is longer than <see cref="MaxLength"/>; raised when that text is reached.</exception>
    /// <exception cref="OperationCanceledException">During enumeration, <paramref name="cancellationToken"/> was cancelled.</exception>
    public IEnumerable<Doc> ProcessMany(IEnumerable<string> texts, int batchSize = 64, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        return ProcessManyCore(texts, batchSize, cancellationToken);
    }

    /// <summary>
    /// The streaming twin of <see cref="ProcessMany"/> over an asynchronous source (a network or file reader). Only reading the
    /// source awaits; processing a batch is synchronous CPU work.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <param name="batchSize">Texts per batch, at least 1.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A text is longer than <see cref="MaxLength"/>; raised when that text is reached.</exception>
    public IAsyncEnumerable<Doc> ProcessManyAsync(IAsyncEnumerable<string> texts, int batchSize = 64, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        return ProcessManyAsyncCore(texts, batchSize, cancellationToken);
    }

    /// <summary>Disposes the components, and the tokenizer, that implement <see cref="IDisposable"/>.</summary>
    public void Dispose()
    {
        (Tokenizer as IDisposable)?.Dispose();
        if (_available is not null)
        {
            foreach (IPipelineComponent component in _available)
            {
                (component as IDisposable)?.Dispose();
            }
        }

        foreach (IPipelineComponent component in _components)
        {
            (component as IDisposable)?.Dispose();
        }
    }

    private string CheckedItem(string text)
    {
        Nlp.CheckLength(text, MaxLength, "texts", "Pipeline.MaxLength");
        return text;
    }

    private IEnumerable<Doc> ProcessManyCore(IEnumerable<string> texts, int batchSize, CancellationToken cancellationToken)
    {
        var batch = new List<Doc>(batchSize);
        foreach (string text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(Tokenizer.Tokenize(CheckedItem(text ?? throw new ArgumentException("A text is null.", nameof(texts))), Strings));
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
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(Tokenizer.Tokenize(CheckedItem(text ?? throw new ArgumentException("A text is null.", nameof(texts))), Strings));
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
