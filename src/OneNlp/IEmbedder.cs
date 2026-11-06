namespace RecursiveAI.OneNlp;

/// <summary>
/// Turns text into a fixed-length vector (a sentence embedding) so that texts with similar meaning are close under the dot product
/// (cosine similarity when the vectors are normalized). Implementations are in <c>RecursiveAI.OneNlp.Neural</c>.
/// </summary>
/// <remarks>
/// Every method is synchronous and CPU-bound. Implementations in this library are safe to call from several threads; calls are
/// serialized per embedder, so use one embedder per thread for parallel throughput. Dispose releases the model.
/// </remarks>
public interface IEmbedder : IDisposable
{
    /// <summary>The number of values in each vector.</summary>
    int Dimensions { get; }

    /// <summary>Embeds one text.</summary>
    /// <param name="text">The text; may be empty. Text beyond the model's maximum sequence length is cut off, never an error.</param>
    /// <returns>A new array of <see cref="Dimensions"/> values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    float[] Embed(string text);

    /// <summary>Embeds a batch of texts. The result for a text does not depend on its neighbours beyond float rounding.</summary>
    /// <param name="texts">The texts, in order; may be empty.</param>
    /// <returns>One vector per text, in the order of <paramref name="texts"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> or one of its items is <see langword="null"/>.</exception>
    float[][] EmbedBatch(IReadOnlyList<string> texts);

    /// <summary>Embeds a sequence of any length lazily, in internal batches, so the whole input is never held at once.</summary>
    /// <param name="texts">The texts; enumerated once, in order.</param>
    /// <returns>One vector per text, in order, produced as the sequence is consumed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> is <see langword="null"/>.</exception>
    IEnumerable<float[]> EmbedMany(IEnumerable<string> texts);
}
