using RecursiveAI.OneModel.Tensors;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// What an encoder produces for a batch of documents: one vector per token, the documents' rows stacked in order in a single
/// matrix. Heads read rows of <see cref="Vectors"/>; in training the matrix carries the autodiff graph back into the encoder.
/// </summary>
public sealed class EncodedBatch
{
    private readonly int[] _starts;

    /// <summary>Wraps encoder output.</summary>
    /// <param name="vectors">A float32 matrix <c>[total tokens, width]</c>, the rows of document 0 first.</param>
    /// <param name="docStarts">The first row of each document followed by the total row count, so document <c>i</c> owns rows <c>docStarts[i]</c> up to <c>docStarts[i + 1]</c>; at least one entry.</param>
    /// <exception cref="ArgumentException">The starts are not non-decreasing from 0, or do not end at the row count of <paramref name="vectors"/>.</exception>
    public EncodedBatch(Tensor vectors, int[] docStarts)
    {
        ArgumentNullException.ThrowIfNull(docStarts);
        if (docStarts.Length == 0 || docStarts[0] != 0 || vectors.Rank != 2 || docStarts[^1] != vectors.Shape[0])
        {
            throw new ArgumentException("The document starts must begin at 0 and end at the number of rows of the vectors.", nameof(docStarts));
        }

        for (int i = 1; i < docStarts.Length; i++)
        {
            if (docStarts[i] < docStarts[i - 1])
            {
                throw new ArgumentException("The document starts must not decrease.", nameof(docStarts));
            }
        }

        Vectors = vectors;
        _starts = docStarts;
    }

    /// <summary>The token vectors, <c>[total tokens, width]</c>, float32.</summary>
    public Tensor Vectors { get; }

    /// <summary>The number of documents.</summary>
    public int DocCount => _starts.Length - 1;

    /// <summary>The total number of tokens (rows).</summary>
    public int TokenCount => _starts[^1];

    /// <summary>The first row of a document.</summary>
    /// <param name="doc">The document's position in the batch.</param>
    public int Start(int doc) => _starts[doc];

    /// <summary>The number of rows (tokens) of a document.</summary>
    /// <param name="doc">The document's position in the batch.</param>
    public int Length(int doc) => _starts[doc + 1] - _starts[doc];

    /// <summary>The rows of one document as a view <c>[tokens, width]</c> that passes gradients back to <see cref="Vectors"/>.</summary>
    /// <param name="doc">The document's position in the batch.</param>
    public Tensor Rows(int doc) => Vectors.Slice(0, _starts[doc], _starts[doc + 1]);

    /// <summary>The same batch with different vectors of the same shape (after dropout, say).</summary>
    /// <param name="vectors">The replacement matrix.</param>
    public EncodedBatch WithVectors(Tensor vectors) => new(vectors, _starts);
}
