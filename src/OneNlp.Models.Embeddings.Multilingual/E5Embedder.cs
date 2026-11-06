using RecursiveAI.OneNlp.Neural.Embeddings;

namespace RecursiveAI.OneNlp.Models.Embeddings.Multilingual;

/// <summary>
/// multilingual-e5-small with its input prefixes. The model was trained with <c>"query: "</c> in front of a search query (and in front of
/// both texts of a symmetric task such as similarity) and <c>"passage: "</c> in front of a passage to be searched. <see cref="Embed"/>,
/// <see cref="EmbedBatch"/> and <see cref="EmbedMany"/> use <c>"query: "</c>, which is what <see cref="Nlp.Embed(string)"/> gets;
/// <see cref="EmbedPassage(string)"/> and its batch form use <c>"passage: "</c>.
/// </summary>
public sealed class E5Embedder : IEmbedder
{
    /// <summary>The prefix for queries and for symmetric tasks.</summary>
    public const string QueryPrefix = "query: ";

    /// <summary>The prefix for passages to be searched.</summary>
    public const string PassagePrefix = "passage: ";

    private readonly SentenceEmbedder _inner;

    /// <summary>Wraps an embedder over a multilingual-e5 model (loaded with no prefix of its own). Disposing this object disposes it.</summary>
    /// <param name="inner">The embedder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public E5Embedder(SentenceEmbedder inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>The wrapped embedder, for token IDs, the batch size and the configuration.</summary>
    public SentenceEmbedder Inner => _inner;

    /// <inheritdoc/>
    public int Dimensions => _inner.Dimensions;

    /// <summary>Embeds a text with the query prefix.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A unit-length vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public float[] Embed(string text) => EmbedQuery(text);

    /// <summary>Embeds texts with the query prefix.</summary>
    /// <param name="texts">The texts.</param>
    /// <returns>One vector per text, in order.</returns>
    /// <exception cref="ArgumentNullException">A text is <see langword="null"/>.</exception>
    public float[][] EmbedBatch(IReadOnlyList<string> texts) => EmbedQueries(texts);

    /// <summary>Embeds a stream of texts with the query prefix, in batches.</summary>
    /// <param name="texts">The texts, enumerated lazily.</param>
    /// <returns>The vectors, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> is <see langword="null"/>.</exception>
    public IEnumerable<float[]> EmbedMany(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return _inner.EmbedMany(texts.Select(t => QueryPrefix + t));
    }

    /// <summary>Embeds a search query.</summary>
    /// <param name="text">The query.</param>
    /// <returns>A unit-length vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public float[] EmbedQuery(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _inner.Embed(QueryPrefix + text);
    }

    /// <summary>Embeds a passage to be searched.</summary>
    /// <param name="text">The passage.</param>
    /// <returns>A unit-length vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public float[] EmbedPassage(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _inner.Embed(PassagePrefix + text);
    }

    /// <summary>Embeds search queries.</summary>
    /// <param name="texts">The queries.</param>
    /// <returns>One vector per query, in order.</returns>
    /// <exception cref="ArgumentNullException">A text is <see langword="null"/>.</exception>
    public float[][] EmbedQueries(IReadOnlyList<string> texts) => Prefixed(texts, QueryPrefix);

    /// <summary>Embeds passages to be searched.</summary>
    /// <param name="texts">The passages.</param>
    /// <returns>One vector per passage, in order.</returns>
    /// <exception cref="ArgumentNullException">A text is <see langword="null"/>.</exception>
    public float[][] EmbedPassages(IReadOnlyList<string> texts) => Prefixed(texts, PassagePrefix);

    /// <inheritdoc/>
    public void Dispose() => _inner.Dispose();

    private float[][] Prefixed(IReadOnlyList<string> texts, string prefix)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var list = new string[texts.Count];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = prefix + (texts[i] ?? throw new ArgumentNullException(nameof(texts), "A text is null."));
        }

        return _inner.EmbedBatch(list);
    }
}
